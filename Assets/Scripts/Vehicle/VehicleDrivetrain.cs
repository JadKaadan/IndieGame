using System;
using IndieGame.Vehicles.Data;
using UnityEngine;

namespace IndieGame.Vehicles
{
    /// <summary>
    /// Splits driveline torque between axles and, within an axle, between the two
    /// wheels according to the differential type.
    ///
    /// Torque is never simply copied to every wheel. An open differential delivers
    /// equal torque to both sides, which is why lifting an inside wheel kills
    /// drive. A limited-slip differential biases torque toward the slower wheel in
    /// proportion to the speed difference, which is what lets a rear-drive car put
    /// power down out of a corner and hold a slide.
    /// </summary>
    [Serializable]
    public class VehicleDrivetrain
    {
        private VehicleDefinition _definition;

        /// <summary>Average angular velocity of the driven wheels, rad/s. This is the driveline speed.</summary>
        public float DrivenWheelAngularVelocity { get; private set; }

        public float FrontAxleTorqueNm { get; private set; }
        public float RearAxleTorqueNm { get; private set; }

        /// <summary>Bias torque the rear LSD is currently applying, Nm. Positive biases toward the right wheel.</summary>
        public float RearLockTorqueNm { get; private set; }
        public float FrontLockTorqueNm { get; private set; }

        public void Initialise(VehicleDefinition definition)
        {
            _definition = definition;
        }

        /// <summary>
        /// Recomputes driveline speed from the wheels. Call before the transmission
        /// ticks, since the clutch needs to know what speed it is coupling to.
        /// </summary>
        public void SampleDrivelineSpeed(VehicleWheel[] wheels)
        {
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < wheels.Length; i++)
            {
                if (!wheels[i].IsDriven) continue;
                sum += wheels[i].AngularVelocity;
                count++;
            }
            DrivenWheelAngularVelocity = count > 0 ? sum / count : 0f;
        }

        /// <summary>
        /// Writes <see cref="VehicleWheel.DriveTorqueNm"/> on every wheel.
        /// </summary>
        /// <param name="drivelineTorqueNm">Torque at the differential input, already through the gearing.</param>
        /// <param name="throttle">Used to pick the LSD power or coast ramp.</param>
        public void DistributeTorque(VehicleWheel[] wheels, float drivelineTorqueNm, float throttle)
        {
            var config = _definition.Drivetrain;

            float frontShare;
            switch (config.Layout)
            {
                case DriveLayout.FrontWheelDrive: frontShare = 1f; break;
                case DriveLayout.RearWheelDrive: frontShare = 0f; break;
                default: frontShare = Mathf.Clamp01(config.FrontTorqueSplit); break;
            }

            FrontAxleTorqueNm = drivelineTorqueNm * frontShare;
            RearAxleTorqueNm = drivelineTorqueNm * (1f - frontShare);

            for (int i = 0; i < wheels.Length; i++)
                wheels[i].DriveTorqueNm = 0f;

            FrontLockTorqueNm = ApplyAxle(wheels, true, FrontAxleTorqueNm, config.FrontDifferential, throttle);
            RearLockTorqueNm = ApplyAxle(wheels, false, RearAxleTorqueNm, config.RearDifferential, throttle);
        }

        /// <summary>Distributes one axle's torque across its two wheels. Returns the bias torque applied.</summary>
        private float ApplyAxle(VehicleWheel[] wheels, bool frontAxle, float axleTorqueNm,
                                DifferentialType differential, float throttle)
        {
            VehicleWheel left = null;
            VehicleWheel right = null;

            for (int i = 0; i < wheels.Length; i++)
            {
                var wheel = wheels[i];
                if (!wheel.IsDriven || wheel.IsFrontAxle != frontAxle) continue;
                if (wheel.LateralSign < 0f) left = wheel; else right = wheel;
            }

            if (left == null && right == null) return 0f;

            // Single driven wheel on this axle (three-wheeler, or a partly authored car).
            if (left == null) { right.DriveTorqueNm = axleTorqueNm; return 0f; }
            if (right == null) { left.DriveTorqueNm = axleTorqueNm; return 0f; }

            float half = axleTorqueNm * 0.5f;
            float lockTorque = 0f;
            var config = _definition.Drivetrain;

            switch (differential)
            {
                case DifferentialType.Open:
                    // Equal torque, independent speeds. Nothing further to do.
                    break;

                case DifferentialType.LimitedSlip:
                {
                    float speedDifference = left.AngularVelocity - right.AngularVelocity;
                    bool onPower = throttle > 0.05f;
                    float coefficient = onPower ? config.LsdPowerLockCoefficient : config.LsdCoastLockCoefficient;
                    float preload = Mathf.Sign(speedDifference) * config.LsdPreloadNm
                                    * Mathf.Clamp01(Mathf.Abs(speedDifference) * 4f);

                    lockTorque = Mathf.Clamp(speedDifference * coefficient + preload,
                                             -config.LsdMaxLockNm, config.LsdMaxLockNm);
                    break;
                }

                case DifferentialType.Locked:
                {
                    // Approximated as a very stiff LSD. A true kinematic lock would
                    // require solving both wheels as one inertia; this is stable and
                    // indistinguishable in feel below the lock limit.
                    float speedDifference = left.AngularVelocity - right.AngularVelocity;
                    lockTorque = Mathf.Clamp(speedDifference * config.LsdPowerLockCoefficient * 8f,
                                             -config.LsdMaxLockNm * 2f, config.LsdMaxLockNm * 2f);
                    break;
                }
            }

            // Bias torque is taken from the faster wheel and given to the slower one,
            // so the differential redistributes torque and never creates any.
            left.DriveTorqueNm = half - lockTorque;
            right.DriveTorqueNm = half + lockTorque;
            return lockTorque;
        }
    }
}
