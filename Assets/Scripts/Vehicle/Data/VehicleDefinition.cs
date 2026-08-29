using System;
using UnityEngine;

namespace IndieGame.Vehicles.Data
{
    /// <summary>
    /// Every physical characteristic of a car, as data. No subsystem ever
    /// branches on a vehicle's name - it reads this asset. Adding car number 50
    /// means creating one of these plus a prefab, and writing zero new code.
    ///
    /// Prefab-level data (exhaust tip transforms, dashboard needle transforms,
    /// camera anchors, light references) deliberately lives on the prefab, not
    /// here, because it is per-model geometry rather than per-model engineering.
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/Vehicle Definition", fileName = "NewVehicleDefinition")]
    public class VehicleDefinition : ScriptableObject
    {
        // ==================================================================
        [Serializable]
        public class IdentityConfig
        {
            public string DisplayName = "Prototype Car";
            public string Manufacturer = "Fictional";
            public int ModelYear = 2024;
            public string VehicleClass = "Sports Coupe";

            [TextArea(2, 6)]
            [Tooltip("Where each number came from. Mark anything estimated as an approximation. " +
                     "Never leave a researched real-world spec unattributed.")]
            public string SpecificationSource =
                "Fictional vehicle. All values authored for gameplay, not derived from a real car.";
        }

        // ==================================================================
        [Serializable]
        public class ChassisConfig
        {
            [Tooltip("Kerb mass in kg, including fluids but excluding occupants.")]
            public float MassKg = 1520f;

            [Tooltip("Centre of mass in the vehicle's local space, in metres. " +
                     "The project's convention is that a vehicle prefab's root sits at GROUND level, " +
                     "centred between the wheels, facing +Z. So Y here is the CoM height above the road " +
                     "(roughly 0.42-0.55 m for a car, 0.65-0.8 m for an SUV) and Z is positive toward the front. " +
                     "This value is authoritative - FrontWeightDistribution below is informational.")]
            public Vector3 CentreOfMassOffset = new Vector3(0f, 0.48f, -0.05f);

            [Tooltip("Distance between front and rear axle centres, in metres.")]
            public float WheelbaseM = 2.75f;

            [Tooltip("Distance between left and right wheel centres, in metres.")]
            public float TrackWidthM = 1.58f;

            [Tooltip("Scales the inertia tensor Unity computes from the colliders. " +
                     "Values above 1 make the car feel heavier to rotate and reduce twitchiness.")]
            [Range(0.5f, 3f)] public float InertiaTensorScale = 1.35f;

            [Tooltip("Fraction of static weight on the front axle. Informational: used by the garage stats " +
                     "screen and by SuggestedCentreOfMassZ. The physics uses CentreOfMassOffset.")]
            [Range(0.3f, 0.7f)] public float FrontWeightDistribution = 0.52f;
        }

        // ==================================================================
        [Serializable]
        public class EngineConfig
        {
            public string Description = "3.0L Turbocharged Inline-6";
            public Aspiration Aspiration = Aspiration.Turbocharged;

            [Tooltip("Displacement in litres. Presentation only for now; used by the garage stats screen.")]
            public float DisplacementLitres = 3.0f;

            public float IdleRpm = 750f;
            public float RedlineRpm = 7000f;

            [Tooltip("RPM at which the limiter cuts fuel. Usually slightly above redline.")]
            public float RevLimiterRpm = 7100f;

            [Tooltip("Normalised torque curve. X is RPM / RedlineRpm (0..1), Y is fraction of PeakTorqueNm. " +
                     "This is the single source of truth for engine output - nothing else fakes power.")]
            public AnimationCurve NormalisedTorqueCurve = DefaultTorqueCurve();

            [Tooltip("Peak crankshaft torque in Nm AT FULL BOOST, on full throttle - i.e. the figure a " +
                     "manufacturer publishes. Off boost the engine makes proportionally less; see BoostTorqueGain.")]
            public float PeakTorqueNm = 500f;

