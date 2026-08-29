using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IndieGame.EditorTools
{
    /// <summary>
    /// Removes the "opened the project and got an empty Untitled scene" problem.
    ///
    /// On the first load of a session it applies the project settings the physics
    /// needs, registers VehicleTest in the build settings, and opens it when the
    /// editor is sitting on an empty untitled scene. It never touches a scene that
    /// has been saved or modified.
    /// </summary>
    [InitializeOnLoad]
    public static class PrototypeAutoSetup
    {
        public const string ScenePath = "Assets/Scenes/VehicleTest.unity";
        private const string SessionKey = "IndieGame.AutoSetupDone";
        private const string SettingsKey = "IndieGame.SettingsConfigured";

        static PrototypeAutoSetup()
        {
            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);

            if (!EditorPrefs.GetBool(SettingsKey, false))
            {
                ProjectBootstrapTool.ConfigureAll();
                EditorPrefs.SetBool(SettingsKey, true);
            }

            RegisterScene();
            OpenIfIdle();
        }

        /// <summary>Adds VehicleTest as the first scene in the build list if it is absent.</summary>
        public static void RegisterScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == ScenePath)) return;

            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[IndieGame] Added VehicleTest to the build settings scene list.");
        }

        private static void OpenIfIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Debug.LogWarning("[IndieGame] " + ScenePath + " not found. " +
                                 "Run Tools > Indie Driving Game > Build Playable Prototype.");
                return;
            }

            Scene active = SceneManager.GetActiveScene();

            // Only take over an unsaved, essentially empty scene - never real work.
            bool untitled = string.IsNullOrEmpty(active.path);
            if (!untitled || active.isDirty || active.rootCount > 2) return;

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("[IndieGame] Opened " + ScenePath + ". Press Play to drive.");
        }

        [MenuItem("Tools/Indie Driving Game/Open VehicleTest Scene", false, 1)]
        public static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                EditorUtility.DisplayDialog("Scene missing",
                    "VehicleTest.unity was not found.\n\nUse Tools > Indie Driving Game > " +
                    "Build Playable Prototype to generate it.", "OK");
                return;
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }
}
