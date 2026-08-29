using UnityEngine;

namespace IndieGame.Vehicles
{
    /// <summary>
    /// Holds the camera mount points a vehicle prefab provides. Keeping them on a
    /// small component rather than on the controller means the camera rig can find
    /// them on any car, including ones authored by hand or imported later, without
    /// the controller knowing anything about cameras.
    /// </summary>
    [AddComponentMenu("IndieGame/Vehicle/Camera Anchors")]
    public class PrototypeCameraAnchors : MonoBehaviour
    {
        public Transform Cockpit;
        public Transform Hood;
        public Transform Bumper;
    }
}
