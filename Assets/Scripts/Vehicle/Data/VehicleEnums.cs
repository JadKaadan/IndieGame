namespace IndieGame.Vehicles.Data
{
    /// <summary>Which wheels receive engine torque.</summary>
    public enum DriveLayout
    {
        FrontWheelDrive,
        RearWheelDrive,
        AllWheelDrive
    }

    /// <summary>How an axle differential distributes torque between its two wheels.</summary>
    public enum DifferentialType
    {
        /// <summary>Equal torque to both wheels. One wheel in the air means no drive.</summary>
        Open,

        /// <summary>Torque biases toward the slower wheel proportionally to the speed difference.</summary>
        LimitedSlip,

        /// <summary>Both wheels forced to (nearly) the same speed.</summary>
        Locked
    }

    public enum TransmissionType
    {
        Manual,
        Automatic,
        DualClutch
    }

    /// <summary>Player-facing shifting behaviour, independent of the physical gearbox.</summary>
    public enum TransmissionMode
    {
        /// <summary>The gearbox chooses gears.</summary>
        Automatic,

        /// <summary>The driver chooses gears with paddles / buttons.</summary>
        Manual
    }

    public enum Aspiration
    {
        NaturallyAspirated,
        Turbocharged,
        Supercharged
    }

    public enum EngineState
    {
        Off,
        Starting,
        Running,
        Stalled
    }

    /// <summary>Assist preset that scales how much the electronics intervene.</summary>
    public enum AssistPreset
    {
        Casual,
        Sport,
        Simulation
    }

    public enum SurfaceType
    {
        Asphalt,
        Concrete,
        Dirt,
        Gravel,
        Grass,
        Wet
    }
}
