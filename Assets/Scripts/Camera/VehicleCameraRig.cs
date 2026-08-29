using IndieGame.Core;
using IndieGame.Vehicles;
using UnityEngine;

namespace IndieGame.Cameras
{
    public enum CameraMode
    {
        Chase,
        Cockpit,
        Hood,
        Bumper
    }

    /// <summary>
    /// Camera for the player's vehicle.
    ///
    /// Written directly rather than with Cinemachine for the prototype: the chase
    /// camera needs speed-driven FOV, an acceleration-driven lag and a collision
    /// probe that all read vehicle telemetry, which is less code here than a
    /// Cinemachine extension plus an asset to configure, and it keeps the project
    /// free of an extra package while the physics is being tuned. Nothing in the
    /// vehicle stack depends on this class, so moving to Cinemachine in Phase 10
    /// touches only this file.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("IndieGame/Camera/Vehicle Camera Rig")]
    [DefaultExecutionOrder(100)]
    public class VehicleCameraRig : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private VehicleController target;

        [Tooltip("Optional. Cockpit camera position, usually at the driver's eye point inside the car.")]
        [SerializeField] private Transform cockpitAnchor;

        [Tooltip("Optional. Bonnet camera position.")]
        [SerializeField] private Transform hoodAnchor;

        [Tooltip("Optional. Front bumper camera position.")]
        [SerializeField] private Transform bumperAnchor;

        [Header("Mode")]
        [SerializeField] private CameraMode mode = CameraMode.Chase;

        [Header("Chase camera")]
        [SerializeField] private float chaseDistance = 6.2f;
        [SerializeField] private float chaseHeight = 2.15f;
        [SerializeField] private float chaseLookAheadHeight = 0.9f;

        [Tooltip("Half-life in seconds for the camera to close on its ideal position. Higher lags more.")]
        [SerializeField, Range(0.01f, 0.5f)] private float positionHalfLife = 0.075f;

        [Tooltip("Half-life in seconds for the camera to close on its ideal rotation.")]
        [SerializeField, Range(0.01f, 0.5f)] private float rotationHalfLife = 0.055f;

        [Tooltip("How far the camera swings sideways under lateral acceleration, metres per g.")]
        [SerializeField] private float lateralAccelerationOffset = 0.35f;

        [Header("Field of view")]
        [SerializeField] private float baseFieldOfView = 62f;
        [SerializeField] private float maxFieldOfView = 82f;

        [Tooltip("Speed in km/h at which the FOV reaches its maximum.")]
        [SerializeField] private float fieldOfViewSpeedKmh = 260f;

        [SerializeField, Range(0.01f, 0.5f)] private float fieldOfViewHalfLife = 0.25f;

        [Header("Collision")]
        [SerializeField] private bool avoidCollisions = true;
        [SerializeField] private float collisionRadius = 0.32f;
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private float minimumDistance = 1.6f;

        [Header("Free look")]
        [SerializeField] private float lookSensitivity = 140f;
        [SerializeField] private float maxYawDeg = 80f;
        [SerializeField] private float maxPitchDeg = 35f;
        [SerializeField, Range(0.01f, 0.6f)] private float lookReturnHalfLife = 0.25f;

        [Header("Audio perspective")]
        [Tooltip("Optional. Told when the view moves inside or outside the car so the engine " +
                 "note can be muffled in the cockpit.")]
        [SerializeField] private IndieGame.Vehicles.Audio.VehicleEngineAudio engineAudio;

        [Header("Cockpit motion")]
        [Tooltip("Subtle head movement under acceleration. Keep small - large values cause motion sickness.")]
        [SerializeField] private bool cockpitMotionEffects = true;
        [SerializeField] private float cockpitMotionAmount = 0.022f;

        private Camera _camera;
        private Vector3 _smoothedPosition;
        private Quaternion _smoothedRotation;
        private float _lookYaw;
        private float _lookPitch;
        private Vector3 _cockpitOffset;
        private bool _subscribed;

        public CameraMode Mode
        {
            get => mode;
            set => mode = value;
        }

        public VehicleController Target
        {
            get => target;
            set
            {
                Unsubscribe();
                target = value;
                Subscribe();
            }
        }

        /// <summary>Repoints the rig at another car's mount points.</summary>
        public void SetAnchors(IndieGame.Vehicles.PrototypeCameraAnchors anchors)
        {
            if (anchors == null) return;
            cockpitAnchor = anchors.Cockpit;
            hoodAnchor = anchors.Hood;
            bumperAnchor = anchors.Bumper;
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.fieldOfView = baseFieldOfView;
        }

