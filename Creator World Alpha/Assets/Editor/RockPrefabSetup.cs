using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Creates rock prefabs from GLB models with proper URP materials and grounded pivot.
/// </summary>
public class RockPrefabSetup : EditorWindow
{
    [MenuItem("Tools/Setup Rock Prefabs")]
    public static void SetupRockPrefabs()
    {
        string rocksFolder = "Assets/Art/Models/Environment/Rocks";
        string materialsFolder = rocksFolder + "/Materials";
        string prefabsFolder = rocksFolder + "/Prefabs";

        // Create folders if needed
        if (!AssetDatabase.IsValidFolder(materialsFolder))
        {
            AssetDatabase.CreateFolder(rocksFolder, "Materials");
        }
        if (!AssetDatabase.IsValidFolder(prefabsFolder))
        {
            AssetDatabase.CreateFolder(rocksFolder, "Prefabs");
        }

        // Find URP Lit shader
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("URP Lit shader not found! Make sure URP is installed.");
            return;
        }

        // Setup Rock 1 (rock_1.glb with maps folder textures)
        SetupRock1(rocksFolder, materialsFolder, prefabsFolder, urpLit);

        // Setup Alaskan Cliff Rock
        SetupAlaskanRock(rocksFolder, materialsFolder, prefabsFolder, urpLit);

