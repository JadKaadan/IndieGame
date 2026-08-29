using IndieGame.Core;
using IndieGame.Persistence;
using UnityEngine;

namespace IndieGame.Vehicles
{
    /// <summary>
    /// Measures the car's real performance while you drive it, and keeps the best
    /// result per vehicle in the save file.
    ///
    /// The garage shows these rather than an estimate, so the numbers on the stats
    /// screen are ones the physics actually produced. That is also what makes it
    /// possible to check a car against its published specification, and to see
    /// whether a tuning part did anything.
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    [AddComponentMenu("IndieGame/Vehicle/Performance Recorder")]
    [DefaultExecutionOrder(70)]
    public class VehiclePerformanceRecorder : MonoBehaviour
    {
        [Tooltip("Speed below which a new acceleration run can be armed, km/h.")]
        [SerializeField] private float launchThresholdKmh = 1.5f;

        [Tooltip("Wheel slip above which a run is discarded, to reject a rolling start on a hill.")]
        [SerializeField] private float rejectIfAirborne = 3f;

        private VehicleController _controller;
        private VehicleSaveData _data;

        private bool _accelArmed;
        private bool _accelRunning;
        private float _accelTimer;
        private bool _hundredLogged;

        private bool _brakeArmed;
        private bool _brakeRunning;
        private Vector3 _brakeStart;

        /// <summary>Live time of the current acceleration run, or -1 when not running.</summary>
        public float CurrentRunSeconds => _accelRunning ? _accelTimer : -1f;

        public float BestZeroToHundred => _data != null ? _data.BestZeroToHundredKmh : -1f;
        public float BestZeroToTwoHundred => _data != null ? _data.BestZeroToTwoHundredKmh : -1f;
        public float BestTopSpeed => _data != null ? _data.BestTopSpeedKmh : -1f;
        public float BestBrakingDistance => _data != null ? _data.BestHundredToZeroMetres : -1f;

        private void Awake() => _controller = GetComponent<VehicleController>();

        private void Update()
        {
            if (_data == null)
            {
                _data = _controller.SaveData;
                if (_data == null) return;
            }

            var telemetry = _controller.Telemetry;
            if (telemetry == null) return;

            float dt = Time.deltaTime;
            float kmh = telemetry.SpeedKmh;
            bool planted = telemetry.WheelsOnGround >= rejectIfAirborne;

            RecordTopSpeed(kmh, planted);
            RecordAcceleration(kmh, planted, dt);
            RecordBraking(kmh, planted);
        }

        private void RecordTopSpeed(float kmh, bool planted)
        {
            if (!planted) return;
            if (kmh > _data.BestTopSpeedKmh) _data.BestTopSpeedKmh = kmh;
        }

        private void RecordAcceleration(float kmh, bool planted, float dt)
        {
            if (!planted)
            {
                _accelRunning = false;
                _accelArmed = false;
                return;
            }

            // Arm a run whenever the car comes to a stop.
            if (kmh < launchThresholdKmh)
            {
                _accelArmed = true;
                _accelRunning = false;
                _accelTimer = 0f;
                _hundredLogged = false;
                return;
            }

            if (_accelArmed && !_accelRunning)
            {
                _accelRunning = true;
                _accelTimer = 0f;
                _hundredLogged = false;
                _accelArmed = false;
            }

            if (!_accelRunning) return;

            _accelTimer += dt;

            if (!_hundredLogged && kmh >= 100f)
            {
                _hundredLogged = true;
                if (_data.BestZeroToHundredKmh < 0f || _accelTimer < _data.BestZeroToHundredKmh)
                    _data.BestZeroToHundredKmh = _accelTimer;
            }

            if (kmh >= 200f)
            {
                if (_data.BestZeroToTwoHundredKmh < 0f || _accelTimer < _data.BestZeroToTwoHundredKmh)
                    _data.BestZeroToTwoHundredKmh = _accelTimer;
                _accelRunning = false;
            }

            // A run that has taken too long is no longer a standing start.
            if (_accelTimer > 60f) _accelRunning = false;
        }

        private void RecordBraking(float kmh, bool planted)
        {
            if (!planted) { _brakeRunning = false; return; }

            // Arm above 100 km/h; start measuring the moment the car drops through it
            // with the brake applied.
            if (kmh > 102f)
            {
                _brakeArmed = true;
                _brakeRunning = false;
                return;
            }

            bool braking = _controller.Telemetry.Brake > 0.5f;

            if (_brakeArmed && !_brakeRunning && braking && kmh <= 100f)
            {
                _brakeRunning = true;
                _brakeArmed = false;
                _brakeStart = transform.position;
            }

            if (!_brakeRunning) return;

            if (!braking)
            {
                _brakeRunning = false;
                return;
            }

            if (kmh <= 1f)
            {
                float distance = Vector3.Distance(_brakeStart, transform.position);
                if (distance > 5f && (_data.BestHundredToZeroMetres < 0f ||
                                      distance < _data.BestHundredToZeroMetres))
                {
                    _data.BestHundredToZeroMetres = distance;
                }
                _brakeRunning = false;
            }
        }

        /// <summary>Clears the recorded bests, for instance after a tuning change.</summary>
        public void ResetRecords()
        {
            if (_data == null) return;
            _data.BestZeroToHundredKmh = -1f;
            _data.BestZeroToTwoHundredKmh = -1f;
            _data.BestTopSpeedKmh = -1f;
            _data.BestHundredToZeroMetres = -1f;
            SaveSystem.Save();
        }
    }
}