            [Tooltip("Rotating inertia of crank + flywheel + clutch, kg*m^2. " +
                     "Higher means the engine revs more slowly. 0.15 is a light race engine, 0.35 a heavy road unit.")]
            public float InertiaKgM2 = 0.28f;

            [Tooltip("Constant friction torque, Nm. This is most of what you feel as engine braking.")]
            public float FrictionTorqueNm = 22f;

            [Tooltip("Additional friction torque per rad/s of engine speed. Makes engine braking rise with RPM.")]
            public float FrictionTorquePerRadPerSec = 0.045f;

            [Tooltip("Seconds the starter cranks before the engine catches.")]
            public float StarterDurationSeconds = 0.9f;

            [Tooltip("Engine speed below which the engine stalls when the clutch is engaged.")]
            public float StallRpm = 380f;

            [Header("Forced induction (ignored when naturally aspirated)")]
            [Tooltip("Maximum boost pressure in bar above atmospheric.")]
            public float MaxBoostBar = 0.95f;

            [Tooltip("How much of the engine's peak torque comes from boost. 0.62 means that off boost the " +
                     "engine makes 1 / (1 + 0.62) = 62% of PeakTorqueNm, and reaches the full figure only once " +
                     "the turbo is spooled. Raising MaxBoostBar with a turbo upgrade pushes past 100%.")]
            [Range(0f, 1.5f)] public float BoostTorqueGain = 0.62f;

            [Tooltip("Engine speed at which the turbo starts producing meaningful boost.")]
            public float BoostOnsetRpm = 1500f;

            [Tooltip("Engine speed at which the turbo can reach full boost.")]
            public float BoostFullRpm = 3200f;

            [Tooltip("Half-life in seconds for boost to build. This is the turbo lag you feel.")]
            [Range(0.02f, 2f)] public float BoostSpoolHalfLife = 0.30f;

            [Tooltip("Half-life in seconds for boost to bleed off the throttle. Much faster than spool-up.")]
            [Range(0.01f, 1f)] public float BoostDecayHalfLife = 0.10f;

            [Tooltip("Boost above this level when the throttle snaps shut triggers a blow-off valve event.")]
            public float BlowOffThresholdBar = 0.35f;

            /// <summary>
            /// Broad, flat plateau typical of a modern turbo engine: strong from
            /// just above idle, holding to roughly 70% of redline, then tapering.
            /// </summary>
            public static AnimationCurve DefaultTorqueCurve()
            {
                return new AnimationCurve(
                    new Keyframe(0.00f, 0.30f),
                    new Keyframe(0.11f, 0.46f),
                    new Keyframe(0.20f, 0.78f),
                    new Keyframe(0.26f, 1.00f),
                    new Keyframe(0.60f, 1.00f),
                    new Keyframe(0.72f, 0.94f),
                    new Keyframe(0.86f, 0.82f),
                    new Keyframe(1.00f, 0.62f));
            }
        }

        // ==================================================================
        [Serializable]
        public class TransmissionConfig
        {
            public TransmissionType Type = TransmissionType.DualClutch;

            [Tooltip("Forward gear ratios, first gear first. Length defines the gear count.")]
            public float[] ForwardGearRatios = { 5.25f, 3.36f, 2.17f, 1.72f, 1.32f, 1.00f, 0.82f, 0.64f };

            [Tooltip("Reverse gear ratio, entered as a positive number.")]
            public float ReverseGearRatio = 4.72f;

            public float FinalDriveRatio = 3.15f;

            [Tooltip("Fraction of engine torque that survives the driveline. 0.85-0.92 is typical.")]
            [Range(0.6f, 1f)] public float DriveEfficiency = 0.90f;

            [Tooltip("Base shift duration in seconds, before the drive mode multiplier. " +
                     "Torque is cut for this long. ~0.05 for a DCT, ~0.35 for a torque converter auto.")]
            public float ShiftTimeSeconds = 0.12f;

