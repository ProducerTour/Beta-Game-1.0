using UnityEngine;
using UnityEditor;
using CreatorWorld.Combat;

namespace CreatorWorld.Editor
{
    /// <summary>
    /// Sets up weapon prefabs from actual 3D models.
    /// Replaces placeholder weapons with real models.
    /// </summary>
    public class WeaponPrefabSetup
    {
        private const string PREFAB_PATH = "Assets/_Project/Prefabs/Weapons";
        private const string MATERIALS_PATH = "Assets/_Project/Materials";

        // AK-47 paths
        private const string AK47_MODEL_PATH = "Assets/Art/Models/Weapons/AK-47/uploads_files_4656550_AK47.blend";
        private const string AK47_TEXTURE_PATH = "Assets/Art/Models/Weapons/AK-47/textures";
        private const string AK47_ICON_PATH = "Assets/Art/UI/Icons/Weapons/ak47.png";

        // Pistol icon
        private const string PISTOL_ICON_PATH = "Assets/Art/UI/Icons/Weapons/pistol.png";

        [MenuItem("Tools/Creator World/=== SETUP WEAPONS (Real Models) ===", priority = 5)]
        public static void SetupAllWeapons()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Error", "Stop Play mode first.", "OK");
                return;
            }

            Debug.Log("========== WEAPON SETUP START ==========");

            // Ensure folders exist
            EnsureFoldersExist();

            // Clean up old prefabs
            CleanupOldPrefabs();

            // Setup AK-47
            bool ak47Success = SetupAK47();

            // Setup Pistol (placeholder for now until we get a model)
            bool pistolSuccess = SetupPistolPlaceholder();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string message = "Weapon Setup Complete!\n\n";
            if (ak47Success)
                message += "✓ AK-47: Real 3D model with PBR textures\n";
            else
                message += "✗ AK-47: Using placeholder (Blender not found?)\n";

            if (pistolSuccess)
                message += "✓ Pistol: Placeholder (need model)\n";

            message += "\nRun 'SETUP PLAYER' to refresh the player.";

