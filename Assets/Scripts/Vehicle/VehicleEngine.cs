using System;
using IndieGame.Core;
using IndieGame.Vehicles.Data;
using UnityEngine;

namespace IndieGame.Vehicles
{
    /// <summary>
    /// A rotating mass with a torque curve, driven by throttle and loaded by the
    /// clutch. Engine speed is a genuine integrated state, not a number derived
    /// from road speed: in neutral the engine is free to rev, in gear the clutch
    /// pulls it toward driveline speed, and when the driveline is locked the RPM
    /// you read on the tachometer is exactly wheel speed through the gearing.
    /// </summary>
    [Serializable]
    public class VehicleEngine
    {
        private VehicleDefinition _definition;

        // --- State ---------------------------------------------------------
        public EngineState State { get; private set; } = EngineState.Off;

        /// <summary>Crankshaft speed in rad/s.</summary>
        public float AngularVelocity { get; private set; }

        public float Rpm => AngularVelocity * Units.RadPerSecToRpm;

        /// <summary>Commanded throttle after the drive mode map and response lag, 0..1.</summary>
        public float EffectiveThrottle { get; private set; }

        /// <summary>Raw pedal travel as requested by the driver, 0..1.</summary>
        public float RequestedThrottle { get; private set; }

        /// <summary>Net torque produced at the crankshaft this step, Nm.</summary>
        public float OutputTorqueNm { get; private set; }

        /// <summary>Torque the combustion process alone produced, before friction. Used by audio.</summary>
        public float CombustionTorqueNm { get; private set; }

        /// <summary>Current boost pressure in bar above atmospheric.</summary>
        public float BoostBar { get; private set; }

        /// <summary>True while the rev limiter is cutting.</summary>
        public bool LimiterActive { get; private set; }

        /// <summary>True on the step the throttle snapped shut above the BOV threshold.</summary>
        public bool BlowOffTriggered { get; private set; }

        /// <summary>True while the engine is decelerating on a closed throttle in gear - the overrun condition that produces pops.</summary>
        public bool OnOverrun { get; private set; }

        public bool IsRunning => State == EngineState.Running;

        // --- Tuning multipliers (Phase 7 writes these) ----------------------
        /// <summary>Multiplies the whole torque curve. An ECU tune raises this.</summary>
        public float TorqueMultiplier = 1f;

        /// <summary>Adds to the maximum boost the turbo can reach, in bar.</summary>
        public float BoostBarOffset = 0f;

        /// <summary>Scales spool time. Below 1 is a smaller, faster-spooling turbo.</summary>
        public float SpoolSpeedMultiplier = 1f;

        // --- Internals ------------------------------------------------------
        private float _starterTimer;
        private float _limiterCutTimer;
        private float _previousThrottle;
        private const float LimiterCutDuration = 0.06f;

        public void Initialise(VehicleDefinition definition)
        {
            _definition = definition;
            AngularVelocity = 0f;
            EffectiveThrottle = 0f;
            BoostBar = 0f;
            State = EngineState.Off;
        }

        // ==================================================================
        // Ignition
        // ==================================================================
        public void RequestStart()
        {
            if (State == EngineState.Running || State == EngineState.Starting) return;
            State = EngineState.Starting;
            _starterTimer = 0f;
        }

        public void Shutdown()
        {
            State = EngineState.Off;
            EffectiveThrottle = 0f;
            BoostBar = 0f;
        }

        public void ToggleIgnition()
        {
            if (State == EngineState.Running) Shutdown();
            else RequestStart();
        }

        /// <summary>Forces the engine to idle immediately. Used when spawning a car already running.</summary>
        public void ForceRunning()
        {
            State = EngineState.Running;
            AngularVelocity = _definition.Engine.IdleRpm * Units.RpmToRadPerSec;
        }

