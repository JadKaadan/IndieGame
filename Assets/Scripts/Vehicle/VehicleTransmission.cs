using System;
using IndieGame.Core;
using IndieGame.Vehicles.Data;
using UnityEngine;

namespace IndieGame.Vehicles
{
    /// <summary>
    /// Gearbox and clutch. Owns gear selection (automatic schedule or driver
    /// commanded), the torque cut during a shift, and the clutch that couples
    /// the engine to the driveline.
    ///
    /// The clutch is modelled as a torque-limited coupling rather than a boolean.
    /// When the torque required to hold the engine and driveline at the same
    /// speed is within the clutch's capacity, the driveline is effectively rigid
    /// and engine RPM equals wheel speed through the gearing - which is exactly
    /// what the tachometer then shows. When the required torque exceeds capacity
    /// (a launch from standstill, a dumped clutch, a shift) the clutch slips and
    /// the engine is free to rev away from the driveline.
    /// </summary>
    [Serializable]
    public class VehicleTransmission
    {
        public const int ReverseGear = -1;
        public const int NeutralGear = 0;

        private VehicleDefinition _definition;

        // --- State ---------------------------------------------------------
        /// <summary>-1 reverse, 0 neutral, 1..N forward.</summary>
        public int CurrentGear { get; private set; } = NeutralGear;

        public TransmissionMode Mode { get; set; } = TransmissionMode.Automatic;

        public bool IsShifting { get; private set; }

        /// <summary>Gear the box is shifting into, valid while <see cref="IsShifting"/>.</summary>
        public int TargetGear { get; private set; }

        /// <summary>0..1 clutch engagement. 1 is fully locked.</summary>
        public float ClutchLock { get; private set; }

        /// <summary>Torque the clutch is taking off the crankshaft this step, Nm.</summary>
        public float ClutchReactionTorqueNm { get; private set; }

        /// <summary>Torque delivered to the differential input this step, Nm.</summary>
        public float DrivelineTorqueNm { get; private set; }

        /// <summary>Combined gear and final drive ratio. Negative in reverse, zero in neutral.</summary>
        public float TotalRatio { get; private set; }

        /// <summary>Difference between engine speed and driveline speed at the crank, rad/s.</summary>
        public float ClutchSlipRadPerSec { get; private set; }

        /// <summary>Raised for one step when a gear change starts. Consumed by audio and VFX.</summary>
        public bool ShiftEventThisStep { get; private set; }

        /// <summary>True when the shift that just started was a downshift.</summary>
        public bool LastShiftWasDownshift { get; private set; }

        /// <summary>
        /// Throttle multiplier the gearbox is imposing. Drops to zero during a shift.
        ///
        /// Opening the clutch alone is not enough: with the driveline disconnected and
        /// the throttle still open, the engine would flare by around 2,000 rpm during a
        /// 0.12 s shift. Real paddle-shift and dual-clutch boxes cut ignition or fuel
        /// for the duration, which is also what produces the audible bark on an upshift.
        /// </summary>
        public float ShiftThrottleCut { get; private set; } = 1f;

        // --- Tuning (Phase 7) ----------------------------------------------
        /// <summary>Scales every forward gear ratio. Shorter ratios accelerate harder and top out sooner.</summary>
        public float GearRatioMultiplier = 1f;

        /// <summary>Scales the final drive.</summary>
        public float FinalDriveMultiplier = 1f;

        /// <summary>Scales shift duration on top of the drive mode multiplier.</summary>
        public float ShiftSpeedMultiplier = 1f;

        // --- Internals ------------------------------------------------------
        private float _shiftTimer;
        private float _shiftCooldown;
        private float _lastEngineTorqueNm;

        /// <summary>
        /// Fraction of a physics step over which the clutch tries to remove a speed
        /// difference. Below 1 makes the coupling stiffer; the capacity clamp keeps
        /// it stable regardless.
        /// </summary>
        private const float ClutchSyncGain = 0.8f;

        public void Initialise(VehicleDefinition definition)
        {
            _definition = definition;
            CurrentGear = NeutralGear;
            Mode = definition.Transmission.Type == TransmissionType.Manual
                ? TransmissionMode.Manual
                : TransmissionMode.Automatic;
            ClutchLock = 0f;
            TotalRatio = 0f;
        }

        // ==================================================================
        // Gear selection
        // ==================================================================
        public int ForwardGearCount => _definition.ForwardGearCount;

        public string GearLabel
        {
            get
            {
                if (CurrentGear == ReverseGear) return "R";
                if (CurrentGear == NeutralGear) return "N";
                return CurrentGear.ToString();
            }
        }

        public float GearRatio(int gear)
        {
            var t = _definition.Transmission;
            if (gear == NeutralGear) return 0f;
            if (gear == ReverseGear) return -t.ReverseGearRatio * GearRatioMultiplier;
            int index = gear - 1;
            if (index < 0 || index >= t.ForwardGearRatios.Length) return 0f;
            return t.ForwardGearRatios[index] * GearRatioMultiplier;
        }

