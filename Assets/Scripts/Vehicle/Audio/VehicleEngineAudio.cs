using IndieGame.Vehicles.Data;
using UnityEngine;

namespace IndieGame.Vehicles.Audio
{
    /// <summary>
    /// Synthesises engine sound from the simulation instead of pitch-shifting a
    /// recording.
    ///
    /// The fundamental is the firing frequency: a four-stroke engine fires
    /// cylinders/2 times per revolution, so f = rpm / 60 * cylinders / 2. Harmonics
    /// above it are mixed with amplitudes that shift with engine load, which is why
    /// the sound hardens under throttle and thins on the overrun rather than just
    /// getting higher. Intake and exhaust noise are added as filtered noise.
    ///
    /// This ships a real, RPM-and-load-driven engine note with no audio assets to
    /// license. It is a stand-in for recorded sample layers, which is what Phase 5
    /// replaces it with - the crossfade inputs (rpm, load, interior/exterior) are
    /// the same either way.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [AddComponentMenu("IndieGame/Vehicle/Vehicle Engine Audio")]
    public class VehicleEngineAudio : MonoBehaviour
    {
        [SerializeField] private VehicleController controller;

        [Header("Engine")]
        [Tooltip("Cylinder count. Sets the firing frequency and therefore the character of the note.")]
        [SerializeField, Range(3, 12)] private int cylinders = 6;

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] private float intakeNoise = 0.16f;
        [SerializeField, Range(0f, 1f)] private float exhaustBody = 0.55f;

        [Tooltip("Turbo whistle level. Scales with boost pressure.")]
        [SerializeField, Range(0f, 1f)] private float turboLevel = 0.22f;

        [Header("Perspective")]
        [Tooltip("Interior is muffled with less exhaust harmonic content. Set by the camera rig.")]
        [SerializeField] private bool interiorPerspective = false;

        private const int SampleRate = 48000;

        // Audio-thread state. Only written from the audio callback.
        private double _phase;
        private double _turboPhase;
        private float _noiseState;
        private float _lowPassState;
        private System.Random _random;

        // Written on the main thread, read on the audio thread. Floats are written
        // atomically on all platforms Unity targets, so no lock is needed for values
        // that may be one frame stale.
        private volatile float _frequency = 20f;
        private volatile float _gain;
        private volatile float _load;
        private volatile float _boost;
        private volatile float _muffle = 1f;

        private AudioSource _source;

        private void Awake()
        {
            if (controller == null) controller = GetComponentInParent<VehicleController>();
            _random = new System.Random(12345);

            _source = GetComponent<AudioSource>();
            _source.clip = AudioClip.Create("EngineSynth", SampleRate, 1, SampleRate, true, OnPcmRead);
            _source.loop = true;
            _source.playOnAwake = false;
            _source.Play();
        }

        private void Update()
        {
            if (controller == null || controller.Telemetry == null)
            {
                _gain = 0f;
                return;
            }

            var telemetry = controller.Telemetry;

            if (telemetry.EngineState == EngineState.Off || telemetry.EngineState == EngineState.Stalled)
            {
                _gain = Mathf.MoveTowards(_gain, 0f, Time.deltaTime * 3f);
                return;
            }

            float rpm = Mathf.Max(60f, telemetry.EngineRpm);
            _frequency = rpm / 60f * cylinders * 0.5f;

            // Load, not throttle: an engine pulling hard at part throttle sounds
            // loaded, and one revving in neutral does not.
            float loadFromTorque = Mathf.Clamp01(telemetry.EngineTorqueNm / 400f);
            _load = Mathf.Lerp(telemetry.EffectiveThrottle * 0.6f, loadFromTorque, 0.6f);

            _boost = telemetry.BoostBar;

            float startupFade = telemetry.EngineState == EngineState.Starting ? 0.4f : 1f;
            _gain = masterVolume * startupFade;

            // Interior loses the exhaust harmonics and the top end; exterior keeps them.
            _muffle = interiorPerspective ? 0.45f : 1f;
        }

        /// <summary>Switched by the camera rig when the view changes.</summary>
        public void SetInteriorPerspective(bool interior) => interiorPerspective = interior;

        /// <summary>
        /// Runs on the audio thread. No Unity API calls in here.
        /// </summary>
        private void OnPcmRead(float[] data)
        {
            float frequency = _frequency;
            float gain = _gain;
            float load = _load;
            float boost = _boost;
            float muffle = _muffle;

            if (gain <= 0.0001f)
            {
                for (int i = 0; i < data.Length; i++) data[i] = 0f;
                return;
            }

            double increment = frequency / SampleRate;
            double turboIncrement = (2200.0 + boost * 5200.0) / SampleRate;

            // Harmonic amplitudes. The second and fourth carry the exhaust note; the
            // higher odd harmonics are what make it sound hard under load.
            float h1 = 0.55f;
            float h2 = 0.42f * exhaustBody;
            float h3 = 0.20f + load * 0.28f;
            float h4 = 0.14f + load * 0.22f;
            float h6 = (0.05f + load * 0.18f) * muffle;
            float h8 = (0.02f + load * 0.12f) * muffle;

            for (int i = 0; i < data.Length; i++)
            {
                _phase += increment;
                if (_phase > 1.0) _phase -= 1.0;

                double t = _phase * 2.0 * System.Math.PI;

                float sample =
                    h1 * (float)System.Math.Sin(t) +
                    h2 * (float)System.Math.Sin(t * 2.0) +
                    h3 * (float)System.Math.Sin(t * 3.0) +
                    h4 * (float)System.Math.Sin(t * 4.0) +
                    h6 * (float)System.Math.Sin(t * 6.0) +
                    h8 * (float)System.Math.Sin(t * 8.0);

                // Slight waveshaping. A real engine note is not a sum of clean sines,
                // and this roughens it in a way that reads as combustion.
                sample = sample / (1f + 0.55f * Mathf.Abs(sample));

                // Intake and induction noise, low-passed so it sits under the note.
                float white = (float)(_random.NextDouble() * 2.0 - 1.0);
                _noiseState += (white - _noiseState) * 0.28f;
                sample += _noiseState * intakeNoise * (0.35f + load) * muffle;

                // Turbo whistle, present only on boost.
                if (boost > 0.03f)
                {
                    _turboPhase += turboIncrement;
                    if (_turboPhase > 1.0) _turboPhase -= 1.0;
                    sample += (float)System.Math.Sin(_turboPhase * 2.0 * System.Math.PI)
                              * turboLevel * Mathf.Clamp01(boost) * 0.35f * muffle;
                }

                // Cabin muffling is a one-pole low pass; outside is left open.
                float cutoff = Mathf.Lerp(0.22f, 0.85f, muffle);
                _lowPassState += (sample - _lowPassState) * cutoff;

                data[i] = Mathf.Clamp(_lowPassState * gain, -1f, 1f);
            }
        }
    }
}