            [Tooltip("Minimum seconds between shifts. Prevents gear hunting.")]
            public float ShiftCooldownSeconds = 0.55f;

            [Tooltip("Maximum torque the clutch can transmit before it slips, Nm. " +
                     "Typically 1.5-2x peak engine torque.")]
            public float ClutchMaxTorqueNm = 900f;

            [Header("Automatic shift schedule (base values, before drive mode offsets)")]
            public float BaseUpshiftRpm = 5200f;
            public float BaseDownshiftRpm = 1600f;

            [Tooltip("Additional upshift RPM at full throttle. The gearbox holds gears when you ask for power.")]
            public float ThrottleUpshiftRpmGain = 1400f;

            [Tooltip("A downshift is refused if it would put the engine above this fraction of the rev limit. " +
                     "This is what stops the gearbox money-shifting the engine.")]
            [Range(0.7f, 1f)] public float DownshiftRpmSafetyFactor = 0.93f;
        }

        // ==================================================================
        [Serializable]
        public class DrivetrainConfig
        {
            public DriveLayout Layout = DriveLayout.RearWheelDrive;

            [Tooltip("For AWD only: fraction of drive torque sent to the front axle.")]
            [Range(0f, 1f)] public float FrontTorqueSplit = 0.35f;

            public DifferentialType FrontDifferential = DifferentialType.Open;
            public DifferentialType RearDifferential = DifferentialType.LimitedSlip;

            [Tooltip("LSD lock under power. Nm of bias torque per rad/s of wheel speed difference.")]
            public float LsdPowerLockCoefficient = 42f;

            [Tooltip("LSD lock on the overrun (off throttle). Usually weaker than the power side.")]
            public float LsdCoastLockCoefficient = 22f;

            [Tooltip("Static preload torque, Nm. Resists small speed differences even at zero throttle.")]
            public float LsdPreloadNm = 45f;

            [Tooltip("Upper bound on bias torque so the differential can never inject energy.")]
            public float LsdMaxLockNm = 900f;
        }

        // ==================================================================
        [Serializable]
        public class WheelConfig
        {
            [Tooltip("Loaded rolling radius in metres. A 245/40 R19 is about 0.339 m.")]
            public float RadiusM = 0.34f;

            [Tooltip("Tread width in metres. Wider tyres raise peak grip and lower load sensitivity.")]
            public float WidthM = 0.245f;

            [Tooltip("Rotational inertia of wheel + tyre + brake disc, kg*m^2. " +
                     "Roughly 0.5 * mass * radius^2 for a solid disc.")]
            public float InertiaKgM2 = 1.30f;

            [Tooltip("Layers the suspension raycast will hit. Must exclude the car's own colliders.")]
            public LayerMask GroundMask = ~0;
        }

        // ==================================================================
        [Serializable]
        public class TyreConfig
        {
            [Tooltip("Peak friction coefficient on dry asphalt. ~0.85 economy, ~1.15 performance summer, ~1.5 semi-slick.")]
            public float PeakFrictionCoefficient = 1.15f;

            [Header("Longitudinal (Pacejka magic formula, slip ratio)")]
            public float LongStiffnessB = 11.0f;
            public float LongShapeC = 1.65f;
            public float LongPeakD = 1.0f;
            public float LongCurvatureE = 0.95f;

            [Header("Lateral (Pacejka magic formula, slip angle in radians)")]
            public float LatStiffnessB = 14.0f;
            public float LatShapeC = 1.35f;
            public float LatPeakD = 1.0f;
            public float LatCurvatureE = -0.20f;

            [Header("Load sensitivity")]
            [Tooltip("Normal load in newtons at which the tyre delivers its nominal peak friction. " +
                     "Usually static corner weight: mass * 9.81 / 4.")]
            public float NominalLoadN = 3730f;

