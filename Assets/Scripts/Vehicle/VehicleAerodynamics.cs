using System;
using IndieGame.Core;
using IndieGame.Vehicles.Data;
using UnityEngine;

namespace IndieGame.Vehicles
{
    /// <summary>
    /// Aerodynamic drag and downforce.
    ///
    /// Drag rises with the square of speed, which is what actually sets the car's
    /// top speed: the car stops accelerating when the drive force at the wheels
    /// can no longer overcome drag plus rolling resistance. Nothing in this
    /// project clamps speed to a configured number.
    /// </summary>
    [Serializable]
    public class VehicleAerodynamics
    {
        private VehicleDefinition _definition;

        /// <summary>Drag force magnitude applied this step, newtons.</summary>
        public float DragForceN { get; private set; }

        public float FrontDownforceN { get; private set; }
        public float RearDownforceN { get; private set; }

        /// <summary>Added by a spoiler or splitter in Phase 7. Multiplies the definition's coefficients.</summary>
        public float DownforceMultiplier = 1f;
        public float DragMultiplier = 1f;

        public void Initialise(VehicleDefinition definition)
        {
            _definition = definition;
        }

        public void Apply(Rigidbody body, float deltaTime)
        {
            var config = _definition.Aero;
            Vector3 velocity = body.GetLinearVelocity();
            float speed = velocity.magnitude;

            if (speed < 0.05f)
            {
                DragForceN = 0f;
                FrontDownforceN = 0f;
                RearDownforceN = 0f;
                return;
            }

            // F_drag = 0.5 * rho * Cd * A * v^2
            float dynamicPressure = 0.5f * Units.AirDensity * speed * speed;
            DragForceN = dynamicPressure * config.DragCoefficient * config.FrontalAreaM2 * DragMultiplier;

            Vector3 dragPoint = body.transform.TransformPoint(config.CentreOfPressureOffset);
            body.AddForceAtPosition(-velocity.normalized * DragForceN, dragPoint, ForceMode.Force);

            // Downforce is applied at the axles rather than at one point, so a rear
            // wing genuinely loads the rear tyres and shifts the aero balance.
            FrontDownforceN = dynamicPressure * config.FrontDownforceCoefficient * config.FrontalAreaM2 * DownforceMultiplier;
            RearDownforceN = dynamicPressure * config.RearDownforceCoefficient * config.FrontalAreaM2 * DownforceMultiplier;

            float halfWheelbase = _definition.Chassis.WheelbaseM * 0.5f;
            Vector3 down = -body.transform.up;
            Vector3 frontPoint = body.transform.TransformPoint(new Vector3(0f, config.CentreOfPressureOffset.y, halfWheelbase));
            Vector3 rearPoint = body.transform.TransformPoint(new Vector3(0f, config.CentreOfPressureOffset.y, -halfWheelbase));

            body.AddForceAtPosition(down * FrontDownforceN, frontPoint, ForceMode.Force);
            body.AddForceAtPosition(down * RearDownforceN, rearPoint, ForceMode.Force);
        }
    }
}
