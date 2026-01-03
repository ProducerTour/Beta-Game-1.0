using UnityEngine;
using UnityEditor;
using System.IO;

public class OakTreeMaterialSetup : EditorWindow
{
    [MenuItem("Tools/Setup Oak Tree Materials")]
    public static void SetupMaterials()
    {
        string textureFolder = "Assets/Art/Models/Environment/Trees";
        string fbxPath = textureFolder + "/OakTree.fbx";

        // Find textures
        Texture2D leafTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
            textureFolder + "/TexturesCom_Branches0012_1_masked_S.png");
        Texture2D barkAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>(
            textureFolder + "/TexturesCom_PineBark2_1K_albedo.tif");
        Texture2D barkNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(
            textureFolder + "/TexturesCom_PineBark2_1K_normal.tif");
        Texture2D barkAO = AssetDatabase.LoadAssetAtPath<Texture2D>(
            textureFolder + "/TexturesCom_PineBark2_1K_ao.tif");

        // Fix leaf texture import settings - THIS IS KEY FOR ALPHA
        if (leafTexture != null)
        {
            string leafPath = AssetDatabase.GetAssetPath(leafTexture);
            TextureImporter importer = AssetImporter.GetAtPath(leafPath) as TextureImporter;
            if (importer != null)
            {
                importer.alphaIsTransparency = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.SaveAndReimport();
                // Reload texture after reimport
                leafTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(leafPath);
                Debug.Log("Fixed leaf texture alpha settings");
            }
        }
        else
        {
            Debug.LogError("Could not find leaf texture: TexturesCom_Branches0012_1_masked_S.png");
            return;
        }

        // Create Materials folder
        string materialsFolder = textureFolder + "/Materials";
        if (!AssetDatabase.IsValidFolder(materialsFolder))
        {
            AssetDatabase.CreateFolder(textureFolder, "Materials");
        }

        // Find URP Lit shader
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("URP Lit shader not found! Make sure URP is installed.");
            return;
        }

        // Create or update Leaf Material
        string leafMatPath = materialsFolder + "/OakLeaf_Mat.mat";
        Material leafMat = AssetDatabase.LoadAssetAtPath<Material>(leafMatPath);
        if (leafMat == null)
        {
            leafMat = new Material(urpLit);
            AssetDatabase.CreateAsset(leafMat, leafMatPath);
        }

        leafMat.shader = urpLit;
        leafMat.SetTexture("_BaseMap", leafTexture);
        leafMat.SetColor("_BaseColor", Color.white);
        // Enable alpha clipping - CRITICAL
        leafMat.SetFloat("_AlphaClip", 1f);
        leafMat.SetFloat("_Cutoff", 0.5f);
        leafMat.SetFloat("_Surface", 0f); // Opaque
        leafMat.EnableKeyword("_ALPHATEST_ON");
        leafMat.DisableKeyword("_ALPHABLEND_ON");
        leafMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        leafMat.renderQueue = 2450;
        EditorUtility.SetDirty(leafMat);
        Debug.Log("Created/Updated OakLeaf_Mat with alpha clipping");

        // Create or update Bark Material
        string barkMatPath = materialsFolder + "/OakBark_Mat.mat";
        Material barkMat = AssetDatabase.LoadAssetAtPath<Material>(barkMatPath);
        if (barkMat == null)
        {
            barkMat = new Material(urpLit);
            AssetDatabase.CreateAsset(barkMat, barkMatPath);
        }

        barkMat.shader = urpLit;
        if (barkAlbedo != null)
            barkMat.SetTexture("_BaseMap", barkAlbedo);
        if (barkNormal != null)
        {
            barkMat.SetTexture("_BumpMap", barkNormal);
            barkMat.EnableKeyword("_NORMALMAP");
        }
        if (barkAO != null)
            barkMat.SetTexture("_OcclusionMap", barkAO);
        EditorUtility.SetDirty(barkMat);
        Debug.Log("Created/Updated OakBark_Mat");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Now create a prefab with materials assigned
        GameObject fbxModel = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbxModel == null)
        {
            Debug.LogError("Could not find FBX at: " + fbxPath);
            return;
        }

        // Instantiate the FBX in memory
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbxModel);

        // Get all renderers and assign materials based on mesh name
        var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
        Debug.Log($"Found {renderers.Length} renderers in FBX");

        foreach (var renderer in renderers)
        {
            string meshName = renderer.gameObject.name.ToLower();
            Debug.Log($"  - Processing mesh: {renderer.gameObject.name}");

            if (meshName.Contains("leaf") || meshName.Contains("leaves") || meshName.Contains("foliage") || meshName.Contains("branch"))
            {
                renderer.sharedMaterial = leafMat;
                Debug.Log($"    Assigned leaf material to {renderer.gameObject.name}");
            }
            else if (meshName.Contains("tree") || meshName.Contains("trunk") || meshName.Contains("bark"))
            {
                renderer.sharedMaterial = barkMat;
                Debug.Log($"    Assigned bark material to {renderer.gameObject.name}");
            }
        }

        // Calculate bounds to find the bottom of the tree
        Bounds combinedBounds = new Bounds(Vector3.zero, Vector3.zero);
        bool boundsInitialized = false;

        foreach (var renderer in renderers)
        {
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

        float bottomY = combinedBounds.min.y;
        float centerX = combinedBounds.center.x;
        float centerZ = combinedBounds.center.z;

        Debug.Log($"Tree bounds - Bottom Y: {bottomY}, Center X: {centerX}, Center Z: {centerZ}");

        // Create a wrapper that offsets the tree so its base is at origin
        GameObject wrapper = new GameObject("OakTree_Grounded");
        instance.transform.SetParent(wrapper.transform);

        // Offset the tree so bottom is at Y=0 and centered on X/Z
        instance.transform.localPosition = new Vector3(-centerX, -bottomY, -centerZ);

        // Delete old prefab if it exists
        string prefabPath = textureFolder + "/OakTree_Grounded.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            AssetDatabase.DeleteAsset(prefabPath);
        }

        // Save as new prefab
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(wrapper, prefabPath);

        // Destroy the scene instance
        Object.DestroyImmediate(wrapper);

        Debug.Log("\n=== SETUP COMPLETE ===");
        Debug.Log("Prefab created at: " + prefabPath);
        Debug.Log($"Tree offset applied: Y={-bottomY}, X={-centerX}, Z={-centerZ}");
        Debug.Log("\nNEXT STEP:");
        Debug.Log("1. Select ChunkManager in scene");
        Debug.Log("2. Assign 'OakTree_Grounded' prefab to 'Oak Tree Prefab' field");
        Debug.Log("3. Set 'Tree Y Offset' to 0");
        Debug.Log("4. Enter Play mode to test!");

        // Select the prefab
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
    }
}