            [Tooltip("How much grip fades as load rises above nominal. This is what creates weight transfer " +
                     "effects: a heavily loaded outside tyre gains less grip than the inside tyre loses.")]
            [Range(0f, 0.6f)] public float LoadSensitivity = 0.22f;

            [Header("Transient")]
            [Tooltip("Relaxation length in metres. The tyre needs to roll this far before its " +
                     "lateral force catches up with a steering input. Also the main low-speed stabiliser.")]
            public float RelaxationLengthM = 0.55f;

            [Tooltip("Reference speed in m/s used as the slip denominator floor. " +
                     "Below this the slip calculation would divide by ~0 and explode.")]
            public float LowSpeedReferenceMps = 3.0f;

            [Tooltip("Rolling resistance coefficient. 0.010-0.015 for a road tyre on asphalt.")]
            public float RollingResistanceCoefficient = 0.013f;
        }

        // ==================================================================
        [Serializable]
        public class SuspensionAxleConfig
        {
            [Tooltip("Suspension length at full droop, in metres, measured from the anchor to the wheel centre.")]
            public float RestLengthM = 0.30f;

            [Tooltip("Total travel from full droop to full bump, in metres.")]
            public float MaxTravelM = 0.20f;

            [Tooltip("Spring rate in N/m. Static compression = corner weight / rate. " +
                     "Aim for 0.06-0.12 m of static compression on a road car.")]
            public float SpringRateNPerM = 40000f;

            [Tooltip("Damping in Ns/m while the spring compresses. Usually softer than rebound.")]
            public float CompressionDampingNsPerM = 3100f;

            [Tooltip("Damping in Ns/m while the spring extends. Controls how the body settles.")]
            public float ReboundDampingNsPerM = 4700f;

            [Tooltip("Anti-roll bar rate in N/m of left-right compression difference. 0 disables it.")]
            public float AntiRollBarNPerM = 16000f;

            [Tooltip("Bump stop stiffness applied over the last few centimetres of travel, N/m.")]
            public float BumpStopRateNPerM = 260000f;

            [Tooltip("Fraction of travel at each end that engages the bump stop.")]
            [Range(0f, 0.3f)] public float BumpStopZone = 0.08f;
        }

        // ==================================================================
        [Serializable]
        public class BrakeConfig
        {
            [Tooltip("Maximum brake torque per FRONT wheel, Nm. To lock a wheel you need roughly " +
                     "mu * wheelLoad * wheelRadius.")]
            public float MaxTorqueFrontNm = 2600f;

            [Tooltip("Maximum brake torque per REAR wheel, Nm.")]
            public float MaxTorqueRearNm = 1600f;

            [Tooltip("Handbrake torque per rear wheel, Nm. Independent of the main circuit.")]
            public float HandbrakeTorqueNm = 2400f;

            [Header("ABS")]
            public bool AbsAvailable = true;

            [Tooltip("Negative slip ratio at which ABS starts releasing pressure. " +
                     "Real systems target roughly -0.10 to -0.20 where peak braking grip lives.")]
            [Range(0.05f, 0.4f)] public float AbsSlipThreshold = 0.14f;

            [Tooltip("Fraction of brake torque left during an ABS release pulse.")]
            [Range(0f, 0.8f)] public float AbsReleaseFactor = 0.22f;

            [Tooltip("ABS modulation frequency in Hz. Real systems run 8-16 Hz.")]
            public float AbsCycleHz = 12f;

            [Tooltip("Below this speed ABS disengages and lets the wheels lock, as real systems do.")]
            public float AbsMinSpeedMps = 2.5f;
        }

        // ==================================================================
        [Serializable]
        public class SteeringConfig
        {
            [Tooltip("Maximum road wheel angle in degrees at full lock, standing still.")]
            public float MaxSteerAngleDeg = 36f;

            [Tooltip("Total steering wheel rotation lock to lock, in degrees. " +
                     "Used only to animate the cockpit wheel. ~540 sports, ~720 road, ~900 SUV.")]
            public float SteeringWheelLockDeg = 720f;

