using System;
using IndieGame.Core;
using IndieGame.Vehicles.Data;
using IndieGame.World;
using UnityEngine;

namespace IndieGame.Vehicles
{
    /// <summary>
    /// One corner of the car: a raycast suspension strut plus a tyre contact patch.
    ///
    /// This is a plain serializable class rather than a MonoBehaviour on purpose.
    /// The controller owns four of these and ticks them in a guaranteed order
    /// inside a single FixedUpdate. That removes script execution order as a
    /// source of physics bugs, keeps every force accumulation in one place, and
    /// makes the whole simulation step trivially serialisable for networking or
    /// replays later.
    ///
    /// Why not WheelCollider: WheelCollider carries its own hidden sprung-mass
    /// integrator and a two-point friction curve that cannot express a real slip
    /// curve. Its behaviour at low speed and under combined slip is poor, and it
    /// does not expose the intermediate quantities (slip ratio, load, wheel
    /// angular velocity) that the drivetrain, ABS, traction control, dashboard
    /// and audio all need to read. Building on it would mean fighting it, and
    /// replacing it later would mean re-tuning every car from scratch.
    /// </summary>
    [Serializable]
    public class VehicleWheel
    {
        // ==================================================================
        // Authoring
        // ==================================================================
        [Tooltip("Label used by the debug overlay, e.g. FL, FR, RL, RR.")]
        public string Name = "Wheel";

        [Tooltip("Empty transform at the top of the strut. The suspension ray is cast " +
                 "downward from here along the car's local down axis.")]
        public Transform SuspensionAnchor;

        [Tooltip("The wheel mesh. Moved, steered and spun by UpdateVisual. May be null.")]
        public Transform VisualWheel;

        public bool IsFrontAxle = true;
        public bool IsSteered = true;
        public bool IsDriven = false;
        public bool HasHandbrake = false;

        [Tooltip("-1 for a left-hand wheel, +1 for a right-hand wheel. Drives Ackermann and anti-roll pairing.")]
        public float LateralSign = -1f;

        // ==================================================================
        // Runtime state - read by the drivetrain, HUD, audio and VFX.
        // Never written from outside except through the methods below.
        // ==================================================================
        [NonSerialized] public bool IsGrounded;
        [NonSerialized] public Vector3 ContactPoint;
        [NonSerialized] public Vector3 ContactNormal = Vector3.up;
        [NonSerialized] public Collider ContactCollider;
        [NonSerialized] public SurfaceType Surface = SurfaceType.Asphalt;
        [NonSerialized] public float SurfaceFrictionScale = 1f;
        [NonSerialized] public float SurfaceExtraRollingResistance;

        /// <summary>Suspension compression in metres, 0 at full droop.</summary>
        [NonSerialized] public float CompressionM;

        /// <summary>Compression as a fraction of total travel, 0..1.</summary>
        [NonSerialized] public float CompressionNormalised;

        /// <summary>Rate of compression in m/s. Positive while compressing.</summary>
        [NonSerialized] public float CompressionVelocity;

        /// <summary>Vertical force the strut is pushing the body up with, in newtons.</summary>
        [NonSerialized] public float SuspensionForceN;

        /// <summary>Vertical load carried by the contact patch, in newtons.</summary>
        [NonSerialized] public float LoadN;

        /// <summary>Wheel spin rate in rad/s. Positive is forward rotation.</summary>
        [NonSerialized] public float AngularVelocity;

        /// <summary>Filtered longitudinal slip ratio, dimensionless.</summary>
        [NonSerialized] public float SlipRatio;

        /// <summary>Filtered lateral slip angle in radians.</summary>
        [NonSerialized] public float SlipAngleRad;

        [NonSerialized] public float LongitudinalForceN;
        [NonSerialized] public float LateralForceN;
        [NonSerialized] public float TireSaturation;

        [NonSerialized] public float SteerAngleDeg;
        [NonSerialized] public float DriveTorqueNm;
        [NonSerialized] public float BrakeTorqueNm;
        [NonSerialized] public bool AbsActive;

        /// <summary>Speed of the contact patch along the wheel's forward axis, m/s.</summary>
        [NonSerialized] public float ForwardSpeed;

        /// <summary>Speed of the contact patch along the wheel's lateral axis, m/s.</summary>
        [NonSerialized] public float LateralSpeed;

        /// <summary>Upper bound on damper shaft velocity, m/s. Guards against landing spikes.</summary>
        private const float MaxDamperVelocityMps = 5f;

        // Internal
        private float _visualSpinDeg;
        private float _lastSuspensionLength;
        private float _antiRollForceN;
        private Transform _root;

