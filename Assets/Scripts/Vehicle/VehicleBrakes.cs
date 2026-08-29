using System;
using IndieGame.Vehicles.Data;
using UnityEngine;

namespace IndieGame.Vehicles
{
    /// <summary>
    /// Friction brakes with front/rear bias, a separate handbrake circuit on the
    /// rear axle, and per-wheel ABS.
    ///
    /// ABS here works the way a real one does: it watches each wheel's slip ratio,
    /// and when a wheel starts to lock it releases pressure, lets the wheel spin
    /// back up, then reapplies. It modulates at a fixed frequency and switches
    /// itself off below walking pace, which is why an ABS car still chirps its
    /// tyres in a car park.
    /// </summary>
    [Serializable]
    public class VehicleBrakes
    {
        private VehicleDefinition _definition;

        /// <summary>Set false by the settings menu when the driver disables the assist.</summary>
        public bool AbsEnabled = true;

        /// <summary>Scales all brake torque. A brake upgrade raises this.</summary>
        public float BrakeTorqueMultiplier = 1f;

        /// <summary>0..1, moves braking force rearward as it rises. 0.5 is the definition's own bias.</summary>
        public float BrakeBiasAdjustment = 0.5f;

        public bool AnyAbsActive { get; private set; }

        private float[] _absPhase;

        public void Initialise(VehicleDefinition definition, int wheelCount)
        {
            _definition = definition;
            AbsEnabled = definition.Brakes.AbsAvailable;
            _absPhase = new float[wheelCount];
        }

        /// <summary>
        /// Writes <see cref="VehicleWheel.BrakeTorqueNm"/> on every wheel.
        /// </summary>
        /// <param name="brakeInput">0..1 pedal travel.</param>
        /// <param name="handbrakeInput">0..1 lever travel.</param>
        /// <param name="speedMps">Forward speed of the car, used to disable ABS at a crawl.</param>
        public void Apply(VehicleWheel[] wheels, float brakeInput, float handbrakeInput,
                          float speedMps, float deltaTime)
        {
            var config = _definition.Brakes;
            AnyAbsActive = false;

            // Bias adjustment redistributes without changing total capacity, so
            // moving the bias rearward under-brakes the front rather than adding grip.
            float biasShift = (BrakeBiasAdjustment - 0.5f) * 2f; // -1 fully front, +1 fully rear
            float frontScale = Mathf.Clamp01(1f - Mathf.Max(0f, biasShift) * 0.5f);
            float rearScale = Mathf.Clamp01(1f + Mathf.Min(0f, biasShift) * 0.5f);

            for (int i = 0; i < wheels.Length; i++)
            {
                var wheel = wheels[i];

                float maximum = wheel.IsFrontAxle
                    ? config.MaxTorqueFrontNm * frontScale
                    : config.MaxTorqueRearNm * rearScale;

                float torque = maximum * BrakeTorqueMultiplier * Mathf.Clamp01(brakeInput);

                // ABS modulates the service brake only.
                wheel.AbsActive = false;
                if (AbsEnabled && config.AbsAvailable && torque > 1f &&
                    Mathf.Abs(speedMps) > config.AbsMinSpeedMps && wheel.IsGrounded)
                {
                    torque = ModulateAbs(wheel, i, torque, config, deltaTime);
                }

                // The handbrake acts on its own circuit, is not regulated by ABS,
                // and is what makes a deliberate handbrake turn possible.
                if (wheel.HasHandbrake)
                    torque = Mathf.Max(torque, config.HandbrakeTorqueNm * Mathf.Clamp01(handbrakeInput));

                wheel.BrakeTorqueNm = torque;
            }
        }

        private float ModulateAbs(VehicleWheel wheel, int index, float torque,
                                  VehicleDefinition.BrakeConfig config, float deltaTime)
        {
            bool locking = wheel.SlipRatio < -config.AbsSlipThreshold;

            if (locking)
            {
                _absPhase[index] += deltaTime * config.AbsCycleHz;
                wheel.AbsActive = true;
                AnyAbsActive = true;

                // Square-wave release/apply at the modulation frequency.
                bool releasing = (_absPhase[index] % 1f) < 0.55f;
                if (releasing)
                    torque *= config.AbsReleaseFactor;
            }
            else
            {
                _absPhase[index] = 0f;
            }

            return torque;
        }
    }
}
