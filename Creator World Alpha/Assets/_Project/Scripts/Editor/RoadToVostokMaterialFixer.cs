#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace CreatorWorld.Editor
{
    /// <summary>
    /// Fixes pink materials on Road to Vostok assets by:
    /// 1. Setting materials to URP/Lit shader
    /// 2. Auto-assigning TX_*.png textures to Base Map
    ///
    /// IMPORTANT: This script does NOT modify FBX files or their import settings,
    /// so scene object positions are preserved.
    /// </summary>
    public class RoadToVostokMaterialFixer : EditorWindow
    {
        private const string ASSET_PATH = "Assets/Road to Vostok Assets Vol.1";

        [MenuItem("Tools/Assets/Fix Road to Vostok Materials")]
        public static void FixAllMaterials()
        {
            if (!AssetDatabase.IsValidFolder(ASSET_PATH))
            {
                Debug.LogError($"[MaterialFixer] Could not find folder: {ASSET_PATH}");
                return;
            }

            // Find URP Lit shader
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                urpLit = Shader.Find("Universal Render Pipeline/Simple Lit");
            }
            if (urpLit == null)
            {
                Debug.LogError("[MaterialFixer] Could not find URP Lit shader! Make sure URP is installed.");
                return;
            }

            int fixedCount = 0;

            // Only fix existing .mat files - don't touch FBX importers
            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { ASSET_PATH });

            foreach (string matGuid in matGuids)
            {
                string matPath = AssetDatabase.GUIDToAssetPath(matGuid);

                // Skip materials embedded in FBX (only process standalone .mat files)
                if (!matPath.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null) continue;

                // Check if material is pink (missing shader or wrong shader)
                bool needsFix = mat.shader == null ||
                               mat.shader.name == "Hidden/InternalErrorShader" ||
                               mat.shader.name.Contains("Standard") ||
                               (!mat.shader.name.Contains("Universal") && !mat.shader.name.Contains("URP"));

                if (needsFix)
                {
                    // Find texture in the same or parent folder
                    string directory = Path.GetDirectoryName(matPath);

                    // Check parent folder too (materials might be in a "Materials" subfolder)
                    string parentDir = Path.GetDirectoryName(directory);
                    Texture2D texture = FindTextureInFolder(directory) ?? FindTextureInFolder(parentDir);

                    mat.shader = urpLit;

                    // Try to assign texture if missing
                    if (mat.GetTexture("_BaseMap") == null && texture != null)
                    {
                        mat.SetTexture("_BaseMap", texture);
                        mat.SetColor("_BaseColor", Color.white);
                    }

                    EditorUtility.SetDirty(mat);
                    fixedCount++;
                    Debug.Log($"[MaterialFixer] Fixed material: {matPath}" + (texture != null ? $" with texture {texture.name}" : ""));
                }
            }

            AssetDatabase.SaveAssets();
            // Don't call Refresh() - it can trigger reimports

            string message = fixedCount > 0
                ? $"Fixed {fixedCount} materials."
                : "No materials needed fixing.";
            Debug.Log($"[MaterialFixer] {message}");
            EditorUtility.DisplayDialog("Material Fix Complete", message, "OK");
        }

        private static Texture2D FindTextureInFolder(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !AssetDatabase.IsValidFolder(directory))
                return null;

            string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { directory });
            foreach (string guid in texGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileName(path);

                // Look for TX_*.png files (Road to Vostok naming convention)
                if (fileName.StartsWith("TX_") && path.EndsWith(".png"))
                {
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }

            return null;
        }

        private static Texture2D FindTextureForModel(string directory, string modelName)
        {
            // Road to Vostok naming: MS_Brick_Pile.fbx -> TX_Brick_Pile.png or TX_*.png

            // Try exact match first (replace MS_ with TX_)
            string baseName = modelName.Replace("MS_", "");
            string[] possibleNames = new[]
            {
                $"TX_{baseName}",
                $"TX{baseName}",
                baseName
            };

            foreach (string texName in possibleNames)
            {
                string texPath = Path.Combine(directory, texName + ".png");
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex != null) return tex;
            }

            // Fallback: find any TX_*.png in the folder
            return FindTextureInFolder(directory);
        }

        [MenuItem("Tools/Assets/Create ALL Road to Vostok Materials (Bulk)")]
        public static void CreateAllMaterialsBulk()
        {
            if (!AssetDatabase.IsValidFolder(ASSET_PATH))
            {
                Debug.LogError($"[MaterialFixer] Could not find folder: {ASSET_PATH}");
                return;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                urpLit = Shader.Find("Universal Render Pipeline/Simple Lit");
            }
            if (urpLit == null)
            {
                Debug.LogError("[MaterialFixer] Could not find URP Lit shader!");
                return;
            }

            int created = 0;
            int skipped = 0;

            // Find ALL FBX files in Road to Vostok folder
            string[] fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { ASSET_PATH });

            foreach (string guid in fbxGuids)
            {
                string fbxPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!fbxPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                string directory = Path.GetDirectoryName(fbxPath);
                string modelName = Path.GetFileNameWithoutExtension(fbxPath);

                // Find texture for this model
                Texture2D texture = FindTextureForModel(directory, modelName);

                // Create material in same folder as FBX (not a subfolder, simpler)
                string matName = modelName.Replace("MS_", "MAT_");
                string matPath = Path.Combine(directory, matName + ".mat");

                // Skip if material already exists
                if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null)
                {
                    skipped++;
                    continue;
                }

                // Create new URP material
                Material mat = new Material(urpLit);
                mat.name = matName;

                if (texture != null)
                {
                    mat.SetTexture("_BaseMap", texture);
                    mat.SetColor("_BaseColor", Color.white);
                }

                AssetDatabase.CreateAsset(mat, matPath);
                created++;

                Debug.Log($"[MaterialFixer] Created: {matPath}" + (texture != null ? $" with {texture.name}" : " (no texture)"));
            }

            AssetDatabase.SaveAssets();

            string message = $"Created {created} new materials.\nSkipped {skipped} (already exist).\n\nMaterials are in the same folder as each FBX.\nDrag them onto your scene objects.";
            Debug.Log($"[MaterialFixer] Bulk complete: {created} created, {skipped} skipped");
            EditorUtility.DisplayDialog("Bulk Material Creation Complete", message, "OK");
        }

        [MenuItem("Tools/Assets/Create URP Materials for Selected FBX (Safe)")]
        public static void CreateMaterialsForSelected()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("[MaterialFixer] Could not find URP Lit shader!");
                return;
            }

            int created = 0;
            foreach (Object obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                string directory = Path.GetDirectoryName(path);
                string modelName = Path.GetFileNameWithoutExtension(path);

                // Find texture
                Texture2D texture = FindTextureForModel(directory, modelName);

                // Create materials folder if needed (this won't cause reimports)
                string materialsFolder = Path.Combine(directory, "Materials");
                if (!AssetDatabase.IsValidFolder(materialsFolder))
                {
                    AssetDatabase.CreateFolder(directory, "Materials");
                }

                // Create material with a name based on the model
                string matName = modelName.Replace("MS_", "MAT_");
                string matPath = Path.Combine(materialsFolder, matName + ".mat");

                if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null)
                {
                    Debug.Log($"[MaterialFixer] Material already exists: {matPath}");
                    continue;
                }

                Material mat = new Material(urpLit);
                mat.name = matName;

                if (texture != null)
                {
                    mat.SetTexture("_BaseMap", texture);
                    mat.SetColor("_BaseColor", Color.white);
                    Debug.Log($"[MaterialFixer] Created material with texture: {matPath}");
                }
                else
                {
                    Debug.LogWarning($"[MaterialFixer] Created material without texture: {matPath}");
                }

                AssetDatabase.CreateAsset(mat, matPath);
                created++;
            }

            AssetDatabase.SaveAssets();
            // No Refresh() call - prevents reimports that could reset positions

            if (created > 0)
            {
                EditorUtility.DisplayDialog("Materials Created",
                    $"Created {created} materials.\n\nTo use: Select the FBX in scene, then drag the new material to the MeshRenderer.",
                    "OK");
            }
        }

        [MenuItem("Tools/Assets/Fix All Pink Materials in Project")]
        public static void FixAllPinkMaterials()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("[MaterialFixer] Could not find URP Lit shader!");
                return;
            }

            int fixedCount = 0;
            string[] matGuids = AssetDatabase.FindAssets("t:Material");

            foreach (string guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Skip packages and embedded materials
                if (path.StartsWith("Packages/")) continue;
                if (!path.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase)) continue;

                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                // Check if material has missing/error shader
                bool isPink = mat.shader == null ||
                             mat.shader.name == "Hidden/InternalErrorShader";

                if (isPink)
                {
                    mat.shader = urpLit;
                    EditorUtility.SetDirty(mat);
                    fixedCount++;
                    Debug.Log($"[MaterialFixer] Fixed pink material: {path}");
                }
            }

            AssetDatabase.SaveAssets();
            // No Refresh() - prevents reimports

            string message = fixedCount > 0
                ? $"Fixed {fixedCount} pink materials."
                : "No pink materials found.";
            Debug.Log($"[MaterialFixer] {message}");
            EditorUtility.DisplayDialog("Pink Material Fix", message, "OK");
        }

        /// <summary>
        /// Assigns a material to all selected scene objects without modifying FBX import settings.
        /// This preserves positions.
        /// </summary>
        [MenuItem("Tools/Assets/Assign Material to Selected Objects")]
        public static void AssignMaterialToSelected()
        {
            // Check if we have a material selected in Project view
            Material selectedMaterial = null;
            foreach (Object obj in Selection.objects)
            {
                if (obj is Material mat)
                {
                    selectedMaterial = mat;
                    break;
                }
            }

            if (selectedMaterial == null)
            {
                EditorUtility.DisplayDialog("No Material Selected",
                    "Please select a material in the Project view along with scene objects.",
                    "OK");
                return;
            }

            int assignedCount = 0;
            foreach (GameObject go in Selection.gameObjects)
            {
                // Get all renderers in the object and children
                Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
                foreach (Renderer renderer in renderers)
                {
                    // Record for undo
                    Undo.RecordObject(renderer, "Assign Material");

                    // Replace all materials
                    Material[] mats = renderer.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        mats[i] = selectedMaterial;
                    }
                    renderer.sharedMaterials = mats;
                    assignedCount++;
                }
            }

            if (assignedCount > 0)
            {
                Debug.Log($"[MaterialFixer] Assigned {selectedMaterial.name} to {assignedCount} renderers.");
            }
        }

        /// <summary>
        /// Auto-assigns materials to ALL scene objects that use Road to Vostok FBX models.
        /// Matches FBX name to material name (MS_Brick_Pile -> MAT_Brick_Pile).
        /// </summary>
        [MenuItem("Tools/Assets/Auto-Assign Road to Vostok Materials to Scene")]
        public static void AutoAssignMaterialsToScene()
        {
            int assigned = 0;
            int notFound = 0;

            // Find all MeshRenderers in the scene
            MeshRenderer[] renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            MeshFilter[] filters = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);

            // Build a dictionary of mesh filters for quick lookup
            Dictionary<GameObject, MeshFilter> filterDict = new Dictionary<GameObject, MeshFilter>();
            foreach (var filter in filters)
            {
                if (filter.sharedMesh != null)
                    filterDict[filter.gameObject] = filter;
            }

            foreach (MeshRenderer renderer in renderers)
            {
                // Get the mesh to find its source FBX
                if (!filterDict.TryGetValue(renderer.gameObject, out MeshFilter meshFilter))
                    continue;

                if (meshFilter.sharedMesh == null)
                    continue;

                // Get the asset path of the mesh
                string meshPath = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
                if (string.IsNullOrEmpty(meshPath) || !meshPath.Contains("Road to Vostok"))
                    continue;

                // Get the FBX name and find corresponding material
                string directory = Path.GetDirectoryName(meshPath);
                string fbxName = Path.GetFileNameWithoutExtension(meshPath);

                // Handle case where mesh name might differ from FBX name
                // Try to find FBX file in the directory
                string[] fbxFiles = System.IO.Directory.GetFiles(
                    Path.Combine(Application.dataPath.Replace("Assets", ""), directory),
                    "*.fbx",
                    System.IO.SearchOption.TopDirectoryOnly);

                string matName = null;
                string matPath = null;

                // First try: match from mesh path
                matName = fbxName.Replace("MS_", "MAT_");
                matPath = Path.Combine(directory, matName + ".mat");

                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

                // Second try: look for any MAT_*.mat in the folder
                if (mat == null)
                {
                    string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { directory });
                    foreach (string guid in matGuids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (Path.GetFileName(path).StartsWith("MAT_"))
                        {
                            mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                            break;
                        }
                    }
                }

                if (mat != null)
                {
                    // Check if already using correct material
                    bool alreadyAssigned = false;
                    foreach (var existingMat in renderer.sharedMaterials)
                    {
                        if (existingMat == mat)
                        {
                            alreadyAssigned = true;
                            break;
                        }
                    }

                    if (!alreadyAssigned)
                    {
                        Undo.RecordObject(renderer, "Auto-Assign Material");

                        Material[] mats = new Material[renderer.sharedMaterials.Length];
                        for (int i = 0; i < mats.Length; i++)
                        {
                            mats[i] = mat;
                        }
                        renderer.sharedMaterials = mats;
                        assigned++;

                        Debug.Log($"[MaterialFixer] Assigned {mat.name} to {renderer.gameObject.name}");
                    }
                }
                else
                {
                    notFound++;
                    Debug.LogWarning($"[MaterialFixer] No material found for {renderer.gameObject.name} (mesh: {fbxName})");
                }
            }

            string message = $"Assigned materials to {assigned} objects.\n{notFound} objects had no matching material.";
            Debug.Log($"[MaterialFixer] Auto-assign complete: {assigned} assigned, {notFound} not found");
            EditorUtility.DisplayDialog("Auto-Assign Complete", message, "OK");
        }
    }
}
#endif
