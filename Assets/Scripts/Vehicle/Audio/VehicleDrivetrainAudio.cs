using UnityEngine;

namespace IndieGame.Vehicles.Audio
{
    /// <summary>
    /// Gear-change and blow-off valve sounds.
    ///
    /// Both are triggered from <see cref="VehicleController"/> events rather than by
    /// polling telemetry, because both conditions last a single physics step and the
    /// simulation runs several steps per rendered frame.
    ///
    /// The samples are synthesised at load: a shift is a short mechanical knock, a
    /// blow-off is a burst of noise with a fast attack and a falling whoosh whose
    /// length scales with how much boost was actually vented.
    /// </summary>
    [AddComponentMenu("IndieGame/Vehicle/Vehicle Drivetrain Audio")]
    [DefaultExecutionOrder(65)]
    public class VehicleDrivetrainAudio : MonoBehaviour
    {
        [SerializeField] private VehicleController controller;

        [SerializeField, Range(0f, 1f)] private float shiftVolume = 0.34f;
        [SerializeField, Range(0f, 1f)] private float blowOffVolume = 0.55f;

        private AudioSource _source;
        private AudioClip _upshift;
        private AudioClip _downshift;
        private AudioClip _blowOff;
        private bool _subscribed;
        private System.Random _random;

        private void Awake()
        {
            if (controller == null) controller = GetComponentInParent<VehicleController>();
            _random = new System.Random(4242);

            var host = new GameObject("DrivetrainAudio");
            host.transform.SetParent(transform, false);
            _source = host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0.35f;
            _source.minDistance = 3f;
            _source.maxDistance = 90f;
            _source.rolloffMode = AudioRolloffMode.Linear;

            _upshift = BuildKnock("Upshift", 190f, 34f, 0.09f);
            _downshift = BuildKnock("Downshift", 135f, 26f, 0.12f);
            _blowOff = BuildBlowOff();
        }

        private void OnEnable()
        {
            if (controller == null || _subscribed) return;
            controller.GearChanged += OnGearChanged;
            controller.BlowOffTriggered += OnBlowOff;
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (controller == null || !_subscribed) return;
            controller.GearChanged -= OnGearChanged;
            controller.BlowOffTriggered -= OnBlowOff;
            _subscribed = false;
        }

        private void OnGearChanged(bool wasDownshift)
        {
            if (_source == null) return;
            _source.pitch = 0.92f + (float)_random.NextDouble() * 0.18f;
            _source.PlayOneShot(wasDownshift ? _downshift : _upshift, shiftVolume);
        }

        private void OnBlowOff()
        {
            if (_source == null || controller == null) return;

            // Louder the more boost there was to vent.
            float boost = Mathf.Clamp01(controller.Engine.BoostBar /
                                        Mathf.Max(0.1f, controller.Definition.Engine.MaxBoostBar));
            _source.pitch = 0.88f + (float)_random.NextDouble() * 0.22f;
            _source.PlayOneShot(_blowOff, blowOffVolume * (0.45f + boost * 0.75f));
        }

        /// <summary>A short damped thud: a gearbox engaging, not a pop.</summary>
        private static AudioClip BuildKnock(string name, float frequency, float decay, float duration)
        {
            const int sampleRate = 44100;
            int samples = Mathf.RoundToInt(sampleRate * duration);
            var data = new float[samples];
            var random = new System.Random(name.GetHashCode());

            float noiseState = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Exp(-t * decay);

                float body = Mathf.Sin(2f * Mathf.PI * frequency * t);
                float click = (float)(random.NextDouble() * 2.0 - 1.0);
                noiseState += (click - noiseState) * 0.6f;

                data[i] = Mathf.Clamp((body * 0.6f + noiseState * 0.5f) * envelope, -1f, 1f);
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Filtered noise with a fast attack and a falling cutoff: a vent.</summary>
        private static AudioClip BuildBlowOff()
        {
            const int sampleRate = 44100;
            int samples = Mathf.RoundToInt(sampleRate * 0.42f);
            var data = new float[samples];
            var random = new System.Random(777);

            float state = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float progress = i / (float)samples;

                // Fast attack, long tail.
                float envelope = Mathf.Min(1f, t / 0.012f) * Mathf.Exp(-t * 7.5f);

                // The cutoff falls through the sound, which is what makes it whoosh
                // downward rather than just hiss.
                float cutoff = Mathf.Lerp(0.75f, 0.10f, progress);
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                state += (white - state) * cutoff;

                data[i] = Mathf.Clamp(state * envelope * 1.3f, -1f, 1f);
            }

            var clip = AudioClip.Create("BlowOff", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
