using UnityEngine;
using UnityEditor;

/// <summary>
/// Analyzes tree prefabs to determine the correct Y offset for ground placement.
/// The offset is the distance from the prefab's origin to the bottom of its geometry.
/// </summary>
public class TreePrefabAnalyzer : EditorWindow
{
    [MenuItem("Tools/Analyze Tree Prefab Offset")]
    public static void AnalyzeTreePrefab()
    {
        // Try prefab first, then FBX
        string[] possiblePaths = {
            "Assets/Art/Models/Environment/Trees/OakTreeTextured.prefab",
            "Assets/Art/Models/Environment/Trees/OakTree_Grounded.prefab",
            "Assets/Art/Models/Environment/Trees/OakTree.fbx"
        };

        GameObject prefab = null;
        string foundPath = null;

        foreach (var path in possiblePaths)
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                foundPath = path;
                break;
            }
        }

        if (prefab == null)
        {
            Debug.LogError("Could not find any tree prefab or FBX. Tried:\n" + string.Join("\n", possiblePaths));
            return;
        }

        Debug.Log($"Found tree at: {foundPath}");

        // Calculate bounds of all renderers
        Bounds combinedBounds = new Bounds(Vector3.zero, Vector3.zero);
        bool boundsInitialized = false;

        var renderers = prefab.GetComponentsInChildren<Renderer>(true);
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

        if (!boundsInitialized)
        {
            Debug.LogError("No renderers found in prefab!");
            return;
        }

        // The offset needed is the distance from prefab origin (0,0,0) to the bottom of the bounds
        float bottomY = combinedBounds.min.y;
        float topY = combinedBounds.max.y;
        float centerY = combinedBounds.center.y;
        float height = combinedBounds.size.y;

        Debug.Log("=== TREE PREFAB ANALYSIS ===");
        Debug.Log($"Prefab: {foundPath}");
        Debug.Log($"Combined Bounds:");
        Debug.Log($"  Center: {combinedBounds.center}");
        Debug.Log($"  Size: {combinedBounds.size}");
        Debug.Log($"  Min Y (bottom): {bottomY}");
        Debug.Log($"  Max Y (top): {topY}");
        Debug.Log($"  Total Height: {height}");
        Debug.Log("");
        Debug.Log("=== RECOMMENDED OFFSET ===");

        // If bottom Y is negative, the tree base is below the origin - we need positive offset
        // If bottom Y is positive, the tree base is above the origin - we need negative offset
        float recommendedOffset = -bottomY;

        Debug.Log($"Tree Y Offset should be: {recommendedOffset}");
        Debug.Log("");
        Debug.Log("To apply this:");
        Debug.Log("1. Select ChunkManager in scene");
        Debug.Log($"2. Set 'Tree Y Offset' to {recommendedOffset}");

        // Also check individual meshes
        Debug.Log("");
        Debug.Log("=== INDIVIDUAL MESH DETAILS ===");
        foreach (var renderer in renderers)
        {
            Debug.Log($"  {renderer.gameObject.name}:");
            Debug.Log($"    Local Position: {renderer.transform.localPosition}");
            Debug.Log($"    Bounds Min Y: {renderer.bounds.min.y}");
            Debug.Log($"    Bounds Max Y: {renderer.bounds.max.y}");
        }
    }

    [MenuItem("Tools/Fix Tree Prefab Pivot")]
    public static void FixTreePrefabPivot()
    {
        // Try prefab first, then FBX
        string[] possiblePaths = {
            "Assets/Art/Models/Environment/Trees/OakTreeTextured.prefab",
            "Assets/Art/Models/Environment/Trees/OakTree.fbx"
        };

        GameObject prefab = null;
        string foundPath = null;

        foreach (var path in possiblePaths)
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                foundPath = path;
                break;
            }
        }

        if (prefab == null)
        {
            Debug.LogError("Could not find any tree prefab or FBX. Tried:\n" + string.Join("\n", possiblePaths));
            Debug.LogError("Make sure OakTree.fbx exists in Assets/Art/Models/Environment/Trees/");
            return;
        }

        Debug.Log($"Using tree from: {foundPath}");

        // Calculate the bottom of all geometry
        Bounds combinedBounds = new Bounds(Vector3.zero, Vector3.zero);
        bool boundsInitialized = false;

        var renderers = prefab.GetComponentsInChildren<Renderer>(true);
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

        if (!boundsInitialized)
        {
            Debug.LogError("No renderers found in prefab!");
            return;
        }

        float bottomY = combinedBounds.min.y;
        float centerX = combinedBounds.center.x;
        float centerZ = combinedBounds.center.z;

        Debug.Log($"Current bottom Y: {bottomY}");
        Debug.Log($"Current center X: {centerX}, Z: {centerZ}");

        // Create a wrapper object approach
        // We'll create a new prefab with an empty parent that offsets the tree
        string wrapperPrefabPath = "Assets/Art/Models/Environment/Trees/OakTree_Grounded.prefab";

        // Instantiate the original prefab
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        // Create a new empty parent
        GameObject wrapper = new GameObject("OakTree_Grounded");

        // Parent the tree to the wrapper
        instance.transform.SetParent(wrapper.transform);

        // Offset the tree so its bottom is at Y=0
        // Also center it on X/Z
        instance.transform.localPosition = new Vector3(-centerX, -bottomY, -centerZ);

        // Delete old wrapper prefab if exists
        if (AssetDatabase.LoadAssetAtPath<GameObject>(wrapperPrefabPath) != null)
        {
            AssetDatabase.DeleteAsset(wrapperPrefabPath);
        }

        // Save as new prefab
        GameObject newPrefab = PrefabUtility.SaveAsPrefabAsset(wrapper, wrapperPrefabPath);

        // Clean up
        Object.DestroyImmediate(wrapper);

        Debug.Log("=== FIXED PREFAB CREATED ===");
        Debug.Log($"New prefab saved to: {wrapperPrefabPath}");
        Debug.Log("The tree's base is now at Y=0 and centered on X/Z");
        Debug.Log("");
        Debug.Log("To use this:");
        Debug.Log("1. Select ChunkManager in scene");
        Debug.Log("2. Assign 'OakTree_Grounded' prefab to 'Oak Tree Prefab' field");
        Debug.Log("3. Set 'Tree Y Offset' to 0");

        // Select the new prefab
        Selection.activeObject = newPrefab;
        EditorGUIUtility.PingObject(newPrefab);
    }
}
