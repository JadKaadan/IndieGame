using System;
using UnityEngine;

namespace IndieGame.Vehicles
{
    /// <summary>
    /// Persistent distance travelled by one specific car.
    ///
    /// Distance comes from actual movement of the rigidbody, sampled every physics
    /// step, not from integrating a speed readout. Movement that is not driving is
    /// rejected: a teleport, a respawn, or a fall through the world produce a
    /// position jump far larger than the car's speed allows in one step, and a car
    /// with no wheels on the ground is not covering road.
    ///
    /// The total is written into the save file by <see cref="Persistence.SaveSystem"/>,
    /// so the reading survives quitting the game.
    /// </summary>
    [Serializable]
    public class VehicleOdometer
    {
        /// <summary>Lifetime distance for this vehicle, in metres.</summary>
        public double TotalMetres { get; private set; }

        /// <summary>Distance since the trip meter was last reset, in metres.</summary>
        public double TripMetres { get; private set; }

        public float TotalKilometres => (float)(TotalMetres / 1000.0);
        public float TotalMiles => (float)(TotalMetres / 1609.344);
        public float TripKilometres => (float)(TripMetres / 1000.0);

        /// <summary>Set true for one step when a suspicious jump was rejected. Useful for debugging.</summary>
        public bool LastSampleRejected { get; private set; }

        private Vector3 _lastPosition;
        private bool _hasPosition;

        /// <summary>
        /// Tolerance on the plausibility check. A step's displacement may exceed
        /// speed * dt by this factor before it is treated as a teleport.
        /// </summary>
        private const float PlausibilityFactor = 2.5f;

        private const float MinimumStepAllowanceM = 0.05f;

        /// <summary>Seeds the odometer when a saved vehicle is spawned.</summary>
        public void Initialise(Vector3 startPosition, double savedMetres, double savedTripMetres = 0.0)
        {
            TotalMetres = Math.Max(0.0, savedMetres);
            TripMetres = Math.Max(0.0, savedTripMetres);
            _lastPosition = startPosition;
            _hasPosition = true;
        }

        /// <summary>
        /// Call once per physics step, after the physics update, with the car's
        /// current position and speed.
        /// </summary>
        /// <param name="wheelsOnGround">Number of wheels currently in contact.</param>
        public void Tick(Vector3 position, float speedMps, int wheelsOnGround, float deltaTime)
        {
            LastSampleRejected = false;

            if (!_hasPosition)
            {
                _lastPosition = position;
                _hasPosition = true;
                return;
            }

            float travelled = Vector3.Distance(_lastPosition, position);
            _lastPosition = position;

            // A car in mid-air or upside down in a ditch is not accumulating mileage.
            if (wheelsOnGround < 2) return;

            // Reject anything that could not have been driven at the reported speed.
            float allowance = Mathf.Abs(speedMps) * deltaTime * PlausibilityFactor + MinimumStepAllowanceM;
            if (travelled > allowance)
            {
                LastSampleRejected = true;
                return;
            }

            TotalMetres += travelled;
            TripMetres += travelled;
        }

        /// <summary>Called when a vehicle is teleported deliberately, to avoid a false rejection log.</summary>
        public void NotifyTeleport(Vector3 newPosition)
        {
            _lastPosition = newPosition;
        }

        public void ResetTrip() => TripMetres = 0.0;

        /// <summary>Used by the save system. Prefer <see cref="Initialise"/> when spawning.</summary>
        public void RestoreTotals(double totalMetres, double tripMetres)
        {
            TotalMetres = Math.Max(0.0, totalMetres);
            TripMetres = Math.Max(0.0, tripMetres);
        }
    }
}
