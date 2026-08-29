using IndieGame.Vehicles.Data;
using UnityEngine;

namespace IndieGame.Vehicles.Systems
{
    /// <summary>
    /// Exterior lighting driven by vehicle state.
    ///
    /// Lamps are toggled by enabling and disabling their GameObjects rather than
    /// by writing emission properties on shared materials. That keeps this working
    /// identically in the built-in pipeline, URP and HDRP, none of which agree on
    /// emission property names or keywords.
    /// </summary>
    [AddComponentMenu("IndieGame/Vehicle/Vehicle Lights")]
    [DefaultExecutionOrder(60)]
    public class VehicleLights : MonoBehaviour
    {
        [SerializeField] private VehicleController controller;

        [Header("Lamp groups (glow meshes and Light components)")]
        [SerializeField] private GameObject[] headlightLamps = new GameObject[0];
        [SerializeField] private GameObject[] brakeLamps = new GameObject[0];
        [SerializeField] private GameObject[] tailLamps = new GameObject[0];
        [SerializeField] private GameObject[] reverseLamps = new GameObject[0];
        [SerializeField] private GameObject[] leftIndicatorLamps = new GameObject[0];
        [SerializeField] private GameObject[] rightIndicatorLamps = new GameObject[0];

        [Header("Behaviour")]
        [SerializeField] private bool headlightsOn = false;
        [SerializeField] private float indicatorFlashHz = 1.5f;

        [Tooltip("Brake pedal travel above which the brake lamps illuminate.")]
        [SerializeField, Range(0.01f, 0.4f)] private float brakeLampThreshold = 0.05f;

        public bool HeadlightsOn => headlightsOn;
        public bool HazardsOn { get; private set; }
        public bool IndicatingLeft { get; private set; }
        public bool IndicatingRight { get; private set; }

        private float _flashTimer;
        private bool _subscribed;

        private void Awake()
        {
            if (controller == null) controller = GetComponentInParent<VehicleController>();
            SetGroup(headlightLamps, false);
            SetGroup(brakeLamps, false);
            SetGroup(tailLamps, false);
            SetGroup(reverseLamps, false);
            SetGroup(leftIndicatorLamps, false);
            SetGroup(rightIndicatorLamps, false);
        }

        private void OnEnable()
        {
            if (controller == null || _subscribed) return;
            controller.HeadlightToggleRequested += ToggleHeadlights;
            controller.HazardToggleRequested += ToggleHazards;
            controller.IndicatorToggleRequested += ToggleIndicator;
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (controller == null || !_subscribed) return;
            controller.HeadlightToggleRequested -= ToggleHeadlights;
            controller.HazardToggleRequested -= ToggleHazards;
            controller.IndicatorToggleRequested -= ToggleIndicator;
            _subscribed = false;
        }

        private void ToggleHeadlights() => headlightsOn = !headlightsOn;

        private void ToggleHazards()
        {
            HazardsOn = !HazardsOn;
            IndicatingLeft = IndicatingRight = false;
        }

        private void ToggleIndicator(bool left)
        {
            if (left) { IndicatingLeft = !IndicatingLeft; IndicatingRight = false; }
            else { IndicatingRight = !IndicatingRight; IndicatingLeft = false; }
            HazardsOn = false;
        }

        private void Update()
        {
            if (controller == null || controller.Telemetry == null) return;

            var telemetry = controller.Telemetry;
            bool powered = telemetry.EngineState != EngineState.Off;

            SetGroup(headlightLamps, headlightsOn);
            SetGroup(tailLamps, headlightsOn);

            // Brake lamps come on with the pedal or the handbrake, and stay honest:
            // they read the same input the brake torque was computed from.
            bool braking = telemetry.Brake > brakeLampThreshold || telemetry.Handbrake > 0.3f;
            SetGroup(brakeLamps, braking);

            SetGroup(reverseLamps, telemetry.Gear == VehicleTransmission.ReverseGear && powered);

            _flashTimer += Time.deltaTime * indicatorFlashHz;
            bool flashOn = (_flashTimer % 1f) < 0.55f;
            SetGroup(leftIndicatorLamps, flashOn && (IndicatingLeft || HazardsOn));
            SetGroup(rightIndicatorLamps, flashOn && (IndicatingRight || HazardsOn));
        }

        private static void SetGroup(GameObject[] group, bool active)
        {
            if (group == null) return;
            for (int i = 0; i < group.Length; i++)
                if (group[i] != null && group[i].activeSelf != active)
                    group[i].SetActive(active);
        }

        public void SetHeadlights(bool on) => headlightsOn = on;
    }
}
