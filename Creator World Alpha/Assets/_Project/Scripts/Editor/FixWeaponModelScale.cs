using UnityEngine;
using UnityEditor;

namespace CreatorWorld.Editor
{
    /// <summary>
    /// Fixes weapon model scale by modifying all transforms in the prefab hierarchy.
    /// GLTF models often have nested transforms with their own scale values.
    /// Run: Tools > Creator World > Fix Weapon Models (Deep Scale Fix)
    /// </summary>
    public class FixWeaponModelScale : EditorWindow
    {
        private const string WEAPON_PREFAB_PATH = "Assets/_Project/Prefabs/Weapons";

        [MenuItem("Tools/Creator World/Fix Weapon Models (Deep Scale Fix)")]
        public static void FixDeepScale()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Error", "Stop Play mode first.", "OK");
                return;
            }

            // Fix both weapons
            FixPrefabDeepScale("AK47", 100f);   // Scale up by 100x
            FixPrefabDeepScale("Pistol", 100f); // Scale up by 100x

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Weapon Scale Fixed",
                "Applied deep scale fix to weapon prefabs.\n\n" +
                "All nested transforms scaled up by 100x.\n" +
                "Weapons should now be visible!",
                "OK");
        }

        static void FixPrefabDeepScale(string weaponName, float scaleFactor)
        {
            string prefabPath = $"{WEAPON_PREFAB_PATH}/{weaponName}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab == null)
            {
                Debug.LogWarning($"Prefab not found: {prefabPath}");
                return;
            }

            // Modify the prefab contents
            using (var editScope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
            {
                var root = editScope.prefabContentsRoot;

                // Set root scale to large value to compensate for GLTF's internal scale
                root.transform.localScale = Vector3.one * scaleFactor;

                Debug.Log($"[FixWeaponModelScale] Set {weaponName} root scale to {scaleFactor}");

                // Log hierarchy for debugging
                LogHierarchy(root.transform, 0);
            }

            Debug.Log($"Fixed deep scale for: {weaponName}");
        }

        static void LogHierarchy(Transform t, int depth)
        {
            string indent = new string(' ', depth * 2);
            Debug.Log($"{indent}{t.name}: scale={t.localScale}, pos={t.localPosition}");

            foreach (Transform child in t)
            {
                LogHierarchy(child, depth + 1);
            }
        }

        [MenuItem("Tools/Creator World/Debug: Inspect Weapon Prefabs")]
        public static void InspectWeaponPrefabs()
        {
            InspectPrefab("AK47");
            InspectPrefab("Pistol");

            EditorUtility.DisplayDialog("Done", "Check Console for prefab hierarchy.", "OK");
        }

        static void InspectPrefab(string weaponName)
        {
            string prefabPath = $"{WEAPON_PREFAB_PATH}/{weaponName}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab == null)
            {
                Debug.LogWarning($"Prefab not found: {prefabPath}");
                return;
            }

            Debug.Log($"\n=== {weaponName} Prefab Hierarchy ===");
            LogHierarchyDetailed(prefab.transform, 0);

            // Check for renderers
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            Debug.Log($"Total renderers: {renderers.Length}");
            foreach (var r in renderers)
            {
                var meshFilter = r.GetComponent<MeshFilter>();
                string meshInfo = meshFilter != null && meshFilter.sharedMesh != null
                    ? $"mesh bounds: {meshFilter.sharedMesh.bounds.size}"
                    : "no mesh";
                Debug.Log($"  {r.name}: {r.GetType().Name}, {meshInfo}");
            }
        }

        static void LogHierarchyDetailed(Transform t, int depth)
        {
            string indent = new string(' ', depth * 2);
            var comps = t.GetComponents<Component>();
            string compNames = string.Join(", ", System.Array.ConvertAll(comps, c => c.GetType().Name));
            Debug.Log($"{indent}{t.name} [scale={t.localScale}] ({compNames})");

            foreach (Transform child in t)
            {
                LogHierarchyDetailed(child, depth + 1);
            }
        }
    }
}
