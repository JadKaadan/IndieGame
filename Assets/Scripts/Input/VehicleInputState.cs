using System;
using UnityEngine;

namespace IndieGame.VehicleInput
{
    /// <summary>
    /// Device-agnostic snapshot of driver intent for a single simulation step.
    /// Nothing in the physics stack ever reads a keyboard, a gamepad or a wheel
    /// directly - it only reads this struct. That is what makes it possible to
    /// add a Logitech/Fanatec wheel, a replay system or a network client later
    /// without touching VehicleController.
    /// </summary>
    [Serializable]
    public struct VehicleInputState
    {
        // --- Continuous axes -----------------------------------------------
        /// <summary>0..1 accelerator pedal travel.</summary>
        public float Throttle;

        /// <summary>0..1 brake pedal travel.</summary>
        public float Brake;

        /// <summary>-1..1 steering. -1 is full left.</summary>
        public float Steer;

        /// <summary>0..1 clutch pedal travel. 1 means fully depressed (disengaged).</summary>
        public float Clutch;

        /// <summary>0..1 handbrake / parking brake lever travel.</summary>
        public float Handbrake;

        /// <summary>Free look, used by the cockpit and chase cameras.</summary>
        public Vector2 Look;

        // --- Edge-triggered commands ---------------------------------------
        // These are buffered by the input source between physics steps so a tap
        // during a render frame is never dropped by FixedUpdate.
        public bool ShiftUp;
        public bool ShiftDown;
        public bool ToggleIgnition;
        public bool ToggleDriveMode;
        public bool ToggleTransmissionMode;
        public bool ToggleCamera;
        public bool ToggleHeadlights;
        public bool ToggleHazards;
        public bool IndicatorLeft;
        public bool IndicatorRight;

        /// <summary>
        /// Direct gear request for an H-pattern shifter. -100 means "no request".
        /// -1 is reverse, 0 is neutral, 1..N are forward gears.
        /// </summary>
        public int RequestedGear;

        public const int NoGearRequest = -100;

        public static VehicleInputState Neutral => new VehicleInputState { RequestedGear = NoGearRequest };

        /// <summary>Clears only the one-shot commands, keeping the analogue axes.</summary>
        public void ClearEdges()
        {
            ShiftUp = false;
            ShiftDown = false;
            ToggleIgnition = false;
            ToggleDriveMode = false;
            ToggleTransmissionMode = false;
            ToggleCamera = false;
            ToggleHeadlights = false;
            ToggleHazards = false;
            IndicatorLeft = false;
            IndicatorRight = false;
            RequestedGear = NoGearRequest;
        }
    }
}
