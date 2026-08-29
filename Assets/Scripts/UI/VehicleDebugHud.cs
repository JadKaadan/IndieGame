using IndieGame.Vehicles;
using IndieGame.Vehicles.Data;
using UnityEngine;

namespace IndieGame.UI
{
    /// <summary>
    /// Development telemetry overlay. Deliberately IMGUI: it needs no canvas, no
    /// prefab and no asset references, so it works the instant the scene exists
    /// and cannot break when the real HUD is rebuilt in Phase 10.
    ///
    /// Every number here comes from <see cref="VehicleTelemetry"/>, which is the
    /// same source the cockpit gauges will read. If the overlay and the dashboard
    /// ever disagree, the dashboard is lying.
    ///
    /// Excluded from release builds unless <see cref="showInReleaseBuilds"/> is set.
    /// </summary>
    [AddComponentMenu("IndieGame/UI/Vehicle Debug HUD")]
    public class VehicleDebugHud : MonoBehaviour
    {
        [SerializeField] private VehicleController target;
        [SerializeField] private KeyCode toggleKey = KeyCode.F3;
        [SerializeField] private bool visibleOnStart = true;
        [SerializeField] private bool showInReleaseBuilds = false;
        [SerializeField] private bool useImperialUnits = false;

        private bool _visible;
        private GUIStyle _labelStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _boxStyle;

        private void Awake()
        {
            _visible = visibleOnStart;
            if (target == null) target = FindAnyObjectByType<VehicleController>();
        }

        private void Update()
        {
            // The legacy Input class is used here on purpose: the debug overlay must
            // work regardless of which input backend the project is configured for.
            if (UnityEngine.Input.GetKeyDown(toggleKey)) _visible = !_visible;
        }

        private void OnGUI()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            if (!showInReleaseBuilds) return;
#endif
            if (!_visible || target == null || target.Telemetry == null) return;

            EnsureStyles();

            const float width = 330f;
            GUILayout.BeginArea(new Rect(12f, 12f, width, Screen.height - 24f), _boxStyle);

            var t = target.Telemetry;
            var definition = target.Definition;

            Header(definition != null ? definition.Identity.DisplayName : "Vehicle");

            // --- Driver readouts ---------------------------------------------
            string speedUnit = useImperialUnits ? "mph" : "km/h";
            float speed = useImperialUnits ? t.SpeedMph : t.SpeedKmh;
            Row("Speed", $"{speed:0} {speedUnit}");
            Row("Gear", $"{t.GearLabel}   ({t.TransmissionMode})");
            Row("Engine RPM", $"{t.EngineRpm:0}");
            Row("Drive mode", t.DriveModeName + (t.ExhaustValveOpen ? "   [valve open]" : ""));
            Row("Odometer", useImperialUnits ? $"{t.OdometerMiles:0.0} mi" : $"{t.OdometerKm:0.000} km");
            Row("Trip", $"{t.TripKm:0.000} km");

            Header("Engine");
            Row("State", t.EngineState.ToString() + (t.RevLimiterActive ? "   LIMITER" : ""));
            Row("Torque", $"{t.EngineTorqueNm:0} Nm");
            Row("Power", $"{t.EnginePowerHp:0} hp");
            if (definition != null && definition.Engine.Aspiration != Aspiration.NaturallyAspirated)
                Row("Boost", $"{t.BoostBar:0.00} bar");
            Row("Throttle", Bar(t.Throttle) + $" {t.Throttle:0.00}");
            Row("Commanded", Bar(t.EffectiveThrottle) + $" {t.EffectiveThrottle:0.00}");
            Row("Overrun", t.OnOverrun ? "yes" : "no");

            Header("Driveline");
            Row("Clutch lock", $"{t.ClutchLock:0.00}");
            Row("Clutch slip", $"{t.ClutchSlipRpm:0} rpm");
            Row("Shifting", t.IsShifting ? (t.ShiftWasDownshift ? "downshift" : "upshift") : "-");
            if (target.Transmission != null)
                Row("Total ratio", $"{target.Transmission.TotalRatio:0.00}");

