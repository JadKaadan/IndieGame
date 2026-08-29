using System;
using System.Collections.Generic;
using UnityEngine;

namespace IndieGame.Persistence
{
    /// <summary>
    /// Everything persisted about one owned car. The vehicle's identity is its
    /// <see cref="VehicleId"/>, not its model: two cars of the same model are two
    /// separate records with their own mileage and their own tuning, which is what
    /// makes used-car ownership possible later.
    /// </summary>
    [Serializable]
    public class VehicleSaveData
    {
        /// <summary>Unique per owned car, e.g. CAR_000142. Generated at purchase/spawn.</summary>
        public string VehicleId = "";

        /// <summary>Name of the VehicleDefinition asset this car is built from.</summary>
        public string DefinitionName = "";

        public string DisplayName = "";

        // --- Mileage --------------------------------------------------------
        public double OdometerMetres;
        public double TripMetres;

        // --- Driver preferences ---------------------------------------------
        public int DriveModeIndex;
        public bool ManualTransmission;
        public bool AbsEnabled = true;
        public bool TractionControlEnabled = true;
        public bool StabilityControlEnabled = true;

        // --- Tuning (Phase 7 fills these in) --------------------------------
        public float EngineTorqueMultiplier = 1f;
        public float BoostBarOffset;
        public float SpoolSpeedMultiplier = 1f;
        public float GearRatioMultiplier = 1f;
        public float FinalDriveMultiplier = 1f;
        public float ShiftSpeedMultiplier = 1f;
        public float BrakeTorqueMultiplier = 1f;
        public float BrakeBias = 0.5f;
        public float SpringStiffnessMultiplier = 1f;
        public float DamperMultiplier = 1f;
        public float RideHeightOffsetM;
        public float TyreGripMultiplier = 1f;
        public float DownforceMultiplier = 1f;
        public float DragMultiplier = 1f;
        public float MassOffsetKg;
        public float ExhaustAggression = 0.35f;

        // --- Installed upgrades -----------------------------------------------
        /// <summary>Selected level per tuning category, indexed by TuningCategory. 0 is stock.</summary>
        public int[] TuningLevels = new int[0];

        // --- Measured performance ---------------------------------------------
        /// <summary>Best measured times and speeds for this car, -1 when never recorded.</summary>
        public float BestZeroToHundredKmh = -1f;
        public float BestZeroToTwoHundredKmh = -1f;
        public float BestTopSpeedKmh = -1f;
        public float BestHundredToZeroMetres = -1f;

        // --- Fuel -------------------------------------------------------------
        public float FuelLitres = -1f; // -1 means "fill on first spawn"

        // --- Cosmetics --------------------------------------------------------
        public Color PaintColour = new Color(0.10f, 0.11f, 0.13f, 1f);
        public float PaintMetallic = 0.85f;
        public float PaintSmoothness = 0.92f;
    }

    /// <summary>Player-wide state that is not tied to one car.</summary>
    [Serializable]
    public class PlayerSaveData
    {
        public string ActiveVehicleId = "";
        public long Money = 25000;
        public int NextVehicleSerial = 1;
        public Vector3 LastPosition;
        public float LastHeadingDeg;
    }

    /// <summary>Settings that persist independently of any save slot.</summary>
    [Serializable]
    public class SettingsSaveData
    {
        public bool UseImperialUnits;
        public int TrafficDensity = 2;   // 0 off, 1 low, 2 medium, 3 high
        public float MasterVolume = 1f;
        public float EngineVolume = 1f;
        public float ExhaustVolume = 1f;
        public float EnvironmentVolume = 1f;
        public float CameraFieldOfView = 62f;
        public float CameraSensitivity = 1f;
        public bool CameraMotionEffects = true;
        public int AssistPreset = 1;     // matches Data.AssistPreset
    }

    /// <summary>The whole save file.</summary>
    [Serializable]
    public class GameSaveData
    {
        /// <summary>Bumped whenever the layout changes so migrations can be written.</summary>
        public int SaveVersion = 1;

        public PlayerSaveData Player = new PlayerSaveData();
        public SettingsSaveData Settings = new SettingsSaveData();
        public List<VehicleSaveData> Vehicles = new List<VehicleSaveData>();

        public VehicleSaveData FindVehicle(string vehicleId)
        {
            if (string.IsNullOrEmpty(vehicleId)) return null;
            for (int i = 0; i < Vehicles.Count; i++)
                if (Vehicles[i].VehicleId == vehicleId) return Vehicles[i];
            return null;
        }

        public VehicleSaveData GetOrCreateVehicle(string vehicleId, string definitionName, string displayName)
        {
            var existing = FindVehicle(vehicleId);
            if (existing != null) return existing;

            var created = new VehicleSaveData
            {
                VehicleId = vehicleId,
                DefinitionName = definitionName,
                DisplayName = displayName
            };
            Vehicles.Add(created);
            return created;
        }
    }
}
