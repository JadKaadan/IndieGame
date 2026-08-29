using System;
using IndieGame.Vehicles.Data;
using UnityEngine;

namespace IndieGame.Vehicles
{
    /// <summary>
    /// Steering rack. Converts driver input into road wheel angles with a rate
    /// limit, speed-sensitive assistance and Ackermann geometry.
    ///
    /// The rate limit is why the car does not snap to full lock, the speed
    /// sensitivity is why it is not lethal at 250 km/h, and Ackermann is why the
    /// inside wheel turns more sharply than the outside one at parking speeds.
    /// </summary>
    [Serializable]
    public class VehicleSteering
    {
        private VehicleDefinition _definition;

        /// <summary>Current rack position, -1..1. Negative is left.</summary>
        public float RackPosition { get; private set; }

        /// <summary>Road wheel angle in degrees before Ackermann is applied.</summary>
        public float RoadWheelAngleDeg { get; private set; }

        /// <summary>Cockpit steering wheel rotation in degrees. Drives the interior wheel mesh.</summary>
        public float SteeringWheelAngleDeg { get; private set; }

        /// <summary>Fraction of maximum lock currently available because of speed sensitivity.</summary>
        public float AvailableLockFraction { get; private set; } = 1f;

        public void Initialise(VehicleDefinition definition)
        {
            _definition = definition;
            RackPosition = 0f;
        }

        /// <summary>Writes <see cref="VehicleWheel.SteerAngleDeg"/> on every steered wheel.</summary>
        public void Apply(VehicleWheel[] wheels, float steerInput, float speedMps,
                          DriveModeSettings driveMode, float deltaTime)
        {
            var config = _definition.Steering;
            float target = Mathf.Clamp(steerInput, -1f, 1f);

            float rateMultiplier = driveMode != null ? driveMode.SteeringRateMultiplier : 1f;
            bool returningToCentre = Mathf.Abs(target) < Mathf.Abs(RackPosition);

            float rateDegPerSec = (returningToCentre ? config.SteerReturnRateDegPerSec : config.SteerRateDegPerSec)
                                  * rateMultiplier;
            float rackRate = rateDegPerSec / Mathf.Max(1f, config.MaxSteerAngleDeg);

            RackPosition = Mathf.MoveTowards(RackPosition, target, rackRate * deltaTime);

            // Speed-sensitive assist. The drive mode's assist multiplier lets Sport
            // hand the driver more angle at speed than Comfort does.
            float assist = driveMode != null ? driveMode.SteeringAssistMultiplier : 1f;
            float rawSensitivity = Mathf.Clamp01(config.SpeedSensitivity.Evaluate(Mathf.Abs(speedMps)));
            AvailableLockFraction = Mathf.Clamp01(Mathf.Lerp(1f, rawSensitivity, Mathf.Clamp01(assist)));

            float steerAngle = RackPosition * config.MaxSteerAngleDeg * AvailableLockFraction;
            RoadWheelAngleDeg = steerAngle;

            // The cockpit wheel rotates the full mechanical lock regardless of the
            // electronic angle limit, because the rack and the wheel are physically
            // connected. A 720 degree lock means 360 degrees of rotation each way.
            SteeringWheelAngleDeg = RackPosition * config.SteeringWheelLockDeg * 0.5f;

            ApplySteerAngles(wheels, steerAngle, config);
        }

        /// <summary>
        /// Ackermann geometry: both front wheels must trace circles about the same
        /// centre, so the inside wheel needs a larger angle than the outside one.
        /// </summary>
        private void ApplySteerAngles(VehicleWheel[] wheels, float steerAngleDeg,
                                      VehicleDefinition.SteeringConfig config)
        {
            float insideAngle = steerAngleDeg;
            float outsideAngle = steerAngleDeg;

            float magnitude = Mathf.Abs(steerAngleDeg);
            if (magnitude > 0.05f && config.AckermannFactor > 0.001f)
            {
                float wheelbase = Mathf.Max(0.5f, _definition.Chassis.WheelbaseM);
                float halfTrack = Mathf.Max(0.25f, _definition.Chassis.TrackWidthM) * 0.5f;

                // Turn radius to the centre of the rear axle.
                float turnRadius = wheelbase / Mathf.Tan(magnitude * Mathf.Deg2Rad);

                float idealInside = Mathf.Atan(wheelbase / Mathf.Max(0.1f, turnRadius - halfTrack)) * Mathf.Rad2Deg;
                float idealOutside = Mathf.Atan(wheelbase / (turnRadius + halfTrack)) * Mathf.Rad2Deg;

                float blendedInside = Mathf.Lerp(magnitude, idealInside, config.AckermannFactor);
                float blendedOutside = Mathf.Lerp(magnitude, idealOutside, config.AckermannFactor);

                float sign = Mathf.Sign(steerAngleDeg);
                insideAngle = blendedInside * sign;
                outsideAngle = blendedOutside * sign;
            }

            // Turning right (positive angle) makes the right-hand wheel the inside one.
            float turnSign = Mathf.Sign(steerAngleDeg);

            for (int i = 0; i < wheels.Length; i++)
            {
                var wheel = wheels[i];
                if (!wheel.IsSteered)
                {
                    wheel.SteerAngleDeg = 0f;
                    continue;
                }

                bool isInside = magnitude > 0.05f && Mathf.Approximately(Mathf.Sign(wheel.LateralSign), turnSign);
                wheel.SteerAngleDeg = isInside ? insideAngle : outsideAngle;
            }
        }
    }
}
