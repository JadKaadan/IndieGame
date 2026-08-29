using IndieGame.Vehicles.Data;
using UnityEngine;

namespace IndieGame.World
{
    /// <summary>
    /// Attach to any collider that represents a drivable surface. The tyre model
    /// reads the friction scale and the audio system (Phase 5) reads the surface
    /// type to pick roll/slip samples. Anything without this component is treated
    /// as dry asphalt.
    /// </summary>
    [AddComponentMenu("IndieGame/World/Surface Descriptor")]
    public class SurfaceDescriptor : MonoBehaviour
    {
        [SerializeField] private SurfaceType surfaceType = SurfaceType.Asphalt;

        [Tooltip("Multiplies the tyre's peak friction coefficient. " +
                 "1.0 dry asphalt, ~0.75 wet, ~0.55 gravel, ~0.45 grass.")]
        [SerializeField, Range(0.1f, 1.2f)] private float frictionScale = 1f;

        [Tooltip("Extra rolling resistance on loose surfaces, added to the tyre's coefficient.")]
        [SerializeField, Range(0f, 0.15f)] private float extraRollingResistance = 0f;

        public SurfaceType SurfaceType => surfaceType;
        public float FrictionScale => frictionScale;
        public float ExtraRollingResistance => extraRollingResistance;

        /// <summary>Default surface used when a collider carries no descriptor.</summary>
        public const float DefaultFrictionScale = 1f;
    }
}
