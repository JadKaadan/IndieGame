using System;
using UnityEngine;

namespace IndieGame.Vehicles.Data
{
    /// <summary>
    /// A drive mode is a set of real mechanical/electronic overrides, not a UI
    /// label. Every field here is consumed by an actual subsystem: the throttle
    /// map by <see cref="VehicleEngine"/>, the shift offsets by
    /// <see cref="VehicleTransmission"/>, the damper multipliers by the
    /// suspension, the slip allowances by the stability systems.
    /// </summary>
    [Serializable]
    public class DriveModeSettings
    {
        [Header("Identity")]
        public string DisplayName = "COMFORT";

        [Header("Throttle")]
        [Tooltip("Maps raw pedal travel (x, 0..1) to commanded throttle (y, 0..1). " +
                 "A curve below the diagonal is a lazy comfort map; above it is an aggressive sport map.")]
        public AnimationCurve ThrottleMap = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Half-life in seconds for commanded throttle to track the pedal. Lower is sharper.")]
        [Range(0.005f, 0.4f)] public float ThrottleResponseHalfLife = 0.09f;

        [Header("Transmission")]
        [Tooltip("Added to the vehicle's base upshift RPM. Sport holds gears longer.")]
        public float UpshiftRpmOffset = 0f;

        [Tooltip("Added to the vehicle's base downshift RPM. Sport downshifts earlier and more eagerly.")]
        public float DownshiftRpmOffset = 0f;

        [Tooltip("Scales shift duration. Below 1 is a faster, harsher shift.")]
        [Range(0.2f, 2f)] public float ShiftTimeMultiplier = 1f;

        [Tooltip("Pedal travel above which the gearbox will kick down a gear.")]
        [Range(0.4f, 1f)] public float KickdownThreshold = 0.85f;

        [Header("Steering")]
        [Tooltip("Scales how quickly the rack follows the input. Sport is more direct.")]
        [Range(0.4f, 2f)] public float SteeringRateMultiplier = 1f;

        [Tooltip("Scales the speed-sensitive steering reduction. 1 is full assist, 0 removes it.")]
        [Range(0f, 1.5f)] public float SteeringAssistMultiplier = 1f;

        [Header("Suspension (adaptive dampers only)")]
        [Range(0.6f, 1.6f)] public float SpringStiffnessMultiplier = 1f;
        [Range(0.6f, 2f)] public float DamperMultiplier = 1f;
        [Range(0.6f, 2f)] public float AntiRollMultiplier = 1f;

        [Header("Electronics")]
        [Tooltip("Longitudinal slip ratio the traction control will tolerate before cutting torque. Higher lets the car spin more.")]
        [Range(0.02f, 0.6f)] public float TractionControlSlipAllowance = 0.10f;

        [Tooltip("0 disables stability control in this mode, 1 is full intervention.")]
        [Range(0f, 1f)] public float StabilityControlStrength = 1f;

        [Header("Exhaust")]
        [Tooltip("Opens the exhaust valve: louder, and required before overrun pops can occur.")]
        public bool ExhaustValveOpen = false;

        [Tooltip("Base probability multiplier for overrun pops and bangs in this mode.")]
        [Range(0f, 1f)] public float ExhaustOverrunIntensity = 0f;

        /// <summary>Comfort: lazy pedal, early shifts, soft dampers, full electronics.</summary>
        public static DriveModeSettings CreateComfortDefault()
        {
            return new DriveModeSettings
            {
                DisplayName = "COMFORT",
                // Below the diagonal: the first third of pedal travel asks for very little.
                ThrottleMap = new AnimationCurve(
                    new Keyframe(0f, 0f, 0.35f, 0.35f),
                    new Keyframe(0.5f, 0.32f, 0.9f, 0.9f),
                    new Keyframe(1f, 1f, 1.9f, 1.9f)),
                ThrottleResponseHalfLife = 0.16f,
                UpshiftRpmOffset = 0f,
                DownshiftRpmOffset = 0f,
                ShiftTimeMultiplier = 1.35f,
                KickdownThreshold = 0.88f,
                SteeringRateMultiplier = 0.85f,
                SteeringAssistMultiplier = 1f,
                SpringStiffnessMultiplier = 1f,
                DamperMultiplier = 1f,
                AntiRollMultiplier = 1f,
                TractionControlSlipAllowance = 0.08f,
                StabilityControlStrength = 1f,
                ExhaustValveOpen = false,
                ExhaustOverrunIntensity = 0f
            };
        }

        /// <summary>Sport: sharp pedal, gears held to redline, firm dampers, longer leash.</summary>
        public static DriveModeSettings CreateSportDefault()
        {
            return new DriveModeSettings
            {
                DisplayName = "SPORT",
                // Above the diagonal: the first third of pedal travel already asks for half throttle.
                ThrottleMap = new AnimationCurve(
                    new Keyframe(0f, 0f, 2.2f, 2.2f),
                    new Keyframe(0.5f, 0.72f, 0.8f, 0.8f),
                    new Keyframe(1f, 1f, 0.35f, 0.35f)),
                ThrottleResponseHalfLife = 0.045f,
                UpshiftRpmOffset = 1100f,
                DownshiftRpmOffset = 700f,
                ShiftTimeMultiplier = 0.55f,
                KickdownThreshold = 0.62f,
                SteeringRateMultiplier = 1.25f,
                SteeringAssistMultiplier = 0.6f,
                SpringStiffnessMultiplier = 1.15f,
                DamperMultiplier = 1.35f,
                AntiRollMultiplier = 1.25f,
                TractionControlSlipAllowance = 0.22f,
                StabilityControlStrength = 0.45f,
                ExhaustValveOpen = true,
                ExhaustOverrunIntensity = 0.65f
            };
        }
    }
}
