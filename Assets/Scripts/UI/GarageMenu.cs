using IndieGame.Core;
using IndieGame.Persistence;
using IndieGame.Vehicles;
using IndieGame.Vehicles.Data;
using IndieGame.Vehicles.Tuning;
using UnityEngine;

namespace IndieGame.UI
{
    /// <summary>
    /// The garage: vehicle statistics, a dyno plot, and the tuning parts list.
    ///
    /// Every change here writes a physics multiplier and takes effect immediately -
    /// close the menu and the car drives differently. The dyno redraws from the same
    /// torque maths the engine runs, and the performance figures are measured by
    /// <see cref="VehiclePerformanceRecorder"/> while you drive rather than estimated.
    /// </summary>
    [AddComponentMenu("IndieGame/UI/Garage Menu")]
    public class GarageMenu : MonoBehaviour
    {
        [SerializeField] private VehicleController controller;
        [SerializeField] private KeyCode toggleKey = KeyCode.G;
        [SerializeField] private bool pauseWhileOpen = true;

        private bool _open;
        private int[] _levels;
        private VehiclePerformanceRecorder _recorder;
        private float _restoreTimeScale = 1f;

        private Texture2D _panel, _row, _dim, _torqueColour, _powerColour;
        private GUIStyle _title, _heading, _label, _value, _small, _button, _buttonOn;

        public bool IsOpen => _open;

        private void Awake()
        {
            if (controller == null) controller = FindAnyObjectByType<VehicleController>();
            if (controller != null) _recorder = controller.GetComponent<VehiclePerformanceRecorder>();
        }

        private void Update()
        {
            if (HotKey.Pressed(toggleKey)) Toggle();
        }

        public void Toggle()
        {
            if (controller == null) return;
            _open = !_open;

            if (_open)
            {
                _levels = TuningCatalogue.NormaliseLevels(
                    controller.SaveData != null ? controller.SaveData.TuningLevels : null);
                if (pauseWhileOpen)
                {
                    _restoreTimeScale = Time.timeScale;
                    Time.timeScale = 0f;
                }
            }
            else
            {
                if (pauseWhileOpen) Time.timeScale = _restoreTimeScale;
                SaveSystem.Save();
            }
        }

        private void OnDisable()
        {
            if (_open && pauseWhileOpen) Time.timeScale = _restoreTimeScale;
            _open = false;
        }

        private void OnGUI()
        {
            if (!_open || controller == null || controller.Definition == null) return;
            EnsureResources();

            float scale = Mathf.Clamp(Screen.height / 1080f, 0.65f, 1.5f);
            float width = Mathf.Min(Screen.width - 60f, 1180f * scale);
            float height = Mathf.Min(Screen.height - 60f, 760f * scale);
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUI.DrawTexture(rect, _panel);
            GUILayout.BeginArea(new Rect(rect.x + 22f * scale, rect.y + 18f * scale,
                                         rect.width - 44f * scale, rect.height - 36f * scale));

            var definition = controller.Definition;
            _title.fontSize = Mathf.RoundToInt(26f * scale);
            GUILayout.Label(definition.Identity.DisplayName.ToUpperInvariant() + "   GARAGE", _title);
            GUILayout.Space(6f * scale);

            GUILayout.BeginHorizontal();
            DrawStatsColumn(definition, scale, (rect.width - 44f * scale) * 0.36f);
            GUILayout.Space(14f * scale);
            DrawTuningColumn(scale, (rect.width - 44f * scale) * 0.62f);
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            GUILayout.Label("G closes the garage. Changes apply immediately and are saved.", _small);
            GUILayout.EndArea();
        }

        // ==================================================================
        private void DrawStatsColumn(VehicleDefinition definition, float scale, float columnWidth)
        {
            GUILayout.BeginVertical(GUILayout.Width(columnWidth));

            var data = controller.SaveData;
            float torqueMultiplier = data != null ? data.EngineTorqueMultiplier : 1f;
            float boostOffset = data != null ? data.BoostBarOffset : 0f;

            definition.CalculateTunedPeaks(torqueMultiplier, boostOffset,
                                           out float hp, out float hpRpm,
                                           out float nm, out float nmRpm);

            Heading("SPECIFICATION", scale);
            Stat("Power", $"{hp:0} hp @ {hpRpm:0} rpm", scale);
            Stat("Torque", $"{nm:0} Nm @ {nmRpm:0} rpm", scale);
            Stat("Weight", $"{controller.Body.mass:0} kg", scale);
            Stat("Power/weight", $"{hp / Mathf.Max(1f, controller.Body.mass) * 1000f:0} hp/t", scale);
            Stat("Engine", definition.Engine.Description, scale);
            Stat("Drive", DriveLabel(definition.Drivetrain.Layout), scale);
            Stat("Transmission", $"{definition.ForwardGearCount}-speed {definition.Transmission.Type}", scale);
            Stat("Redline", $"{definition.Engine.RedlineRpm:0} rpm", scale);
            Stat("Tyre grip", $"{definition.Tyres.PeakFrictionCoefficient * controller.TyreGripMultiplier:0.00} mu", scale);
            Stat("Mileage", $"{controller.Odometer.TotalKilometres:0.0} km", scale);

            GUILayout.Space(8f * scale);
            Heading("MEASURED", scale);
            if (_recorder != null)
            {
                Stat("0-100 km/h", Format(_recorder.BestZeroToHundred, "s"), scale);
                Stat("0-200 km/h", Format(_recorder.BestZeroToTwoHundred, "s"), scale);
                Stat("Top speed", Format(_recorder.BestTopSpeed, "km/h"), scale);
                Stat("100-0 km/h", Format(_recorder.BestBrakingDistance, "m"), scale);
                GUILayout.Space(4f * scale);
                if (GUILayout.Button("Reset measurements", _button, GUILayout.Height(24f * scale)))
                    _recorder.ResetRecords();
            }
            else
            {
                GUILayout.Label("No performance recorder on this vehicle.", _small);
            }

            GUILayout.Space(10f * scale);
            Heading("DYNO", scale);
            Rect graph = GUILayoutUtility.GetRect(columnWidth, 150f * scale);
            DrawDyno(graph, definition, torqueMultiplier, boostOffset, scale);

            GUILayout.EndVertical();
        }

