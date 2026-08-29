using System;
using IndieGame.Core;
using IndieGame.Vehicles.Data;
using UnityEngine;

namespace IndieGame.Vehicles
{
    /// <summary>
    /// Fuel tank and consumption.
    ///
    /// Consumption is derived from the work the engine is actually doing, using a
    /// brake-specific fuel consumption figure, so cruising uses little and full
    /// throttle in a low gear drinks. The gauge therefore shows something real
    /// rather than a timer counting down.
    /// </summary>
    [Serializable]
    public class VehicleFuelSystem
    {
        /// <summary>Density of petrol, kg per litre.</summary>
        private const float PetrolDensityKgPerLitre = 0.745f;

        private VehicleDefinition _definition;

        public float LitresRemaining { get; private set; }
        public float CapacityLitres => _definition != null ? _definition.Fuel.TankCapacityLitres : 0f;
        public float FractionRemaining => CapacityLitres > 0.01f ? Mathf.Clamp01(LitresRemaining / CapacityLitres) : 0f;

        /// <summary>Instantaneous consumption in litres per 100 km. Zero when stationary.</summary>
        public float ConsumptionLPer100Km { get; private set; }

        /// <summary>When false the tank never empties. Default for the prototype.</summary>
        public bool ConsumptionEnabled = true;

        /// <summary>True once the tank is dry. The controller uses this to cut the engine.</summary>
        public bool IsEmpty => ConsumptionEnabled && LitresRemaining <= 0.001f;

        public void Initialise(VehicleDefinition definition, float savedLitres)
        {
            _definition = definition;
            LitresRemaining = savedLitres >= 0f
                ? Mathf.Clamp(savedLitres, 0f, definition.Fuel.TankCapacityLitres)
                : definition.Fuel.TankCapacityLitres;
        }

        public void Refuel(float litres)
        {
            LitresRemaining = Mathf.Clamp(LitresRemaining + litres, 0f, CapacityLitres);
        }

        public void FillTank() => LitresRemaining = CapacityLitres;

        public void Tick(VehicleEngine engine, float speedMps, float deltaTime)
        {
            if (_definition == null || !ConsumptionEnabled) return;
            if (!engine.IsRunning) { ConsumptionLPer100Km = 0f; return; }

            var config = _definition.Fuel;

            // Idle burn is a floor - the engine keeps running even producing no useful work.
            float idleLitresPerSecond = config.IdleConsumptionLPerHour / 3600f;

            // Useful work: positive crankshaft power only. Engine braking burns nothing
            // extra (modern engines cut injection entirely on the overrun).
            float powerKw = Mathf.Max(0f, Units.TorqueToKilowatts(engine.CombustionTorqueNm, engine.Rpm));
            float gramsPerSecond = powerKw * config.SpecificConsumptionGPerKWh / 3600f;
            float litresPerSecond = gramsPerSecond / 1000f / PetrolDensityKgPerLitre;

            float total = Mathf.Max(idleLitresPerSecond, litresPerSecond);
            LitresRemaining = Mathf.Max(0f, LitresRemaining - total * deltaTime);

            float speedKmh = Mathf.Abs(speedMps) * Units.MetresPerSecondToKmh;
            ConsumptionLPer100Km = speedKmh > 3f ? total * 3600f / speedKmh * 100f : 0f;
        }
    }
}
