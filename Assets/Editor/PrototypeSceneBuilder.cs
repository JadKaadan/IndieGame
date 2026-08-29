using System.IO;
using IndieGame.Cameras;
using IndieGame.UI;
using IndieGame.VehicleInput;
using IndieGame.Vehicles;
using IndieGame.Vehicles.Data;
using IndieGame.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IndieGame.EditorTools
{
    /// <summary>
    /// Builds the Phase 1 test scene and its vehicle entirely from code.
    ///
    /// This exists so the project is playable the moment it is opened, with no
    /// hand-wiring of prefabs or scene files. It also documents, executably, how a
    /// vehicle rig has to be assembled: where the suspension anchors go relative
    /// to the wheel centres, what the ride height works out to, which layer the
    /// car belongs on. When a real car model replaces the primitives, the same
    /// relationships apply.
    ///
    /// The primitive shapes here are a physics test mule, not an art direction.
    /// They exist to make the tyre and suspension behaviour measurable, and are
    /// replaced by real geometry in Phase 8.
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Prototype_TestTrack.unity";
        private const string DefinitionPath = "Assets/Data/Vehicles/MeridianGTS.asset";
        private const string MaterialFolder = "Assets/Art/Materials";

        // ==================================================================
        [MenuItem("Tools/IndieGame/Build Prototype Test Scene", false, 20)]
        public static void BuildScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ProjectBootstrapTool.ConfigureAll();

            VehicleDefinition definition = CreateOrLoadDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            BuildTestTrack();
            GameObject car = BuildVehicle(definition);
            BuildCamera(car.GetComponent<VehicleController>());
            BuildHud(car.GetComponent<VehicleController>());

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log(
                $"[IndieGame] Prototype scene built at {ScenePath}.\n" +
                "Press Play. W/S throttle and brake, A/D steer, Space handbrake, " +
                "E/Q shift up and down, T toggles manual, M cycles drive mode, C changes camera, " +
                "I stops and starts the engine, F3 toggles the telemetry overlay.");
        }

        [MenuItem("Tools/IndieGame/Create Prototype Vehicle Definition", false, 21)]
        public static void CreateDefinitionMenu()
        {
            VehicleDefinition definition = CreateOrLoadDefinition();
            Selection.activeObject = definition;
            EditorGUIUtility.PingObject(definition);
        }

        // ==================================================================
        // Vehicle definition
        // ==================================================================
        /// <summary>
        /// The prototype car. Entirely fictional: every figure below was chosen to
        /// be internally consistent for a mid-size rear-drive turbo coupe, not
        /// copied from any real vehicle. When a real car is added later, its
        /// numbers come from manufacturer data and go in a separate asset.
        /// </summary>
        private static VehicleDefinition CreateOrLoadDefinition()
        {
            var existing = AssetDatabase.LoadAssetAtPath<VehicleDefinition>(DefinitionPath);
            if (existing != null) return existing;

            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Vehicles");

            var definition = ScriptableObject.CreateInstance<VehicleDefinition>();

            definition.Identity.DisplayName = "Meridian GT-S";
            definition.Identity.Manufacturer = "Meridian (fictional)";
            definition.Identity.ModelYear = 2024;
            definition.Identity.VehicleClass = "Sports Coupe";
            definition.Identity.SpecificationSource =
                "FICTIONAL VEHICLE. No specification here is taken from a real car. Figures were chosen " +
                "to be mutually consistent for a 1520 kg rear-drive turbocharged coupe, and verified by " +
                "simulation (see Docs/VEHICLE_VALIDATION.md): 500 Nm at 3200 rpm, 350 hp at 6140 rpm, " +
                "0-100 km/h in about 5.5 s in Sport, and a drag-limited top speed near 270 km/h reached " +
                "in 6th. Replace with sourced manufacturer data when modelling a real vehicle.";

            // --- Chassis ---------------------------------------------------
            definition.Chassis.MassKg = 1520f;
            definition.Chassis.WheelbaseM = 2.75f;
            definition.Chassis.TrackWidthM = 1.58f;
            definition.Chassis.FrontWeightDistribution = 0.52f;
            definition.Chassis.CentreOfMassOffset = new Vector3(0f, 0.48f, definition.SuggestedCentreOfMassZ());
            definition.Chassis.InertiaTensorScale = 1.35f;

            // --- Engine: 3.0 turbo inline-six -------------------------------
            definition.Engine.Description = "3.0L Turbocharged Inline-6";
            definition.Engine.Aspiration = Aspiration.Turbocharged;
            definition.Engine.DisplacementLitres = 3.0f;
            definition.Engine.IdleRpm = 750f;
            definition.Engine.RedlineRpm = 7000f;
            definition.Engine.RevLimiterRpm = 7100f;
            definition.Engine.PeakTorqueNm = 500f;
            definition.Engine.NormalisedTorqueCurve = VehicleDefinition.EngineConfig.DefaultTorqueCurve();
            definition.Engine.InertiaKgM2 = 0.28f;
            definition.Engine.FrictionTorqueNm = 22f;
            definition.Engine.FrictionTorquePerRadPerSec = 0.045f;
            definition.Engine.StarterDurationSeconds = 0.9f;
            definition.Engine.StallRpm = 380f;
            definition.Engine.MaxBoostBar = 0.95f;
            definition.Engine.BoostTorqueGain = 0.62f;
            definition.Engine.BoostOnsetRpm = 1500f;
            definition.Engine.BoostFullRpm = 3200f;
            definition.Engine.BoostSpoolHalfLife = 0.30f;
            definition.Engine.BoostDecayHalfLife = 0.10f;
            definition.Engine.BlowOffThresholdBar = 0.35f;

            // --- Transmission: 8 speed dual clutch ---------------------------
            definition.Transmission.Type = TransmissionType.DualClutch;
            definition.Transmission.ForwardGearRatios = new[] { 5.25f, 3.36f, 2.17f, 1.72f, 1.32f, 1.00f, 0.82f, 0.64f };
            definition.Transmission.ReverseGearRatio = 4.72f;
            definition.Transmission.FinalDriveRatio = 3.15f;
            definition.Transmission.DriveEfficiency = 0.90f;
            definition.Transmission.ShiftTimeSeconds = 0.12f;
            definition.Transmission.ShiftCooldownSeconds = 0.55f;
            definition.Transmission.ClutchMaxTorqueNm = 900f;
            definition.Transmission.BaseUpshiftRpm = 5200f;
            definition.Transmission.BaseDownshiftRpm = 1600f;
            definition.Transmission.ThrottleUpshiftRpmGain = 1400f;

            // --- Drivetrain: rear drive with a limited slip differential -----
            definition.Drivetrain.Layout = DriveLayout.RearWheelDrive;
            definition.Drivetrain.RearDifferential = DifferentialType.LimitedSlip;
            definition.Drivetrain.FrontDifferential = DifferentialType.Open;

            // --- Wheels and tyres: 245/40 R19 --------------------------------
            definition.Wheels.RadiusM = 0.34f;
            definition.Wheels.WidthM = 0.245f;
            definition.Wheels.InertiaKgM2 = 1.30f;

            definition.Tyres.PeakFrictionCoefficient = 1.15f;
            // Static corner load, so load sensitivity is centred on normal driving.
            definition.Tyres.NominalLoadN = definition.Chassis.MassKg * 9.81f / 4f;

            // --- Suspension ---------------------------------------------------
            // Spring rates are chosen for roughly 95 mm of static compression,
            // which is a firm but road-legal setup.
            definition.FrontSuspension.RestLengthM = 0.30f;
            definition.FrontSuspension.MaxTravelM = 0.20f;
            definition.FrontSuspension.SpringRateNPerM = 40000f;
            definition.FrontSuspension.CompressionDampingNsPerM = 3100f;
            definition.FrontSuspension.ReboundDampingNsPerM = 4700f;
            definition.FrontSuspension.AntiRollBarNPerM = 16000f;

            definition.RearSuspension.RestLengthM = 0.30f;
            definition.RearSuspension.MaxTravelM = 0.20f;
            definition.RearSuspension.SpringRateNPerM = 38000f;
            definition.RearSuspension.CompressionDampingNsPerM = 2950f;
            definition.RearSuspension.ReboundDampingNsPerM = 4400f;
            definition.RearSuspension.AntiRollBarNPerM = 13000f;

            // --- Brakes --------------------------------------------------------
            definition.Brakes.MaxTorqueFrontNm = 2600f;
            definition.Brakes.MaxTorqueRearNm = 1600f;
            definition.Brakes.HandbrakeTorqueNm = 2400f;

            // --- Steering -------------------------------------------------------
            definition.Steering.MaxSteerAngleDeg = 36f;
            definition.Steering.SteeringWheelLockDeg = 720f;

            // --- Aero -----------------------------------------------------------
            definition.Aero.DragCoefficient = 0.30f;
            definition.Aero.FrontalAreaM2 = 2.10f;

            definition.DriveModes = new[]
            {
                DriveModeSettings.CreateComfortDefault(),
                DriveModeSettings.CreateSportDefault()
            };

            AssetDatabase.CreateAsset(definition, DefinitionPath);
            AssetDatabase.SaveAssets();

            definition.CalculatePeakPower(out float hp, out float hpRpm);
            definition.CalculatePeakTorque(out float nm, out float nmRpm);
            Debug.Log($"[IndieGame] Created '{definition.Identity.DisplayName}' at {DefinitionPath}. " +
                      $"Peak {hp:0} hp @ {hpRpm:0} rpm, {nm:0} Nm @ {nmRpm:0} rpm.");

            return definition;
        }

        // ==================================================================
        // Vehicle rig
        // ==================================================================
        private static GameObject BuildVehicle(VehicleDefinition definition)
        {
            int vehicleLayer = LayerMask.NameToLayer("Vehicle");
            if (vehicleLayer < 0) vehicleLayer = 0;

            // The suspension must not raycast against the car's own colliders.
            definition.Wheels.GroundMask = vehicleLayer > 0 ? ~(1 << vehicleLayer) : ~0;
            EditorUtility.SetDirty(definition);

            var root = new GameObject("PrototypeVehicle");
            root.transform.position = new Vector3(0f, 0.05f, -60f);
            SetLayerRecursive(root, vehicleLayer);

            var body = root.AddComponent<Rigidbody>();
            body.mass = definition.Chassis.MassKg;

            // Body collider. Kept clear of the road surface: the wheels carry the car,
            // and a body collider that touches the ground would fight the suspension.
            var shell = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shell.name = "BodyShell";
            shell.transform.SetParent(root.transform, false);
            shell.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            shell.transform.localScale = new Vector3(1.86f, 1.06f, 4.55f);
            shell.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("CarPaint_Prototype", new Color(0.09f, 0.10f, 0.13f), 0.85f, 0.90f);
            shell.layer = vehicleLayer;

            // A low, narrow nose block so the primitive mule reads as a car rather than a brick.
            var cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabin.name = "CabinBlock";
            cabin.transform.SetParent(root.transform, false);
            cabin.transform.localPosition = new Vector3(0f, 1.32f, -0.25f);
            cabin.transform.localScale = new Vector3(1.62f, 0.62f, 2.10f);
            cabin.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("Glass_Prototype", new Color(0.05f, 0.07f, 0.09f), 0.2f, 0.95f);
            Object.DestroyImmediate(cabin.GetComponent<Collider>());
            cabin.layer = vehicleLayer;

            // --- Wheels ------------------------------------------------------
            float halfWheelbase = definition.Chassis.WheelbaseM * 0.5f;
            float halfTrack = definition.Chassis.TrackWidthM * 0.5f;

            var wheels = new[]
            {
                CreateWheel(root.transform, definition, "FL", true, -1f, new Vector3(-halfTrack, 0f, halfWheelbase), vehicleLayer),
                CreateWheel(root.transform, definition, "FR", true, 1f, new Vector3(halfTrack, 0f, halfWheelbase), vehicleLayer),
                CreateWheel(root.transform, definition, "RL", false, -1f, new Vector3(-halfTrack, 0f, -halfWheelbase), vehicleLayer),
                CreateWheel(root.transform, definition, "RR", false, 1f, new Vector3(halfTrack, 0f, -halfWheelbase), vehicleLayer)
            };

            // --- Camera anchors ------------------------------------------------
            var cockpit = new GameObject("CockpitCameraAnchor");
            cockpit.transform.SetParent(root.transform, false);
            cockpit.transform.localPosition = new Vector3(-0.38f, 1.18f, 0.05f);

            var hood = new GameObject("HoodCameraAnchor");
            hood.transform.SetParent(root.transform, false);
            hood.transform.localPosition = new Vector3(0f, 1.12f, 1.05f);

            var bumper = new GameObject("BumperCameraAnchor");
            bumper.transform.SetParent(root.transform, false);
            bumper.transform.localPosition = new Vector3(0f, 0.42f, 2.15f);

            // --- Components -----------------------------------------------------
            var input = root.AddComponent<PlayerVehicleInputSource>();
            var controller = root.AddComponent<VehicleController>();

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("definition").objectReferenceValue = definition;
            serialized.FindProperty("inputSourceBehaviour").objectReferenceValue = input;

            SerializedProperty wheelArray = serialized.FindProperty("wheels");
            wheelArray.arraySize = wheels.Length;
            for (int i = 0; i < wheels.Length; i++)
            {
                SerializedProperty element = wheelArray.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Name").stringValue = wheels[i].Name;
                element.FindPropertyRelative("SuspensionAnchor").objectReferenceValue = wheels[i].Anchor;
                element.FindPropertyRelative("VisualWheel").objectReferenceValue = wheels[i].Visual;
                element.FindPropertyRelative("IsFrontAxle").boolValue = wheels[i].IsFront;
                element.FindPropertyRelative("IsSteered").boolValue = wheels[i].IsFront;
                element.FindPropertyRelative("IsDriven").boolValue = !wheels[i].IsFront;
                element.FindPropertyRelative("HasHandbrake").boolValue = !wheels[i].IsFront;
                element.FindPropertyRelative("LateralSign").floatValue = wheels[i].Side;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Stash the anchors on the controller's GameObject for the camera builder.
            var anchors = root.AddComponent<PrototypeCameraAnchors>();
            anchors.Cockpit = cockpit.transform;
            anchors.Hood = hood.transform;
            anchors.Bumper = bumper.transform;

            return root;
        }

        private struct WheelBuild
        {
            public string Name;
            public Transform Anchor;
            public Transform Visual;
            public bool IsFront;
            public float Side;
        }

        private static WheelBuild CreateWheel(Transform parent, VehicleDefinition definition,
                                              string name, bool front, float side,
                                              Vector3 wheelCentreLocal, int layer)
        {
            var axle = front ? definition.FrontSuspension : definition.RearSuspension;
            float radius = definition.Wheels.RadiusM;

            // Static compression = corner weight / spring rate. Placing the anchor this
            // far above the wheel centre means the car sits at exactly its design ride
            // height on spawn rather than dropping or bouncing on the first frame.
            float share = front
                ? definition.Chassis.FrontWeightDistribution
                : 1f - definition.Chassis.FrontWeightDistribution;
            float cornerWeightN = definition.Chassis.MassKg * 9.81f * share * 0.5f;
            float staticCompression = Mathf.Clamp(cornerWeightN / Mathf.Max(1f, axle.SpringRateNPerM),
                                                  0f, axle.MaxTravelM * 0.9f);
            float anchorHeight = radius + (axle.RestLengthM - staticCompression);

            var anchor = new GameObject($"SuspensionAnchor_{name}");
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = new Vector3(wheelCentreLocal.x, anchorHeight, wheelCentreLocal.z);

            var visual = new GameObject($"Wheel_{name}");
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = new Vector3(wheelCentreLocal.x, radius, wheelCentreLocal.z);

            // Unity's cylinder is 2 units tall along local Y with a diameter of 1.
            // Rotating it 90 degrees about Z puts the axle on X, which is what
            // VehicleWheel.UpdateVisual expects for the spin axis.
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mesh.name = "Mesh";
            mesh.transform.SetParent(visual.transform, false);
            mesh.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            mesh.transform.localScale = new Vector3(radius * 2f, definition.Wheels.WidthM * 0.5f, radius * 2f);
            mesh.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("Tyre_Prototype", new Color(0.06f, 0.06f, 0.07f), 0f, 0.30f);
            Object.DestroyImmediate(mesh.GetComponent<Collider>());
            mesh.layer = layer;
            visual.layer = layer;
            anchor.layer = layer;

            return new WheelBuild
            {
                Name = name,
                Anchor = anchor.transform,
                Visual = visual.transform,
                IsFront = front,
                Side = side
            };
        }

        // ==================================================================
        // Scene furniture
        // ==================================================================
        private static void BuildLighting()
        {
            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.90f);
            light.intensity = 1.4f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(42f, 145f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.50f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.30f, 0.32f, 0.35f);
            RenderSettings.ambientGroundColor = new Color(0.14f, 0.14f, 0.15f);
        }

        private static void BuildTestTrack()
        {
            var trackRoot = new GameObject("TestTrack");
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer < 0) groundLayer = 0;

            Material asphalt = GetOrCreateMaterial("Asphalt_Prototype", new Color(0.16f, 0.16f, 0.17f), 0f, 0.42f);
            Material kerb = GetOrCreateMaterial("Marker_Prototype", new Color(0.72f, 0.72f, 0.70f), 0f, 0.35f);

            // Main pad: wide enough for skid pad work, long enough to reach top speed.
            var pad = CreateBox(trackRoot.transform, "GroundPad", new Vector3(0f, -0.5f, 0f),
                                new Vector3(240f, 1f, 1400f), asphalt, groundLayer);
            // Uses its serialized defaults: dry asphalt, friction scale 1.0.
            pad.AddComponent<SurfaceDescriptor>();

            // Distance markers every 100 m along the straight, for acceleration and
            // braking measurements against the validation targets in Docs/VEHICLE_VALIDATION.md.
            for (int i = 1; i <= 8; i++)
            {
                CreateBox(trackRoot.transform, $"Marker_{i * 100}m",
                          new Vector3(-6f, 0.35f, -60f + i * 100f),
                          new Vector3(0.4f, 0.7f, 0.4f), kerb, groundLayer);
            }

            // A 6 percent incline for hill starts and for checking that the car holds
            // still on the brake instead of creeping.
            var incline = CreateBox(trackRoot.transform, "Incline_6pct", new Vector3(70f, 2.0f, 120f),
                                    new Vector3(30f, 1f, 120f), asphalt, groundLayer);
            incline.transform.rotation = Quaternion.Euler(-3.43f, 0f, 0f);

            // A gentler crest and dip to exercise the dampers and the bump stops.
            var crest = CreateBox(trackRoot.transform, "Crest", new Vector3(-70f, -0.15f, 90f),
                                  new Vector3(26f, 1f, 40f), asphalt, groundLayer);
            crest.transform.rotation = Quaternion.Euler(-4f, 0f, 0f);

            var dip = CreateBox(trackRoot.transform, "Dip", new Vector3(-70f, -0.15f, 132f),
                                new Vector3(26f, 1f, 40f), asphalt, groundLayer);
            dip.transform.rotation = Quaternion.Euler(4f, 0f, 0f);

            // Low kerbs bounding the pad so a spin has something to find.
            CreateBox(trackRoot.transform, "Kerb_West", new Vector3(-120f, 0.1f, 620f),
                      new Vector3(1f, 0.25f, 1400f), kerb, groundLayer);
            CreateBox(trackRoot.transform, "Kerb_East", new Vector3(120f, 0.1f, 620f),
                      new Vector3(1f, 0.25f, 1400f), kerb, groundLayer);
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 position,
                                            Vector3 scale, Material material, int layer)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            box.layer = layer;
            return box;
        }

        private static void BuildCamera(VehicleController controller)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 2500f;
            cameraObject.AddComponent<AudioListener>();

            var rig = cameraObject.AddComponent<VehicleCameraRig>();
            var anchors = controller.GetComponent<PrototypeCameraAnchors>();

            var serialized = new SerializedObject(rig);
            serialized.FindProperty("target").objectReferenceValue = controller;
            if (anchors != null)
            {
                serialized.FindProperty("cockpitAnchor").objectReferenceValue = anchors.Cockpit;
                serialized.FindProperty("hoodAnchor").objectReferenceValue = anchors.Hood;
                serialized.FindProperty("bumperAnchor").objectReferenceValue = anchors.Bumper;
            }
            int vehicleLayer = LayerMask.NameToLayer("Vehicle");
            serialized.FindProperty("collisionMask").intValue = vehicleLayer > 0 ? ~(1 << vehicleLayer) : ~0;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            cameraObject.transform.position = controller.transform.position + new Vector3(0f, 2.2f, -6.5f);
        }

        private static void BuildHud(VehicleController controller)
        {
            var hudObject = new GameObject("DebugHUD");
            var hud = hudObject.AddComponent<VehicleDebugHud>();
            var serialized = new SerializedObject(hud);
            serialized.FindProperty("target").objectReferenceValue = controller;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ==================================================================
        // Helpers
        // ==================================================================
        private static Material GetOrCreateMaterial(string name, Color colour, float metallic, float smoothness)
        {
            EnsureFolder("Assets/Art");
            EnsureFolder(MaterialFolder);

            string path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            // Works in HDRP, URP or the built-in pipeline. The prototype does not
            // depend on any one of them, which is what lets the physics be built and
            // tuned before the render pipeline is decided.
            Shader shader = Shader.Find("HDRP/Lit")
                            ?? Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Standard");

            if (shader == null)
            {
                Debug.LogWarning("[IndieGame] No suitable lit shader found; materials will use the default.");
                return null;
            }

            var material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Color")) material.SetColor("_Color", colour);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(parent)) return; // "Assets" itself, nothing to create
            parent = parent.Replace('\\', '/');

            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void SetLayerRecursive(GameObject target, int layer)
        {
            target.layer = layer;
            foreach (Transform child in target.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
