using System;
using IndieGame.Core;
using IndieGame.Persistence;
using IndieGame.VehicleInput;
using IndieGame.Vehicles.Data;
using UnityEngine;

namespace IndieGame.Vehicles
{
    /// <summary>
    /// The only MonoBehaviour in the vehicle simulation. It owns the subsystems as
    /// plain objects and ticks them in one fixed, explicit order every physics step.
    ///
    /// Why one component rather than fifteen: script execution order between many
    /// MonoBehaviours is a notorious source of physics bugs that only appear on
    /// some machines. Ticking the whole car from a single FixedUpdate makes the
    /// data flow readable top to bottom, guarantees the order, avoids a pile of
    /// GetComponent lookups, and leaves the simulation state trivially capturable
    /// for a replay or a network snapshot.
    ///
    /// The subsystems are still fully separate classes with their own
    /// responsibilities, so swapping the tyre model or the gearbox means editing
    /// one file.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("IndieGame/Vehicle/Vehicle Controller")]
    [DefaultExecutionOrder(-50)]
    public class VehicleController : MonoBehaviour
    {
        // ==================================================================
        // Authoring
        // ==================================================================
        [Header("Configuration")]
        [Tooltip("All of this car's engineering data. Required.")]
        [SerializeField] private VehicleDefinition definition;

        [Tooltip("The four corners. Order does not matter; roles come from the axle flags.")]
        [SerializeField] private VehicleWheel[] wheels = new VehicleWheel[0];

        [Tooltip("Where the driver's input comes from. Leave empty to search this GameObject and its children.")]
        [SerializeField] private MonoBehaviour inputSourceBehaviour;

        [Header("Identity and persistence")]
        [Tooltip("Unique id for this owned car. Leave empty to generate one on first spawn.")]
        [SerializeField] private string vehicleId = "";

        [Tooltip("Load and save mileage, tuning and preferences through SaveSystem.")]
        [SerializeField] private bool persistState = true;

        [Tooltip("Seconds between background saves. Protects mileage against a crash.")]
        [SerializeField] private float autoSaveIntervalSeconds = 15f;

        [Header("Startup")]
        [SerializeField] private bool startEngineOnSpawn = true;

        [Tooltip("Select first gear automatically when the engine starts in Automatic mode.")]
        [SerializeField] private bool selectDriveOnStart = true;

        [Header("Behaviour")]
        [Tooltip("Derive which wheels are driven, steered and handbraked from the definition's drive layout, " +
                 "instead of the per-wheel checkboxes.")]
        [SerializeField] private bool deriveWheelRoles = true;

        [Tooltip("Holds the car still when it is stopped with the brake or handbrake applied. " +
                 "A raycast rig has no static friction, so without this a parked car creeps on a slope.")]
        [SerializeField] private bool holdWhenStopped = true;

        // ==================================================================
        // Subsystems
        // ==================================================================
        public VehicleEngine Engine { get; private set; }
        public VehicleTransmission Transmission { get; private set; }
        public VehicleDrivetrain Drivetrain { get; private set; }
        public VehicleBrakes Brakes { get; private set; }
        public VehicleSteering Steering { get; private set; }
        public VehicleAerodynamics Aerodynamics { get; private set; }
        public VehicleStabilitySystems Stability { get; private set; }
        public VehicleOdometer Odometer { get; private set; }
        public VehicleFuelSystem Fuel { get; private set; }
        public VehicleTelemetry Telemetry { get; private set; }

        public VehicleDefinition Definition => definition;
        public VehicleWheel[] Wheels => wheels;
        public Rigidbody Body { get; private set; }
        public string VehicleId => vehicleId;

        /// <summary>Input read on the most recent physics step. Presentation code may read it.</summary>
        public VehicleInputState CurrentInput { get; private set; }

        /// <summary>Raised when the driver asks to change camera. The camera rig subscribes.</summary>
        public event Action CameraToggleRequested;

        /// <summary>Raised when the drive mode changes, with the new index.</summary>
        public event Action<int> DriveModeChanged;

        public int DriveModeIndex { get; private set; }

        public DriveModeSettings CurrentDriveMode =>
            definition != null && definition.DriveModes != null && definition.DriveModes.Length > 0
                ? definition.DriveModes[Mathf.Clamp(DriveModeIndex, 0, definition.DriveModes.Length - 1)]
                : null;

