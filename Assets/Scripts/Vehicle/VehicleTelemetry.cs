using IndieGame.Vehicles.Data;

namespace IndieGame.Vehicles
{
    /// <summary>
    /// Read-only view of the simulation for everything that presents it: the HUD,
    /// the cockpit gauges, the audio system, the exhaust VFX, the debug overlay.
    ///
    /// Presentation code reads this and never reaches into the physics classes.
    /// That boundary is what keeps the dashboard honest - the needle can only ever
    /// show what the simulation actually computed - and it is also the seam a
    /// network layer would replicate across later.
    /// </summary>
    public class VehicleTelemetry
    {
        // --- Motion ---------------------------------------------------------
        public float SpeedMps;
        public float ForwardSpeedMps;
        public float SpeedKmh;
        public float SpeedMph;
        public float LateralAccelerationG;
        public float LongitudinalAccelerationG;

        // --- Engine ---------------------------------------------------------
        public float EngineRpm;
        public float EngineRpmNormalised;
        public EngineState EngineState;
        public float EngineTorqueNm;
        public float EnginePowerHp;
        public float BoostBar;
        public bool RevLimiterActive;
        public bool BlowOffTriggered;
        public bool OnOverrun;

        // --- Driveline ------------------------------------------------------
        public int Gear;
        public string GearLabel = "N";
        public bool IsShifting;
        public bool ShiftEvent;
        public bool ShiftWasDownshift;
        public float ClutchLock;
        public float ClutchSlipRpm;
        public TransmissionMode TransmissionMode;

        // --- Driver input ---------------------------------------------------
        public float Throttle;
        public float EffectiveThrottle;
        public float Brake;
        public float Clutch;
        public float Steer;
        public float Handbrake;
        public float SteeringWheelAngleDeg;
        public float RoadWheelAngleDeg;

        // --- Assists --------------------------------------------------------
        public bool AbsActive;
        public bool TractionControlActive;
        public bool StabilityControlActive;
        public bool AbsEnabled;
        public bool TractionControlEnabled;
        public bool StabilityControlEnabled;

        // --- Drive mode -----------------------------------------------------
        public int DriveModeIndex;
        public string DriveModeName = "COMFORT";
        public bool ExhaustValveOpen;

        // --- Wheels ---------------------------------------------------------
        public int WheelsOnGround;
        public float MaxDriveWheelSlip;
        public float MaxLateralSlipDeg;
        public float MaxTyreSaturation;
        public SurfaceType DominantSurface;

        // --- Consumables ----------------------------------------------------
        public float FuelLitres;
        public float FuelCapacityLitres;
        public float FuelFractionRemaining;
        public float InstantConsumptionLPer100Km;

        // --- Mileage --------------------------------------------------------
        public float OdometerKm;
        public float OdometerMiles;
        public float TripKm;

        // --- Aero -----------------------------------------------------------
        public float DragForceN;
        public float TotalDownforceN;
    }
}