        private void OnEnable()
        {
            Subscribe();
            if (target != null)
            {
                _smoothedPosition = transform.position;
                _smoothedRotation = transform.rotation;
            }
        }

        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (_subscribed || target == null) return;
            target.CameraToggleRequested += CycleMode;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || target == null) return;
            target.CameraToggleRequested -= CycleMode;
            _subscribed = false;
        }

        public void CycleMode()
        {
            // Only offer the views this car actually has anchors for.
            for (int attempt = 0; attempt < 4; attempt++)
            {
                mode = (CameraMode)(((int)mode + 1) % 4);
                if (mode == CameraMode.Chase) return;
                if (mode == CameraMode.Cockpit && cockpitAnchor != null) return;
                if (mode == CameraMode.Hood && hoodAnchor != null) return;
                if (mode == CameraMode.Bumper && bumperAnchor != null) return;
            }
            mode = CameraMode.Chase;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            var telemetry = target.Telemetry;
            UpdateFreeLook(target.CurrentInput.Look, dt);
            UpdateFieldOfView(telemetry.SpeedKmh, dt);

            if (engineAudio != null) engineAudio.SetInteriorPerspective(mode == CameraMode.Cockpit);

            switch (mode)
            {
                case CameraMode.Chase:
                    UpdateChase(telemetry, dt);
                    break;
                case CameraMode.Cockpit:
                    UpdateAnchored(cockpitAnchor, telemetry, dt, true);
                    break;
                case CameraMode.Hood:
                    UpdateAnchored(hoodAnchor, telemetry, dt, false);
                    break;
                case CameraMode.Bumper:
                    UpdateAnchored(bumperAnchor, telemetry, dt, false);
                    break;
            }
        }

        private void UpdateFreeLook(Vector2 look, float dt)
        {
            bool looking = look.sqrMagnitude > 0.0001f;
            if (looking)
            {
                _lookYaw = Mathf.Clamp(_lookYaw + look.x * lookSensitivity * dt, -maxYawDeg, maxYawDeg);
                _lookPitch = Mathf.Clamp(_lookPitch - look.y * lookSensitivity * dt, -maxPitchDeg, maxPitchDeg);
            }
            else
            {
                _lookYaw = SimMath.Damp(_lookYaw, 0f, lookReturnHalfLife, dt);
                _lookPitch = SimMath.Damp(_lookPitch, 0f, lookReturnHalfLife, dt);
            }
        }

        private void UpdateFieldOfView(float speedKmh, float dt)
        {
            float t = Mathf.Clamp01(speedKmh / Mathf.Max(1f, fieldOfViewSpeedKmh));
            // Squared so the widening is barely noticeable around town and obvious at speed.
            float targetFov = Mathf.Lerp(baseFieldOfView, maxFieldOfView, t * t);
            _camera.fieldOfView = SimMath.Damp(_camera.fieldOfView, targetFov, fieldOfViewHalfLife, dt);
        }

        private void UpdateChase(VehicleTelemetry telemetry, float dt)
        {
            Transform car = target.transform;

            // Above about walking pace the camera follows the direction of travel
            // rather than the car's nose, so a slide reads correctly on screen.
            Vector3 velocity = target.Body.GetLinearVelocity();
            Vector3 followForward = car.forward;
            if (velocity.sqrMagnitude > 4f)
            {
                Vector3 flatVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up).normalized;
                float alignment = Vector3.Dot(flatVelocity, car.forward);
                if (alignment > 0.2f)
                    followForward = Vector3.Slerp(car.forward, flatVelocity, 0.35f);
            }

            Quaternion baseRotation = Quaternion.LookRotation(
                Vector3.ProjectOnPlane(followForward, Vector3.up).normalized, Vector3.up);
            Quaternion orbit = Quaternion.Euler(_lookPitch, _lookYaw, 0f);

            Vector3 pivot = car.position + Vector3.up * chaseHeight;
            Vector3 desired = pivot - (baseRotation * orbit * Vector3.forward) * chaseDistance;

            // Lateral acceleration nudges the camera outward in a corner. Small, but
            // it is a large part of why speed reads on screen.
            desired += car.right * (-telemetry.LateralAccelerationG * lateralAccelerationOffset);

            if (avoidCollisions)
                desired = ResolveCollision(pivot, desired);

            _smoothedPosition = SimMath.Damp(_smoothedPosition, desired, positionHalfLife, dt);

            Vector3 lookTarget = car.position + Vector3.up * chaseLookAheadHeight;
            Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - _smoothedPosition, Vector3.up);
            _smoothedRotation = Quaternion.Slerp(_smoothedRotation, desiredRotation,
                                                 1f - Mathf.Pow(0.5f, dt / Mathf.Max(0.001f, rotationHalfLife)));

            transform.SetPositionAndRotation(_smoothedPosition, _smoothedRotation);
        }

        private void UpdateAnchored(Transform anchor, VehicleTelemetry telemetry, float dt, bool allowMotion)
        {
            if (anchor == null)
            {
                mode = CameraMode.Chase;
                return;
            }

            Vector3 offset = Vector3.zero;
            if (allowMotion && cockpitMotionEffects)
            {
                // The body moves slightly against the acceleration, the way a driver's
                // head does. Deliberately tiny.
                Vector3 target = new Vector3(
                    -telemetry.LateralAccelerationG * cockpitMotionAmount,
                    0f,
                    -telemetry.LongitudinalAccelerationG * cockpitMotionAmount);
                _cockpitOffset = SimMath.Damp(_cockpitOffset, target, 0.12f, dt);
                offset = _cockpitOffset;
            }

            transform.position = anchor.TransformPoint(offset);
            transform.rotation = anchor.rotation * Quaternion.Euler(_lookPitch, _lookYaw, 0f);
        }

        private Vector3 ResolveCollision(Vector3 pivot, Vector3 desired)
        {
            Vector3 direction = desired - pivot;
            float distance = direction.magnitude;
            if (distance < 0.01f) return desired;
            direction /= distance;

            if (Physics.SphereCast(pivot, collisionRadius, direction, out RaycastHit hit,
                                   distance, collisionMask, QueryTriggerInteraction.Ignore))
            {
                // Ignore the car's own colliders.
                if (target != null && hit.collider.transform.IsChildOf(target.transform))
                    return desired;

                float safe = Mathf.Max(minimumDistance, hit.distance - collisionRadius);
                return pivot + direction * safe;
            }

            return desired;
        }
    }
}
