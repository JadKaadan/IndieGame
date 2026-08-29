using IndieGame.Core;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IndieGame.VehicleInput
{
    /// <summary>
    /// Local player input. Builds its action set in code so the project works
    /// the moment it is opened - no .inputactions asset to wire up and no GUID
    /// references to break. When the rebinding UI is built (Phase 10) an
    /// InputActionAsset can be assigned to <see cref="overrideActions"/> and
    /// this class will read from it instead.
    ///
    /// Edge commands (shift up, ignition, camera) are latched during Update and
    /// drained in FixedUpdate, so a quick tap is never lost between physics steps.
    /// </summary>
    [AddComponentMenu("IndieGame/Input/Player Vehicle Input Source")]
    public class PlayerVehicleInputSource : MonoBehaviour, IVehicleInputSource
    {
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

        /// <summary>Read-only view for HUD / debug overlays.</summary>
        public VehicleInputState CurrentState => _state;

        // ------------------------------------------------------------------
        // IVehicleInputSource
        // ------------------------------------------------------------------
        public VehicleInputState ConsumeInput()
        {
            VehicleInputState snapshot = _state;
            _state.ClearEdges();
            return snapshot;
        }

#if ENABLE_INPUT_SYSTEM
        // ------------------------------------------------------------------
        // Unity Input System path (recommended)
        // ------------------------------------------------------------------
        private InputAction _throttle;
        private InputAction _brake;
        private InputAction _steerDigital;
        private InputAction _steerAnalog;
        private InputAction _clutch;
        private InputAction _handbrake;
        private InputAction _look;
        private InputAction _shiftUp;
        private InputAction _shiftDown;
        private InputAction _ignition;
        private InputAction _driveMode;
        private InputAction _transmissionMode;
        private InputAction _camera;
        private InputAction _headlights;
        private InputAction _hazards;
        private InputAction _indicatorLeft;
        private InputAction _indicatorRight;

        private InputAction[] _all;

        private void OnEnable()
        {
            _throttle = Value("Throttle", "<Keyboard>/w", "<Keyboard>/upArrow", "<Gamepad>/rightTrigger");
            _brake = Value("Brake", "<Keyboard>/s", "<Keyboard>/downArrow", "<Gamepad>/leftTrigger");
            _clutch = Value("Clutch", "<Keyboard>/leftShift", "<Gamepad>/leftShoulder");
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

            _shiftUp = Button("ShiftUp", "<Keyboard>/e", "<Gamepad>/rightShoulder");
            _shiftDown = Button("ShiftDown", "<Keyboard>/q", "<Gamepad>/leftShoulder");
            _ignition = Button("Ignition", "<Keyboard>/i", "<Gamepad>/select");
            _driveMode = Button("DriveMode", "<Keyboard>/m", "<Gamepad>/dpad/up");
            _transmissionMode = Button("TransmissionMode", "<Keyboard>/t", "<Gamepad>/dpad/down");
            _camera = Button("Camera", "<Keyboard>/c", "<Gamepad>/buttonNorth");
            _headlights = Button("Headlights", "<Keyboard>/l");
            _hazards = Button("Hazards", "<Keyboard>/h");
            _indicatorLeft = Button("IndicatorLeft", "<Keyboard>/z");
            _indicatorRight = Button("IndicatorRight", "<Keyboard>/x");

            _all = new[]
            {
                _throttle, _brake, _steerDigital, _steerAnalog, _clutch, _handbrake, _look,
                _shiftUp, _shiftDown, _ignition, _driveMode, _transmissionMode, _camera,
                _headlights, _hazards, _indicatorLeft, _indicatorRight
            };

            for (int i = 0; i < _all.Length; i++) _all[i].Enable();
        }

        private void OnDisable()
        {
            if (_all == null) return;
            for (int i = 0; i < _all.Length; i++)
            {
                _all[i].Disable();
                _all[i].Dispose();
            }
            _all = null;
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

            // Latch edges - OR them in so nothing is lost before FixedUpdate reads.
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
        // Legacy Input Manager fallback.
        // Active when Player Settings > Active Input Handling is set to
        // "Input Manager (Old)". Bindings are hard-coded because the legacy
        // manager cannot be configured from script.
        // ------------------------------------------------------------------
        private void Update()
        {
            if (!IsEnabled)
            {
                _state = VehicleInputState.Neutral;
                return;
            }

            float dt = Time.unscaledDeltaTime;

            _state.Throttle = UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow) ? 1f : 0f;
            _state.Brake = UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow) ? 1f : 0f;
            _state.Clutch = UnityEngine.Input.GetKey(KeyCode.LeftShift) ? 1f : 0f;
            _state.Handbrake = UnityEngine.Input.GetKey(KeyCode.Space) ? 1f : 0f;

            float digital = 0f;
            if (UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow)) digital -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow)) digital += 1f;
            _state.Steer = ResolveSteer(0f, digital, dt);

            Vector2 look = new Vector2(UnityEngine.Input.GetAxisRaw("Mouse X"),
                                       UnityEngine.Input.GetAxisRaw("Mouse Y")) * lookSensitivity;
            if (invertLookY) look.y = -look.y;
            _state.Look = look;

            _state.ShiftUp |= UnityEngine.Input.GetKeyDown(KeyCode.E);
            _state.ShiftDown |= UnityEngine.Input.GetKeyDown(KeyCode.Q);
            _state.ToggleIgnition |= UnityEngine.Input.GetKeyDown(KeyCode.I);
            _state.ToggleDriveMode |= UnityEngine.Input.GetKeyDown(KeyCode.M);
            _state.ToggleTransmissionMode |= UnityEngine.Input.GetKeyDown(KeyCode.T);
            _state.ToggleCamera |= UnityEngine.Input.GetKeyDown(KeyCode.C);
            _state.ToggleHeadlights |= UnityEngine.Input.GetKeyDown(KeyCode.L);
            _state.ToggleHazards |= UnityEngine.Input.GetKeyDown(KeyCode.H);
            _state.IndicatorLeft |= UnityEngine.Input.GetKeyDown(KeyCode.Z);
            _state.IndicatorRight |= UnityEngine.Input.GetKeyDown(KeyCode.X);
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
                _digitalSteer = shaped; // keep them in sync so releasing the stick does not jump
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