        // ==================================================================
        // Internals
        // ==================================================================
        private IVehicleInputSource _inputSource;
        private ITireModel _tireModel = PacejkaTireModel.Shared;
        private VehicleSaveData _saveData;
        private float _autoSaveTimer;
        private Vector3 _previousVelocity;
        private bool _initialised;

        // Cached axle pairs for the anti-roll bars.
        private VehicleWheel _frontLeft, _frontRight, _rearLeft, _rearRight;

        // ==================================================================
        // Lifecycle
        // ==================================================================
        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            if (definition == null)
            {
                Debug.LogError($"[VehicleController] '{name}' has no VehicleDefinition assigned. Disabling.", this);
                enabled = false;
                return;
            }

            ResolveInputSource();
            BuildSubsystems();
            ConfigureRigidbody();
            _initialised = true;
        }

        private void Start()
        {
            // The inertia tensor is only meaningful once the colliders exist, which
            // is why this is not in Awake.
            ApplyInertiaTensor();
            LoadPersistentState();

            if (startEngineOnSpawn)
            {
                Engine.ForceRunning();
                if (selectDriveOnStart && Transmission.Mode == TransmissionMode.Automatic)
                    Transmission.SelectGear(1, 0f);
            }
        }

        private void OnDestroy()
        {
            SaveState();
        }

        private void OnApplicationQuit()
        {
            SaveState();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveState();
        }

        private void ResolveInputSource()
        {
            _inputSource = inputSourceBehaviour as IVehicleInputSource;
            if (_inputSource != null) return;

            _inputSource = GetComponentInChildren<IVehicleInputSource>();
            if (_inputSource == null)
                Debug.LogWarning($"[VehicleController] '{name}' has no input source. The car will not respond to the driver.", this);
        }

        private void BuildSubsystems()
        {
            Engine = new VehicleEngine();
            Transmission = new VehicleTransmission();
            Drivetrain = new VehicleDrivetrain();
            Brakes = new VehicleBrakes();
            Steering = new VehicleSteering();
            Aerodynamics = new VehicleAerodynamics();
            Stability = new VehicleStabilitySystems();
            Odometer = new VehicleOdometer();
            Fuel = new VehicleFuelSystem();
            Telemetry = new VehicleTelemetry();

            Engine.Initialise(definition);
            Transmission.Initialise(definition);
            Drivetrain.Initialise(definition);
            Brakes.Initialise(definition, wheels.Length);
            Steering.Initialise(definition);
            Aerodynamics.Initialise(definition);
            Stability.Initialise(definition);
            Fuel.Initialise(definition, -1f);

            if (deriveWheelRoles) AssignWheelRoles();

            for (int i = 0; i < wheels.Length; i++)
            {
                var axle = wheels[i].IsFrontAxle ? definition.FrontSuspension : definition.RearSuspension;
                wheels[i].Initialise(transform, axle);
            }

            CacheAxlePairs();
            Odometer.Initialise(transform.position, 0.0);
        }

        /// <summary>Which wheels drive, steer and hold the handbrake follows from the drive layout.</summary>
        private void AssignWheelRoles()
        {
            var layout = definition.Drivetrain.Layout;
            for (int i = 0; i < wheels.Length; i++)
            {
                var wheel = wheels[i];
                wheel.IsDriven = layout == DriveLayout.AllWheelDrive
                                 || (layout == DriveLayout.FrontWheelDrive && wheel.IsFrontAxle)
                                 || (layout == DriveLayout.RearWheelDrive && !wheel.IsFrontAxle);
                wheel.IsSteered = wheel.IsFrontAxle;
                wheel.HasHandbrake = !wheel.IsFrontAxle;
            }
        }

        private void CacheAxlePairs()
        {
            for (int i = 0; i < wheels.Length; i++)
            {
                var wheel = wheels[i];
                if (wheel.IsFrontAxle)
                {
                    if (wheel.LateralSign < 0f) _frontLeft = wheel; else _frontRight = wheel;
                }
                else
                {
                    if (wheel.LateralSign < 0f) _rearLeft = wheel; else _rearRight = wheel;
                }
            }
        }