        // ==================================================================
        // Simulation step
        // ==================================================================
        /// <summary>
        /// Advances the engine one physics step.
        /// </summary>
        /// <param name="throttleInput">Raw pedal travel, 0..1.</param>
        /// <param name="mode">Active drive mode, supplying the throttle map and response.</param>
        /// <param name="clutchReactionTorqueNm">
        /// Torque the clutch is taking off the crankshaft this step. Positive means
        /// the driveline is loading the engine. Supplied by the transmission.
        /// </param>
        /// <param name="clutchEngaged">True when the clutch can stall the engine.</param>
        public void Tick(float throttleInput, DriveModeSettings mode,
                         float clutchReactionTorqueNm, bool clutchEngaged, float deltaTime)
        {
            var config = _definition.Engine;
            BlowOffTriggered = false;
            RequestedThrottle = Mathf.Clamp01(throttleInput);

            // --- Throttle map and response ---------------------------------
            // Comfort and Sport differ here first: a different pedal curve and a
            // different half-life. This is a real change in commanded load, so it
            // propagates into torque, boost, gearbox behaviour and audio for free.
            float mapped = mode != null && mode.ThrottleMap != null
                ? Mathf.Clamp01(mode.ThrottleMap.Evaluate(RequestedThrottle))
                : RequestedThrottle;

            float halfLife = mode != null ? mode.ThrottleResponseHalfLife : 0.09f;
            EffectiveThrottle = SimMath.Damp(EffectiveThrottle, mapped, halfLife, deltaTime);

            // --- Ignition state machine ------------------------------------
            switch (State)
            {
                case EngineState.Off:
                case EngineState.Stalled:
                    EffectiveThrottle = 0f;
                    BoostBar = 0f;
                    // The crank still spins down through friction and is still
                    // coupled to the driveline, so a stalled car in gear drags.
                    IntegrateFreeSpinDown(clutchReactionTorqueNm, deltaTime);
                    OutputTorqueNm = 0f;
                    CombustionTorqueNm = 0f;
                    UpdateBoost(0f, deltaTime);
                    _previousThrottle = 0f;
                    return;

                case EngineState.Starting:
                    _starterTimer += deltaTime;
                    // Crank up toward idle. Real starters take the engine to roughly
                    // 250-350 rpm before it fires; here we ramp to idle over the
                    // configured duration so the audio and the needle both have
                    // something honest to follow.
                    float crankTarget = config.IdleRpm * 1.08f * Units.RpmToRadPerSec;
                    AngularVelocity = Mathf.MoveTowards(AngularVelocity, crankTarget,
                        crankTarget / Mathf.Max(0.05f, config.StarterDurationSeconds) * deltaTime);
                    if (_starterTimer >= config.StarterDurationSeconds)
                        State = EngineState.Running;
                    OutputTorqueNm = 0f;
                    CombustionTorqueNm = 0f;
                    return;
            }

            // --- Running ----------------------------------------------------
            float rpm = Rpm;

            // Rev limiter: a genuine fuel cut with a short dwell, so the engine
            // bounces off the limiter instead of sitting pinned at a value.
            if (_limiterCutTimer > 0f)
            {
                _limiterCutTimer -= deltaTime;
                LimiterActive = true;
            }
            else if (rpm >= config.RevLimiterRpm)
            {
                _limiterCutTimer = LimiterCutDuration;
                LimiterActive = true;
            }
            else
            {
                LimiterActive = false;
            }

            float fuelCut = LimiterActive ? 0f : 1f;

            // --- Boost -------------------------------------------------------
            float boostDemand = TargetBoost(rpm, EffectiveThrottle);
            DetectBlowOff();
            UpdateBoost(boostDemand, deltaTime);

            float boostMultiplier = 1f;
            if (config.Aspiration != Aspiration.NaturallyAspirated)
            {
                // Normalised against the definition's own boost ceiling, not the tuned
                // one, so that a turbo upgrade raising MaxBoostBar pushes the fraction
                // past 1 and genuinely adds torque instead of being renormalised away.
                float boostFraction = BoostBar / Mathf.Max(0.01f, config.MaxBoostBar);
                boostMultiplier = _definition.BoostMultiplierFromFraction(boostFraction);
            }

            // --- Combustion torque ------------------------------------------
            // Torque = curve(RPM) * throttle * boost * tune. Nothing else.
            float naturalTorque = _definition.EvaluateNaturalTorque(rpm);
            CombustionTorqueNm = naturalTorque * EffectiveThrottle * boostMultiplier
                                 * TorqueMultiplier * fuelCut;

            // --- Friction and idle governor ---------------------------------
            float friction = config.FrictionTorqueNm + config.FrictionTorquePerRadPerSec * Mathf.Abs(AngularVelocity);

            // A real ECU holds idle with a throttle bypass. Without this the engine
            // would die every time you lift off, which is not what an automatic car does.
            float idleAssist = 0f;
            if (rpm < config.IdleRpm)
            {
                float deficit = SimMath.Remap01(config.IdleRpm - rpm, 0f, config.IdleRpm * 0.5f);
                idleAssist = deficit * (friction + 25f) * 1.6f;
            }

            OnOverrun = EffectiveThrottle < 0.05f && clutchEngaged && rpm > config.IdleRpm * 1.6f;

            float netTorque = CombustionTorqueNm + idleAssist - friction - clutchReactionTorqueNm;
            OutputTorqueNm = CombustionTorqueNm + idleAssist - friction;

            AngularVelocity += netTorque / Mathf.Max(0.02f, config.InertiaKgM2) * deltaTime;
            AngularVelocity = Mathf.Max(0f, AngularVelocity);

            // --- Stall --------------------------------------------------------
            if (clutchEngaged && Rpm < config.StallRpm)
            {
                State = EngineState.Stalled;
                BoostBar = 0f;
            }

            _previousThrottle = RequestedThrottle;
        }