        // ==================================================================
        public void Initialise(Transform vehicleRoot, VehicleDefinition.SuspensionAxleConfig axle)
        {
            _root = vehicleRoot;
            _lastSuspensionLength = axle.RestLengthM;
            AngularVelocity = 0f;
            _visualSpinDeg = 0f;
        }

        /// <summary>World-space direction the strut compresses along (the car's up axis).</summary>
        public Vector3 SuspensionUp => SuspensionAnchor != null ? SuspensionAnchor.up : Vector3.up;

        /// <summary>World-space forward direction of the steered wheel, projected onto the contact plane.</summary>
        public Vector3 WheelForward { get; private set; } = Vector3.forward;

        /// <summary>World-space right direction of the steered wheel, projected onto the contact plane.</summary>
        public Vector3 WheelRight { get; private set; } = Vector3.right;

        // ==================================================================
        // Step 1: suspension
        // ==================================================================
        /// <summary>
        /// Casts the strut, resolves contact, and computes the spring/damper force.
        /// The force is stored rather than applied so the anti-roll bar can adjust
        /// the pair before anything reaches the rigidbody.
        /// </summary>
        public void CastSuspension(Rigidbody body, VehicleDefinition definition,
                                   VehicleDefinition.SuspensionAxleConfig axle,
                                   float springMultiplier, float damperMultiplier,
                                   float deltaTime)
        {
            SuspensionForceN = 0f;
            _antiRollForceN = 0f;

            if (SuspensionAnchor == null)
            {
                IsGrounded = false;
                return;
            }

            float radius = definition.Wheels.RadiusM;
            Vector3 origin = SuspensionAnchor.position;
            Vector3 down = -SuspensionAnchor.up;
            float maxDistance = axle.RestLengthM + radius;

            IsGrounded = Physics.Raycast(origin, down, out RaycastHit hit, maxDistance,
                                         definition.Wheels.GroundMask, QueryTriggerInteraction.Ignore);

            if (!IsGrounded)
            {
                // Full droop. Track the length so the damper does not spike on landing.
                CompressionM = 0f;
                CompressionNormalised = 0f;
                CompressionVelocity = 0f;
                LoadN = 0f;
                ContactCollider = null;
                SurfaceFrictionScale = SurfaceDescriptor.DefaultFrictionScale;
                SurfaceExtraRollingResistance = 0f;
                _lastSuspensionLength = axle.RestLengthM;
                return;
            }

            ContactPoint = hit.point;
            ContactNormal = hit.normal;
            ContactCollider = hit.collider;
            ResolveSurface(hit.collider);

            // Suspension length is the distance from anchor to wheel centre.
            float suspensionLength = Mathf.Clamp(hit.distance - radius, 0f, axle.RestLengthM);
            CompressionM = Mathf.Clamp(axle.RestLengthM - suspensionLength, 0f, axle.MaxTravelM);
            CompressionNormalised = axle.MaxTravelM > 0.0001f ? CompressionM / axle.MaxTravelM : 0f;

            // Compression velocity from the geometric change in strut length.
            // Using the length delta rather than the body's point velocity keeps
            // the damper stable when driving over a moving or uneven surface.
            // Clamped because a wheel that lands after being airborne would otherwise
            // report a several-hundred-m/s damper velocity for one step and launch the car.
            // Real damper shaft speeds stay under about 3 m/s.
            CompressionVelocity = Mathf.Clamp(
                (_lastSuspensionLength - suspensionLength) / Mathf.Max(0.0001f, deltaTime),
                -MaxDamperVelocityMps, MaxDamperVelocityMps);
            _lastSuspensionLength = suspensionLength;

            float springRate = axle.SpringRateNPerM * springMultiplier;
            float springForce = springRate * CompressionM;

            // Bump stop: a very stiff progressive spring over the last part of travel.
            float bumpStopStart = axle.MaxTravelM * (1f - axle.BumpStopZone);
            if (axle.BumpStopZone > 0.0001f && CompressionM > bumpStopStart)
            {
                float intoStop = CompressionM - bumpStopStart;
                springForce += axle.BumpStopRateNPerM * intoStop * intoStop / Mathf.Max(0.001f, axle.MaxTravelM * axle.BumpStopZone);
            }

            // Asymmetric damping: compression is softer than rebound on a real damper.
            float dampingRate = CompressionVelocity > 0f
                ? axle.CompressionDampingNsPerM
                : axle.ReboundDampingNsPerM;
            float damperForce = dampingRate * damperMultiplier * CompressionVelocity;

            // A strut can push but never pull.
            SuspensionForceN = Mathf.Max(0f, springForce + damperForce);
        }