            [Tooltip("Degrees per second the rack can move. Limits how fast the driver can saw at the wheel.")]
            public float SteerRateDegPerSec = 260f;

            [Tooltip("Degrees per second the rack returns to centre when the input is released.")]
            public float SteerReturnRateDegPerSec = 340f;

            [Tooltip("Available steering angle as a fraction of maximum (Y) versus speed in m/s (X). " +
                     "This is why the car is not twitchy at 250 km/h.")]
            public AnimationCurve SpeedSensitivity = new AnimationCurve(
                new Keyframe(0f, 1.00f),
                new Keyframe(14f, 0.72f),
                new Keyframe(33f, 0.42f),
                new Keyframe(55f, 0.26f),
                new Keyframe(85f, 0.20f));

            [Tooltip("1 applies full Ackermann geometry (inside wheel turns more than outside). " +
                     "0 keeps both front wheels parallel.")]
            [Range(0f, 1f)] public float AckermannFactor = 0.85f;
        }

        // ==================================================================
        [Serializable]
        public class AeroConfig
        {
            [Tooltip("Drag coefficient. ~0.30 modern coupe, ~0.35 SUV, ~0.24 slippery sedan.")]
            public float DragCoefficient = 0.30f;

            [Tooltip("Frontal area in m^2. ~2.1 for a coupe, ~2.8 for an SUV.")]
            public float FrontalAreaM2 = 2.10f;

            [Tooltip("Front downforce coefficient (Cl * A equivalent). Road cars are near zero.")]
            public float FrontDownforceCoefficient = 0.06f;

            [Tooltip("Rear downforce coefficient. A big wing raises this and the drag coefficient together.")]
            public float RearDownforceCoefficient = 0.12f;

            [Tooltip("Local point where aero forces are applied, relative to the rigidbody origin.")]
            public Vector3 CentreOfPressureOffset = new Vector3(0f, 0.15f, -0.20f);
        }

        // ==================================================================
        [Serializable]
        public class StabilityConfig
        {
            public bool TractionControlAvailable = true;
            public bool StabilityControlAvailable = true;

            [Tooltip("How hard traction control cuts torque once slip exceeds the drive mode allowance. " +
                     "Higher is more abrupt.")]
            public float TractionControlGain = 3.5f;

            [Tooltip("Maximum brake torque ESC can apply to a single wheel to correct yaw, Nm.")]
            public float StabilityControlMaxBrakeNm = 900f;

            [Tooltip("Yaw rate error in rad/s that ESC tolerates before it intervenes.")]
            public float StabilityYawErrorDeadZone = 0.12f;

            [Tooltip("Gain from yaw rate error to corrective brake torque.")]
            public float StabilityControlGain = 2200f;
        }

        // ==================================================================
        [Serializable]
        public class FuelConfig
        {
            public float TankCapacityLitres = 60f;

            [Tooltip("Brake-specific fuel consumption, grams per kWh. " +
                     "Modern petrol engines sit around 250-320 g/kWh at their best point.")]
            public float SpecificConsumptionGPerKWh = 290f;

            [Tooltip("Fuel burned at idle, litres per hour.")]
            public float IdleConsumptionLPerHour = 0.85f;
        }

        // ==================================================================
        // Inspector layout
        // ==================================================================
        public IdentityConfig Identity = new IdentityConfig();
        public ChassisConfig Chassis = new ChassisConfig();
        public EngineConfig Engine = new EngineConfig();
        public TransmissionConfig Transmission = new TransmissionConfig();
        public DrivetrainConfig Drivetrain = new DrivetrainConfig();
        public WheelConfig Wheels = new WheelConfig();
        public TyreConfig Tyres = new TyreConfig();
        public SuspensionAxleConfig FrontSuspension = new SuspensionAxleConfig();
        public SuspensionAxleConfig RearSuspension = new SuspensionAxleConfig();
        public BrakeConfig Brakes = new BrakeConfig();
        public SteeringConfig Steering = new SteeringConfig();
        public AeroConfig Aero = new AeroConfig();
        public StabilityConfig Stability = new StabilityConfig();
        public FuelConfig Fuel = new FuelConfig();

