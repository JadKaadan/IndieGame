using System.Collections.Generic;
using UnityEngine;

namespace IndieGame.Vehicles.Visual
{
    /// <summary>
    /// Generates a coupe body shell at edit time by lofting a set of cross
    /// sections along the car's length.
    ///
    /// This exists so the prototype has a car-shaped car with believable
    /// proportions without shipping or licensing a third-party model. It is a
    /// stand-in for a real asset, not the final art: replace the mesh by
    /// disabling this component and dropping an imported model under Visuals.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    [AddComponentMenu("IndieGame/Vehicle/Procedural Car Body")]
    public class ProceduralCarBody : MonoBehaviour
    {
        public enum Part
        {
            /// <summary>The full closed shell.</summary>
            Body,

            /// <summary>Only the glazed bands: side windows, windscreen, rear window.</summary>
            Glass
        }

        [SerializeField] private Part part = Part.Body;

        [Tooltip("Overall length in metres. Cross sections are scaled to fit.")]
        [SerializeField] private float length = 4.55f;

        [Tooltip("Overall width in metres.")]
        [SerializeField] private float width = 1.86f;

        [Tooltip("Metres the glass sits proud of the body shell, to avoid z-fighting.")]
        [SerializeField] private float glassOffset = 0.010f;

        [Tooltip("Rebuild the mesh whenever a value changes in the inspector.")]
        [SerializeField] private bool rebuildOnValidate = true;

        // Cross sections, rear to front.
        // z, sill half width, roof half width, floor Y, shoulder Y, roof Y  (metres, at reference size)
        private static readonly float[,] Stations =
        {
            { -2.275f, 0.72f, 0.66f, 0.44f, 0.86f, 0.94f }, // rear valance
            { -2.100f, 0.83f, 0.76f, 0.36f, 0.92f, 1.02f }, // rear bumper
            { -1.850f, 0.90f, 0.82f, 0.31f, 0.95f, 1.05f }, // boot lid
            { -1.500f, 0.93f, 0.85f, 0.28f, 0.97f, 1.12f }, // rear haunch
            { -1.150f, 0.92f, 0.78f, 0.27f, 0.98f, 1.28f }, // C pillar base
            { -0.800f, 0.91f, 0.67f, 0.27f, 0.99f, 1.37f }, // rear of roof
            { -0.250f, 0.92f, 0.65f, 0.27f, 1.00f, 1.40f }, // roof peak
            {  0.300f, 0.92f, 0.66f, 0.27f, 1.00f, 1.38f }, // front of roof
            {  0.780f, 0.91f, 0.75f, 0.28f, 0.98f, 1.22f }, // windscreen base
            {  1.150f, 0.90f, 0.84f, 0.29f, 0.94f, 1.00f }, // cowl
            {  1.560f, 0.92f, 0.86f, 0.30f, 0.88f, 0.94f }, // over front axle
            {  1.950f, 0.87f, 0.80f, 0.33f, 0.82f, 0.89f }, // bonnet front
            {  2.180f, 0.78f, 0.72f, 0.38f, 0.74f, 0.82f }, // nose
            {  2.275f, 0.60f, 0.56f, 0.46f, 0.66f, 0.72f }  // front valance
        };

        private const int RingSize = 12;
        private const float ReferenceLength = 4.55f;
        private const float ReferenceWidth = 1.86f;

        // Glazed bands: first station index, last station index, first ring index, last ring index.
        private static readonly int[,] GlassBands =
        {
            { 4, 8, 3, 4 },  // left side window  (shoulder -> roof edge)
            { 4, 8, 7, 8 },  // right side window (roof edge -> shoulder)
            { 7, 8, 4, 7 },  // windscreen        (across the top)
            { 4, 5, 4, 7 }   // rear window
        };

        private MeshFilter _filter;

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

            Mesh mesh = part == Part.Body ? BuildShell() : BuildGlass();
            mesh.name = part == Part.Body ? "CarBody" : "CarGlass";

            // sharedMesh rather than mesh so this works outside play mode without
            // leaking a mesh instance every time the inspector changes.
            _filter.sharedMesh = mesh;
        }