            Header("Driver input");
            Row("Brake", Bar(t.Brake) + $" {t.Brake:0.00}");
            Row("Handbrake", Bar(t.Handbrake) + $" {t.Handbrake:0.00}");
            Row("Steer", $"{t.Steer:+0.00;-0.00; 0.00}   rack {t.RoadWheelAngleDeg:+0.0;-0.0; 0.0} deg");
            Row("Wheel", $"{t.SteeringWheelAngleDeg:+0;-0; 0} deg");

            Header("Chassis");
            Row("Long accel", $"{t.LongitudinalAccelerationG:+0.00;-0.00; 0.00} g");
            Row("Lat accel", $"{t.LateralAccelerationG:+0.00;-0.00; 0.00} g");
            Row("Drag", $"{t.DragForceN:0} N");
            Row("Downforce", $"{t.TotalDownforceN:0} N");
            Row("Wheels down", $"{t.WheelsOnGround} / {target.Wheels.Length}");

            Header("Assists");
            Row("ABS", Assist(t.AbsEnabled, t.AbsActive));
            Row("TC", Assist(t.TractionControlEnabled, t.TractionControlActive));
            Row("ESC", Assist(t.StabilityControlEnabled, t.StabilityControlActive));

            Header("Wheels");
            var wheels = target.Wheels;
            for (int i = 0; i < wheels.Length; i++)
            {
                var wheel = wheels[i];
                string flags = wheel.IsGrounded ? "" : " AIR";
                if (wheel.AbsActive) flags += " ABS";
                GUILayout.Label(
                    $"{wheel.Name,-3} load {wheel.LoadN,6:0} N  slip {wheel.SlipRatio,6:+0.00;-0.00} " +
                    $"ang {wheel.SlipAngleRad * Mathf.Rad2Deg,5:+0.0;-0.0} deg{flags}",
                    _labelStyle);
                GUILayout.Label(
                    $"    Fx {wheel.LongitudinalForceN,7:0} N  Fy {wheel.LateralForceN,7:0} N  " +
                    $"trq {wheel.DriveTorqueNm,6:0}  brk {wheel.BrakeTorqueNm,6:0}",
                    _labelStyle);
            }

            Header("Fuel");
            Row("Tank", $"{t.FuelLitres:0.0} / {t.FuelCapacityLitres:0} L");
            Row("Consumption", t.InstantConsumptionLPer100Km > 0.01f ? $"{t.InstantConsumptionLPer100Km:0.0} L/100km" : "-");

            GUILayout.Space(6f);
            GUILayout.Label($"F3 hides this panel", _labelStyle);

            GUILayout.EndArea();
        }

        private static string Assist(bool enabled, bool active)
        {
            if (!enabled) return "OFF";
            return active ? "ON   [intervening]" : "ON";
        }

        private static string Bar(float value01)
        {
            int filled = Mathf.RoundToInt(Mathf.Clamp01(value01) * 10f);
            return "[" + new string('|', filled) + new string('.', 10 - filled) + "]";
        }

        private void Row(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _labelStyle, GUILayout.Width(96f));
            GUILayout.Label(value, _labelStyle);
            GUILayout.EndHorizontal();
        }

        private void Header(string text)
        {
            GUILayout.Space(6f);
            GUILayout.Label(text.ToUpperInvariant(), _headerStyle);
        }

        private void EnsureStyles()
        {
            if (_labelStyle != null) return;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                richText = false,
                wordWrap = false
            };
            _labelStyle.normal.textColor = new Color(0.92f, 0.94f, 0.96f);

            _headerStyle = new GUIStyle(_labelStyle) { fontStyle = FontStyle.Bold, fontSize = 11 };
            _headerStyle.normal.textColor = new Color(0.45f, 0.78f, 1f);

            _boxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8) };
        }

        public void SetTarget(VehicleController controller) => target = controller;
        public void SetUnits(bool imperial) => useImperialUnits = imperial;
    }
}
