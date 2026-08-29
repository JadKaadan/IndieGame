using IndieGame.Core;
using IndieGame.Vehicles;
using IndieGame.Vehicles.Data;
using UnityEngine;

namespace IndieGame.UI
{
    /// <summary>
    /// The player HUD: speed, tachometer, gear, drive mode, transmission mode and
    /// odometer.
    ///
    /// Drawn with IMGUI and runtime-generated textures rather than a uGUI canvas.
    /// That is a deliberate trade: it needs no font asset, no canvas prefab and no
    /// package reference, so it renders correctly the first time the scene is
    /// opened on any machine and in any render pipeline. It is replaced by a
    /// designed canvas HUD in the polish phase.
    ///
    /// Every value comes from <see cref="VehicleTelemetry"/>.
    /// </summary>
    [AddComponentMenu("IndieGame/UI/Vehicle HUD")]
    public class VehicleHud : MonoBehaviour
    {
        [SerializeField] private VehicleController controller;
        [SerializeField] private bool useImperialUnits = false;
        [SerializeField] private KeyCode toggleUnitsKey = KeyCode.U;

        [Header("Tachometer")]
        [SerializeField] private float displayMaxRpm = 8000f;

        private Texture2D _panel;
        private Texture2D _white;
        private Texture2D _accent;
        private Texture2D _red;
        private Texture2D _dim;

        private GUIStyle _hugeStyle;
        private GUIStyle _bigStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _smallStyle;

        private void Awake()
        {
            if (controller == null) controller = FindAnyObjectByType<VehicleController>();
            if (controller != null && controller.Definition != null)
                displayMaxRpm = Mathf.Ceil(controller.Definition.Engine.RedlineRpm / 1000f) * 1000f;
        }

        public void SetTarget(VehicleController target) => controller = target;

        private void Update()
        {
            if (HotKey.Pressed(toggleUnitsKey)) useImperialUnits = !useImperialUnits;
        }

        private void OnGUI()
        {
            if (controller == null || controller.Telemetry == null) return;
            EnsureResources();

            var t = controller.Telemetry;
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.6f, 1.6f);

            float panelWidth = 470f * scale;
            float panelHeight = 168f * scale;
            float x = (Screen.width - panelWidth) * 0.5f;
            float y = Screen.height - panelHeight - 26f * scale;

            var panelRect = new Rect(x, y, panelWidth, panelHeight);
            GUI.DrawTexture(panelRect, _panel);

            // ---- Speed -----------------------------------------------------
            float speed = useImperialUnits ? t.SpeedMph : t.SpeedKmh;
            string unit = useImperialUnits ? "MPH" : "KM/H";

            _hugeStyle.fontSize = Mathf.RoundToInt(74f * scale);
            _labelStyle.fontSize = Mathf.RoundToInt(15f * scale);
            _bigStyle.fontSize = Mathf.RoundToInt(46f * scale);
            _smallStyle.fontSize = Mathf.RoundToInt(14f * scale);

            var speedRect = new Rect(x + 22f * scale, y + 12f * scale, 210f * scale, 84f * scale);
            GUI.Label(speedRect, Mathf.RoundToInt(Mathf.Abs(speed)).ToString(), _hugeStyle);
            GUI.Label(new Rect(x + 22f * scale, y + 92f * scale, 210f * scale, 22f * scale), unit, _labelStyle);

            // ---- Gear ------------------------------------------------------
            string gear = t.GearLabel;
            if (t.TransmissionMode == TransmissionMode.Automatic && t.Gear > 0) gear = "D" + t.Gear;

            var gearRect = new Rect(x + panelWidth - 122f * scale, y + 16f * scale, 100f * scale, 60f * scale);
            GUI.Label(gearRect, gear, _bigStyle);
            GUI.Label(new Rect(x + panelWidth - 122f * scale, y + 74f * scale, 100f * scale, 20f * scale),
                      t.TransmissionMode == TransmissionMode.Manual ? "MANUAL" : "AUTO", _labelStyle);

            // ---- Tachometer bar --------------------------------------------
            float barX = x + 22f * scale;
            float barY = y + 118f * scale;
            float barWidth = panelWidth - 44f * scale;
            float barHeight = 14f * scale;

            GUI.DrawTexture(new Rect(barX, barY, barWidth, barHeight), _dim);

