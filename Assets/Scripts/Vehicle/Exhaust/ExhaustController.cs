using UnityEngine;

namespace IndieGame.Vehicles.Exhaust
{
    /// <summary>
    /// Overrun pops, bangs and flames.
    ///
    /// Nothing here fires on a timer. A bang needs unburnt fuel reaching a hot
    /// exhaust, so the conditions checked are the real ones: the engine was making
    /// power, the throttle snapped shut or a gear changed, revs are high, and the
    /// exhaust is open. A stock car in Comfort with the valve shut produces
    /// essentially nothing; a hard downshift in Sport near the limiter usually
    /// crackles. Flames need all of that plus a much higher bar.
    ///
    /// Particle systems and audio are built at runtime so no VFX or audio assets
    /// are required to see and hear it working.
    /// </summary>
    [AddComponentMenu("IndieGame/Vehicle/Exhaust Controller")]
    [DefaultExecutionOrder(60)]
    public class ExhaustController : MonoBehaviour
    {
        [SerializeField] private VehicleController controller;

        [Tooltip("One per exhaust tip. Flames and pops are emitted here, pointing along each transform's +Z.")]
        [SerializeField] private Transform[] exhaustPoints = new Transform[0];

        [Header("Tune")]
        [Tooltip("How aggressive the exhaust system is. 0 is a stock silencer, 1 is a decatted sports system. " +
                 "Phase 7 tuning writes this.")]
        [SerializeField, Range(0f, 1f)] private float exhaustAggression = 0.35f;

        [Tooltip("Engine speed below which the exhaust is never hot or loaded enough to bang.")]
        [SerializeField] private float minimumRpm = 3200f;

        [Tooltip("Seconds of enforced quiet between events, so a lift does not machine-gun.")]
        [SerializeField] private float minimumInterval = 0.11f;

        [Header("Flames")]
        [Tooltip("Combined aggression and overrun intensity needed before flames are possible at all.")]
        [SerializeField, Range(0f, 1f)] private float flameThreshold = 0.55f;

        [SerializeField, Range(0f, 1f)] private float flameChance = 0.35f;

        [Header("Audio")]
        [SerializeField, Range(0f, 1f)] private float popVolume = 0.7f;

        public float ExhaustAggression
        {
            get => exhaustAggression;
            set => exhaustAggression = Mathf.Clamp01(value);
        }

        private ParticleSystem[] _flames;
        private AudioSource _audio;
        private AudioClip[] _popClips;
        private float _cooldown;
        private float _previousThrottle;
        private System.Random _random;
        private bool _pendingShift;
        private bool _pendingShiftWasDown;
        private bool _subscribed;

        private void Awake()
        {
            if (controller == null) controller = GetComponentInParent<VehicleController>();
            _random = new System.Random(System.Environment.TickCount);

            BuildFlames();
            BuildAudio();
        }

        private void OnEnable()
        {
            if (controller == null || _subscribed) return;
            controller.GearChanged += OnGearChanged;
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (controller == null || !_subscribed) return;
            controller.GearChanged -= OnGearChanged;
            _subscribed = false;
        }

        /// <summary>
        /// A gear change lasts one physics step. Latching it here means the check in
        /// Update cannot miss one at a 200 Hz timestep.
        /// </summary>
        private void OnGearChanged(bool wasDownshift)
        {
            _pendingShift = true;
            _pendingShiftWasDown = wasDownshift;
        }

        private void Update()
        {
            if (controller == null || controller.Telemetry == null) return;

            float dt = Time.deltaTime;
            if (_cooldown > 0f) _cooldown -= dt;

            var telemetry = controller.Telemetry;
            var mode = controller.CurrentDriveMode;

            float throttle = telemetry.Throttle;
            bool snapShut = _previousThrottle > 0.45f && throttle < 0.12f;
            _previousThrottle = throttle;

            bool shifted = _pendingShift;
            bool shiftWasDown = _pendingShiftWasDown;
            _pendingShift = false;

            if (_cooldown > 0f) return;
            if (telemetry.EngineState != Data.EngineState.Running) return;
            if (telemetry.EngineRpm < minimumRpm) return;

            // The drive mode decides whether the valve is open at all. With it shut,
            // a stock car stays quiet no matter how it is driven.
            float modeIntensity = mode != null ? mode.ExhaustOverrunIntensity : 0f;
            if (mode != null && !mode.ExhaustValveOpen) modeIntensity *= 0.15f;

            float intensity = Mathf.Clamp01(modeIntensity * 0.6f + exhaustAggression * 0.7f);
            if (intensity < 0.05f) return;

            // How far past the useful rev range we are, which is what makes a
            // near-limiter downshift bang harder than a 3,500 rpm lift.
            float revFactor = Mathf.Clamp01((telemetry.EngineRpm - minimumRpm) / 3000f);

            float chance = 0f;
            if (snapShut) chance = 0.75f * intensity * (0.35f + revFactor);
            else if (shifted && shiftWasDown) chance = 0.60f * intensity * (0.4f + revFactor);
            else if (shifted) chance = 0.30f * intensity * revFactor;
            else if (telemetry.OnOverrun) chance = 0.030f * intensity * revFactor; // occasional crackle while coasting
            else if (telemetry.RevLimiterActive) chance = 0.10f * intensity;

            if (chance <= 0f) return;
            if (_random.NextDouble() > chance) return;

            Fire(intensity, revFactor);
        }