        private void DrawDyno(Rect area, VehicleDefinition definition,
                              float torqueMultiplier, float boostOffset, float scale)
        {
            GUI.DrawTexture(area, _dim);

            float idle = definition.Engine.IdleRpm;
            float redline = definition.Engine.RedlineRpm;

            // Scale both curves against the tuned peaks so an upgrade visibly moves them.
            definition.CalculateTunedPeaks(torqueMultiplier, boostOffset,
                                           out float peakHp, out _, out float peakNm, out _);
            float torqueScale = Mathf.Max(1f, peakNm * 1.1f);
            float powerScale = Mathf.Max(1f, peakHp * 1.1f);

            int columns = Mathf.Max(24, Mathf.RoundToInt(area.width));
            for (int i = 0; i < columns; i++)
            {
                float t = i / (float)(columns - 1);
                float rpm = Mathf.Lerp(idle, redline, t);
                float torque = definition.TunedTorqueAtRpm(rpm, torqueMultiplier, boostOffset);
                float power = Units.TorqueToHorsepower(torque, rpm);

                float x = area.x + t * area.width;
                float torqueHeight = torque / torqueScale * area.height;
                float powerHeight = power / powerScale * area.height;

                GUI.color = new Color(1f, 1f, 1f, 0.9f);
                GUI.DrawTexture(new Rect(x, area.yMax - torqueHeight, 1f, 2f), _torqueColour);
                GUI.DrawTexture(new Rect(x, area.yMax - powerHeight, 1f, 2f), _powerColour);
            }
            GUI.color = Color.white;

            // Thousand-rpm gridlines.
            for (float rpm = 1000f; rpm < redline; rpm += 1000f)
            {
                float t = Mathf.InverseLerp(idle, redline, rpm);
                if (t <= 0f || t >= 1f) continue;
                GUI.DrawTexture(new Rect(area.x + t * area.width, area.y, 1f, area.height), _row);
            }

            _small.fontSize = Mathf.RoundToInt(11f * scale);
            GUI.Label(new Rect(area.x + 4f, area.y + 2f, area.width, 18f * scale),
                      $"torque to {torqueScale:0} Nm", ColouredStyle(new Color(0.35f, 0.85f, 1f)));
            GUI.Label(new Rect(area.x + 4f, area.y + 16f * scale, area.width, 18f * scale),
                      $"power to {powerScale:0} hp", ColouredStyle(new Color(1f, 0.66f, 0.2f)));
            GUI.Label(new Rect(area.x + 4f, area.yMax - 18f * scale, area.width, 18f * scale),
                      $"{idle:0} - {redline:0} rpm", _small);
        }