        public float FinalDrive => _definition.Transmission.FinalDriveRatio * FinalDriveMultiplier;

        /// <summary>Engine RPM that a given gear would produce at the current wheel speed.</summary>
        public float PredictRpm(int gear, float wheelAngularVelocity)
        {
            float ratio = GearRatio(gear) * FinalDrive;
            return Mathf.Abs(wheelAngularVelocity * ratio) * Units.RadPerSecToRpm;
        }

        public void ShiftUp(float speedMps)
        {
            if (IsShifting) return;
            if (CurrentGear == ReverseGear) { BeginShift(NeutralGear); return; }
            if (CurrentGear == NeutralGear) { BeginShift(1); return; }
            if (CurrentGear < ForwardGearCount) BeginShift(CurrentGear + 1);
        }

        public void ShiftDown(float speedMps)
        {
            if (IsShifting) return;
            if (CurrentGear > 1) { BeginShift(CurrentGear - 1); return; }
            if (CurrentGear == 1)
            {
                // Only drop out of first when almost stopped, so a downshift request at
                // speed never selects neutral by accident.
                if (Mathf.Abs(speedMps) < 2f) BeginShift(NeutralGear);
                return;
            }
            if (CurrentGear == NeutralGear && Mathf.Abs(speedMps) < 2f) BeginShift(ReverseGear);
        }

        public void SelectGear(int gear, float speedMps)
        {
            if (IsShifting) return;
            if (gear == CurrentGear) return;
            if (gear < ReverseGear || gear > ForwardGearCount) return;
            if ((gear == ReverseGear || CurrentGear == ReverseGear) && Mathf.Abs(speedMps) > 3f) return;
            BeginShift(gear);
        }

        private void BeginShift(int gear)
        {
            var t = _definition.Transmission;
            LastShiftWasDownshift = gear < CurrentGear && gear > NeutralGear && CurrentGear > NeutralGear;
            TargetGear = gear;
            IsShifting = true;
            ShiftEventThisStep = true;
            _shiftTimer = 0f;
            // Neutral engages instantly - there is no synchro work to do.
            if (gear == NeutralGear)
            {
                CurrentGear = NeutralGear;
                IsShifting = false;
                _shiftCooldown = t.ShiftCooldownSeconds * 0.4f;
            }
        }

        // ==================================================================
        // Simulation step
        // ==================================================================
        /// <summary>
        /// Clears the one-step event flags. The controller calls this at the top of
        /// its FixedUpdate, before it forwards the driver's shift commands, so a
        /// shift requested this step is still reported by <see cref="ShiftEventThisStep"/>.
        /// </summary>
        public void BeginStep()
        {
            ShiftEventThisStep = false;
        }

