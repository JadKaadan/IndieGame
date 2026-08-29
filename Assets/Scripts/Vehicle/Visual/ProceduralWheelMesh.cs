using System.Collections.Generic;
using UnityEngine;

namespace IndieGame.Vehicles.Visual
{
    /// <summary>
    /// Generates a tyre carcass or a spoked rim at edit time. Same purpose as
    /// <see cref="ProceduralCarBody"/>: a believable wheel without a licensed
    /// asset, sized from the vehicle's real rolling radius and tread width so the
    /// visual matches the physics.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    [AddComponentMenu("IndieGame/Vehicle/Procedural Wheel Mesh")]
    public class ProceduralWheelMesh : MonoBehaviour
    {
        public enum Part { Tyre, Rim }

        [SerializeField] private Part part = Part.Tyre;

        [Tooltip("Rolling radius in metres. Must match the vehicle definition's wheel radius.")]
        [SerializeField] private float radius = 0.34f;

        [Tooltip("Tread width in metres.")]
        [SerializeField] private float treadWidth = 0.245f;

        [Tooltip("Rim diameter as a fraction of the tyre's outer diameter.")]
        [SerializeField, Range(0.4f, 0.9f)] private float rimFraction = 0.68f;

        [SerializeField, Range(8, 64)] private int radialSegments = 28;
        [SerializeField, Range(3, 10)] private int spokeCount = 5;
        [SerializeField] private bool rebuildOnValidate = true;

        private MeshFilter _filter;

        public float Radius
        {
            get => radius;
            set { radius = value; Rebuild(); }
        }

        private void Awake() => Rebuild();

        private void OnValidate()
        {
            if (rebuildOnValidate) Rebuild();
        }

        [ContextMenu("Rebuild Mesh")]
        public void Rebuild()
        {
            if (_filter == null) _filter = GetComponent<MeshFilter>();
            if (_filter == null) return;

            Mesh mesh = part == Part.Tyre ? BuildTyre() : BuildRim();
            mesh.name = part == Part.Tyre ? "Tyre" : "Rim";
            _filter.sharedMesh = mesh;
        }

        /// <summary>
        /// A closed surface of revolution about the local X axis. The profile runs
        /// from the inner bead, out over the rounded shoulder, across the tread and
        /// back, so the tyre reads correctly from any angle.
        /// </summary>
        private Mesh BuildTyre()
        {
            float halfWidth = treadWidth * 0.5f;
            float rimRadius = radius * rimFraction;
            float shoulder = radius * 0.965f;

            // (x along the axle, radius from the axle)
            var profile = new[]
            {
                new Vector2(-halfWidth,          rimRadius),
                new Vector2(-halfWidth,          radius * 0.86f),
                new Vector2(-halfWidth * 0.94f,  shoulder),
                new Vector2(-halfWidth * 0.72f,  radius),
                new Vector2( halfWidth * 0.72f,  radius),
                new Vector2( halfWidth * 0.94f,  shoulder),
                new Vector2( halfWidth,          radius * 0.86f),
                new Vector2( halfWidth,          rimRadius)
            };

            return Revolve(profile, false);
        }

        /// <summary>Rim face: outer barrel, an annular lip, spokes and a hub.</summary>
        private Mesh BuildRim()
        {
            float halfWidth = treadWidth * 0.5f;
            float rimRadius = radius * rimFraction;
            float faceX = halfWidth * 0.30f;
            float hubRadius = rimRadius * 0.26f;
            float lipInner = rimRadius * 0.88f;

            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            // Barrel: a short cylinder joining the two tyre beads so no gap shows.
            var barrel = new[]
            {
                new Vector2(-halfWidth, rimRadius),
                new Vector2( faceX,     rimRadius)
            };
            AppendRevolve(vertices, triangles, barrel, false);

            // Outer lip: a flat annulus at the face.
            AppendAnnulus(vertices, triangles, faceX, lipInner, rimRadius, true);

            // Hub cap.
            AppendDisc(vertices, triangles, faceX + 0.012f, hubRadius, true);

            // Spokes: flat wedges from hub to lip, leaving gaps between them.
            float step = Mathf.PI * 2f / Mathf.Max(1, spokeCount);
            float halfSpoke = step * 0.30f;
            for (int s = 0; s < spokeCount; s++)
            {
                float centre = s * step;
                float a0 = centre - halfSpoke;
                float a1 = centre + halfSpoke;

                int b = vertices.Count;
                vertices.Add(new Vector3(faceX + 0.006f, Mathf.Sin(a0) * hubRadius, Mathf.Cos(a0) * hubRadius));
                vertices.Add(new Vector3(faceX + 0.006f, Mathf.Sin(a1) * hubRadius, Mathf.Cos(a1) * hubRadius));
                vertices.Add(new Vector3(faceX + 0.006f, Mathf.Sin(a1 * 0.999f) * lipInner, Mathf.Cos(a1 * 0.999f) * lipInner));
                vertices.Add(new Vector3(faceX + 0.006f, Mathf.Sin(a0 * 0.999f) * lipInner, Mathf.Cos(a0 * 0.999f) * lipInner));

                triangles.Add(b); triangles.Add(b + 1); triangles.Add(b + 2);
                triangles.Add(b); triangles.Add(b + 2); triangles.Add(b + 3);
            }

            // Brake disc behind the spokes, so the gaps show something dark.
            AppendDisc(vertices, triangles, -halfWidth * 0.10f, rimRadius * 0.80f, true);

            return Finalise(vertices, triangles);
        }

