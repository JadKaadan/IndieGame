using UnityEngine;

namespace IndieGame.Core
{
    /// <summary>Small numerical helpers used by the vehicle simulation.</summary>
    public static class SimMath
    {
        /// <summary>
        /// Frame-rate independent exponential smoothing.
        /// <paramref name="halfLife"/> is the time for the value to close half
        /// the remaining gap. Unlike Lerp(a, b, rate * dt) this is stable at any
        /// timestep, which matters because we run physics at 200 Hz.
        /// </summary>
        public static float Damp(float current, float target, float halfLife, float deltaTime)
        {
            if (halfLife <= 0.0001f) return target;
            float t = 1f - Mathf.Pow(0.5f, deltaTime / halfLife);
            return current + (target - current) * t;
        }

        public static Vector3 Damp(Vector3 current, Vector3 target, float halfLife, float deltaTime)
        {
            if (halfLife <= 0.0001f) return target;
            float t = 1f - Mathf.Pow(0.5f, deltaTime / halfLife);
            return current + (target - current) * t;
        }

        /// <summary>Safe divide that avoids NaN when the denominator collapses.</summary>
        public static float SafeDivide(float numerator, float denominator, float epsilon = 0.0001f)
        {
            if (Mathf.Abs(denominator) < epsilon)
                denominator = denominator >= 0f ? epsilon : -epsilon;
            return numerator / denominator;
        }

        /// <summary>Remaps <paramref name="value"/> from one range to another, clamped 0..1 style.</summary>
        public static float Remap01(float value, float inMin, float inMax)
        {
            if (Mathf.Approximately(inMax, inMin)) return 0f;
            return Mathf.Clamp01((value - inMin) / (inMax - inMin));
        }

        /// <summary>Signed value with a dead zone applied and the remainder rescaled to full range.</summary>
        public static float ApplyDeadZone(float value, float deadZone)
        {
            float magnitude = Mathf.Abs(value);
            if (magnitude < deadZone) return 0f;
            float scaled = (magnitude - deadZone) / (1f - deadZone);
            return Mathf.Sign(value) * Mathf.Clamp01(scaled);
        }
    }
}
