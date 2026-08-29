using System.Collections.Generic;
using IndieGame.Core;
using IndieGame.Persistence;
using IndieGame.UI;
using IndieGame.VehicleInput;
using UnityEngine;

namespace IndieGame.Vehicles
{
    /// <summary>
    /// Moves the driver between the cars parked in the scene.
    ///
    /// Only the active vehicle receives input; the others keep simulating their own
    /// physics but sit still. The camera, HUD and garage are retargeted together, so
    /// every readout follows the car you are actually in - which is also what makes
    /// the difference between a rear-drive coupe and a front-drive hatchback
    /// immediately obvious.
    /// </summary>
    [AddComponentMenu("IndieGame/Vehicle/Vehicle Switcher")]
    [DefaultExecutionOrder(-40)]
    public class VehicleSwitcher : MonoBehaviour
    {
        [SerializeField] private List<VehicleController> vehicles = new List<VehicleController>();
        [SerializeField] private Cameras.VehicleCameraRig cameraRig;
        [SerializeField] private VehicleHud hud;
        [SerializeField] private VehicleDebugHud debugHud;
        [SerializeField] private GarageMenu garage;
        [SerializeField] private KeyCode switchKey = KeyCode.Tab;
        [SerializeField] private int activeIndex;

        public VehicleController Active =>
            vehicles.Count > 0 ? vehicles[Mathf.Clamp(activeIndex, 0, vehicles.Count - 1)] : null;

        private void Start()
        {
            if (vehicles.Count == 0)
                vehicles.AddRange(FindObjectsByType<VehicleController>(FindObjectsSortMode.None));

            // Restore whichever car the player was last driving.
            string saved = SaveSystem.Current.Player.ActiveVehicleId;
            if (!string.IsNullOrEmpty(saved))
            {
                int found = vehicles.FindIndex(v => v != null && v.VehicleId == saved);
                if (found >= 0) activeIndex = found;
            }

            Select(activeIndex);
        }

        private void Update()
        {
            if (vehicles.Count < 2) return;
            if (garage != null && garage.IsOpen) return;
            if (HotKey.Pressed(switchKey)) Select(activeIndex + 1);
        }

        public void Select(int index)
        {
            if (vehicles.Count == 0) return;
            activeIndex = ((index % vehicles.Count) + vehicles.Count) % vehicles.Count;

            for (int i = 0; i < vehicles.Count; i++)
            {
                VehicleController vehicle = vehicles[i];
                if (vehicle == null) continue;

                bool isActive = i == activeIndex;
                var input = vehicle.GetComponentInChildren<PlayerVehicleInputSource>(true);
                if (input != null) input.IsEnabled = isActive;

                // A parked car should not roll away or idle audibly, but it must stay
                // simulated so it settles on its springs and is ready to drive.
                if (isActive) vehicle.ReleaseParkingBrake();
                else vehicle.ApplyParkingBrake();
            }

            VehicleController active = Active;
            if (active == null) return;

            if (cameraRig != null) cameraRig.Target = active;
            if (hud != null) hud.SetTarget(active);
            if (debugHud != null) debugHud.SetTarget(active);
            if (garage != null) garage.SetTarget(active);

            var anchors = active.GetComponent<PrototypeCameraAnchors>();
            if (cameraRig != null && anchors != null) cameraRig.SetAnchors(anchors);

            SaveSystem.Current.Player.ActiveVehicleId = active.VehicleId;
        }

        public void Register(VehicleController vehicle)
        {
            if (vehicle != null && !vehicles.Contains(vehicle)) vehicles.Add(vehicle);
        }
    }
}