        // Setup Alpine Rock
        SetupAlpineRock(rocksFolder, materialsFolder, prefabsFolder, urpLit);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("\n=== ROCK PREFABS SETUP COMPLETE ===");
        Debug.Log("Prefabs created in: " + prefabsFolder);
        Debug.Log("\nNEXT STEPS:");
        Debug.Log("1. Select ChunkManager in scene");
        Debug.Log("2. Expand 'Rock Prefabs' array");
        Debug.Log("3. Drag the rock prefabs from " + prefabsFolder);
        Debug.Log("4. Set 'Rock Y Offset' to 0");
    }

    private static void SetupRock1(string rocksFolder, string materialsFolder, string prefabsFolder, Shader urpLit)
    {
        string glbPath = rocksFolder + "/rock_1.glb";
        Debug.Log($"Looking for GLB at: {glbPath}");

        // Try to load the main asset (root GameObject) from the GLB
        GameObject glbModel = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);

        // If that fails, try loading all assets and find the first GameObject
        if (glbModel == null)
        {
            Debug.Log("Direct load failed, trying to load all sub-assets...");
            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(glbPath);
            Debug.Log($"Found {allAssets?.Length ?? 0} assets at path");

            foreach (var asset in allAssets ?? new Object[0])
            {
                Debug.Log($"  - {asset.name} ({asset.GetType().Name})");
                if (asset is GameObject go && glbModel == null)
                {
                    glbModel = go;
                }
            }
        }

        if (glbModel == null)
        {
            Debug.LogWarning("Could not find rock_1.glb - GLB may not be imported correctly. Check if Draco package is installed.");
            return;
        }

        Debug.Log($"Successfully loaded: {glbModel.name}");

        // Load textures from maps folder
        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(rocksFolder + "/maps/default_Base_Color.jpg");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(rocksFolder + "/maps/default_Normal_OpenGL.jpg");
        Texture2D ao = AssetDatabase.LoadAssetAtPath<Texture2D>(rocksFolder + "/maps/default_Ambient_occlusion.jpg");
        Texture2D roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(rocksFolder + "/maps/default_Roughness.jpg");

        // Fix normal map import settings
        if (normal != null)
        {
            TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal)) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
                normal = AssetDatabase.LoadAssetAtPath<Texture2D>(rocksFolder + "/maps/default_Normal_OpenGL.jpg");
            }
        }

        // Create material
        string matPath = materialsFolder + "/Rock1_Mat.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(urpLit);
            AssetDatabase.CreateAsset(mat, matPath);
        }

        mat.shader = urpLit;
        if (albedo != null) mat.SetTexture("_BaseMap", albedo);
        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
        }
        if (ao != null) mat.SetTexture("_OcclusionMap", ao);
        // URP uses smoothness (inverse of roughness) - handled in shader
        EditorUtility.SetDirty(mat);

        // Create prefab
        CreateGroundedPrefab(glbModel, mat, prefabsFolder + "/Rock_Large.prefab", "Rock_Large");
        Debug.Log("Created Rock_Large prefab from rock_1.glb");
    }

    private static void SetupAlaskanRock(string rocksFolder, string materialsFolder, string prefabsFolder, Shader urpLit)
    {
        string glbPath = rocksFolder + "/alaskan-cliff-rock-9-free/alaskan_cliff_rock.glb";
        Debug.Log($"Looking for GLB at: {glbPath}");

        GameObject glbModel = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);

        if (glbModel == null)
        {
            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(glbPath);
            Debug.Log($"Found {allAssets?.Length ?? 0} assets at path");
            foreach (var asset in allAssets ?? new Object[0])
            {
                Debug.Log($"  - {asset.name} ({asset.GetType().Name})");
                if (asset is GameObject go && glbModel == null)
                    glbModel = go;
            }
        }

        if (glbModel == null)
        {
            Debug.LogWarning("Could not find alaskan_cliff_rock.glb");
            return;
        }

        Debug.Log($"Successfully loaded: {glbModel.name}");

        // Load textures
        string texFolder = rocksFolder + "/alaskan-cliff-rock-9-free/textures";
        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(texFolder + "/CliffRock_0009_2k_Albedo.png");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(texFolder + "/CliffRock_0009_2k_Normal.png");
        Texture2D roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(texFolder + "/CliffRock_0009_2k_Roughness.png");

        // Fix normal map import settings
        if (normal != null)
        {
            TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal)) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
                normal = AssetDatabase.LoadAssetAtPath<Texture2D>(texFolder + "/CliffRock_0009_2k_Normal.png");
            }
        }

        // Create material
        string matPath = materialsFolder + "/AlaskanRock_Mat.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(urpLit);
            AssetDatabase.CreateAsset(mat, matPath);
        }

        mat.shader = urpLit;
        if (albedo != null) mat.SetTexture("_BaseMap", albedo);
        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
        }
        EditorUtility.SetDirty(mat);

        // Create prefab
        CreateGroundedPrefab(glbModel, mat, prefabsFolder + "/Rock_Cliff.prefab", "Rock_Cliff");
        Debug.Log("Created Rock_Cliff prefab from alaskan_cliff_rock.glb");
    }

    private static void SetupAlpineRock(string rocksFolder, string materialsFolder, string prefabsFolder, Shader urpLit)
    {
        string glbPath = rocksFolder + "/export/alpine_rock.glb";
        Debug.Log($"Looking for GLB at: {glbPath}");

        GameObject glbModel = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);

        if (glbModel == null)
        {
            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(glbPath);
            Debug.Log($"Found {allAssets?.Length ?? 0} assets at path");
            foreach (var asset in allAssets ?? new Object[0])
            {
                Debug.Log($"  - {asset.name} ({asset.GetType().Name})");
                if (asset is GameObject go && glbModel == null)
                    glbModel = go;
            }
        }

        if (glbModel == null)
        {
            Debug.LogWarning("Could not find alpine_rock.glb");
            return;
        }

        Debug.Log($"Successfully loaded: {glbModel.name}");

        // This rock may not have separate textures - use a simple gray material
        string matPath = materialsFolder + "/AlpineRock_Mat.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(urpLit);
            AssetDatabase.CreateAsset(mat, matPath);
        }

        mat.shader = urpLit;
        mat.SetColor("_BaseColor", new Color(0.5f, 0.5f, 0.5f)); // Gray rock color
        mat.SetFloat("_Smoothness", 0.2f); // Rough surface
        EditorUtility.SetDirty(mat);

        // Create prefab
        CreateGroundedPrefab(glbModel, mat, prefabsFolder + "/Rock_Alpine.prefab", "Rock_Alpine");
        Debug.Log("Created Rock_Alpine prefab from alpine_rock.glb");
    }

    private static void CreateGroundedPrefab(GameObject sourceModel, Material material, string prefabPath, string prefabName)
    {
        if (sourceModel == null)
        {
            Debug.LogError($"Source model is null for {prefabName}");
            return;
        }

        Debug.Log($"Creating prefab from: {sourceModel.name}");

        // For imported models (GLB/FBX), use Object.Instantiate instead of PrefabUtility
        GameObject instance = Object.Instantiate(sourceModel);
        instance.name = sourceModel.name;

        // Calculate combined bounds
        Bounds combinedBounds = new Bounds(Vector3.zero, Vector3.zero);
        bool boundsInitialized = false;

        var renderers = instance.GetComponentsInChildren<Renderer>(true);
        Debug.Log($"  Found {renderers.Length} renderers");

        foreach (var renderer in renderers)
        {
            // Assign material
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            if (!boundsInitialized)
            {
                combinedBounds = renderer.bounds;
                boundsInitialized = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        if (!boundsInitialized)
        {
            Debug.LogWarning($"No renderers found in {sourceModel.name}");
            Object.DestroyImmediate(instance);
            return;
        }

        float bottomY = combinedBounds.min.y;
        float centerX = combinedBounds.center.x;
        float centerZ = combinedBounds.center.z;

        Debug.Log($"  Bounds: bottom={bottomY}, centerX={centerX}, centerZ={centerZ}");

        // Create wrapper with grounded pivot
        GameObject wrapper = new GameObject(prefabName);
        instance.transform.SetParent(wrapper.transform);
        instance.transform.localPosition = new Vector3(-centerX, -bottomY, -centerZ);

        // Delete old prefab if exists
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            AssetDatabase.DeleteAsset(prefabPath);
        }

        // Save as prefab
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(wrapper, prefabPath);

        if (prefab != null)
        {
            Debug.Log($"  SUCCESS: Created prefab at {prefabPath}");
        }
        else
        {
            Debug.LogError($"  FAILED: Could not save prefab at {prefabPath}");
        }

        // Cleanup scene instance
        Object.DestroyImmediate(wrapper);

        // Select and ping the prefab
        if (prefab != null)
        {
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }
    }
}
