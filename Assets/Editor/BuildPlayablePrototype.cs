using System.IO;
using IndieGame.Cameras;
using IndieGame.Core;
using IndieGame.UI;
using IndieGame.Vehicles;
using IndieGame.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IndieGame.EditorTools
{
    /// <summary>
    /// Rebuilds the playable prototype scene from code.
    ///
    /// The scene and the vehicle prefab are committed to the repository, so this is
    /// not normally needed. It exists as a repair and regeneration path: if the
    /// scene is deleted, damaged, or you want the track rebuilt with different
    /// dimensions, one menu click restores a complete, playable scene.
    /// </summary>
    public static class BuildPlayablePrototype
    {
        private const string ScenePath = "Assets/Scenes/VehicleTest.unity";
        private const string PrefabPath = "Assets/Prefabs/Vehicles/PlayerVehicle.prefab";
        private const string MaterialFolder = "Assets/Art/Materials";

        // Circuit geometry, matching the committed scene.
        private const float StraightZ0 = -260f;
        private const float StraightZ1 = 940f;
        private const float RoadWidth = 16f;
        private const float ReturnX = -140f;
        private const float TurnRadius = 70f;
        private const float TurnCentreX = -70f;
        private const float RoadTop = 0.02f;
        private const float SlabThickness = 0.4f;
        private const int TurnSegments = 26;

        private static int GroundLayer => Mathf.Max(0, LayerMask.NameToLayer("Ground"));
        private static int EnvironmentLayer => Mathf.Max(0, LayerMask.NameToLayer("Environment"));

        [MenuItem("Tools/Indie Driving Game/Build Playable Prototype", false, 0)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ProjectBootstrapTool.ConfigureAll();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Vehicle prefab missing",
                    "Could not find " + PrefabPath + ".\n\nThe prefab is part of the repository; " +
                    "restore it before rebuilding the scene.", "OK");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildManager();
            BuildLighting();
            BuildEnvironment();
            VehicleController vehicle = BuildVehicle(prefab);
            BuildCamera(vehicle);
            BuildUi(vehicle);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            PrototypeAutoSetup.RegisterScene();
            PipelineMaterialFixer.Convert(false);

            Debug.Log("[IndieGame] Rebuilt " + ScenePath + ". Press Play to drive.\n" +
                      "W/S throttle-brake, A/D steer, Space handbrake, R/Q shift, " +
                      "M auto-manual, B drive mode, V camera, E ignition, L lights, F3 telemetry.");
        }

        [MenuItem("Tools/Indie Driving Game/Save Scene Vehicle As Prefab", false, 41)]
        public static void SaveSceneVehicleAsPrefab()
        {
            var vehicle = Object.FindAnyObjectByType<VehicleController>();
            if (vehicle == null)
            {
                EditorUtility.DisplayDialog("No vehicle", "No VehicleController in the open scene.", "OK");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAsset(vehicle.gameObject, PrefabPath, out bool success);
            Debug.Log(success
                ? "[IndieGame] Saved " + vehicle.name + " to " + PrefabPath
                : "[IndieGame] Failed to save the vehicle prefab.");
        }

        // ==================================================================
        private static void BuildManager()
        {
            new GameObject("GameManager").AddComponent<GameManager>();
        }

        private static void BuildLighting()
        {
            var lighting = new GameObject("Lighting");
            var sunObject = new GameObject("DirectionalLight");
            sunObject.transform.SetParent(lighting.transform);
            sunObject.transform.SetPositionAndRotation(new Vector3(0f, 60f, 0f),
                                                       Quaternion.Euler(46f, 152f, 0f));

            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.957f, 0.878f);
            sun.intensity = 1.45f;
            sun.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.47f, 0.55f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.36f, 0.37f, 0.39f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.16f, 0.15f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.0011f;
            RenderSettings.fogColor = new Color(0.62f, 0.68f, 0.75f);
        }

        private static void BuildEnvironment()
        {
            var root = new GameObject("Environment").transform;
            float midZ = (StraightZ0 + StraightZ1) * 0.5f;
            float length = StraightZ1 - StraightZ0;
            float slabY = RoadTop - SlabThickness * 0.5f;

            var ground = Box(root, "Ground", new Vector3(TurnCentreX, -0.75f, 340f),
                             new Vector3(940f, 1.5f, 1860f), Quaternion.identity, "Grass", GroundLayer);
            ground.AddComponent<SurfaceDescriptor>();

            Box(root, "MainStraight", new Vector3(0f, slabY, midZ),
                new Vector3(RoadWidth, SlabThickness, length), Quaternion.identity, "Asphalt", GroundLayer);
            Box(root, "ReturnStraight", new Vector3(ReturnX, slabY, midZ),
                new Vector3(RoadWidth, SlabThickness, length), Quaternion.identity, "Asphalt", GroundLayer);

            BuildTurn(root, "North", StraightZ1, 0f, Mathf.PI);
            BuildTurn(root, "South", StraightZ0, Mathf.PI, Mathf.PI * 2f);

            // Markings
            foreach (float cx in new[] { 0f, ReturnX })
            {
                foreach (int side in new[] { -1, 1 })
                {
                    Box(root, "EdgeLine",
                        new Vector3(cx + side * (RoadWidth * 0.5f - 0.45f), RoadTop + 0.012f, midZ),
                        new Vector3(0.2f, 0.02f, length), Quaternion.identity, "RoadLineWhite",
                        EnvironmentLayer, collider: false);
                }
            }
            int dashes = Mathf.FloorToInt(length / 30f);
            for (int i = 0; i < dashes; i++)
            {
                float z = StraightZ0 + 15f + i * 30f;
                foreach (float cx in new[] { 0f, ReturnX })
                    Box(root, "Dash", new Vector3(cx, RoadTop + 0.012f, z),
                        new Vector3(0.18f, 0.02f, 3.2f), Quaternion.identity, "RoadLineWhite",
                        EnvironmentLayer, collider: false);
            }

            // Straight barriers
            foreach (var pair in new[] { (0f, 1f), (ReturnX, -1f) })
            {
                for (int i = 0; i < 8; i++)
                {
                    float sectionLength = length / 8f;
                    float z = StraightZ0 + sectionLength * (i + 0.5f);
                    Box(root, "Barrier",
                        new Vector3(pair.Item1 + pair.Item2 * (RoadWidth * 0.5f + 5f), 0.55f, z),
                        new Vector3(0.35f, 1.1f, sectionLength * 0.98f), Quaternion.identity,
                        "Barrier", EnvironmentLayer);
                }
            }

            // Infield
            Box(root, "SkidPad", new Vector3(TurnCentreX, slabY, 300f),
                new Vector3(120f, SlabThickness, 120f), Quaternion.identity, "Asphalt", GroundLayer);
            Box(root, "ParkingArea", new Vector3(TurnCentreX, slabY, -80f),
                new Vector3(74f, SlabThickness, 46f), Quaternion.identity, "AsphaltWorn", GroundLayer);
            Box(root, "Garage", new Vector3(TurnCentreX, 4f, -140f),
                new Vector3(34f, 8f, 22f), Quaternion.identity, "Concrete", EnvironmentLayer);

            Box(root, "BumpApproach", new Vector3(TurnCentreX, slabY, 540f),
                new Vector3(14f, SlabThickness, 60f), Quaternion.identity, "AsphaltWorn", GroundLayer);
            for (int i = 0; i < 7; i++)
                Box(root, "Bump", new Vector3(TurnCentreX, 0f, 520f + i * 7f),
                    new Vector3(14f, 0.22f, 1.7f), Quaternion.identity, "ConcreteDark", GroundLayer);
            Box(root, "Crest", new Vector3(TurnCentreX, 0.34f, 600f),
                new Vector3(14f, 0.5f, 26f), Quaternion.Euler(-5f, 0f, 0f), "AsphaltWorn", GroundLayer);

            // A few buildings so the world has scale.
            for (int i = 0; i < 10; i++)
            {
                float r1 = Frac(Mathf.Sin(i * 12.9898f) * 43758.5453f);
                float r2 = Frac(Mathf.Sin(i * 78.233f) * 43758.5453f);
                float height = 12f + r1 * 30f;
                float x = i % 2 == 0 ? 50f + r1 * 110f : ReturnX - 50f - r2 * 110f;
                Box(root, "Building_" + i, new Vector3(x, height * 0.5f, -160f + r2 * 1040f),
                    new Vector3(20f + r1 * 22f, height, 20f + r2 * 20f), Quaternion.identity,
                    i % 2 == 0 ? "BuildingA" : "BuildingC", EnvironmentLayer);
            }
        }

        private static void BuildTurn(Transform root, string name, float centreZ, float from, float to)
        {
            float step = (to - from) / TurnSegments;
            float segmentLength = TurnRadius * Mathf.Abs(step) * 1.08f;
            float slabY = RoadTop - SlabThickness * 0.5f;

            for (int i = 0; i < TurnSegments; i++)
            {
                float theta = from + step * (i + 0.5f);
                var rotation = Quaternion.Euler(0f, -theta * Mathf.Rad2Deg, 0f);

                Box(root, name + "_Road", ArcPoint(theta, TurnRadius, centreZ, slabY),
                    new Vector3(RoadWidth, SlabThickness, segmentLength), rotation, "Asphalt", GroundLayer);

                Box(root, name + "_Kerb", ArcPoint(theta, TurnRadius + RoadWidth * 0.5f + 0.7f, centreZ, 0.05f),
                    new Vector3(1.4f, 0.1f, segmentLength), rotation,
                    i % 2 == 0 ? "Kerb" : "KerbRed", GroundLayer);

                Box(root, name + "_Barrier", ArcPoint(theta, TurnRadius + RoadWidth * 0.5f + 6f, centreZ, 0.55f),
                    new Vector3(0.35f, 1.1f, segmentLength), rotation, "Barrier", EnvironmentLayer);
            }
        }

        private static Vector3 ArcPoint(float theta, float radius, float centreZ, float y)
        {
            return new Vector3(TurnCentreX + radius * Mathf.Cos(theta), y, centreZ + radius * Mathf.Sin(theta));
        }

        private static float Frac(float value) => value - Mathf.Floor(value);

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale,
                                      Quaternion rotation, string material, int layer, bool collider = true)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.SetPositionAndRotation(position, rotation);
            box.transform.localScale = scale;
            box.layer = layer;
            box.isStatic = true;

            if (!collider)
            {
                Object.DestroyImmediate(box.GetComponent<Collider>());
            }

            var renderer = box.GetComponent<Renderer>();
            var asset = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{material}.mat");
            if (asset != null) renderer.sharedMaterial = asset;

            return box;
        }

        private static VehicleController BuildVehicle(GameObject prefab)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetPositionAndRotation(new Vector3(4.2f, RoadTop, -200f), Quaternion.identity);
            return instance.GetComponent<VehicleController>();
        }

        private static void BuildCamera(VehicleController vehicle)
        {
            var cameras = new GameObject("Cameras").transform;
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(cameras, false);
            cameraObject.transform.SetPositionAndRotation(new Vector3(4.2f, 2.4f, -207f),
                                                          Quaternion.Euler(6f, 0f, 0f));

            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 2500f;
            camera.fieldOfView = 62f;
            cameraObject.AddComponent<AudioListener>();

            var rig = cameraObject.AddComponent<VehicleCameraRig>();
            var anchors = vehicle != null ? vehicle.GetComponent<PrototypeCameraAnchors>() : null;

            var serialized = new SerializedObject(rig);
            serialized.FindProperty("target").objectReferenceValue = vehicle;
            if (anchors != null)
            {
                serialized.FindProperty("cockpitAnchor").objectReferenceValue = anchors.Cockpit;
                serialized.FindProperty("hoodAnchor").objectReferenceValue = anchors.Hood;
                serialized.FindProperty("bumperAnchor").objectReferenceValue = anchors.Bumper;
            }
            if (vehicle != null)
            {
                var audio = vehicle.GetComponentInChildren<Vehicles.Audio.VehicleEngineAudio>(true);
                if (audio != null) serialized.FindProperty("engineAudio").objectReferenceValue = audio;
            }
            serialized.FindProperty("collisionMask").intValue =
                (1 << GroundLayer) | (1 << EnvironmentLayer);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildUi(VehicleController vehicle)
        {
            var ui = new GameObject("UI").transform;

            var hudObject = new GameObject("HUD");
            hudObject.transform.SetParent(ui, false);
            var hud = hudObject.AddComponent<VehicleHud>();
            hud.SetTarget(vehicle);

            var debugObject = new GameObject("TelemetryOverlay");
            debugObject.transform.SetParent(ui, false);
            var debug = debugObject.AddComponent<VehicleDebugHud>();
            debug.SetTarget(vehicle);

            var serialized = new SerializedObject(debug);
            serialized.FindProperty("visibleOnStart").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