        private Mesh Revolve(Vector2[] profile, bool closeProfile)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            AppendRevolve(vertices, triangles, profile, closeProfile);
            return Finalise(vertices, triangles);
        }

        private void AppendRevolve(List<Vector3> vertices, List<int> triangles, Vector2[] profile, bool closeProfile)
        {
            int segments = Mathf.Max(8, radialSegments);
            int rings = profile.Length;
            int baseIndex = vertices.Count;

            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s <= segments; s++)
                {
                    float angle = s / (float)segments * Mathf.PI * 2f;
                    vertices.Add(new Vector3(profile[r].x,
                                             Mathf.Sin(angle) * profile[r].y,
                                             Mathf.Cos(angle) * profile[r].y));
                }
            }

            int stride = segments + 1;
            int lastRing = closeProfile ? rings : rings - 1;
            for (int r = 0; r < lastRing; r++)
            {
                int r0 = baseIndex + r * stride;
                int r1 = baseIndex + ((r + 1) % rings) * stride;
                for (int s = 0; s < segments; s++)
                {
                    triangles.Add(r0 + s); triangles.Add(r1 + s); triangles.Add(r1 + s + 1);
                    triangles.Add(r0 + s); triangles.Add(r1 + s + 1); triangles.Add(r0 + s + 1);
                }
            }
        }

        private void AppendAnnulus(List<Vector3> vertices, List<int> triangles, float x,
                                   float innerRadius, float outerRadius, bool faceOutward)
        {
            int segments = Mathf.Max(8, radialSegments);
            int baseIndex = vertices.Count;

            for (int s = 0; s <= segments; s++)
            {
                float angle = s / (float)segments * Mathf.PI * 2f;
                float sin = Mathf.Sin(angle), cos = Mathf.Cos(angle);
                vertices.Add(new Vector3(x, sin * innerRadius, cos * innerRadius));
                vertices.Add(new Vector3(x, sin * outerRadius, cos * outerRadius));
            }

            for (int s = 0; s < segments; s++)
            {
                int a = baseIndex + s * 2;
                if (faceOutward)
                {
                    triangles.Add(a); triangles.Add(a + 1); triangles.Add(a + 3);
                    triangles.Add(a); triangles.Add(a + 3); triangles.Add(a + 2);
                }
                else
                {
                    triangles.Add(a); triangles.Add(a + 3); triangles.Add(a + 1);
                    triangles.Add(a); triangles.Add(a + 2); triangles.Add(a + 3);
                }
            }
        }

        private void AppendDisc(List<Vector3> vertices, List<int> triangles, float x, float discRadius, bool faceOutward)
        {
            int segments = Mathf.Max(8, radialSegments);
            int centreIndex = vertices.Count;
            vertices.Add(new Vector3(x, 0f, 0f));

            for (int s = 0; s <= segments; s++)
            {
                float angle = s / (float)segments * Mathf.PI * 2f;
                vertices.Add(new Vector3(x, Mathf.Sin(angle) * discRadius, Mathf.Cos(angle) * discRadius));
            }

            for (int s = 0; s < segments; s++)
            {
                int a = centreIndex + 1 + s;
                if (faceOutward)
                {
                    triangles.Add(centreIndex); triangles.Add(a); triangles.Add(a + 1);
                }
                else
                {
                    triangles.Add(centreIndex); triangles.Add(a + 1); triangles.Add(a);
                }
            }
        }

        private static Mesh Finalise(List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            var uvs = new Vector2[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 v = vertices[i];
                uvs[i] = new Vector2(Mathf.Atan2(v.y, v.z) / (Mathf.PI * 2f) + 0.5f, v.x + 0.5f);
            }
            mesh.uv = uvs;

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
