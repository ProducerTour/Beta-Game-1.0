using UnityEngine;
using UnityEditor;

/// <summary>
/// Creates rock prefabs from FBX files with proper URP materials and grounded pivot.
/// </summary>
public class FBXRockPrefabSetup : EditorWindow
{
    [MenuItem("Tools/Setup FBX Rock Prefabs")]
    public static void SetupFBXRocks()
    {
        string fbxFolder = "Assets/Art/Models/Environment/Rocks/Stone_01_FBXs";
        string prefabsFolder = "Assets/Art/Models/Environment/Rocks/Prefabs";
        string materialsFolder = "Assets/Art/Models/Environment/Rocks/Materials";

        // Create folders if needed
        if (!AssetDatabase.IsValidFolder(prefabsFolder))
        {
            AssetDatabase.CreateFolder("Assets/Art/Models/Environment/Rocks", "Prefabs");
        }
        if (!AssetDatabase.IsValidFolder(materialsFolder))
        {
            AssetDatabase.CreateFolder("Assets/Art/Models/Environment/Rocks", "Materials");
        }

        // Find URP Lit shader
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("URP Lit shader not found!");
            return;
        }

        // Load textures from the textures folder
        string texturesFolder = fbxFolder + "/uploads_files_913599_textures";
        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(texturesFolder + "/stone_01_albedo.jpg");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(texturesFolder + "/Stone_01_normal.jpg");
        Texture2D occlusion = AssetDatabase.LoadAssetAtPath<Texture2D>(texturesFolder + "/Stone_01_occlusion.jpg");
        Texture2D roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(texturesFolder + "/stone_01_Roughness.jpg");

        Debug.Log($"Textures found - Albedo: {albedo != null}, Normal: {normal != null}, AO: {occlusion != null}, Roughness: {roughness != null}");

        // Fix normal map import settings
        if (normal != null)
        {
            TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal)) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
                normal = AssetDatabase.LoadAssetAtPath<Texture2D>(texturesFolder + "/Stone_01_normal.jpg");
                Debug.Log("Fixed normal map import settings");
            }
        }

        // Create rock material with textures
        string matPath = materialsFolder + "/Stone_Mat.mat";
        Material rockMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (rockMat == null)
        {
            rockMat = new Material(urpLit);
            AssetDatabase.CreateAsset(rockMat, matPath);
        }

        // Apply textures to material
        rockMat.shader = urpLit;
        if (albedo != null)
        {
            rockMat.SetTexture("_BaseMap", albedo);
            rockMat.SetColor("_BaseColor", Color.white); // Don't tint the texture
        }
        else
        {
            rockMat.SetColor("_BaseColor", new Color(0.45f, 0.42f, 0.38f)); // Fallback gray-brown
        }

        if (normal != null)
        {
            rockMat.SetTexture("_BumpMap", normal);
            rockMat.EnableKeyword("_NORMALMAP");
            rockMat.SetFloat("_BumpScale", 1.0f);
        }

        if (occlusion != null)
        {
            rockMat.SetTexture("_OcclusionMap", occlusion);
            rockMat.SetFloat("_OcclusionStrength", 1.0f);
        }

        // URP uses smoothness (inverse of roughness) - set low smoothness for rough rock
        rockMat.SetFloat("_Smoothness", 0.1f);

        EditorUtility.SetDirty(rockMat);
        Debug.Log("Created/Updated stone material with PBR textures");

        // Setup the lighter FBX models (skip the 51MB decimated one)
        CreateRockPrefab(fbxFolder + "/Stone_01_Base.fbx", rockMat, prefabsFolder + "/Rock_Stone_Base.prefab", "Rock_Stone_Base");
        CreateRockPrefab(fbxFolder + "/Stone_01_BaseTri.fbx", rockMat, prefabsFolder + "/Rock_Stone_Tri.prefab", "Rock_Stone_Tri");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("\n=== FBX ROCK PREFABS CREATED ===");
        Debug.Log("Prefabs in: " + prefabsFolder);
        Debug.Log("\nNow assign them to ChunkManager's Rock Prefabs array");
    }

    private static void CreateRockPrefab(string fbxPath, Material material, string prefabPath, string prefabName)
    {
        GameObject fbxModel = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);

        if (fbxModel == null)
        {
            Debug.LogWarning($"Could not load FBX: {fbxPath}");
            return;
        }

        Debug.Log($"Loading: {fbxPath}");

        // Instantiate the FBX
        GameObject instance = Object.Instantiate(fbxModel);
        instance.name = fbxModel.name;

        // Calculate bounds
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
            Debug.LogWarning($"No renderers in {fbxPath}");
            Object.DestroyImmediate(instance);
            return;
        }

        float bottomY = combinedBounds.min.y;
        float centerX = combinedBounds.center.x;
        float centerZ = combinedBounds.center.z;

        Debug.Log($"  Bounds: bottom={bottomY}, center=({centerX}, {centerZ}), size={combinedBounds.size}");

        // Baked scale factor
        float scaleFactor = 15f;

        // Create wrapper with grounded pivot and baked scale
        GameObject wrapper = new GameObject(prefabName);
        instance.transform.SetParent(wrapper.transform);

        // Apply scale first
        instance.transform.localScale = Vector3.one * scaleFactor;

        // Offset must be multiplied by scale since geometry is scaled
        instance.transform.localPosition = new Vector3(-centerX * scaleFactor, -bottomY * scaleFactor, -centerZ * scaleFactor);

        Debug.Log($"  Final: scale={scaleFactor}, offset=({-centerX * scaleFactor}, {-bottomY * scaleFactor}, {-centerZ * scaleFactor})");

        // Remove any colliders from the FBX (we don't need them for decoration)
        foreach (var col in wrapper.GetComponentsInChildren<Collider>())
        {
            Object.DestroyImmediate(col);
        }

        // Delete old prefab if exists
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            AssetDatabase.DeleteAsset(prefabPath);
        }

        // Save as prefab
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(wrapper, prefabPath);

        // Cleanup
        Object.DestroyImmediate(wrapper);

        if (prefab != null)
        {
            Debug.Log($"  SUCCESS: {prefabPath}");
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }
        else
        {
            Debug.LogError($"  FAILED: Could not create {prefabPath}");
        }
    }
}