        /// <summary>Spins the crank down when the engine is not firing.</summary>
        private void IntegrateFreeSpinDown(float clutchReactionTorqueNm, float deltaTime)
        {
            var config = _definition.Engine;
            float friction = config.FrictionTorqueNm + config.FrictionTorquePerRadPerSec * Mathf.Abs(AngularVelocity);
            AngularVelocity -= (friction + clutchReactionTorqueNm) / Mathf.Max(0.02f, config.InertiaKgM2) * deltaTime;
            AngularVelocity = Mathf.Max(0f, AngularVelocity);
        }

        private float TargetBoost(float rpm, float throttle)
        {
            var config = _definition.Engine;
            if (config.Aspiration == Aspiration.NaturallyAspirated) return 0f;

            float maxBoost = Mathf.Max(0f, config.MaxBoostBar + BoostBarOffset);
            float spoolWindow = SimMath.Remap01(rpm, config.BoostOnsetRpm, config.BoostFullRpm);

            if (config.Aspiration == Aspiration.Supercharged)
            {
                // Belt driven: boost tracks engine speed almost linearly and there is
                // essentially no lag, which is why the two aspiration types feel different.
                return maxBoost * Mathf.Max(spoolWindow, rpm / Mathf.Max(1f, config.RedlineRpm)) * throttle;
            }

            // Exhaust driven: boost needs both revs and load.
            return maxBoost * spoolWindow * throttle;
        }

        private void UpdateBoost(float target, float deltaTime)
        {
            var config = _definition.Engine;
            bool building = target > BoostBar;
            float halfLife = building
                ? config.BoostSpoolHalfLife * Mathf.Max(0.05f, SpoolSpeedMultiplier)
                : config.BoostDecayHalfLife;

            BoostBar = SimMath.Damp(BoostBar, target, halfLife, deltaTime);
            BoostBar = Mathf.Max(0f, BoostBar);
        }

        private void DetectBlowOff()
        {
            var config = _definition.Engine;
            if (config.Aspiration != Aspiration.Turbocharged) return;

            bool snapShut = _previousThrottle > 0.35f && RequestedThrottle < 0.10f;
            if (snapShut && BoostBar >= config.BlowOffThresholdBar)
                BlowOffTriggered = true;
        }
    }
}