            float redlineFraction = controller.Definition != null
                ? Mathf.Clamp01(controller.Definition.Engine.RedlineRpm / displayMaxRpm)
                : 0.87f;
            GUI.DrawTexture(new Rect(barX + barWidth * redlineFraction, barY,
                                     barWidth * (1f - redlineFraction), barHeight), _red);

            float rpmFraction = Mathf.Clamp01(t.EngineRpm / displayMaxRpm);
            bool inRedline = rpmFraction >= redlineFraction;
            GUI.DrawTexture(new Rect(barX, barY, barWidth * rpmFraction, barHeight),
                            inRedline ? _red : _accent);

            // Thousand-rpm ticks, so the bar reads as an instrument.
            for (int rpm = 1000; rpm < displayMaxRpm; rpm += 1000)
            {
                float f = rpm / displayMaxRpm;
                GUI.DrawTexture(new Rect(barX + barWidth * f, barY, Mathf.Max(1f, scale), barHeight), _panel);
            }

            GUI.Label(new Rect(barX, barY + barHeight + 3f * scale, 200f * scale, 20f * scale),
                      $"{t.EngineRpm:0} RPM", _smallStyle);

            // ---- Right-hand readouts ---------------------------------------
            string odometer = useImperialUnits ? $"{t.OdometerMiles:0.0} mi" : $"{t.OdometerKm:0.0} km";
            var infoRect = new Rect(x + barWidth - 190f * scale, barY + barHeight + 3f * scale, 212f * scale, 20f * scale);
            GUI.Label(infoRect, $"{t.DriveModeName}    ODO {odometer}", _smallStyle);

            // ---- Warning lamps ---------------------------------------------
            float lampY = y + 96f * scale;
            float lampX = x + 150f * scale;
            DrawLamp(ref lampX, lampY, scale, "ABS", t.AbsActive, new Color(1f, 0.78f, 0.15f));
            DrawLamp(ref lampX, lampY, scale, "TC", t.TractionControlActive, new Color(1f, 0.78f, 0.15f));
            DrawLamp(ref lampX, lampY, scale, "ESC", t.StabilityControlActive, new Color(1f, 0.78f, 0.15f));
            DrawLamp(ref lampX, lampY, scale, "P", t.Handbrake > 0.3f, new Color(0.95f, 0.25f, 0.2f));
            if (t.EngineState != EngineState.Running)
                DrawLamp(ref lampX, lampY, scale, "ENG", true, new Color(0.95f, 0.25f, 0.2f));

            // ---- Controls hint ---------------------------------------------
            GUI.Label(new Rect(14f * scale, Screen.height - 24f * scale, 900f * scale, 20f * scale),
                      "W/S throttle-brake   A/D steer   Space handbrake   R/Q shift   M auto-manual   " +
                      "B drive mode   V camera   E ignition   L lights   U units   F3 telemetry",
                      _smallStyle);
        }

        private void DrawLamp(ref float lampX, float lampY, float scale, string label, bool on, Color colour)
        {
            if (!on) { lampX += 44f * scale; return; }

            var rect = new Rect(lampX, lampY, 38f * scale, 20f * scale);
            Color previous = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(rect, _white);
            GUI.color = previous;

            var style = new GUIStyle(_smallStyle) { alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = new Color(0.06f, 0.06f, 0.07f);
            GUI.Label(rect, label, style);

            lampX += 44f * scale;
        }

        private void EnsureResources()
        {
            if (_panel != null) return;

            _panel = Solid(new Color(0.055f, 0.06f, 0.07f, 0.86f));
            _white = Solid(Color.white);
            _accent = Solid(new Color(0.30f, 0.72f, 1f, 1f));
            _red = Solid(new Color(0.92f, 0.22f, 0.18f, 1f));
            _dim = Solid(new Color(0.16f, 0.17f, 0.19f, 0.95f));

            _hugeStyle = new GUIStyle { fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft };
            _hugeStyle.normal.textColor = Color.white;

            _bigStyle = new GUIStyle { fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
            _bigStyle.normal.textColor = new Color(0.35f, 0.85f, 1f);

            _labelStyle = new GUIStyle { alignment = TextAnchor.UpperLeft };
            _labelStyle.normal.textColor = new Color(0.62f, 0.66f, 0.72f);

            _smallStyle = new GUIStyle { alignment = TextAnchor.UpperLeft };
            _smallStyle.normal.textColor = new Color(0.72f, 0.76f, 0.82f);
        }

        private static Texture2D Solid(Color colour)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixel(0, 0, colour);
            texture.Apply();
            return texture;
        }
    }
}
