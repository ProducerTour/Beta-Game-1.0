using UnityEngine;
using UnityEditor;
using CreatorWorld.World;

namespace CreatorWorld.Editor
{
    /// <summary>
    /// Editor tool to set up procedural grass system.
    /// </summary>
    public class GrassSetup : EditorWindow
    {
        private const string SHADER_PATH = "Assets/_Project/Shaders/ProceduralGrass.shader";
        private const string MATERIAL_PATH = "Assets/_Project/Materials/GrassMaterial.mat";
        private const string SETTINGS_PATH = "Assets/_Project/ScriptableObjects/DefaultGrassSettings.asset";
        private const string CULLING_SHADER_PATH = "Assets/_Project/Shaders/GrassCulling.compute";

        [MenuItem("Tools/Creator World/Setup Procedural Grass", priority = 30)]
        public static void SetupGrass()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Error", "Exit Play mode first.", "OK");
                return;
            }

            Debug.Log("========== GRASS SETUP START ==========");

            // Create material
            Material grassMat = CreateGrassMaterial();

            // Create settings
            GrassSettings settings = CreateGrassSettings();

            // Find or create grass object
            GameObject grassObj = CreateGrassObject(grassMat);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeGameObject = grassObj;

            string message = "Procedural Grass Setup Complete!\n\n";
            message += "Created:\n";
            message += "- Grass Material\n";
            message += "- Default Grass Settings\n";
            message += "- ProceduralGrass GameObject\n\n";
            message += "Select the ProceduralGrass object to adjust settings.\n";
            message += "Enter Play mode to see the grass render.";

