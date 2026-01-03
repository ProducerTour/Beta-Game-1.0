using UnityEngine;
using UnityEditor;

/// <summary>
/// Creates simple rock prefabs using Unity primitives as a fallback.
/// Use this if GLB import isn't working.
/// </summary>
public class SimpleRockPrefabCreator : EditorWindow
{
    [MenuItem("Tools/Create Simple Rock Prefabs (Fallback)")]
    public static void CreateSimpleRocks()
    {
        string prefabsFolder = "Assets/Art/Models/Environment/Rocks/Prefabs";

        // Create folder if needed
        if (!AssetDatabase.IsValidFolder(prefabsFolder))
        {
            AssetDatabase.CreateFolder("Assets/Art/Models/Environment/Rocks", "Prefabs");
        }

        // Find URP Lit shader
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("URP Lit shader not found!");
            return;
        }

        // Create rock material
        string matPath = "Assets/Art/Models/Environment/Rocks/Materials/SimpleRock_Mat.mat";
        Material rockMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (rockMat == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art/Models/Environment/Rocks/Materials"))
            {
                AssetDatabase.CreateFolder("Assets/Art/Models/Environment/Rocks", "Materials");
            }
            rockMat = new Material(urpLit);
            rockMat.SetColor("_BaseColor", new Color(0.4f, 0.38f, 0.35f)); // Gray-brown rock
            rockMat.SetFloat("_Smoothness", 0.15f); // Rough
            AssetDatabase.CreateAsset(rockMat, matPath);
        }

        // Create 3 rock variations
        CreateRockPrefab("Rock_Small", rockMat, prefabsFolder, new Vector3(0.8f, 0.5f, 0.7f));
        CreateRockPrefab("Rock_Medium", rockMat, prefabsFolder, new Vector3(1.5f, 0.9f, 1.3f));
        CreateRockPrefab("Rock_Large", rockMat, prefabsFolder, new Vector3(2.5f, 1.5f, 2.2f));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("=== SIMPLE ROCK PREFABS CREATED ===");
        Debug.Log("Created 3 rock prefabs in: " + prefabsFolder);
        Debug.Log("\nNow assign them to ChunkManager's Rock Prefabs array");
    }

    private static void CreateRockPrefab(string name, Material material, string folder, Vector3 scale)
    {
        // Create a flattened sphere to look more rock-like
        GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rock.name = name;
        rock.transform.localScale = scale;

        // Apply material
        rock.GetComponent<MeshRenderer>().sharedMaterial = material;

        // Remove collider (we don't want rocks to have collision for now)
        Object.DestroyImmediate(rock.GetComponent<Collider>());

        // Save as prefab
        string prefabPath = folder + "/" + name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            AssetDatabase.DeleteAsset(prefabPath);
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(rock, prefabPath);
        Object.DestroyImmediate(rock);

        Debug.Log($"Created: {prefabPath}");

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
    }
}