        private void ConfigureRigidbody()
        {
            Body.mass = definition.Chassis.MassKg;
            Body.centerOfMass = definition.Chassis.CentreOfMassOffset;

            // Aerodynamic drag is modelled explicitly, so PhysX's own damping must be off
            // or the car would be slowed twice. A little angular damping stops an
            // airborne car from spinning forever.
            Body.SetLinearDamping(0f);
            Body.SetAngularDamping(0.15f);

            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private void ApplyInertiaTensor()
        {
            Body.ResetInertiaTensor();
            float scale = Mathf.Max(0.1f, definition.Chassis.InertiaTensorScale);
            Body.inertiaTensor = Body.inertiaTensor * scale;
        }

        // ==================================================================
        // Simulation
        // ==================================================================
        private void FixedUpdate()
        {
            if (!_initialised) return;

            float dt = Time.fixedDeltaTime;

            VehicleInputState input = _inputSource != null ? _inputSource.ConsumeInput() : VehicleInputState.Neutral;
            CurrentInput = input;

            Vector3 velocity = Body.GetLinearVelocity();
            float forwardSpeed = Vector3.Dot(velocity, transform.forward);

            Transmission.BeginStep();
            HandleDiscreteCommands(input, forwardSpeed);

            // 1. Suspension. Cast first, then let the anti-roll bars adjust the pair,
            //    then push the forces in, so the tyre loads below are already correct.
            var mode = CurrentDriveMode;
            float springMultiplier = mode != null ? mode.SpringStiffnessMultiplier : 1f;
            float damperMultiplier = mode != null ? mode.DamperMultiplier : 1f;
            float antiRollMultiplier = mode != null ? mode.AntiRollMultiplier : 1f;

            for (int i = 0; i < wheels.Length; i++)
            {
                var axle = wheels[i].IsFrontAxle ? definition.FrontSuspension : definition.RearSuspension;
                wheels[i].CastSuspension(Body, definition, axle, springMultiplier, damperMultiplier, dt);
            }

            if (_frontLeft != null && _frontRight != null)
                VehicleWheel.ApplyAntiRollBar(_frontLeft, _frontRight, definition.FrontSuspension, antiRollMultiplier);
            if (_rearLeft != null && _rearRight != null)
                VehicleWheel.ApplyAntiRollBar(_rearLeft, _rearRight, definition.RearSuspension, antiRollMultiplier);

            for (int i = 0; i < wheels.Length; i++)
                wheels[i].ApplySuspensionForce(Body);

            // 2. Steering, before the tyres, because slip is measured in the steered frame.
            Steering.Apply(wheels, input.Steer, forwardSpeed, mode, dt);

            // 3. Tyre forces. This is what actually accelerates, brakes and turns the car.
            for (int i = 0; i < wheels.Length; i++)
                wheels[i].UpdateTireForces(Body, definition, _tireModel, dt);

            // 4. Driveline speed feeds the clutch, so it must be sampled before the gearbox.
            Drivetrain.SampleDrivelineSpeed(wheels);

            // 5. Traction control trims driver throttle before the engine ever sees it.
            float tractionMultiplier = Stability.EvaluateTractionControl(wheels, mode, dt);
            float throttleRequest = Mathf.Clamp01(input.Throttle) * tractionMultiplier;

            // 6. Gearbox and clutch, then the engine, then the differential.
            Transmission.Tick(Engine, Drivetrain.DrivenWheelAngularVelocity, forwardSpeed,
                              input.Throttle, input.Clutch, mode, dt);

            // The gearbox cuts ignition during a shift, so the engine cannot flare
            // while the clutch is open.
            throttleRequest *= Transmission.ShiftThrottleCut;

            if (Fuel.IsEmpty && Engine.IsRunning) Engine.Shutdown();

            Engine.Tick(throttleRequest, mode, Transmission.ClutchReactionTorqueNm,
                        Transmission.CanStallEngine, dt);

            Drivetrain.DistributeTorque(wheels, Transmission.DrivelineTorqueNm, Engine.EffectiveThrottle);

            // 7. Brakes, then stability control, which may only add brake torque.
            Brakes.Apply(wheels, input.Brake, input.Handbrake, forwardSpeed, dt);
            Stability.ApplyStabilityControl(Body, wheels, Steering.RoadWheelAngleDeg, forwardSpeed, mode);

            // 8. Integrate wheel rotation with everything that was applied to it.
            for (int i = 0; i < wheels.Length; i++)
                wheels[i].IntegrateRotation(definition, dt);

            // 9. Aerodynamics. This is what sets top speed.
            Aerodynamics.Apply(Body, dt);

            // 10. Housekeeping.
            ApplyStoppedHold(input, forwardSpeed, velocity);
            Fuel.Tick(Engine, forwardSpeed, dt);

            int grounded = CountWheelsOnGround();
            Odometer.Tick(transform.position, forwardSpeed, grounded, dt);

            UpdateTelemetry(velocity, forwardSpeed, grounded, dt);

            if (persistState)
            {
                _autoSaveTimer += dt;
                if (_autoSaveTimer >= autoSaveIntervalSeconds)
                {
                    _autoSaveTimer = 0f;
                    SaveState();
                }
            }

            _previousVelocity = velocity;
        }

        private void Update()
        {
            if (!_initialised) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < wheels.Length; i++)
            {
                var axle = wheels[i].IsFrontAxle ? definition.FrontSuspension : definition.RearSuspension;
                wheels[i].UpdateVisual(definition, axle, dt);
            }
        }

