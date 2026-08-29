using IndieGame.Core;
using IndieGame.Vehicles.Data;
using UnityEngine;

namespace IndieGame.Vehicles.Audio
{
    /// <summary>
    /// Tyre roll and slip noise, driven by the contact patches.
    ///
    /// Roll noise scales with speed and load; scrub scales with how saturated the
    /// tyres are, which is the same value the tyre model uses to decide it is
    /// sliding. So the squeal starts exactly when grip actually runs out rather
    /// than at an arbitrary steering angle, and the surface type changes its
    /// character.
    /// </summary>
    [AddComponentMenu("IndieGame/Vehicle/Vehicle Tyre Audio")]
    [DefaultExecutionOrder(65)]
    public class VehicleTyreAudio : MonoBehaviour
    {
        [SerializeField] private VehicleController controller;

        [SerializeField, Range(0f, 1f)] private float rollVolume = 0.30f;
        [SerializeField, Range(0f, 1f)] private float slipVolume = 0.55f;

        [Tooltip("Tyre saturation at which scrub noise starts. 1.0 is the grip limit.")]
        [SerializeField, Range(0.3f, 1.2f)] private float slipOnset = 0.72f;

        private AudioSource _roll;
        private AudioSource _slip;
        private float _rollTarget;
        private float _slipTarget;
        private float _slipPitchTarget = 1f;

        private void Awake()
        {
            if (controller == null) controller = GetComponentInParent<VehicleController>();

            _roll = CreateSource("TyreRoll", BuildNoiseClip("TyreRoll", 0.30f, 0.0f), 0.0f);
            _slip = CreateSource("TyreSlip", BuildNoiseClip("TyreSlip", 0.72f, 0.55f), 0.0f);
        }

        private AudioSource CreateSource(string name, AudioClip clip, float startVolume)
        {
            var host = new GameObject(name);
            host.transform.SetParent(transform, false);

            var source = host.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.volume = startVolume;
            source.spatialBlend = 0.4f;
            source.minDistance = 3f;
            source.maxDistance = 90f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.Play();
            return source;
        }

        private void Update()
        {
            if (controller == null || controller.Telemetry == null) return;

            var telemetry = controller.Telemetry;
            float dt = Time.deltaTime;
            var wheels = controller.Wheels;

            int grounded = 0;
            float worstSaturation = 0f;
            float scrubSpeed = 0f;

            for (int i = 0; i < wheels.Length; i++)
            {
                var wheel = wheels[i];
                if (!wheel.IsGrounded) continue;
                grounded++;
                worstSaturation = Mathf.Max(worstSaturation, wheel.TireSaturation);

                // How fast the contact patch is actually sliding across the road.
                float slideLong = Mathf.Abs(wheel.AngularVelocity * controller.Definition.Wheels.RadiusM
                                            - wheel.ForwardSpeed);
                float slideLat = Mathf.Abs(wheel.LateralSpeed);
                scrubSpeed = Mathf.Max(scrubSpeed, Mathf.Max(slideLong, slideLat));
            }

            float speedKmh = telemetry.SpeedKmh;
            bool onGround = grounded > 0;

            // Loose surfaces are noisier at the same speed than smooth asphalt.
            float surfaceGain = SurfaceGain(telemetry.DominantSurface);

            _rollTarget = onGround
                ? Mathf.Clamp01(speedKmh / 90f) * rollVolume * surfaceGain
                : 0f;

            float saturationExcess = Mathf.InverseLerp(slipOnset, 1.25f, worstSaturation);
            float scrubAmount = Mathf.Clamp01(scrubSpeed / 9f);
            _slipTarget = onGround && speedKmh > 3f
                ? Mathf.Clamp01(saturationExcess * 0.75f + scrubAmount * 0.55f) * slipVolume
                : 0f;

            _slipPitchTarget = 0.85f + Mathf.Clamp01(scrubSpeed / 16f) * 0.5f;

            _roll.volume = SimMath.Damp(_roll.volume, _rollTarget, 0.09f, dt);
            _roll.pitch = 0.75f + Mathf.Clamp01(speedKmh / 220f) * 0.85f;

            _slip.volume = SimMath.Damp(_slip.volume, _slipTarget, 0.05f, dt);
            _slip.pitch = SimMath.Damp(_slip.pitch, _slipPitchTarget, 0.08f, dt);
        }

        private static float SurfaceGain(SurfaceType surface)
        {
            switch (surface)
            {
                case SurfaceType.Gravel: return 1.9f;
                case SurfaceType.Dirt: return 1.6f;
                case SurfaceType.Grass: return 1.35f;
                case SurfaceType.Wet: return 1.25f;
                default: return 1f;
            }
        }

        /// <summary>
        /// A one-second looping noise bed. <paramref name="tone"/> mixes in a
        /// resonant component, which is what turns hiss into a tyre squeal.
        /// </summary>
        private static AudioClip BuildNoiseClip(string name, float lowPass, float tone)
        {
            const int sampleRate = 44100;
            int samples = sampleRate;
            var data = new float[samples];
            var random = new System.Random(name.GetHashCode());

            float state = 0f;
            float resonant = 0f;
            float resonantVelocity = 0f;
            float cutoff = Mathf.Clamp01(lowPass);

            for (int i = 0; i < samples; i++)
            {
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                state += (white - state) * cutoff;

                float sample = state;

                if (tone > 0.001f)
                {
                    // A lightly damped resonator excited by the noise: a narrow band
                    // around roughly 1.1 kHz, which is where tyre scrub sits.
                    const float frequency = 1100f;
                    float omega = 2f * Mathf.PI * frequency / sampleRate;
                    resonantVelocity += (white * 0.02f - resonant * omega * omega) - resonantVelocity * 0.012f;
                    resonant += resonantVelocity;
                    sample = Mathf.Lerp(sample, Mathf.Clamp(resonant * 8f, -1f, 1f), tone);
                }

                data[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            // Crossfade the seam so the loop does not click.
            int fade = 512;
            for (int i = 0; i < fade; i++)
            {
                float t = i / (float)fade;
                data[i] = Mathf.Lerp(data[samples - fade + i], data[i], t);
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