        private void Fire(float intensity, float revFactor)
        {
            _cooldown = minimumInterval;

            float strength = Mathf.Clamp01(intensity * (0.45f + revFactor * 0.75f));

            // A short burst of two to four crackles reads much more like an exhaust
            // than one isolated pop.
            int crackles = 1 + _random.Next(0, strength > 0.6f ? 3 : 2);
            for (int i = 0; i < crackles; i++)
                Invoke(nameof(EmitOne), i * (0.035f + (float)_random.NextDouble() * 0.05f));

            bool flame = strength >= flameThreshold && _random.NextDouble() < flameChance * strength;
            if (flame && _flames != null)
            {
                for (int i = 0; i < _flames.Length; i++)
                {
                    if (_flames[i] == null) continue;
                    _flames[i].Emit(6 + Mathf.RoundToInt(strength * 10f));
                }
            }
        }

        /// <summary>One crackle: a pop sample at a randomised pitch and level.</summary>
        private void EmitOne()
        {
            if (_audio == null || _popClips == null || _popClips.Length == 0) return;

            AudioClip clip = _popClips[_random.Next(0, _popClips.Length)];
            _audio.pitch = 0.82f + (float)_random.NextDouble() * 0.42f;
            _audio.PlayOneShot(clip, popVolume * (0.55f + (float)_random.NextDouble() * 0.45f));
        }

        private void BuildFlames()
        {
            if (exhaustPoints == null || exhaustPoints.Length == 0) return;

            _flames = new ParticleSystem[exhaustPoints.Length];
            for (int i = 0; i < exhaustPoints.Length; i++)
            {
                if (exhaustPoints[i] == null) continue;

                var host = new GameObject("ExhaustFlame");
                host.transform.SetParent(exhaustPoints[i], false);

                var system = host.AddComponent<ParticleSystem>();

                // Configure before the system is ever played.
                var main = system.main;
                main.duration = 1f;
                main.loop = false;
                main.playOnAwake = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(7f, 16f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.22f);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(1f, 0.62f, 0.16f, 1f), new Color(1f, 0.32f, 0.05f, 1f));
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 60;
                main.gravityModifier = -0.05f;

                var emission = system.emission;
                emission.enabled = false; // emit only on demand

                var shape = system.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 7f;
                shape.radius = 0.02f;

                var colourOverLifetime = system.colorOverLifetime;
                colourOverLifetime.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(new Color(1f, 0.95f, 0.65f), 0f),
                        new GradientColorKey(new Color(1f, 0.45f, 0.08f), 0.45f),
                        new GradientColorKey(new Color(0.35f, 0.06f, 0.01f), 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(0.85f, 0.35f),
                        new GradientAlphaKey(0f, 1f)
                    });
                colourOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

                var sizeOverLifetime = system.sizeOverLifetime;
                sizeOverLifetime.enabled = true;
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f));

                var renderer = host.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    renderer.renderMode = ParticleSystemRenderMode.Billboard;
                    renderer.alignment = ParticleSystemRenderSpace.View;
                    renderer.sortingOrder = 5;
                }

                _flames[i] = system;
            }
        }

        private void BuildAudio()
        {
            Transform host = exhaustPoints != null && exhaustPoints.Length > 0 && exhaustPoints[0] != null
                ? exhaustPoints[0]
                : transform;

            var audioObject = new GameObject("ExhaustAudio");
            audioObject.transform.SetParent(host, false);

            _audio = audioObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0.6f;
            _audio.minDistance = 4f;
            _audio.maxDistance = 140f;
            _audio.rolloffMode = AudioRolloffMode.Linear;

            _popClips = new AudioClip[4];
            for (int i = 0; i < _popClips.Length; i++)
                _popClips[i] = CreatePopClip(i);
        }

        /// <summary>
        /// A pop is a noise burst with a very fast attack and an exponential decay,
        /// plus a low thump. Generating four variants and randomising pitch on top
        /// keeps it from sounding like the same sample looping.
        /// </summary>
        private static AudioClip CreatePopClip(int variant)
        {
            const int sampleRate = 44100;
            float duration = 0.16f + variant * 0.035f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);

            var data = new float[sampleCount];
            var random = new System.Random(9000 + variant);

            float decay = 26f + variant * 8f;
            float thumpHz = 68f + variant * 14f;
            float lowPass = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Exp(-t * decay);

                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                lowPass += (noise - lowPass) * 0.45f;

                float thump = Mathf.Sin(2f * Mathf.PI * thumpHz * t) * Mathf.Exp(-t * decay * 0.55f);

                data[i] = Mathf.Clamp(lowPass * envelope * 0.85f + thump * 0.5f, -1f, 1f);
            }

            var clip = AudioClip.Create($"ExhaustPop{variant}", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