        // ==================================================================
        private void HandleDiscreteCommands(VehicleInputState input, float forwardSpeed)
        {
            if (input.ToggleIgnition)
            {
                bool wasRunning = Engine.IsRunning;
                Engine.ToggleIgnition();
                if (!wasRunning && selectDriveOnStart && Transmission.Mode == TransmissionMode.Automatic
                    && Transmission.CurrentGear == VehicleTransmission.NeutralGear)
                {
                    Transmission.SelectGear(1, forwardSpeed);
                }
            }

            if (input.ToggleDriveMode) CycleDriveMode();

            if (input.ToggleTransmissionMode)
            {
                Transmission.Mode = Transmission.Mode == TransmissionMode.Automatic
                    ? TransmissionMode.Manual
                    : TransmissionMode.Automatic;
            }

            if (input.ShiftUp) Transmission.ShiftUp(forwardSpeed);
            if (input.ShiftDown) Transmission.ShiftDown(forwardSpeed);

            if (input.RequestedGear != VehicleInputState.NoGearRequest)
                Transmission.SelectGear(input.RequestedGear, forwardSpeed);

            if (input.ToggleCamera) CameraToggleRequested?.Invoke();
        }

        public void CycleDriveMode()
        {
            if (definition.DriveModes == null || definition.DriveModes.Length == 0) return;
            SetDriveMode((DriveModeIndex + 1) % definition.DriveModes.Length);
        }

        public void SetDriveMode(int index)
        {
            if (definition.DriveModes == null || definition.DriveModes.Length == 0) return;
            DriveModeIndex = Mathf.Clamp(index, 0, definition.DriveModes.Length - 1);
            DriveModeChanged?.Invoke(DriveModeIndex);
        }

        /// <summary>
        /// A raycast rig has no static friction, so a stopped car would slide on any
        /// slope. While the driver is holding the car still we damp the remaining
        /// horizontal velocity directly. The force is bounded and only ever opposes
        /// motion, so it cannot push the car anywhere.
        /// </summary>
        private void ApplyStoppedHold(VehicleInputState input, float forwardSpeed, Vector3 velocity)
        {
            if (!holdWhenStopped) return;

            bool driverHolding = input.Brake > 0.1f || input.Handbrake > 0.1f;
            bool notAccelerating = input.Throttle < 0.05f;
            bool nearlyStopped = Mathf.Abs(forwardSpeed) < 0.6f;

            if (!driverHolding || !notAccelerating || !nearlyStopped) return;
            if (CountWheelsOnGround() < 3) return;

            Vector3 horizontal = Vector3.ProjectOnPlane(velocity, transform.up);
            Body.AddForce(-horizontal * Body.mass * 6f, ForceMode.Force);

            for (int i = 0; i < wheels.Length; i++)
                wheels[i].AngularVelocity *= 0.5f;
        }

        private int CountWheelsOnGround()
        {
            int count = 0;
            for (int i = 0; i < wheels.Length; i++)
                if (wheels[i].IsGrounded) count++;
            return count;
        }

