using UnityEngine;
using UnityEditor;

namespace CreatorWorld.Editor
{
    /// <summary>
    /// Creates the grass instanced material.
    /// Access via: Tools > Creator World > Create Grass Material
    /// </summary>
    public static class GrassMaterialSetup
    {
        [MenuItem("Tools/Creator World/Create Grass Material", priority = 151)]
        public static void CreateGrassMaterial()
        {
            // Ensure directory exists
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Materials"))
                AssetDatabase.CreateFolder("Assets/_Project", "Materials");

            // Find the grass shader
            Shader grassShader = Shader.Find("Custom/GrassInstanced");
            if (grassShader == null)
            {
                // Try loading from file
                grassShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Shaders/GrassInstanced.shader");
            }

            if (grassShader == null)
            {
                EditorUtility.DisplayDialog("Shader Not Found",
                    "GrassInstanced shader not found.\n\nMake sure GrassInstanced.shader exists in:\nAssets/_Project/Shaders/",
                    "OK");
                return;
            }

            // Create material
            Material grassMat = new Material(grassShader);
            grassMat.name = "GrassInstanced";

            // Set default properties
            grassMat.SetColor("_BaseColor", new Color(0.2f, 0.4f, 0.1f));
            grassMat.SetColor("_TipColor", new Color(0.4f, 0.6f, 0.2f));
            grassMat.SetColor("_AOColor", new Color(0.1f, 0.15f, 0.05f));
            grassMat.SetFloat("_WindStrength", 0.5f);
            grassMat.SetFloat("_WindSpeed", 1.0f);
            grassMat.SetVector("_WindDirection", new Vector4(1f, 0.5f, 0, 0));
            grassMat.SetFloat("_WindNoiseScale", 0.1f);
            grassMat.SetFloat("_MaxViewDistance", 150f);
            grassMat.SetFloat("_FadeStart", 0.7f);
            grassMat.SetFloat("_FadeEnd", 1.0f);
            grassMat.SetFloat("_AlphaCutoff", 0.5f);

            // Enable GPU instancing
            grassMat.enableInstancing = true;

            // Set render queue for transparency
            grassMat.renderQueue = 2450; // AlphaTest queue

            AssetDatabase.CreateAsset(grassMat, "Assets/_Project/Materials/GrassInstanced.mat");
            AssetDatabase.SaveAssets();

            Debug.Log("[Setup] Created GrassInstanced material");
            EditorUtility.DisplayDialog("Material Created",
                "Created GrassInstanced.mat\n\nLocation: Assets/_Project/Materials/\n\n✓ GPU Instancing enabled\n✓ Default grass colors set",
                "OK");

            Selection.activeObject = grassMat;
        }
    }
}
