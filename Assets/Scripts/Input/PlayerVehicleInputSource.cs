using IndieGame.Core;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IndieGame.VehicleInput
{
    /// <summary>
    /// Local player input.
    ///
    /// Bindings come from Assets/Resources/Input/Driving.inputactions when it is
    /// present, which is what a rebinding UI will edit later. If that asset is
    /// missing or does not contain every action this component needs, it falls
    /// back to an identical action set built in code, so the vehicle is never left
    /// uncontrollable because of an asset problem.
    ///
    /// Edge commands are latched during Update and drained in FixedUpdate, so a
    /// quick tap is never lost between physics steps.
    /// </summary>
    [AddComponentMenu("IndieGame/Input/Player Vehicle Input Source")]
    public class PlayerVehicleInputSource : MonoBehaviour, IVehicleInputSource
    {
        [Header("Bindings")]
        [Tooltip("Optional. Leave empty to load Resources/Input/Driving, then fall back to code-built actions.")]
        [SerializeField] private ScriptableObject actionsAsset;

        [Header("Feel")]
        [Tooltip("Seconds for a digital (keyboard) steering input to reach full lock. " +
                 "Analogue sticks and wheels bypass this entirely.")]
        [SerializeField] private float digitalSteerRampTime = 0.35f;

        [Tooltip("Seconds for a digital steering input to return to centre.")]
        [SerializeField] private float digitalSteerReturnTime = 0.20f;

        [SerializeField, Range(0f, 0.5f)] private float stickDeadZone = 0.12f;

        [Header("Look")]
        [SerializeField] private float lookSensitivity = 1f;
        [SerializeField] private bool invertLookY = false;

        private VehicleInputState _state = VehicleInputState.Neutral;
        private float _digitalSteer;

        public bool IsEnabled { get; set; } = true;

        /// <summary>True when bindings were taken from the .inputactions asset.</summary>
        public bool UsingActionAsset { get; private set; }

        public VehicleInputState CurrentState => _state;

        public VehicleInputState ConsumeInput()
        {
            VehicleInputState snapshot = _state;
            _state.ClearEdges();
            return snapshot;
        }

#if ENABLE_INPUT_SYSTEM
        private const string ResourcePath = "Input/Driving";
        private const string MapName = "Vehicle";

        private InputActionAsset _runtimeAsset;
        private bool _ownsActions;

        private InputAction _throttle, _brake, _clutch, _handbrake;
        private InputAction _steerDigital, _steerAnalog, _look;
        private InputAction _shiftUp, _shiftDown, _ignition, _driveMode, _transmissionMode;
        private InputAction _camera, _headlights, _hazards, _indicatorLeft, _indicatorRight;
        private InputAction[] _all;

        private void OnEnable()
        {
            if (!TryBindFromAsset()) BuildCodeActions();

            _all = new[]
            {
                _throttle, _brake, _clutch, _handbrake, _steerDigital, _steerAnalog, _look,
                _shiftUp, _shiftDown, _ignition, _driveMode, _transmissionMode,
                _camera, _headlights, _hazards, _indicatorLeft, _indicatorRight
            };

            for (int i = 0; i < _all.Length; i++) _all[i]?.Enable();
        }

        private void OnDisable()
        {
            if (_all != null)
            {
                for (int i = 0; i < _all.Length; i++)
                {
                    if (_all[i] == null) continue;
                    _all[i].Disable();
                    if (_ownsActions) _all[i].Dispose();
                }
                _all = null;
            }

            if (_runtimeAsset != null)
            {
                Destroy(_runtimeAsset);
                _runtimeAsset = null;
            }
        }

        private bool TryBindFromAsset()
        {
            var source = actionsAsset as InputActionAsset;
            if (source == null) source = Resources.Load<InputActionAsset>(ResourcePath);
            if (source == null) return false;

            // A private copy, so two vehicles in one scene do not share action state.
            _runtimeAsset = Instantiate(source);
            InputActionMap map = _runtimeAsset.FindActionMap(MapName, false);
            if (map == null) { Destroy(_runtimeAsset); _runtimeAsset = null; return false; }

            _throttle = map.FindAction("Throttle", false);
            _brake = map.FindAction("Brake", false);
            _clutch = map.FindAction("Clutch", false);
            _handbrake = map.FindAction("Handbrake", false);
            _steerDigital = map.FindAction("SteerDigital", false);
            _steerAnalog = map.FindAction("SteerAnalog", false);
            _look = map.FindAction("Look", false);
            _shiftUp = map.FindAction("ShiftUp", false);
            _shiftDown = map.FindAction("ShiftDown", false);
            _ignition = map.FindAction("Ignition", false);
            _driveMode = map.FindAction("DriveMode", false);
            _transmissionMode = map.FindAction("TransmissionMode", false);
            _camera = map.FindAction("Camera", false);
            _headlights = map.FindAction("Headlights", false);
            _hazards = map.FindAction("Hazards", false);
            _indicatorLeft = map.FindAction("IndicatorLeft", false);
            _indicatorRight = map.FindAction("IndicatorRight", false);

            bool complete = _throttle != null && _brake != null && _clutch != null && _handbrake != null
                            && _steerDigital != null && _steerAnalog != null && _look != null
                            && _shiftUp != null && _shiftDown != null && _ignition != null
                            && _driveMode != null && _transmissionMode != null && _camera != null
                            && _headlights != null && _hazards != null
                            && _indicatorLeft != null && _indicatorRight != null;

            if (!complete)
            {
                Destroy(_runtimeAsset);
                _runtimeAsset = null;
                return false;
            }

            _ownsActions = false;
            UsingActionAsset = true;
            return true;
        }

        private void BuildCodeActions()
        {
            _ownsActions = true;
            UsingActionAsset = false;

            _throttle = Value("Throttle", "<Keyboard>/w", "<Keyboard>/upArrow", "<Gamepad>/rightTrigger");
            _brake = Value("Brake", "<Keyboard>/s", "<Keyboard>/downArrow", "<Gamepad>/leftTrigger");
            _clutch = Value("Clutch", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            _handbrake = Value("Handbrake", "<Keyboard>/space", "<Gamepad>/buttonEast");

            _steerDigital = new InputAction("SteerDigital", InputActionType.Value);
            _steerDigital.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/a")
                .With("Positive", "<Keyboard>/d");
            _steerDigital.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/leftArrow")
                .With("Positive", "<Keyboard>/rightArrow");

            _steerAnalog = Value("SteerAnalog", "<Gamepad>/leftStick/x");

            _look = new InputAction("Look", InputActionType.Value);
            _look.AddBinding("<Mouse>/delta");
            _look.AddBinding("<Gamepad>/rightStick");

            _shiftUp = Button("ShiftUp", "<Keyboard>/r", "<Gamepad>/rightShoulder");
            _shiftDown = Button("ShiftDown", "<Keyboard>/q", "<Gamepad>/leftShoulder");
            _ignition = Button("Ignition", "<Keyboard>/e", "<Gamepad>/select");
            _driveMode = Button("DriveMode", "<Keyboard>/b", "<Gamepad>/dpad/up");
            _transmissionMode = Button("TransmissionMode", "<Keyboard>/m", "<Gamepad>/dpad/down");
            _camera = Button("Camera", "<Keyboard>/v", "<Gamepad>/buttonNorth");
            _headlights = Button("Headlights", "<Keyboard>/l", "<Gamepad>/dpad/left");
            _hazards = Button("Hazards", "<Keyboard>/h");
            _indicatorLeft = Button("IndicatorLeft", "<Keyboard>/z");
            _indicatorRight = Button("IndicatorRight", "<Keyboard>/x");
        }

        private static InputAction Value(string name, params string[] paths)
        {
            var action = new InputAction(name, InputActionType.Value);
            for (int i = 0; i < paths.Length; i++) action.AddBinding(paths[i]);
            return action;
        }

        private static InputAction Button(string name, params string[] paths)
        {
            var action = new InputAction(name, InputActionType.Button);
            for (int i = 0; i < paths.Length; i++) action.AddBinding(paths[i]);
            return action;
        }

        private void Update()
        {
            if (!IsEnabled || _all == null)
            {
                _state = VehicleInputState.Neutral;
                return;
            }

            float dt = Time.unscaledDeltaTime;

            _state.Throttle = Mathf.Clamp01(_throttle.ReadValue<float>());
            _state.Brake = Mathf.Clamp01(_brake.ReadValue<float>());
            _state.Clutch = Mathf.Clamp01(_clutch.ReadValue<float>());
            _state.Handbrake = Mathf.Clamp01(_handbrake.ReadValue<float>());
            _state.Steer = ResolveSteer(_steerAnalog.ReadValue<float>(), _steerDigital.ReadValue<float>(), dt);

            Vector2 look = _look.ReadValue<Vector2>() * lookSensitivity;
            if (invertLookY) look.y = -look.y;
            _state.Look = look;

            _state.ShiftUp |= _shiftUp.triggered;
            _state.ShiftDown |= _shiftDown.triggered;
            _state.ToggleIgnition |= _ignition.triggered;
            _state.ToggleDriveMode |= _driveMode.triggered;
            _state.ToggleTransmissionMode |= _transmissionMode.triggered;
            _state.ToggleCamera |= _camera.triggered;
            _state.ToggleHeadlights |= _headlights.triggered;
            _state.ToggleHazards |= _hazards.triggered;
            _state.IndicatorLeft |= _indicatorLeft.triggered;
            _state.IndicatorRight |= _indicatorRight.triggered;
        }

#else
        // ------------------------------------------------------------------
        // Legacy Input Manager fallback, used when Player Settings has
        // Active Input Handling set to "Input Manager (Old)".
        // ------------------------------------------------------------------
        private void Update()
        {
            if (!IsEnabled)
            {
                _state = VehicleInputState.Neutral;
                return;
            }

            float dt = Time.unscaledDeltaTime;
            var input = UnityEngine.Input;

            _state.Throttle = input.GetKey(KeyCode.W) || input.GetKey(KeyCode.UpArrow) ? 1f : 0f;
            _state.Brake = input.GetKey(KeyCode.S) || input.GetKey(KeyCode.DownArrow) ? 1f : 0f;
            _state.Clutch = input.GetKey(KeyCode.LeftShift) ? 1f : 0f;
            _state.Handbrake = input.GetKey(KeyCode.Space) ? 1f : 0f;

            float digital = 0f;
            if (input.GetKey(KeyCode.A) || input.GetKey(KeyCode.LeftArrow)) digital -= 1f;
            if (input.GetKey(KeyCode.D) || input.GetKey(KeyCode.RightArrow)) digital += 1f;
            _state.Steer = ResolveSteer(0f, digital, dt);

            Vector2 look = new Vector2(input.GetAxisRaw("Mouse X"), input.GetAxisRaw("Mouse Y")) * lookSensitivity;
            if (invertLookY) look.y = -look.y;
            _state.Look = look;

            _state.ShiftUp |= input.GetKeyDown(KeyCode.R);
            _state.ShiftDown |= input.GetKeyDown(KeyCode.Q);
            _state.ToggleIgnition |= input.GetKeyDown(KeyCode.E);
            _state.ToggleDriveMode |= input.GetKeyDown(KeyCode.B);
            _state.ToggleTransmissionMode |= input.GetKeyDown(KeyCode.M);
            _state.ToggleCamera |= input.GetKeyDown(KeyCode.V);
            _state.ToggleHeadlights |= input.GetKeyDown(KeyCode.L);
            _state.ToggleHazards |= input.GetKeyDown(KeyCode.H);
            _state.IndicatorLeft |= input.GetKeyDown(KeyCode.Z);
            _state.IndicatorRight |= input.GetKeyDown(KeyCode.X);
        }
#endif

        /// <summary>
        /// Analogue devices win when they are outside the dead zone; otherwise the
        /// keyboard axis is ramped so digital steering does not snap to full lock.
        /// </summary>
        private float ResolveSteer(float analog, float digital, float deltaTime)
        {
            float shaped = SimMath.ApplyDeadZone(analog, stickDeadZone);
            if (Mathf.Abs(shaped) > 0.001f)
            {
                _digitalSteer = shaped;
                return shaped;
            }

            float target = Mathf.Clamp(digital, -1f, 1f);
            bool returning = Mathf.Approximately(target, 0f);
            float rampTime = returning ? digitalSteerReturnTime : digitalSteerRampTime;
            float rate = rampTime > 0.001f ? 1f / rampTime : 1000f;
            _digitalSteer = Mathf.MoveTowards(_digitalSteer, target, rate * deltaTime);
            return _digitalSteer;
        }
    }
}
