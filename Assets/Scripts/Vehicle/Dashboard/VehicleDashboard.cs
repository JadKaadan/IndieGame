using IndieGame.Core;
using IndieGame.Vehicles.Data;
using UnityEngine;

namespace IndieGame.Vehicles.Dashboard
{
    /// <summary>
    /// Drives the physical instrument cluster: the speedometer and tachometer
    /// needles and the steering wheel. Every angle comes from
    /// <see cref="VehicleTelemetry"/>, so a needle can only ever show a value the
    /// simulation actually produced.
    /// </summary>
    [AddComponentMenu("IndieGame/Vehicle/Vehicle Dashboard")]
    [DefaultExecutionOrder(60)]
    public class VehicleDashboard : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private VehicleController controller;

        [Tooltip("Needle pivot. Local +Y is the needle tip at zero rotation; it rotates about local Z.")]
        [SerializeField] private Transform speedometerNeedle;

        [SerializeField] private Transform tachometerNeedle;

        [Tooltip("Rotates about its local Z, which must point at the driver.")]
        [SerializeField] private Transform steeringWheel;

        [Header("Speedometer")]
        [SerializeField] private float speedometerMaxKmh = 320f;
        [SerializeField] private float speedometerMinAngle = 130f;
        [SerializeField] private float speedometerMaxAngle = -130f;

        [Header("Tachometer")]
        [SerializeField] private float tachometerMaxRpm = 8000f;
        [SerializeField] private float tachometerMinAngle = 130f;
        [SerializeField] private float tachometerMaxAngle = -130f;

        [Header("Response")]
        [Tooltip("Half-life in seconds. Real needles have mass; too low looks digital, too high looks laggy.")]
        [SerializeField, Range(0.005f, 0.4f)] private float needleHalfLife = 0.055f;

        [Header("Startup sweep")]
        [Tooltip("Sweep both needles to full scale and back when the engine is started.")]
        [SerializeField] private bool sweepOnStart = true;
        [SerializeField] private float sweepDuration = 1.4f;

        private float _speedAngle;
        private float _rpmAngle;
        private Quaternion _steeringWheelRest;
        private float _sweepTimer = -1f;
        private EngineState _lastEngineState = EngineState.Off;

        private void Awake()
        {
            if (controller == null) controller = GetComponentInParent<VehicleController>();
            if (steeringWheel != null) _steeringWheelRest = steeringWheel.localRotation;

            _speedAngle = speedometerMinAngle;
            _rpmAngle = tachometerMinAngle;
        }

        private void Update()
        {
            if (controller == null || controller.Telemetry == null) return;

            var telemetry = controller.Telemetry;
            float dt = Time.deltaTime;

            // The sweep is triggered by the engine actually starting, not by a timer.
            if (sweepOnStart && telemetry.EngineState == EngineState.Starting && _lastEngineState == EngineState.Off)
                _sweepTimer = 0f;
            _lastEngineState = telemetry.EngineState;

            float targetSpeedAngle;
            float targetRpmAngle;

            if (_sweepTimer >= 0f)
            {
                _sweepTimer += dt;
                float t = Mathf.Clamp01(_sweepTimer / Mathf.Max(0.1f, sweepDuration));
                // Out and back, eased so it looks mechanical rather than linear.
                float sweep = Mathf.Sin(t * Mathf.PI);
                sweep = sweep * sweep * (3f - 2f * sweep);
                targetSpeedAngle = Mathf.Lerp(speedometerMinAngle, speedometerMaxAngle, sweep);
                targetRpmAngle = Mathf.Lerp(tachometerMinAngle, tachometerMaxAngle, sweep);
                if (t >= 1f) _sweepTimer = -1f;

                _speedAngle = targetSpeedAngle;
                _rpmAngle = targetRpmAngle;
            }
            else
            {
                bool powered = telemetry.EngineState != EngineState.Off;

                float speedFraction = powered
                    ? Mathf.Clamp01(telemetry.SpeedKmh / Mathf.Max(1f, speedometerMaxKmh))
                    : 0f;
                float rpmFraction = powered
                    ? Mathf.Clamp01(telemetry.EngineRpm / Mathf.Max(1f, tachometerMaxRpm))
                    : 0f;

                targetSpeedAngle = Mathf.Lerp(speedometerMinAngle, speedometerMaxAngle, speedFraction);
                targetRpmAngle = Mathf.Lerp(tachometerMinAngle, tachometerMaxAngle, rpmFraction);

                _speedAngle = SimMath.Damp(_speedAngle, targetSpeedAngle, needleHalfLife, dt);
                // The tachometer is lighter than the speedometer and tracks faster.
                _rpmAngle = SimMath.Damp(_rpmAngle, targetRpmAngle, needleHalfLife * 0.6f, dt);
            }

            if (speedometerNeedle != null)
                speedometerNeedle.localRotation = Quaternion.Euler(0f, 0f, _speedAngle);

            if (tachometerNeedle != null)
                tachometerNeedle.localRotation = Quaternion.Euler(0f, 0f, _rpmAngle);

            if (steeringWheel != null)
            {
                // The rim turns the full mechanical lock from the definition, which is
                // why it can pass a quarter turn where the road wheels cannot.
                steeringWheel.localRotation = _steeringWheelRest *
                                              Quaternion.Euler(0f, 0f, -telemetry.SteeringWheelAngleDeg);
            }
        }

        /// <summary>Called by the vehicle builder so the gauge scales match the car.</summary>
        public void Configure(VehicleController owner, float maxKmh, float maxRpm)
        {
            controller = owner;
            speedometerMaxKmh = maxKmh;
            tachometerMaxRpm = maxRpm;
        }
    }
}