        [Tooltip("Drive modes in cycle order. The first entry is the default at spawn.")]
        public DriveModeSettings[] DriveModes =
        {
            DriveModeSettings.CreateComfortDefault(),
            DriveModeSettings.CreateSportDefault()
        };

        // ------------------------------------------------------------------
        // Derived values used by the garage screen and by the simulation
        // ------------------------------------------------------------------
        public int ForwardGearCount => Transmission.ForwardGearRatios != null ? Transmission.ForwardGearRatios.Length : 0;

        /// <summary>Crankshaft torque in Nm at a given RPM, at wide open throttle, without boost.</summary>
        public float EvaluateNaturalTorque(float rpm)
        {
            float normalised = Mathf.Clamp01(rpm / Mathf.Max(1f, Engine.RedlineRpm));
            return Engine.NormalisedTorqueCurve.Evaluate(normalised) * Engine.PeakTorqueNm;
        }

        /// <summary>Peak crankshaft torque including full boost, and the RPM where it occurs.</summary>
        public void CalculatePeakTorque(out float torqueNm, out float atRpm)
        {
            torqueNm = 0f;
            atRpm = Engine.IdleRpm;
            for (float rpm = Engine.IdleRpm; rpm <= Engine.RedlineRpm; rpm += 25f)
            {
                float t = EvaluateNaturalTorque(rpm) * BoostMultiplierAtRpm(rpm);
                if (t > torqueNm) { torqueNm = t; atRpm = rpm; }
            }
        }

        /// <summary>Peak power in mechanical horsepower, and the RPM where it occurs.</summary>
        public void CalculatePeakPower(out float horsepower, out float atRpm)
        {
            horsepower = 0f;
            atRpm = Engine.IdleRpm;
            for (float rpm = Engine.IdleRpm; rpm <= Engine.RedlineRpm; rpm += 25f)
            {
                float t = EvaluateNaturalTorque(rpm) * BoostMultiplierAtRpm(rpm);
                float hp = Core.Units.TorqueToHorsepower(t, rpm);
                if (hp > horsepower) { horsepower = hp; atRpm = rpm; }
            }
        }

        /// <summary>
        /// Steady-state torque multiplier from forced induction at a given RPM on full throttle.
        /// Normalised so that full boost yields exactly 1.0, which is what makes
        /// <see cref="EngineConfig.PeakTorqueNm"/> the published peak rather than an NA base figure.
        /// </summary>
        public float BoostMultiplierAtRpm(float rpm)
        {
            if (Engine.Aspiration == Aspiration.NaturallyAspirated) return 1f;
            float spool = Core.SimMath.Remap01(rpm, Engine.BoostOnsetRpm, Engine.BoostFullRpm);
            if (Engine.Aspiration == Aspiration.Supercharged) spool = Mathf.Max(spool, 0.55f); // belt driven: boost from low RPM
            return BoostMultiplierFromFraction(spool);
        }

        /// <summary>
        /// Converts a boost fraction (1 = the definition's MaxBoostBar) into a torque
        /// multiplier. Values above 1 are reachable once a turbo upgrade raises the
        /// boost ceiling, which is how a tune genuinely adds power.
        /// </summary>
        public float BoostMultiplierFromFraction(float boostFraction)
        {
            float gain = Engine.BoostTorqueGain;
            return (1f + gain * boostFraction) / (1f + gain);
        }

