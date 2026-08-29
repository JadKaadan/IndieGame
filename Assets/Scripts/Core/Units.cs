namespace IndieGame.Core
{
    /// <summary>
    /// Central place for unit conversions and physical constants.
    /// Rule: the simulation works in SI units (metres, seconds, kilograms,
    /// newtons, radians). Only presentation code converts to km/h, mph or RPM.
    /// </summary>
    public static class Units
    {
        // --- Speed ---------------------------------------------------------
        public const float MetresPerSecondToKmh = 3.6f;
        public const float MetresPerSecondToMph = 2.236936f;
        public const float KmhToMetresPerSecond = 1f / 3.6f;

        // --- Rotation ------------------------------------------------------
        /// <summary>rad/s -> rev/min. 60 / (2 * PI).</summary>
        public const float RadPerSecToRpm = 9.549297f;

        /// <summary>rev/min -> rad/s. (2 * PI) / 60.</summary>
        public const float RpmToRadPerSec = 0.1047198f;

        // --- Environment ---------------------------------------------------
        /// <summary>Air density at sea level, 15 C, kg/m^3.</summary>
        public const float AirDensity = 1.225f;

        /// <summary>Standard gravity, m/s^2.</summary>
        public const float Gravity = 9.80665f;

        // --- Pressure ------------------------------------------------------
        public const float BarToPascal = 100000f;
        public const float BarToPsi = 14.503774f;

        /// <summary>
        /// Mechanical horsepower from torque (Nm) and engine speed (rpm).
        /// hp = (Nm * rpm) / 7127. Use <see cref="TorqueToKilowatts"/> for SI.
        /// </summary>
        public static float TorqueToHorsepower(float newtonMetres, float rpm)
        {
            return newtonMetres * rpm / 7127f;
        }

        /// <summary>Metric horsepower (PS) from torque (Nm) and rpm.</summary>
        public static float TorqueToMetricHorsepower(float newtonMetres, float rpm)
        {
            return newtonMetres * rpm / 7023.5f;
        }

        /// <summary>Power in kilowatts from torque (Nm) and rpm.</summary>
        public static float TorqueToKilowatts(float newtonMetres, float rpm)
        {
            return newtonMetres * rpm * RpmToRadPerSec / 1000f;
        }
    }
}