            Debug.Log("========== WEAPON SETUP COMPLETE ==========");
            EditorUtility.DisplayDialog("Weapon Setup", message, "OK");
        }

        [MenuItem("Tools/Creator World/Setup AK-47 Only")]
        public static void SetupAK47Only()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Error", "Stop Play mode first.", "OK");
                return;
            }

            EnsureFoldersExist();
            bool success = SetupAK47();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (success)
                EditorUtility.DisplayDialog("Success", "AK-47 prefab created with real model!", "OK");
            else
                EditorUtility.DisplayDialog("Warning", "Could not load AK-47 model. Check if Blender is installed.", "OK");
        }

        static void EnsureFoldersExist()
        {
            if (!AssetDatabase.IsValidFolder(MATERIALS_PATH))
                AssetDatabase.CreateFolder("Assets/_Project", "Materials");
            if (!AssetDatabase.IsValidFolder(PREFAB_PATH))
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Weapons");
        }

        static void CleanupOldPrefabs()
        {
            // Delete old prefabs to start fresh
            string[] oldPrefabs = {
                $"{PREFAB_PATH}/AK47.prefab",
                $"{PREFAB_PATH}/Pistol.prefab"
            };

            foreach (var path in oldPrefabs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                {
                    AssetDatabase.DeleteAsset(path);
                    Debug.Log($"Deleted old prefab: {path}");
                }
            }
        }

        static bool SetupAK47()
        {
            Debug.Log("Setting up AK-47...");

            // Try to load the model
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(AK47_MODEL_PATH);

            if (modelAsset == null)
            {
                Debug.LogWarning($"Could not load AK-47 model at: {AK47_MODEL_PATH}");
                Debug.LogWarning("Unity requires Blender to import .blend files. Creating placeholder instead.");
                return CreateAK47Placeholder();
            }

            // Create material with PBR textures
            Material material = CreateAK47Material();

            // Create prefab from model
            string prefabPath = $"{PREFAB_PATH}/AK47.prefab";

            // Instantiate the model
            GameObject ak47 = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            ak47.name = "AK47";

            // Apply material to all renderers
            if (material != null)
            {
                foreach (var renderer in ak47.GetComponentsInChildren<Renderer>())
                {
                    var materials = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++)
                    {
                        materials[i] = material;
                    }
                    renderer.sharedMaterials = materials;
                }
                Debug.Log("  Applied PBR material to model");
            }

            // Calculate bounds and scale appropriately
            var bounds = CalculateBounds(ak47);
            float maxDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);

            // Target size: ~0.8m for AK-47
            if (maxDimension > 0.01f && maxDimension != 0.8f)
            {
                float targetSize = 0.8f;
                float scale = targetSize / maxDimension;
                ak47.transform.localScale = Vector3.one * scale;
                Debug.Log($"  Scaled model: {maxDimension:F3}m -> {targetSize}m (scale: {scale:F3})");
            }

            // Create muzzle point
            var muzzleGO = new GameObject("MuzzlePoint");
            muzzleGO.transform.SetParent(ak47.transform);
            muzzleGO.transform.localPosition = new Vector3(0, 0.02f, 0.4f);

            // Remove colliders
            foreach (var col in ak47.GetComponentsInChildren<Collider>())
            {
                Object.DestroyImmediate(col);
            }

            // Add Rifle component
            var rifle = ak47.AddComponent<Rifle>();

            // Configure via SerializedObject
            var so = new SerializedObject(rifle);
            SetProperty(so, "weaponName", "AK-47");
            SetProperty(so, "muzzlePoint", muzzleGO.transform);

            // Load and set icon
            var icon = AssetDatabase.LoadAssetAtPath<Sprite>(AK47_ICON_PATH);
            if (icon != null)
            {
                SetProperty(so, "weaponIcon", icon);
                Debug.Log("  Set weapon icon");
            }
            so.ApplyModifiedProperties();

            // Add AudioSource
            var audioSource = ak47.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;

            // Add WeaponAlignment for grip positioning
            var alignment = ak47.AddComponent<WeaponAlignment>();
            alignment.gripPosition = new Vector3(-0.170f, -0.150f, 0.020f);
            alignment.gripRotation = new Vector3(105.0f, 85.0f, 5.0f);

            // Create left hand target for two-handed grip
            var leftHandTarget = new GameObject("LeftHandTarget");
            leftHandTarget.transform.SetParent(ak47.transform);
            leftHandTarget.transform.localPosition = new Vector3(-0.05f, 0.03f, 0.25f);
            alignment.leftHandTarget = leftHandTarget.transform;

            // Make sure it starts inactive (weapon inventory will activate it)
            ak47.SetActive(false);

            // Save prefab
            PrefabUtility.SaveAsPrefabAsset(ak47, prefabPath);
            Object.DestroyImmediate(ak47);

            Debug.Log($"  Created AK-47 prefab: {prefabPath}");
            return true;
        }

        static Material CreateAK47Material()
        {
            string matPath = $"{MATERIALS_PATH}/AK47_Material.mat";

            // Delete old material to recreate fresh
            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null)
            {
                AssetDatabase.DeleteAsset(matPath);
            }

            // Create new URP material
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
                Debug.LogWarning("URP shader not found, using Standard");
            }

            Material mat = new Material(shader);

            // Load textures
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{AK47_TEXTURE_PATH}/AK_Base_color.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{AK47_TEXTURE_PATH}/AK_Normal_OpenGL.png");
            var metallic = AssetDatabase.LoadAssetAtPath<Texture2D>($"{AK47_TEXTURE_PATH}/AK_Metallic.png");
            var ao = AssetDatabase.LoadAssetAtPath<Texture2D>($"{AK47_TEXTURE_PATH}/AK_Mixed_AO.png");

            // Apply textures
            if (albedo != null)
            {
                mat.SetTexture("_BaseMap", albedo);
                mat.mainTexture = albedo;
                Debug.Log("    Albedo texture applied");
            }

            if (normal != null)
            {
                // Ensure normal map import settings
                string normalPath = AssetDatabase.GetAssetPath(normal);
                var importer = AssetImporter.GetAtPath(normalPath) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                }
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
                Debug.Log("    Normal texture applied");
            }

            if (metallic != null)
            {
                mat.SetTexture("_MetallicGlossMap", metallic);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                Debug.Log("    Metallic texture applied");
            }

            if (ao != null)
            {
                mat.SetTexture("_OcclusionMap", ao);
                Debug.Log("    AO texture applied");
            }

            // Set metallic/smoothness
            mat.SetFloat("_Metallic", 0.8f);
            mat.SetFloat("_Smoothness", 0.4f);

            AssetDatabase.CreateAsset(mat, matPath);
            Debug.Log($"  Created material: {matPath}");

            return mat;
        }

        static bool CreateAK47Placeholder()
        {
            // Fallback placeholder if model can't be loaded
            string prefabPath = $"{PREFAB_PATH}/AK47.prefab";

            var rifle = new GameObject("AK47");

            // Simple box shape
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(rifle.transform);
            body.transform.localScale = new Vector3(0.05f, 0.08f, 0.8f);
            body.transform.localPosition = new Vector3(0, 0, 0.3f);

            // URP material
            var mat = CreateSimpleMaterial("AK47_Placeholder", new Color(0.4f, 0.3f, 0.2f));
            body.GetComponent<Renderer>().sharedMaterial = mat;

            // Muzzle
            var muzzle = new GameObject("MuzzlePoint");
            muzzle.transform.SetParent(rifle.transform);
            muzzle.transform.localPosition = new Vector3(0, 0.02f, 0.7f);

            // Remove colliders
            foreach (var col in rifle.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(col);

            // Add components
            var rifleComp = rifle.AddComponent<Rifle>();
            var so = new SerializedObject(rifleComp);
            SetProperty(so, "weaponName", "AK-47");
            SetProperty(so, "muzzlePoint", muzzle.transform);
            so.ApplyModifiedProperties();

            rifle.AddComponent<AudioSource>().playOnAwake = false;

            // Add WeaponAlignment
            var alignment = rifle.AddComponent<WeaponAlignment>();
            alignment.gripPosition = new Vector3(-0.170f, -0.150f, 0.020f);
            alignment.gripRotation = new Vector3(105.0f, 85.0f, 5.0f);

            rifle.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(rifle, prefabPath);
            Object.DestroyImmediate(rifle);

            Debug.Log($"  Created AK-47 placeholder: {prefabPath}");
            return false; // Return false to indicate it's not the real model
        }

        static bool SetupPistolPlaceholder()
        {
            Debug.Log("Setting up Pistol (placeholder)...");

            string prefabPath = $"{PREFAB_PATH}/Pistol.prefab";
            var pistol = new GameObject("Pistol");

            // Body
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(pistol.transform);
            body.transform.localScale = new Vector3(0.03f, 0.1f, 0.15f);
            body.transform.localPosition = new Vector3(0, 0, 0.05f);

            var mat = CreateSimpleMaterial("Pistol_Material", new Color(0.15f, 0.15f, 0.15f));
            body.GetComponent<Renderer>().sharedMaterial = mat;

            // Grip
            var grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            grip.name = "Grip";
            grip.transform.SetParent(pistol.transform);
            grip.transform.localScale = new Vector3(0.025f, 0.08f, 0.04f);
            grip.transform.localPosition = new Vector3(0, -0.07f, -0.02f);
            grip.GetComponent<Renderer>().sharedMaterial = mat;

            // Muzzle
            var muzzle = new GameObject("MuzzlePoint");
            muzzle.transform.SetParent(pistol.transform);
            muzzle.transform.localPosition = new Vector3(0, 0.02f, 0.12f);

            // Remove colliders
            foreach (var col in pistol.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(col);

            // Add components
            var pistolComp = pistol.AddComponent<Pistol>();
            var so = new SerializedObject(pistolComp);
            SetProperty(so, "weaponName", "Pistol");
            SetProperty(so, "muzzlePoint", muzzle.transform);

            var icon = AssetDatabase.LoadAssetAtPath<Sprite>(PISTOL_ICON_PATH);
            if (icon != null)
                SetProperty(so, "weaponIcon", icon);
            so.ApplyModifiedProperties();

            pistol.AddComponent<AudioSource>().playOnAwake = false;

            // Add WeaponAlignment
            var alignment = pistol.AddComponent<WeaponAlignment>();
            alignment.gripPosition = new Vector3(0.02f, 0.01f, 0.05f);
            alignment.gripRotation = new Vector3(0, 90, 0);

            pistol.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(pistol, prefabPath);
            Object.DestroyImmediate(pistol);

            Debug.Log($"  Created Pistol placeholder: {prefabPath}");
            return true;
        }

        static Material CreateSimpleMaterial(string name, Color color)
        {
            string matPath = $"{MATERIALS_PATH}/{name}.mat";

            // Delete old if exists
            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null)
                AssetDatabase.DeleteAsset(matPath);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            var mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            mat.color = color;

            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        static Bounds CalculateBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.one * 0.1f);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds;
        }

        static void SetProperty(SerializedObject so, string name, object value)
        {
            var prop = so.FindProperty(name);
            if (prop == null) return;

            if (value is string s)
                prop.stringValue = s;
            else if (value is Object obj)
                prop.objectReferenceValue = obj;
            else if (value is int i)
                prop.intValue = i;
            else if (value is float f)
                prop.floatValue = f;
            else if (value is bool b)
                prop.boolValue = b;
        }

        [MenuItem("Tools/Creator World/Cleanup Old Weapon Files")]
        public static void CleanupOldFiles()
        {
            // Delete old placeholder script (we're replacing it)
            string oldScript = "Assets/_Project/Scripts/Editor/CreatePlaceholderWeapons.cs";
            if (AssetDatabase.LoadAssetAtPath<MonoScript>(oldScript) != null)
            {
                AssetDatabase.DeleteAsset(oldScript);
                Debug.Log("Deleted old CreatePlaceholderWeapons.cs");
            }

            // Delete old materials
            string[] oldMaterials = {
                "Assets/_Project/Materials/RifleMaterial.mat",
                "Assets/_Project/Materials/PistolMaterial.mat"
            };

            foreach (var mat in oldMaterials)
            {
                if (AssetDatabase.LoadAssetAtPath<Material>(mat) != null)
                {
                    AssetDatabase.DeleteAsset(mat);
                    Debug.Log($"Deleted old material: {mat}");
                }
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Cleanup Complete", "Old weapon files removed.", "OK");
        }
    }
}