        /// <summary>
        /// Advances gear selection and computes the clutch and driveline torques.
        /// Call after the wheels have been integrated and before the engine ticks,
        /// passing the engine's torque from the previous step as the feed-forward.
        /// </summary>
        public void Tick(VehicleEngine engine, float averageDrivenWheelOmega, float speedMps,
                         float throttleInput, float clutchPedal, DriveModeSettings driveMode,
                         float deltaTime)
        {
            var t = _definition.Transmission;

            if (_shiftCooldown > 0f) _shiftCooldown -= deltaTime;

            // --- Shift progress ---------------------------------------------
            if (IsShifting)
            {
                float modeMultiplier = driveMode != null ? driveMode.ShiftTimeMultiplier : 1f;
                float duration = Mathf.Max(0.01f,
                    t.ShiftTimeSeconds * modeMultiplier * Mathf.Max(0.1f, ShiftSpeedMultiplier));
                _shiftTimer += deltaTime;
                if (_shiftTimer >= duration)
                {
                    CurrentGear = TargetGear;
                    IsShifting = false;
                    _shiftCooldown = t.ShiftCooldownSeconds;
                }
            }
            else if (Mode == TransmissionMode.Automatic)
            {
                EvaluateAutomaticSchedule(engine, averageDrivenWheelOmega, speedMps, throttleInput, driveMode);
            }

            // The cut is abrupt on purpose - it is an ignition cut, not a lift.
            ShiftThrottleCut = IsShifting ? 0f : 1f;

            // --- Ratios -------------------------------------------------------
            TotalRatio = GearRatio(CurrentGear) * FinalDrive;
            bool inGear = Mathf.Abs(TotalRatio) > 0.0001f;

            // --- Clutch engagement --------------------------------------------
            float engagement = 0f;
            if (inGear && !IsShifting)
            {
                float idleRpm = Mathf.Max(1f, _definition.Engine.IdleRpm);
                float drivelineRpm = Mathf.Abs(averageDrivenWheelOmega * TotalRatio) * Units.RadPerSecToRpm;

                // Below roughly half idle speed the clutch must slip or the engine
                // would be dragged to a stop. This is what makes a standing start work.
                float launchEngagement = SimMath.Remap01(drivelineRpm, idleRpm * 0.45f, idleRpm * 1.15f);

                // Creeping off the line on throttle alone engages a little sooner,
                // the way a torque converter or a DCT's launch map behaves.
                if (throttleInput > 0.05f)
                    launchEngagement = Mathf.Max(launchEngagement, Mathf.Min(0.55f, throttleInput * 0.7f));

                engagement = launchEngagement * (1f - Mathf.Clamp01(clutchPedal));
            }

            ClutchLock = SimMath.Damp(ClutchLock, engagement, 0.03f, deltaTime);

            // --- Clutch torque -------------------------------------------------
            if (!inGear || ClutchLock < 0.01f)
            {
                ClutchReactionTorqueNm = 0f;
                DrivelineTorqueNm = 0f;
                ClutchSlipRadPerSec = engine.AngularVelocity;
                _lastEngineTorqueNm = engine.OutputTorqueNm;
                return;
            }

            float drivelineOmegaAtCrank = averageDrivenWheelOmega * TotalRatio;
            ClutchSlipRadPerSec = engine.AngularVelocity - drivelineOmegaAtCrank;

            float capacity = t.ClutchMaxTorqueNm * ClutchLock;

            // A partly engaged clutch is being modulated - by a pedal, or by the
            // gearbox's own launch logic. What it transmits then tracks what the
            // engine is producing, so light throttle gives a gentle pull-away and
            // the engine holds its revs instead of being dragged down by the full
            // friction capacity. Once engagement completes, the full capacity is
            // available again, which is what lets a dumped clutch shock the driveline.
            // Tying the slipping limit to engine torque is also what gives traction
            // control authority during a launch.
            float slippingLimit = Mathf.Max(0f, _lastEngineTorqueNm) * 1.05f + 30f;
            float effectiveCapacity = Mathf.Min(capacity, Mathf.Lerp(slippingLimit, capacity, ClutchLock));

            // Feed-forward the engine's own torque so a locked clutch transmits the
            // full output with no steady-state speed error, then add a proportional
            // term that removes any drift. The clamp is what turns this into a real
            // friction clutch: past its capacity it simply slips.
            float syncStiffness = _definition.Engine.InertiaKgM2 / Mathf.Max(0.0001f, deltaTime) * ClutchSyncGain;
            float required = _lastEngineTorqueNm + ClutchSlipRadPerSec * syncStiffness;

            ClutchReactionTorqueNm = Mathf.Clamp(required, -effectiveCapacity, effectiveCapacity);
            DrivelineTorqueNm = ClutchReactionTorqueNm * TotalRatio * t.DriveEfficiency;

            _lastEngineTorqueNm = engine.OutputTorqueNm;
        }

        /// <summary>True when the clutch is engaged enough that the engine can be stalled by the driveline.</summary>
        public bool CanStallEngine => ClutchLock > 0.6f;

        // ==================================================================
        private void EvaluateAutomaticSchedule(VehicleEngine engine, float averageDrivenWheelOmega,
                                               float speedMps, float throttleInput, DriveModeSettings driveMode)
        {
            if (_shiftCooldown > 0f) return;
            if (CurrentGear <= NeutralGear) return;
            if (!engine.IsRunning) return;

            var t = _definition.Transmission;
            float upshiftOffset = driveMode != null ? driveMode.UpshiftRpmOffset : 0f;
            float downshiftOffset = driveMode != null ? driveMode.DownshiftRpmOffset : 0f;
            float kickdownThreshold = driveMode != null ? driveMode.KickdownThreshold : 0.85f;

            // Load-sensitive schedule: light throttle shifts early and short, full
            // throttle holds each gear far longer. Sport adds a flat offset on top.
            float upshiftRpm = t.BaseUpshiftRpm + upshiftOffset + throttleInput * t.ThrottleUpshiftRpmGain;
            upshiftRpm = Mathf.Min(upshiftRpm, _definition.Engine.RedlineRpm * 0.985f);

            float downshiftRpm = t.BaseDownshiftRpm + downshiftOffset;
            float rpm = engine.Rpm;
            float safeRpm = _definition.Engine.RevLimiterRpm * t.DownshiftRpmSafetyFactor;

            // Upshift
            if (CurrentGear < ForwardGearCount && rpm >= upshiftRpm)
            {
                BeginShift(CurrentGear + 1);
                return;
            }

            // Kickdown: a deliberate stab of throttle asks for the lowest gear that
            // will not over-rev the engine.
            if (throttleInput >= kickdownThreshold && CurrentGear > 1)
            {
                int candidate = CurrentGear - 1;
                float predicted = PredictRpm(candidate, averageDrivenWheelOmega);
                if (predicted < safeRpm && predicted > rpm + 600f)
                {
                    BeginShift(candidate);
                    return;
                }
            }

            // Coast downshift, with a guard so the box can never money-shift the engine.
            if (CurrentGear > 1 && rpm <= downshiftRpm)
            {
                int candidate = CurrentGear - 1;
                if (PredictRpm(candidate, averageDrivenWheelOmega) < safeRpm)
                    BeginShift(candidate);
            }
        }
    }
}