        /// <summary>One closed cross section ring, in local space.</summary>
        private Vector3[] Ring(int station)
        {
            float lengthScale = length / ReferenceLength;
            float widthScale = width / ReferenceWidth;

            float z = Stations[station, 0] * lengthScale;
            float sill = Stations[station, 1] * widthScale;
            float roofHalf = Stations[station, 2] * widthScale;
            float floorY = Stations[station, 3];
            float shoulderY = Stations[station, 4];
            float roofY = Stations[station, 5];

            float lowerY = Mathf.Lerp(floorY, shoulderY, 0.45f);

            return new[]
            {
                new Vector3(-sill * 0.78f, floorY, z),          // 0 floor left
                new Vector3(-sill * 0.99f, lowerY, z),          // 1 rocker left
                new Vector3(-sill, shoulderY * 0.82f, z),       // 2 door left
                new Vector3(-sill * 0.97f, shoulderY, z),       // 3 shoulder left
                new Vector3(-roofHalf, roofY - 0.03f, z),       // 4 roof edge left
                new Vector3(-roofHalf * 0.52f, roofY, z),       // 5 roof left
                new Vector3( roofHalf * 0.52f, roofY, z),       // 6 roof right
                new Vector3( roofHalf, roofY - 0.03f, z),       // 7 roof edge right
                new Vector3( sill * 0.97f, shoulderY, z),       // 8 shoulder right
                new Vector3( sill, shoulderY * 0.82f, z),       // 9 door right
                new Vector3( sill * 0.99f, lowerY, z),          // 10 rocker right
                new Vector3( sill * 0.78f, floorY, z)           // 11 floor right
            };
        }

        private Mesh BuildShell()
        {
            int stationCount = Stations.GetLength(0);
            var vertices = new List<Vector3>(stationCount * RingSize + 2);
            var triangles = new List<int>();

            for (int s = 0; s < stationCount; s++)
                vertices.AddRange(Ring(s));

            // Loft the sides.
            for (int s = 0; s < stationCount - 1; s++)
            {
                int a = s * RingSize;
                int b = (s + 1) * RingSize;
                for (int i = 0; i < RingSize; i++)
                {
                    int j = (i + 1) % RingSize;
                    AddQuad(triangles, a + i, a + j, b + j, b + i);
                }
            }

            // Underside: a flat floor closing the ring bottoms (indices 11 -> 0).
            for (int s = 0; s < stationCount - 1; s++)
            {
                int a = s * RingSize;
                int b = (s + 1) * RingSize;
                AddQuad(triangles, a + 11, a + 0, b + 0, b + 11);
            }

            // Caps.
            AddCap(vertices, triangles, 0, true);
            AddCap(vertices, triangles, stationCount - 1, false);

            return Finalise(vertices, triangles);
        }

        private Mesh BuildGlass()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            var rings = new Vector3[Stations.GetLength(0)][];
            for (int s = 0; s < rings.Length; s++) rings[s] = Ring(s);

            for (int band = 0; band < GlassBands.GetLength(0); band++)
            {
                int firstStation = GlassBands[band, 0];
                int lastStation = GlassBands[band, 1];
                int firstIndex = GlassBands[band, 2];
                int lastIndex = GlassBands[band, 3];

                for (int s = firstStation; s < lastStation; s++)
                {
                    for (int i = firstIndex; i < lastIndex; i++)
                    {
                        int baseIndex = vertices.Count;
                        vertices.Add(Offset(rings[s][i]));
                        vertices.Add(Offset(rings[s][i + 1]));
                        vertices.Add(Offset(rings[s + 1][i + 1]));
                        vertices.Add(Offset(rings[s + 1][i]));
                        AddQuad(triangles, baseIndex, baseIndex + 1, baseIndex + 2, baseIndex + 3);
                    }
                }
            }

            return Finalise(vertices, triangles);
        }

        /// <summary>Pushes a body vertex outward from the car's spine so glass sits on the shell.</summary>
        private Vector3 Offset(Vector3 point)
        {
            Vector3 outward = new Vector3(point.x, point.y - 0.75f, 0f);
            if (outward.sqrMagnitude < 0.0001f) return point;
            return point + outward.normalized * glassOffset;
        }

        private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(a); triangles.Add(c); triangles.Add(d);
        }

        private void AddCap(List<Vector3> vertices, List<int> triangles, int station, bool facingBack)
        {
            int ringStart = station * RingSize;

            Vector3 centre = Vector3.zero;
            for (int i = 0; i < RingSize; i++) centre += vertices[ringStart + i];
            centre /= RingSize;

            int centreIndex = vertices.Count;
            vertices.Add(centre);

            for (int i = 0; i < RingSize; i++)
            {
                int j = (i + 1) % RingSize;
                if (facingBack)
                {
                    triangles.Add(centreIndex); triangles.Add(ringStart + j); triangles.Add(ringStart + i);
                }
                else
                {
                    triangles.Add(centreIndex); triangles.Add(ringStart + i); triangles.Add(ringStart + j);
                }
            }
        }

        private static Mesh Finalise(List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh();
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            // Simple planar UVs from the top so a tiling paint or dirt texture has
            // something to work with later.
            var uvs = new Vector2[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
                uvs[i] = new Vector2(vertices[i].x * 0.25f + 0.5f, vertices[i].z * 0.12f + 0.5f);
            mesh.uv = uvs;

            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