        /// <summary>Anti-roll bars couple the two wheels on an axle. Called between cast and apply.</summary>
        public static void ApplyAntiRollBar(VehicleWheel left, VehicleWheel right,
                                            VehicleDefinition.SuspensionAxleConfig axle,
                                            float multiplier)
        {
            if (axle.AntiRollBarNPerM <= 0.0001f) return;
            if (!left.IsGrounded && !right.IsGrounded) return;

            float travelDifference = left.CompressionM - right.CompressionM;
            float force = travelDifference * axle.AntiRollBarNPerM * multiplier;

            // The more compressed side is pushed down, the other pulled up.
            left._antiRollForceN = -force;
            right._antiRollForceN = force;
        }

        /// <summary>Pushes the strut force into the rigidbody and records the tyre's normal load.</summary>
        public void ApplySuspensionForce(Rigidbody body)
        {
            if (!IsGrounded)
            {
                LoadN = 0f;
                return;
            }

            float total = Mathf.Max(0f, SuspensionForceN + _antiRollForceN);

            // Project onto the contact normal so slopes reduce the effective load
            // correctly, while keeping the force along the strut axis for stability.
            Vector3 up = SuspensionUp;
            float normalAlignment = Mathf.Clamp01(Vector3.Dot(up, ContactNormal));
            LoadN = total * normalAlignment;

            body.AddForceAtPosition(up * total, ContactPoint, ForceMode.Force);
        }

        // ==================================================================
        // Step 2: tyre forces
        // ==================================================================
        /// <summary>
        /// Builds the wheel's ground frame, measures slip, evaluates the tyre model
        /// and applies the resulting forces. Must run after ApplySuspensionForce so
        /// that <see cref="LoadN"/> is current.
        /// </summary>
        public void UpdateTireForces(Rigidbody body, VehicleDefinition definition,
                                     ITireModel tireModel, float deltaTime)
        {
            var tyre = definition.Tyres;

            // Steered frame: rotate the car's forward by the steer angle, then flatten
            // onto the contact plane so forces never push the car into or out of the ground.
            Vector3 normal = IsGrounded ? ContactNormal : Vector3.up;
            Quaternion steerRotation = Quaternion.AngleAxis(SteerAngleDeg, SuspensionUp);
            Vector3 forward = steerRotation * (_root != null ? _root.forward : Vector3.forward);

            WheelForward = Vector3.ProjectOnPlane(forward, normal).normalized;
            if (WheelForward.sqrMagnitude < 0.0001f) WheelForward = forward;
            // Cross(up, forward) is +right in Unity's coordinate system.
            WheelRight = Vector3.Cross(normal, WheelForward).normalized;

            if (!IsGrounded || LoadN <= 0.01f)
            {
                LongitudinalForceN = 0f;
                LateralForceN = 0f;
                TireSaturation = 0f;
                // Airborne wheels relax their slip back toward zero so they do not
                // land with a stale force spike.
                SlipRatio = Mathf.MoveTowards(SlipRatio, 0f, deltaTime * 4f);
                SlipAngleRad = Mathf.MoveTowards(SlipAngleRad, 0f, deltaTime * 4f);
                Vector3 airVelocity = body.GetPointVelocity(SuspensionAnchor != null ? SuspensionAnchor.position : body.position);
                ForwardSpeed = Vector3.Dot(airVelocity, WheelForward);
                LateralSpeed = Vector3.Dot(airVelocity, WheelRight);
                return;
            }

            Vector3 patchVelocity = body.GetPointVelocity(ContactPoint);
            ForwardSpeed = Vector3.Dot(patchVelocity, WheelForward);
            LateralSpeed = Vector3.Dot(patchVelocity, WheelRight);

            float radius = definition.Wheels.RadiusM;
            float wheelSurfaceSpeed = AngularVelocity * radius;

            // Denominator floor. Slip ratio is (wheelSpeed - roadSpeed) / roadSpeed,
            // which is singular at standstill. Using a reference speed as the floor is
            // the standard fix and is also what keeps the car stable when parked.
            float reference = Mathf.Max(Mathf.Abs(ForwardSpeed), tyre.LowSpeedReferenceMps);

            float steadySlipRatio = Mathf.Clamp((wheelSurfaceSpeed - ForwardSpeed) / reference, -4f, 4f);
            float steadySlipAngle = Mathf.Atan2(LateralSpeed, reference);

            // Tyre relaxation. A real tyre carcass needs to roll a relaxation length
            // before its force catches up with a slip change. Modelling that lag is
            // both more accurate and the single most effective numerical stabiliser
            // in a raycast rig - it removes the force oscillation that otherwise
            // appears when a stiff slip curve meets a discrete timestep.
            float relaxRate = Mathf.Clamp01(reference * deltaTime / Mathf.Max(0.05f, tyre.RelaxationLengthM));
            SlipRatio += (steadySlipRatio - SlipRatio) * relaxRate;
            SlipAngleRad += (steadySlipAngle - SlipAngleRad) * relaxRate;

            TireForces forces = tireModel.Evaluate(SlipRatio, SlipAngleRad, LoadN,
                                                   SurfaceFrictionScale, tyre);

            LongitudinalForceN = forces.Longitudinal;
            LateralForceN = forces.Lateral;
            TireSaturation = forces.Saturation;

            body.AddForceAtPosition(WheelForward * LongitudinalForceN + WheelRight * LateralForceN,
                                    ContactPoint, ForceMode.Force);
        }