        private void DrawTuningColumn(float scale, float columnWidth)
        {
            GUILayout.BeginVertical(GUILayout.Width(columnWidth));
            Heading("TUNING", scale);

            var data = controller.SaveData;
            if (data == null)
            {
                GUILayout.Label("This vehicle has no save record, so tuning cannot persist.", _small);
                GUILayout.EndVertical();
                return;
            }

            bool changed = false;
            var categories = TuningCatalogue.Categories;

            for (int i = 0; i < categories.Length; i++)
            {
                TuningLevel[] options = TuningCatalogue.Levels(categories[i]);
                int level = Mathf.Clamp(_levels[i], 0, options.Length - 1);

                GUILayout.BeginHorizontal(GUILayout.Height(26f * scale));
                GUILayout.Label(TuningCatalogue.DisplayName(categories[i]), _label,
                                GUILayout.Width(110f * scale));

                if (GUILayout.Button("<", _button, GUILayout.Width(26f * scale)))
                {
                    _levels[i] = (level - 1 + options.Length) % options.Length;
                    changed = true;
                }
                GUILayout.Label(options[level].Name, _value, GUILayout.Width(120f * scale));
                if (GUILayout.Button(">", _button, GUILayout.Width(26f * scale)))
                {
                    _levels[i] = (level + 1) % options.Length;
                    changed = true;
                }

                GUILayout.Label(options[level].Effect, _small);
                GUILayout.FlexibleSpace();
                GUILayout.Label(options[level].Price > 0 ? $"{options[level].Price:n0}" : "-",
                                _small, GUILayout.Width(60f * scale));
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8f * scale);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Total build cost: {TuningCatalogue.TotalSpend(_levels):n0}", _label);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Return to stock", _button, GUILayout.Width(150f * scale),
                                 GUILayout.Height(26f * scale)))
            {
                for (int i = 0; i < _levels.Length; i++) _levels[i] = 0;
                _levels[(int)TuningCategory.Tyres] = 1;
                changed = true;
            }
            GUILayout.EndHorizontal();

            if (changed) ApplyLevels(data);

            GUILayout.Space(10f * scale);
            Heading("DRIVER SETTINGS", scale);
            DrawToggle("ABS", ref data.AbsEnabled, scale, v => controller.Brakes.AbsEnabled = v);
            DrawToggle("Traction control", ref data.TractionControlEnabled, scale,
                       v => controller.Stability.TractionControlEnabled = v);
            DrawToggle("Stability control", ref data.StabilityControlEnabled, scale,
                       v => controller.Stability.StabilityControlEnabled = v);

            GUILayout.EndVertical();
        }

        private void ApplyLevels(VehicleSaveData data)
        {
            TuningCatalogue.Apply(data, _levels);
            controller.ApplyTuning(data);
            SaveSystem.Save();
        }

        private void DrawToggle(string label, ref bool value, float scale, System.Action<bool> onChanged)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(24f * scale));
            GUILayout.Label(label, _label, GUILayout.Width(150f * scale));
            if (GUILayout.Button(value ? "ON" : "OFF", value ? _buttonOn : _button,
                                 GUILayout.Width(64f * scale)))
            {
                value = !value;
                onChanged(value);
                SaveSystem.Save();
            }
            GUILayout.EndHorizontal();
        }

        private static string DriveLabel(DriveLayout layout)
        {
            switch (layout)
            {
                case DriveLayout.FrontWheelDrive: return "FWD";
                case DriveLayout.RearWheelDrive: return "RWD";
                default: return "AWD";
            }
        }

        private static string Format(float value, string unit)
        {
            return value < 0f ? "not measured" : $"{value:0.00} {unit}";
        }

        // ==================================================================
        private void Heading(string text, float scale)
        {
            _heading.fontSize = Mathf.RoundToInt(13f * scale);
            GUILayout.Label(text, _heading);
        }

        private void Stat(string label, string value, float scale)
        {
            _label.fontSize = Mathf.RoundToInt(13f * scale);
            _value.fontSize = Mathf.RoundToInt(13f * scale);
            GUILayout.BeginHorizontal(GUILayout.Height(20f * scale));
            GUILayout.Label(label, _label, GUILayout.Width(120f * scale));
            GUILayout.Label(value, _value);
            GUILayout.EndHorizontal();
        }

        private GUIStyle ColouredStyle(Color colour)
        {
            var style = new GUIStyle(_small);
            style.normal.textColor = colour;
            return style;
        }

        private void EnsureResources()
        {
            if (_panel != null) return;

            _panel = Solid(new Color(0.045f, 0.05f, 0.06f, 0.97f));
            _row = Solid(new Color(1f, 1f, 1f, 0.06f));
            _dim = Solid(new Color(0.10f, 0.11f, 0.13f, 1f));
            _torqueColour = Solid(new Color(0.35f, 0.85f, 1f));
            _powerColour = Solid(new Color(1f, 0.66f, 0.2f));

            _title = new GUIStyle { fontStyle = FontStyle.Bold };
            _title.normal.textColor = Color.white;

            _heading = new GUIStyle { fontStyle = FontStyle.Bold };
            _heading.normal.textColor = new Color(0.35f, 0.78f, 1f);

            _label = new GUIStyle();
            _label.normal.textColor = new Color(0.60f, 0.65f, 0.72f);

            _value = new GUIStyle { fontStyle = FontStyle.Bold };
            _value.normal.textColor = Color.white;

            _small = new GUIStyle { fontSize = 11 };
            _small.normal.textColor = new Color(0.55f, 0.60f, 0.66f);

            _button = new GUIStyle(GUI.skin.button) { fontSize = 12 };
            _buttonOn = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold };
            _buttonOn.normal.textColor = new Color(0.35f, 0.9f, 0.55f);
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

        public void SetTarget(VehicleController target)
        {
            controller = target;
            _recorder = target != null ? target.GetComponent<VehiclePerformanceRecorder>() : null;
        }
    }
}