        // ==================================================================
        private void UpdateTelemetry(Vector3 velocity, float forwardSpeed, int grounded, float dt)
        {
            var t = Telemetry;

            t.SpeedMps = velocity.magnitude;
            t.ForwardSpeedMps = forwardSpeed;
            t.SpeedKmh = t.SpeedMps * Units.MetresPerSecondToKmh;
            t.SpeedMph = t.SpeedMps * Units.MetresPerSecondToMph;

            Vector3 acceleration = (velocity - _previousVelocity) / Mathf.Max(0.0001f, dt);
            Vector3 localAcceleration = transform.InverseTransformDirection(acceleration);
            t.LongitudinalAccelerationG = localAcceleration.z / Units.Gravity;
            t.LateralAccelerationG = localAcceleration.x / Units.Gravity;

            t.EngineRpm = Engine.Rpm;
            t.EngineRpmNormalised = Mathf.Clamp01(Engine.Rpm / Mathf.Max(1f, definition.Engine.RedlineRpm));
            t.EngineState = Engine.State;
            t.EngineTorqueNm = Engine.OutputTorqueNm;
            t.EnginePowerHp = Units.TorqueToHorsepower(Mathf.Max(0f, Engine.CombustionTorqueNm), Engine.Rpm);
            t.BoostBar = Engine.BoostBar;
            t.RevLimiterActive = Engine.LimiterActive;
            t.BlowOffTriggered = Engine.BlowOffTriggered;
            t.OnOverrun = Engine.OnOverrun;

            t.Gear = Transmission.CurrentGear;
            t.GearLabel = Transmission.GearLabel;
            t.IsShifting = Transmission.IsShifting;
            t.ShiftEvent = Transmission.ShiftEventThisStep;
            t.ShiftWasDownshift = Transmission.LastShiftWasDownshift;
            t.ClutchLock = Transmission.ClutchLock;
            t.ClutchSlipRpm = Transmission.ClutchSlipRadPerSec * Units.RadPerSecToRpm;
            t.TransmissionMode = Transmission.Mode;

            t.Throttle = CurrentInput.Throttle;
            t.EffectiveThrottle = Engine.EffectiveThrottle;
            t.Brake = CurrentInput.Brake;
            t.Clutch = CurrentInput.Clutch;
            t.Steer = CurrentInput.Steer;
            t.Handbrake = CurrentInput.Handbrake;
            t.SteeringWheelAngleDeg = Steering.SteeringWheelAngleDeg;
            t.RoadWheelAngleDeg = Steering.RoadWheelAngleDeg;

            t.AbsActive = Brakes.AnyAbsActive;
            t.TractionControlActive = Stability.TractionControlActive;
            t.StabilityControlActive = Stability.StabilityControlActive;
            t.AbsEnabled = Brakes.AbsEnabled;
            t.TractionControlEnabled = Stability.TractionControlEnabled;
            t.StabilityControlEnabled = Stability.StabilityControlEnabled;

            var mode = CurrentDriveMode;
            t.DriveModeIndex = DriveModeIndex;
            t.DriveModeName = mode != null ? mode.DisplayName : "-";
            t.ExhaustValveOpen = mode != null && mode.ExhaustValveOpen;

            t.WheelsOnGround = grounded;
            float maxDriveSlip = 0f, maxLateral = 0f, maxSaturation = 0f;
            for (int i = 0; i < wheels.Length; i++)
            {
                var wheel = wheels[i];
                if (wheel.IsDriven) maxDriveSlip = Mathf.Max(maxDriveSlip, Mathf.Abs(wheel.SlipRatio));
                maxLateral = Mathf.Max(maxLateral, Mathf.Abs(wheel.SlipAngleRad));
                maxSaturation = Mathf.Max(maxSaturation, wheel.TireSaturation);
            }
            t.MaxDriveWheelSlip = maxDriveSlip;
            t.MaxLateralSlipDeg = maxLateral * Mathf.Rad2Deg;
            t.MaxTyreSaturation = maxSaturation;
            t.DominantSurface = wheels.Length > 0 ? wheels[0].Surface : SurfaceType.Asphalt;

            t.FuelLitres = Fuel.LitresRemaining;
            t.FuelCapacityLitres = Fuel.CapacityLitres;
            t.FuelFractionRemaining = Fuel.FractionRemaining;
            t.InstantConsumptionLPer100Km = Fuel.ConsumptionLPer100Km;

            t.OdometerKm = Odometer.TotalKilometres;
            t.OdometerMiles = Odometer.TotalMiles;
            t.TripKm = Odometer.TripKilometres;

            t.DragForceN = Aerodynamics.DragForceN;
            t.TotalDownforceN = Aerodynamics.FrontDownforceN + Aerodynamics.RearDownforceN;
        }