        // ==================================================================
        // Step 3: wheel rotation
        // ==================================================================
        /// <summary>
        /// Integrates the wheel's angular velocity from drive torque, brake torque,
        /// the tyre's reaction torque and rolling resistance.
        ///
        /// The car accelerates because <see cref="LongitudinalForceN"/> was applied
        /// to the rigidbody above - never by writing to the body's velocity.
        /// </summary>
        public void IntegrateRotation(VehicleDefinition definition, float deltaTime)
        {
            float radius = definition.Wheels.RadiusM;
            float inertia = Mathf.Max(0.05f, definition.Wheels.InertiaKgM2);

            // Reaction from the contact patch: pushing the car forward slows the wheel.
            float reactionTorque = -LongitudinalForceN * radius;

            float rollingResistance = 0f;
            if (IsGrounded && LoadN > 0.01f)
            {
                float coefficient = definition.Tyres.RollingResistanceCoefficient + SurfaceExtraRollingResistance;
                rollingResistance = -Mathf.Sign(AngularVelocity) * coefficient * LoadN * radius;
                // Never let rolling resistance reverse the wheel within one step.
                float maxResistance = Mathf.Abs(AngularVelocity) * inertia / Mathf.Max(0.0001f, deltaTime);
                rollingResistance = Mathf.Clamp(rollingResistance, -maxResistance, maxResistance);
            }

            AngularVelocity += (DriveTorqueNm + reactionTorque + rollingResistance) / inertia * deltaTime;

            // Brakes are applied as a decelerating torque that may bring the wheel to a
            // stop but must never spin it backwards inside a single step - that would
            // make the slip ratio flip sign every frame and shake the car apart.
            if (BrakeTorqueNm > 0.01f)
            {
                float deltaOmega = BrakeTorqueNm / inertia * deltaTime;
                if (Mathf.Abs(AngularVelocity) <= deltaOmega)
                    AngularVelocity = 0f;
                else
                    AngularVelocity -= Mathf.Sign(AngularVelocity) * deltaOmega;
            }

            // Sanity clamp: a free-spinning wheel at 400 rad/s is already 490 km/h of
            // surface speed. Anything beyond that is a numerical escape, not driving.
            AngularVelocity = Mathf.Clamp(AngularVelocity, -500f, 500f);
        }

        // ==================================================================
        // Presentation
        // ==================================================================
        /// <summary>Places, steers and spins the wheel mesh. Called from Update, not FixedUpdate.</summary>
        public void UpdateVisual(VehicleDefinition definition,
                                 VehicleDefinition.SuspensionAxleConfig axle,
                                 float deltaTime)
        {
            if (VisualWheel == null || SuspensionAnchor == null) return;

            float suspensionLength = axle.RestLengthM - CompressionM;
            VisualWheel.position = SuspensionAnchor.position - SuspensionAnchor.up * suspensionLength;

            _visualSpinDeg += AngularVelocity * Mathf.Rad2Deg * deltaTime;
            if (_visualSpinDeg > 360f || _visualSpinDeg < -360f)
                _visualSpinDeg %= 360f;

            VisualWheel.rotation = SuspensionAnchor.rotation
                                   * Quaternion.Euler(0f, SteerAngleDeg, 0f)
                                   * Quaternion.Euler(_visualSpinDeg, 0f, 0f);
        }

        private void ResolveSurface(Collider collider)
        {
            var descriptor = collider != null ? collider.GetComponentInParent<SurfaceDescriptor>() : null;
            if (descriptor != null)
            {
                Surface = descriptor.SurfaceType;
                SurfaceFrictionScale = descriptor.FrictionScale;
                SurfaceExtraRollingResistance = descriptor.ExtraRollingResistance;
            }
            else
            {
                Surface = SurfaceType.Asphalt;
                SurfaceFrictionScale = SurfaceDescriptor.DefaultFrictionScale;
                SurfaceExtraRollingResistance = 0f;
            }
        }

        /// <summary>Wheel rotational speed expressed in RPM, for the debug overlay.</summary>
        public float AngularVelocityRpm => AngularVelocity * Units.RadPerSecToRpm;
    }
}