            EditorUtility.DisplayDialog("Grass Setup", message, "OK");
            Debug.Log("========== GRASS SETUP COMPLETE ==========");
        }

        private static Material CreateGrassMaterial()
        {
            // Check if material exists
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
            if (mat != null)
            {
                Debug.Log("Grass material already exists");
                return mat;
            }

            // Load shader
            Shader grassShader = AssetDatabase.LoadAssetAtPath<Shader>(SHADER_PATH);
            if (grassShader == null)
            {
                Debug.LogError($"Grass shader not found at {SHADER_PATH}");
                return null;
            }

            // Create material
            mat = new Material(grassShader);
            mat.name = "GrassMaterial";

            // Set default properties
            mat.SetColor("_BaseColor", new Color(0.1f, 0.35f, 0.1f));
            mat.SetColor("_TipColor", new Color(0.45f, 0.6f, 0.25f));
            mat.SetFloat("_BladeWidth", 0.03f);
            mat.SetFloat("_BladeHeight", 0.5f);
            mat.SetFloat("_BendAmount", 0.3f);
            mat.SetFloat("_WindStrength", 0.5f);
            mat.SetFloat("_AmbientOcclusion", 0.4f);

            // Ensure materials folder exists
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Materials"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Materials");
            }

            AssetDatabase.CreateAsset(mat, MATERIAL_PATH);
            Debug.Log($"Created grass material: {MATERIAL_PATH}");

            return mat;
        }

        private static GrassSettings CreateGrassSettings()
        {
            // Check if settings exist
            GrassSettings settings = AssetDatabase.LoadAssetAtPath<GrassSettings>(SETTINGS_PATH);
            if (settings != null)
            {
                Debug.Log("Grass settings already exist");
                return settings;
            }

            // Ensure folder exists
            if (!AssetDatabase.IsValidFolder("Assets/_Project/ScriptableObjects"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "ScriptableObjects");
            }

            // Create settings
            settings = ScriptableObject.CreateInstance<GrassSettings>();
            settings.grassPerMeter = 8;
            settings.renderRadius = 50f;
            settings.maxRenderDistance = 100f;
            settings.maxGrassBlades = 500000;
            settings.terrainSeed = 12345;
            settings.maxSlope = 40f;
            settings.baseColor = new Color(0.1f, 0.35f, 0.1f);
            settings.tipColor = new Color(0.45f, 0.6f, 0.25f);
            settings.bladeWidth = 0.04f;
            settings.bladeHeight = 0.5f;
            settings.bendAmount = 0.3f;
            settings.windDirection = new Vector2(1f, 0.3f);
            settings.windStrength = 0.5f;
            settings.windFrequency = 0.1f;

            AssetDatabase.CreateAsset(settings, SETTINGS_PATH);
            Debug.Log($"Created grass settings: {SETTINGS_PATH}");

            return settings;
        }

        private static GameObject CreateGrassObject(Material grassMat)
        {
            // Find existing
            ProceduralGrassRenderer existing = Object.FindFirstObjectByType<ProceduralGrassRenderer>();
            if (existing != null)
            {
                Debug.Log("ProceduralGrassRenderer already exists in scene");
                return existing.gameObject;
            }

            // Create new
            GameObject grassObj = new GameObject("ProceduralGrass");

            var renderer = grassObj.AddComponent<ProceduralGrassRenderer>();

            // Set references via SerializedObject
            SerializedObject so = new SerializedObject(renderer);

            var matProp = so.FindProperty("grassMaterial");
            if (matProp != null) matProp.objectReferenceValue = grassMat;

            // Assign culling compute shader for GPU frustum culling
            var cullingShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(CULLING_SHADER_PATH);
            var cullingProp = so.FindProperty("cullingShader");
            if (cullingProp != null && cullingShader != null)
            {
                cullingProp.objectReferenceValue = cullingShader;
                Debug.Log("Assigned GPU culling shader for performance");
            }

            // Set default values (matching new ProceduralGrassRenderer properties)
            SetPropertyValue(so, "grassPerMeter", 8);
            SetPropertyValue(so, "renderRadius", 50f);
            SetPropertyValue(so, "maxRenderDistance", 100f);
            SetPropertyValue(so, "baseColor", new Color(0.1f, 0.35f, 0.1f, 1f));
            SetPropertyValue(so, "tipColor", new Color(0.45f, 0.6f, 0.25f, 1f));
            SetPropertyValue(so, "bladeWidth", 0.04f);
            SetPropertyValue(so, "bladeHeight", 0.5f);
            SetPropertyValue(so, "bendAmount", 0.3f);
            SetPropertyValue(so, "windDirection", new Vector2(1f, 0.3f));
            SetPropertyValue(so, "windStrength", 0.5f);
            SetPropertyValue(so, "windFrequency", 0.1f);
            SetPropertyValue(so, "terrainSeed", 12345);
            SetPropertyValue(so, "maxSlope", 40f);
            SetPropertyValue(so, "maxGrassBlades", 500000);

            so.ApplyModifiedProperties();

            Debug.Log("Created ProceduralGrass GameObject");
            return grassObj;
        }

        private static void SetPropertyValue(SerializedObject so, string name, object value)
        {
            var prop = so.FindProperty(name);
            if (prop == null) return;

            if (value is int i) prop.intValue = i;
            else if (value is float f) prop.floatValue = f;
            else if (value is bool b) prop.boolValue = b;
            else if (value is Color c) prop.colorValue = c;
            else if (value is Vector2 v2) prop.vector2Value = v2;
            else if (value is Vector3 v3) prop.vector3Value = v3;
        }

        [MenuItem("Tools/Creator World/Debug Grass System")]
        public static void DebugGrassSystem()
        {
            Debug.Log("========== GRASS DEBUG START ==========");

            // Check for renderer in scene
            var renderer = Object.FindFirstObjectByType<ProceduralGrassRenderer>();
            if (renderer == null)
            {
                Debug.LogError("[GrassDebug] No ProceduralGrassRenderer found in scene! Run 'Setup Procedural Grass' first.");
                return;
            }

            Debug.Log($"[GrassDebug] Found ProceduralGrassRenderer at position: {renderer.transform.position}");

            // Check material via SerializedObject
            var so = new SerializedObject(renderer);
            var matProp = so.FindProperty("grassMaterial");
            if (matProp != null && matProp.objectReferenceValue != null)
            {
                Debug.Log($"[GrassDebug] Material assigned: {matProp.objectReferenceValue.name}");
            }
            else
            {
                Debug.LogError("[GrassDebug] NO MATERIAL ASSIGNED! This is the problem.");

                // Try to fix it
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
                if (mat != null)
                {
                    matProp.objectReferenceValue = mat;
                    so.ApplyModifiedProperties();
                    Debug.Log("[GrassDebug] Auto-assigned material from: " + MATERIAL_PATH);
                }
                else
                {
                    Debug.LogError("[GrassDebug] Material not found at: " + MATERIAL_PATH);
                }
            }

            // Check shader
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(SHADER_PATH);
            if (shader != null)
            {
                Debug.Log($"[GrassDebug] Shader found: {shader.name}");
                if (!shader.isSupported)
                {
                    Debug.LogError("[GrassDebug] SHADER NOT SUPPORTED on this platform!");
                }
            }
            else
            {
                Debug.LogError("[GrassDebug] Shader NOT FOUND at: " + SHADER_PATH);
            }

            // Test terrain generator
            float testHeight = TerrainGenerator.GetHeightAt(0, 0, 12345);
            BiomeType testBiome = TerrainGenerator.GetBiomeAt(0, 0, 12345);
            Debug.Log($"[GrassDebug] TerrainGenerator test at (0,0): Height={testHeight}, Biome={testBiome}");

            // Check render settings
            var radiusProp = so.FindProperty("renderRadius");
            var seedProp = so.FindProperty("terrainSeed");
            Debug.Log($"[GrassDebug] Render radius: {radiusProp?.floatValue ?? -1}, Seed: {seedProp?.intValue ?? -1}");

            Debug.Log("========== GRASS DEBUG END ==========");
            Debug.Log("Now enter Play mode and check console for [GrassRenderer] messages");
        }

        [MenuItem("Tools/Creator World/Find Grassland Biomes")]
        public static void FindGrasslandBiomes()
        {
            Debug.Log("========== SEARCHING FOR GRASSLAND ==========");

            int seed = 12345;
            int searchRadius = 500;
            int step = 10;

            Vector3 firstGrassland = Vector3.zero;
            bool found = false;
            int grasslandCount = 0;
            int forestCount = 0;

            // Search in a grid
            for (int x = -searchRadius; x <= searchRadius && !found; x += step)
            {
                for (int z = -searchRadius; z <= searchRadius; z += step)
                {
                    BiomeType biome = TerrainGenerator.GetBiomeAt(x, z, seed);

                    if (biome == BiomeType.Grassland)
                    {
                        grasslandCount++;
                        if (!found)
                        {
                            float height = TerrainGenerator.GetHeightAt(x, z, seed);
                            firstGrassland = new Vector3(x, height, z);
                            found = true;
                            Debug.Log($"[BiomeSearch] Found GRASSLAND at ({x}, {z}), height: {height:F1}");
                        }
                    }
                    else if (biome == BiomeType.Forest)
                    {
                        forestCount++;
                    }
                }
            }

            // Sample what biome is at (0,0)
            BiomeType centerBiome = TerrainGenerator.GetBiomeAt(0, 0, seed);
            float centerHeight = TerrainGenerator.GetHeightAt(0, 0, seed);
            Debug.Log($"[BiomeSearch] Center (0,0) biome: {centerBiome}, height: {centerHeight:F1}");

            Debug.Log($"[BiomeSearch] Found {grasslandCount} grassland tiles, {forestCount} forest tiles in {searchRadius}m radius");

            if (found)
            {
                Debug.Log($"[BiomeSearch] Move ProceduralGrass object to: {firstGrassland}");

                // Auto-move the grass renderer if it exists
                var renderer = Object.FindFirstObjectByType<ProceduralGrassRenderer>();
                if (renderer != null)
                {
                    Undo.RecordObject(renderer.transform, "Move Grass to Grassland");
                    renderer.transform.position = firstGrassland;
                    Debug.Log("[BiomeSearch] Auto-moved ProceduralGrass to grassland location!");
                }
            }
            else
            {
                Debug.LogWarning("[BiomeSearch] No grassland found! Try checking 'Ignore Biome Filter' on the ProceduralGrassRenderer.");
            }

            Debug.Log("========== BIOME SEARCH COMPLETE ==========");
        }

        [MenuItem("Tools/Creator World/Create Grass Settings Asset")]
        public static void CreateGrassSettingsMenu()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Grass Settings",
                "GrassSettings",
                "asset",
                "Choose a location for the grass settings"
            );

            if (string.IsNullOrEmpty(path)) return;

            GrassSettings settings = ScriptableObject.CreateInstance<GrassSettings>();
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();

            Selection.activeObject = settings;
            EditorUtility.DisplayDialog("Created", $"Grass Settings created at:\n{path}", "OK");
        }
    }
}
