using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace IndieGame.EditorTools
{
    /// <summary>
    /// The project's materials are authored against the built-in Standard shader so
    /// they are valid in a plain Unity project. If the project is using URP or HDRP,
    /// a Standard-shader material renders magenta, so this converts them on load.
    ///
    /// It reads the Standard properties first, swaps the shader, then writes the
    /// pipeline's equivalents. It only ever touches materials still on the Standard
    /// shader, so it is safe to run repeatedly and never fights a manual edit.
    /// </summary>
    public static class PipelineMaterialFixer
    {
        private const string MaterialFolder = "Assets/Art/Materials";

        [InitializeOnLoadMethod]
        private static void OnLoad()
        {
            // Deferred: the shader database is not reliable during domain reload.
            EditorApplication.delayCall += () => Convert(false);
        }

        [MenuItem("Tools/Indie Driving Game/Convert Materials To Active Pipeline", false, 40)]
        public static void ConvertMenu() => Convert(true);

        public static void Convert(bool verbose)
        {
            Shader target = ResolveTargetShader(out string pipelineName);
            if (target == null)
            {
                if (verbose)
                    Debug.Log("[IndieGame] Built-in render pipeline detected; materials already correct.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { MaterialFolder });
            var converted = new List<string>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null) continue;
                if (material.shader.name != "Standard") continue;

                // Read before the swap: these properties disappear with the shader.
                Color baseColour = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.grey;
                float metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
                float smoothness = material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : 0.5f;
                Color emission = material.HasProperty("_EmissionColor")
                    ? material.GetColor("_EmissionColor") : Color.black;
                bool emissive = emission.maxColorComponent > 0.001f;

                material.shader = target;

                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColour);
                if (material.HasProperty("_Color")) material.SetColor("_Color", baseColour);
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);

                if (emissive)
                {
                    material.EnableKeyword("_EMISSION");
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

                    if (material.HasProperty("_EmissionColor"))
                        material.SetColor("_EmissionColor", emission);

                    // HDRP uses its own emissive property and an explicit intensity toggle.
                    if (material.HasProperty("_EmissiveColor"))
                        material.SetColor("_EmissiveColor", emission);
                    if (material.HasProperty("_UseEmissiveIntensity"))
                        material.SetFloat("_UseEmissiveIntensity", 0f);
                    if (material.HasProperty("_EmissiveIntensity"))
                        material.SetFloat("_EmissiveIntensity", 1f);
                }

                EditorUtility.SetDirty(material);
                converted.Add(System.IO.Path.GetFileNameWithoutExtension(path));
            }

            if (converted.Count > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[IndieGame] Converted {converted.Count} materials to {pipelineName}.");
            }
            else if (verbose)
            {
                Debug.Log($"[IndieGame] No Standard-shader materials left to convert ({pipelineName}).");
            }
        }

        private static Shader ResolveTargetShader(out string pipelineName)
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null)
            {
                pipelineName = "Built-in";
                return null;
            }

            string type = pipeline.GetType().Name;
            if (type.Contains("HDRenderPipeline"))
            {
                pipelineName = "HDRP";
                return Shader.Find("HDRP/Lit");
            }

            if (type.Contains("Universal"))
            {
                pipelineName = "URP";
                return Shader.Find("Universal Render Pipeline/Lit");
            }

            pipelineName = type;
            return null;
        }
    }
}
