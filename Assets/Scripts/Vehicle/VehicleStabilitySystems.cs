using System;
using IndieGame.Core;
using IndieGame.Vehicles.Data;
using UnityEngine;

namespace IndieGame.Vehicles
{
    /// <summary>
    /// Traction control and electronic stability control.
    ///
    /// Neither system is allowed to glue the car to the road. Traction control
    /// only reduces the throttle the engine is given, and stability control only
    /// applies brake torque to individual wheels - the same two levers the real
    /// systems have. If the tyres are past their limit, the car still slides.
    ///
    /// The drive mode decides how much slip each system tolerates, which is why
    /// Sport lets the rear step out and Comfort does not.
    /// </summary>
    [Serializable]
    public class VehicleStabilitySystems
    {
        private VehicleDefinition _definition;

        public bool TractionControlEnabled = true;
        public bool StabilityControlEnabled = true;

        /// <summary>0..1 multiplier the traction control is applying to driver throttle.</summary>
        public float ThrottleReduction { get; private set; } = 1f;

        public bool TractionControlActive { get; private set; }
        public bool StabilityControlActive { get; private set; }

        /// <summary>Yaw rate the steering angle and speed are asking for, rad/s.</summary>
        public float TargetYawRate { get; private set; }

        /// <summary>Yaw rate the car is actually producing, rad/s.</summary>
        public float ActualYawRate { get; private set; }

        public void Initialise(VehicleDefinition definition)
        {
            _definition = definition;
            TractionControlEnabled = definition.Stability.TractionControlAvailable;
            StabilityControlEnabled = definition.Stability.StabilityControlAvailable;
        }

        /// <summary>
        /// Returns the throttle multiplier to hand the engine. Call before the
        /// engine ticks.
        /// </summary>
        public float EvaluateTractionControl(VehicleWheel[] wheels, DriveModeSettings driveMode, float deltaTime)
        {
            TractionControlActive = false;

            if (!TractionControlEnabled || !_definition.Stability.TractionControlAvailable)
            {
                ThrottleReduction = SimMath.Damp(ThrottleReduction, 1f, 0.05f, deltaTime);
                return ThrottleReduction;
            }

            float allowance = driveMode != null ? driveMode.TractionControlSlipAllowance : 0.10f;
            float worstExcess = 0f;

            for (int i = 0; i < wheels.Length; i++)
            {
                var wheel = wheels[i];
                if (!wheel.IsDriven || !wheel.IsGrounded) continue;
                // Only positive slip matters here - negative slip is braking, which is ABS's job.
                float excess = wheel.SlipRatio - allowance;
                if (excess > worstExcess) worstExcess = excess;
            }

            float target = 1f;
            if (worstExcess > 0f)
            {
                TractionControlActive = true;
                target = Mathf.Clamp01(1f - worstExcess * _definition.Stability.TractionControlGain);
            }

            // Cut quickly, restore gently - the same asymmetry real systems use so
            // the car does not surge as grip returns.
            float halfLife = target < ThrottleReduction ? 0.015f : 0.12f;
            ThrottleReduction = SimMath.Damp(ThrottleReduction, target, halfLife, deltaTime);
            return ThrottleReduction;
        }

        /// <summary>
        /// Adds corrective brake torque to individual wheels. Call after the brakes
        /// have written their own torque, so this can only ever add to it.
        /// </summary>
        public void ApplyStabilityControl(Rigidbody body, VehicleWheel[] wheels,
                                          float roadWheelAngleDeg, float speedMps,
                                          DriveModeSettings driveMode)
        {
            StabilityControlActive = false;

            var config = _definition.Stability;
            float strength = driveMode != null ? driveMode.StabilityControlStrength : 1f;

            if (!StabilityControlEnabled || !config.StabilityControlAvailable || strength <= 0.001f)
                return;

            // Below this speed yaw rate is meaningless and intervention just fights
            // the driver in a car park.
            if (Mathf.Abs(speedMps) < 4f) return;

            // Bicycle model target: yaw = v * tan(steer) / wheelbase.
            float wheelbase = Mathf.Max(0.5f, _definition.Chassis.WheelbaseM);
            TargetYawRate = speedMps * Mathf.Tan(roadWheelAngleDeg * Mathf.Deg2Rad) / wheelbase;

            Vector3 localAngular = body.transform.InverseTransformDirection(body.angularVelocity);
            ActualYawRate = localAngular.y;

            float error = TargetYawRate - ActualYawRate;
            if (Mathf.Abs(error) < config.StabilityYawErrorDeadZone) return;

            StabilityControlActive = true;

            float corrective = Mathf.Clamp(Mathf.Abs(error) * config.StabilityControlGain * strength,
                                           0f, config.StabilityControlMaxBrakeNm);

            bool understeering = Mathf.Abs(ActualYawRate) < Mathf.Abs(TargetYawRate);
            float turnSign = Mathf.Sign(TargetYawRate);

            for (int i = 0; i < wheels.Length; i++)
            {
                var wheel = wheels[i];
                if (!wheel.IsGrounded) continue;

                bool isInside = Mathf.Approximately(Mathf.Sign(wheel.LateralSign), turnSign);

                if (understeering)
                {
                    // The car is running wide: brake the inside rear to rotate it in.
                    if (!wheel.IsFrontAxle && isInside)
                        wheel.BrakeTorqueNm += corrective;
                }
                else
                {
                    // The car is rotating too much: brake the outside front to pull it straight.
                    if (wheel.IsFrontAxle && !isInside)
                        wheel.BrakeTorqueNm += corrective;
                }
            }
        }
    }
}
