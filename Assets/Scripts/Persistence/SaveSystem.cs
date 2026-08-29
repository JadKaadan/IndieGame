using System;
using System.IO;
using UnityEngine;

namespace IndieGame.Persistence
{
    /// <summary>
    /// JSON save file under Application.persistentDataPath.
    ///
    /// Deliberately not PlayerPrefs: mileage, tuning and a list of owned cars are
    /// structured data, and PlayerPrefs is a flat string table with no atomicity.
    /// Writes go to a temporary file first and are then swapped in, so a crash
    /// mid-write leaves the previous save intact rather than a truncated one.
    ///
    /// JSON is chosen for development because it is inspectable while tuning.
    /// A checksum or binary format can be layered on later without changing any
    /// caller, since everything goes through Load/Save here.
    /// </summary>
    public static class SaveSystem
    {
        public const string SaveFileName = "indiegame_save.json";

        private static GameSaveData _cached;

        public static string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        /// <summary>The in-memory save. Loaded from disk on first access.</summary>
        public static GameSaveData Current
        {
            get
            {
                if (_cached == null) Load();
                return _cached;
            }
        }

        public static GameSaveData Load()
        {
            try
            {
                if (File.Exists(SaveFilePath))
                {
                    string json = File.ReadAllText(SaveFilePath);
                    var data = JsonUtility.FromJson<GameSaveData>(json);
                    if (data != null)
                    {
                        Migrate(data);
                        _cached = data;
                        return _cached;
                    }
                    Debug.LogWarning($"[SaveSystem] Save file at {SaveFilePath} could not be parsed. Starting fresh.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SaveSystem] Failed to load save: {exception.Message}");
            }

            _cached = new GameSaveData();
            return _cached;
        }

        public static void Save()
        {
            if (_cached == null) return;

            string temporaryPath = SaveFilePath + ".tmp";
            try
            {
                string json = JsonUtility.ToJson(_cached, true);
                File.WriteAllText(temporaryPath, json);

                // Swap the new file in. File.Replace preserves the old copy only long
                // enough to be safe against a crash between the write and the rename.
                if (File.Exists(SaveFilePath))
                    File.Replace(temporaryPath, SaveFilePath, null);
                else
                    File.Move(temporaryPath, SaveFilePath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SaveSystem] Failed to write save: {exception.Message}");
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
                catch { /* nothing further we can do */ }
            }
        }

        /// <summary>Generates the next unique vehicle id, e.g. CAR_000142.</summary>
        public static string GenerateVehicleId()
        {
            var player = Current.Player;
            string id = $"CAR_{player.NextVehicleSerial:D6}";
            player.NextVehicleSerial++;
            return id;
        }

        /// <summary>Deletes the save file. Used by the settings menu and by tests.</summary>
        public static void DeleteSave()
        {
            try
            {
                if (File.Exists(SaveFilePath)) File.Delete(SaveFilePath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SaveSystem] Failed to delete save: {exception.Message}");
            }
            _cached = new GameSaveData();
        }

        /// <summary>Applies forward migrations for older save layouts.</summary>
        private static void Migrate(GameSaveData data)
        {
            if (data.Player == null) data.Player = new PlayerSaveData();
            if (data.Settings == null) data.Settings = new SettingsSaveData();
            if (data.Vehicles == null) data.Vehicles = new System.Collections.Generic.List<VehicleSaveData>();

            // Future: if (data.SaveVersion < 2) { ... }
            data.SaveVersion = 1;
        }
    }
}