        // ==================================================================
        // Persistence
        // ==================================================================
        private void LoadPersistentState()
        {
            if (!persistState) return;

            if (string.IsNullOrEmpty(vehicleId))
                vehicleId = SaveSystem.GenerateVehicleId();

            _saveData = SaveSystem.Current.GetOrCreateVehicle(vehicleId, definition.name, definition.Identity.DisplayName);

            Odometer.Initialise(transform.position, _saveData.OdometerMetres, _saveData.TripMetres);

            SetDriveMode(_saveData.DriveModeIndex);
            Transmission.Mode = _saveData.ManualTransmission ? TransmissionMode.Manual : TransmissionMode.Automatic;

            Brakes.AbsEnabled = _saveData.AbsEnabled && definition.Brakes.AbsAvailable;
            Stability.TractionControlEnabled = _saveData.TractionControlEnabled && definition.Stability.TractionControlAvailable;
            Stability.StabilityControlEnabled = _saveData.StabilityControlEnabled && definition.Stability.StabilityControlAvailable;

            ApplyTuning(_saveData);
            Fuel.Initialise(definition, _saveData.FuelLitres);
        }

        /// <summary>
        /// Pushes saved tuning into the subsystems. Every one of these multiplies a
        /// real physical quantity, so a tune changes how the car drives, not a
        /// number on a menu.
        /// </summary>
        public void ApplyTuning(VehicleSaveData data)
        {
            if (data == null) return;

            Engine.TorqueMultiplier = data.EngineTorqueMultiplier;
            Engine.BoostBarOffset = data.BoostBarOffset;
            Engine.SpoolSpeedMultiplier = data.SpoolSpeedMultiplier;

            Transmission.GearRatioMultiplier = data.GearRatioMultiplier;
            Transmission.FinalDriveMultiplier = data.FinalDriveMultiplier;
            Transmission.ShiftSpeedMultiplier = data.ShiftSpeedMultiplier;

            Brakes.BrakeTorqueMultiplier = data.BrakeTorqueMultiplier;
            Brakes.BrakeBiasAdjustment = data.BrakeBias;

            Aerodynamics.DownforceMultiplier = data.DownforceMultiplier;
            Aerodynamics.DragMultiplier = data.DragMultiplier;

            if (Body != null && Mathf.Abs(data.MassOffsetKg) > 0.01f)
                Body.mass = Mathf.Max(200f, definition.Chassis.MassKg + data.MassOffsetKg);
        }

        public void SaveState()
        {
            if (!persistState || _saveData == null) return;

            _saveData.OdometerMetres = Odometer.TotalMetres;
            _saveData.TripMetres = Odometer.TripMetres;
            _saveData.DriveModeIndex = DriveModeIndex;
            _saveData.ManualTransmission = Transmission.Mode == TransmissionMode.Manual;
            _saveData.AbsEnabled = Brakes.AbsEnabled;
            _saveData.TractionControlEnabled = Stability.TractionControlEnabled;
            _saveData.StabilityControlEnabled = Stability.StabilityControlEnabled;
            _saveData.FuelLitres = Fuel.LitresRemaining;

            SaveSystem.Save();
        }

        // ==================================================================
        // Utilities
        // ==================================================================
        /// <summary>Repositions the car without the odometer counting the jump as distance driven.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            Body.SetLinearVelocity(Vector3.zero);
            Body.angularVelocity = Vector3.zero;
            Body.position = position;
            Body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);

            for (int i = 0; i < wheels.Length; i++) wheels[i].AngularVelocity = 0f;
            Odometer.NotifyTeleport(position);
        }

        /// <summary>Swaps the tyre model at runtime. Used by tests and by the tuning screen.</summary>
        public void SetTireModel(ITireModel model)
        {
            if (model != null) _tireModel = model;
        }

        private void OnDrawGizmosSelected()
        {
            if (definition == null || wheels == null) return;

            // Centre of mass, the single most important thing to get right when
            // authoring a car and the easiest to get wrong.
            Gizmos.color = Color.yellow;
            Vector3 com = transform.TransformPoint(definition.Chassis.CentreOfMassOffset);
            Gizmos.DrawWireSphere(com, 0.12f);

            for (int i = 0; i < wheels.Length; i++)
            {
                var wheel = wheels[i];
                if (wheel.SuspensionAnchor == null) continue;

                var axle = wheel.IsFrontAxle ? definition.FrontSuspension : definition.RearSuspension;
                Vector3 origin = wheel.SuspensionAnchor.position;
                Vector3 down = -wheel.SuspensionAnchor.up;

                Gizmos.color = Color.green;
                Gizmos.DrawLine(origin, origin + down * axle.RestLengthM);

                Gizmos.color = wheel.IsGrounded ? Color.cyan : Color.red;
                Gizmos.DrawWireSphere(origin + down * (axle.RestLengthM - wheel.CompressionM),
                                      definition.Wheels.RadiusM);
            }
        }
    }
}
