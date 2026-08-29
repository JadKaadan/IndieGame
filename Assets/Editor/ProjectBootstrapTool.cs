using UnityEditor;
using UnityEngine;

namespace IndieGame.EditorTools
{
    /// <summary>
    /// Applies the project settings the vehicle simulation depends on.
    ///
    /// These live in ProjectSettings/*.asset, which is not something a runtime
    /// script can configure, and getting them wrong (a 50 Hz timestep in
    /// particular) makes a good tyre model feel bad for reasons that are very hard
    /// to diagnose. Running this once, from the menu, removes that whole class of
    /// problem and documents the reasoning next to the values.
    /// </summary>
    public static class ProjectBootstrapTool
    {
        /// <summary>
        /// 200 Hz. A tyre model with a stiff slip curve needs a short timestep or
        /// the force can overshoot within a step and oscillate. Unity's 50 Hz
        /// default is far too coarse for this; 200 Hz is cheap while the scene
        /// holds one car and can be relaxed to 100 Hz once traffic arrives.
        /// </summary>
        public const float FixedTimestep = 0.005f;

        /// <summary>Caps how much simulation one long frame can try to catch up on.</summary>
        public const float MaximumAllowedTimestep = 0.06f;

        public const int SolverIterations = 12;
        public const int SolverVelocityIterations = 6;

        private static readonly string[] RequiredLayers =
        {
            "Vehicle",     // the player's car and traffic bodies
            "Wheel",       // reserved for wheel colliders if ever needed
            "Ground",      // drivable surfaces the suspension may hit
            "Environment", // buildings, barriers, props
            "Traffic"      // AI vehicle triggers and sensors
        };

        [MenuItem("Tools/IndieGame/Configure Project Settings", false, 0)]
        public static void ConfigureAll()
        {
            ConfigureTime();
            ConfigurePhysics();
            ConfigureLayers();
            AssetDatabase.SaveAssets();

            Debug.Log(
                "[IndieGame] Project settings configured.\n" +
                $"  Fixed Timestep: {FixedTimestep}s ({1f / FixedTimestep:0} Hz)\n" +
                $"  Maximum Allowed Timestep: {MaximumAllowedTimestep}s\n" +
                $"  Solver iterations: {SolverIterations} position / {SolverVelocityIterations} velocity\n" +
                $"  Layers ensured: {string.Join(", ", RequiredLayers)}");
        }

        private static void ConfigureTime()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("[IndieGame] Could not open TimeManager.asset. Set Fixed Timestep manually in Edit > Project Settings > Time.");
                return;
            }

            var timeManager = new SerializedObject(assets[0]);
            SetFloat(timeManager, "Fixed Timestep", FixedTimestep);
            SetFloat(timeManager, "Maximum Allowed Timestep", MaximumAllowedTimestep);
            timeManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigurePhysics()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("[IndieGame] Could not open DynamicsManager.asset. Set solver iterations manually in Edit > Project Settings > Physics.");
                return;
            }

            var physics = new SerializedObject(assets[0]);
            SetInt(physics, "m_DefaultSolverIterations", SolverIterations);
            SetInt(physics, "m_DefaultSolverVelocityIterations", SolverVelocityIterations);
            SetFloat(physics, "m_DefaultMaxAngularSpeed", 60f);
            SetFloat(physics, "m_BounceThreshold", 2f);
            physics.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureLayers()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("[IndieGame] Could not open TagManager.asset. Create the layers manually.");
                return;
            }

            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            if (layers == null || !layers.isArray) return;

            foreach (string layerName in RequiredLayers)
            {
                if (LayerExists(layers, layerName)) continue;
                if (!AddLayer(layers, layerName))
                    Debug.LogWarning($"[IndieGame] No free user layer slot for '{layerName}'.");
            }

            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool LayerExists(SerializedProperty layers, string layerName)
        {
            for (int i = 0; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName) return true;
            return false;
        }

        private static bool AddLayer(SerializedProperty layers, string layerName)
        {
            // Layers 0-7 are reserved by Unity. User layers start at 8.
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty element = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(element.stringValue))
                {
                    element.stringValue = layerName;
                    return true;
                }
            }
            return false;
        }

        private static void SetFloat(SerializedObject target, string path, float value)
        {
            SerializedProperty property = target.FindProperty(path);
            if (property != null) property.floatValue = value;
            else Debug.LogWarning($"[IndieGame] Property '{path}' not found. Unity may have renamed it in this version.");
        }

        private static void SetInt(SerializedObject target, string path, int value)
        {
            SerializedProperty property = target.FindProperty(path);
            if (property != null) property.intValue = value;
            else Debug.LogWarning($"[IndieGame] Property '{path}' not found. Unity may have renamed it in this version.");
        }
    }
}