        /// <summary>
        /// Crankshaft torque at a given RPM with tuning applied: the ECU multiplier
        /// scales the whole curve, and a turbo upgrade raises the boost ceiling so the
        /// multiplier can exceed 1. This is the same maths the engine runs, so the
        /// dyno screen and the car cannot disagree.
        /// </summary>
        public float TunedTorqueAtRpm(float rpm, float torqueMultiplier, float boostBarOffset)
        {
            float natural = EvaluateNaturalTorque(rpm);
            if (Engine.Aspiration == Aspiration.NaturallyAspirated)
                return natural * torqueMultiplier;

            float spool = Core.SimMath.Remap01(rpm, Engine.BoostOnsetRpm, Engine.BoostFullRpm);
            if (Engine.Aspiration == Aspiration.Supercharged) spool = Mathf.Max(spool, 0.55f);

            float ceilingBar = Mathf.Max(0.01f, Engine.MaxBoostBar + boostBarOffset);
            float boostFraction = spool * ceilingBar / Mathf.Max(0.01f, Engine.MaxBoostBar);
            return natural * BoostMultiplierFromFraction(boostFraction) * torqueMultiplier;
        }

        /// <summary>Peak tuned power in hp and the RPM it occurs at.</summary>
        public void CalculateTunedPeaks(float torqueMultiplier, float boostBarOffset,
                                        out float horsepower, out float hpRpm,
                                        out float torqueNm, out float torqueRpm)
        {
            horsepower = 0f; hpRpm = Engine.IdleRpm;
            torqueNm = 0f; torqueRpm = Engine.IdleRpm;
            for (float rpm = Engine.IdleRpm; rpm <= Engine.RedlineRpm; rpm += 25f)
            {
                float t = TunedTorqueAtRpm(rpm, torqueMultiplier, boostBarOffset);
                float hp = Core.Units.TorqueToHorsepower(t, rpm);
                if (t > torqueNm) { torqueNm = t; torqueRpm = rpm; }
                if (hp > horsepower) { horsepower = hp; hpRpm = rpm; }
            }
        }

        /// <summary>
        /// Local Z the centre of mass would need in order to produce
        /// <see cref="ChassisConfig.FrontWeightDistribution"/>, given that the root is
        /// centred between the axles. Use it when authoring a car from a real corner-weight figure.
        /// </summary>
        public float SuggestedCentreOfMassZ()
        {
            // Distance ahead of the rear axle = frontShare * wheelbase; the root sits at mid-wheelbase.
            float fromRearAxle = Chassis.FrontWeightDistribution * Chassis.WheelbaseM;
            return fromRearAxle - Chassis.WheelbaseM * 0.5f;
        }

        /// <summary>Engine RPM at a given road speed in a given gear. Used to validate gearing against real data.</summary>
        public float RpmAtSpeed(float speedMps, int forwardGearIndex)
        {
            if (forwardGearIndex < 0 || forwardGearIndex >= ForwardGearCount) return 0f;
            float wheelOmega = speedMps / Mathf.Max(0.01f, Wheels.RadiusM);
            float ratio = Transmission.ForwardGearRatios[forwardGearIndex] * Transmission.FinalDriveRatio;
            return wheelOmega * ratio * Core.Units.RadPerSecToRpm;
        }

        private void OnValidate()
        {
            Engine.RedlineRpm = Mathf.Max(Engine.IdleRpm + 500f, Engine.RedlineRpm);
            Engine.RevLimiterRpm = Mathf.Max(Engine.RedlineRpm, Engine.RevLimiterRpm);
            Chassis.MassKg = Mathf.Max(200f, Chassis.MassKg);
            Wheels.RadiusM = Mathf.Max(0.05f, Wheels.RadiusM);
            Wheels.InertiaKgM2 = Mathf.Max(0.05f, Wheels.InertiaKgM2);
            Engine.InertiaKgM2 = Mathf.Max(0.02f, Engine.InertiaKgM2);
            Transmission.FinalDriveRatio = Mathf.Max(0.1f, Transmission.FinalDriveRatio);
        }
    }
}
