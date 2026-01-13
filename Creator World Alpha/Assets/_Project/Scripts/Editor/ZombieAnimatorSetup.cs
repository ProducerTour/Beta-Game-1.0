#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

namespace CreatorWorld.Editor
{
    /// <summary>
    /// Editor tool to create a clean, minimal zombie animator controller.
    /// States: Locomotion (blend tree), Attack, Death
    /// </summary>
    public static class ZombieAnimatorSetup
    {
        private const string ZOMBIE_ANIM_PATH = "Assets/Art/Models/NPCs/Zombies";
        private const string OUTPUT_PATH = "Assets/_Project/Settings";

        [MenuItem("Tools/Enemy/Create Zombie Animator Controller")]
        public static void CreateZombieAnimatorController()
        {
            string controllerPath = $"{OUTPUT_PATH}/ZombieAnimator.controller";

            if (File.Exists(controllerPath))
            {
                if (!EditorUtility.DisplayDialog("Overwrite?",
                    "ZombieAnimator.controller already exists. Overwrite?", "Yes", "No"))
                    return;
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            // Minimal parameters - only what we need
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("IsAttacking", AnimatorControllerParameterType.Bool);

            var rootStateMachine = controller.layers[0].stateMachine;

            // Find animation clips
            AnimationClip idleClip = FindAnimationClip("zombie idle");
            AnimationClip walkClip = FindAnimationClip("zombie walk");
            AnimationClip runClip = FindAnimationClip("zombie run");
            AnimationClip attackClip = FindAnimationClip("zombie attack");
            AnimationClip deathClip = FindAnimationClip("zombie death");

            // Create locomotion blend tree (the core state)
            var locomotionState = controller.CreateBlendTreeInController("Locomotion", out BlendTree locomotionTree);
            locomotionTree.blendType = BlendTreeType.Simple1D;
            locomotionTree.blendParameter = "Speed";
            locomotionTree.useAutomaticThresholds = false;

            if (idleClip != null)
                locomotionTree.AddChild(idleClip, 0f);
            else
                Debug.LogWarning("[ZombieAnimatorSetup] zombie idle animation not found");

            if (walkClip != null)
                locomotionTree.AddChild(walkClip, 1.5f);  // Lower threshold - walk shows at slower speeds
            else
                Debug.LogWarning("[ZombieAnimatorSetup] zombie walk animation not found");

            if (runClip != null)
                locomotionTree.AddChild(runClip, 4f);  // Lower threshold - matches chase speed of 4.5
            else
                Debug.LogWarning("[ZombieAnimatorSetup] zombie run animation not found");

            // CRITICAL: Set blend tree threshold bounds explicitly
            locomotionTree.minThreshold = 0f;
            locomotionTree.maxThreshold = 4f;

            rootStateMachine.defaultState = locomotionState;

            // Create attack state with proper transitions
            if (attackClip != null)
            {
                var attackState = rootStateMachine.AddState("Attack");
                attackState.motion = attackClip;

                // Locomotion -> Attack (trigger)
                var toAttack = locomotionState.AddTransition(attackState);
                toAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
                toAttack.hasExitTime = false;
                toAttack.duration = 0.15f;

                // Attack -> Locomotion (after animation completes)
                var fromAttack = attackState.AddTransition(locomotionState);
                fromAttack.hasExitTime = true;
                fromAttack.exitTime = 0.85f;
                fromAttack.duration = 0.15f;

                Debug.Log("[ZombieAnimatorSetup] Attack state created with transitions");
            }
            else
            {
                Debug.LogWarning("[ZombieAnimatorSetup] zombie attack animation not found - no attack state created");
            }

            // Create death state (from any state, no exit)
            if (deathClip != null)
            {
                var deathState = rootStateMachine.AddState("Death");
                deathState.motion = deathClip;

                // Any State -> Death
                var toDeath = rootStateMachine.AddAnyStateTransition(deathState);
                toDeath.AddCondition(AnimatorConditionMode.If, 0, "Death");
                toDeath.hasExitTime = false;
                toDeath.duration = 0.1f;
                toDeath.canTransitionToSelf = false;

                Debug.Log("[ZombieAnimatorSetup] Death state created");
            }
            else
            {
                Debug.LogWarning("[ZombieAnimatorSetup] zombie death animation not found - no death state created");
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ZombieAnimatorSetup] Created clean ZombieAnimator.controller at {controllerPath}");
            Debug.Log("[ZombieAnimatorSetup] States: Locomotion (blend tree), Attack, Death");

            Selection.activeObject = controller;
        }

        private static AnimationClip FindAnimationClip(string clipName)
        {
            // Try direct file path first (e.g., "zombie idle.fbx")
            string directPath = $"{ZOMBIE_ANIM_PATH}/{clipName}.fbx";
            if (File.Exists(directPath))
            {
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(directPath);
                foreach (Object asset in assets)
                {
                    if (asset is AnimationClip clip && !clip.name.Contains("__preview__"))
                    {
                        Debug.Log($"[ZombieAnimatorSetup] Found animation: {clip.name} from {directPath}");
                        return clip;
                    }
                }
            }

            // Search for FBX files containing the clip name
            string[] allFbxGuids = AssetDatabase.FindAssets("t:Model", new[] { ZOMBIE_ANIM_PATH });
            foreach (string guid in allFbxGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path).ToLower();

                // Check if file name matches what we're looking for
                if (fileName.Contains(clipName.ToLower().Replace(" ", "")) ||
                    fileName.Replace(" ", "").Contains(clipName.ToLower().Replace(" ", "")))
                {
                    Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                    foreach (Object asset in assets)
                    {
                        if (asset is AnimationClip clip && !clip.name.Contains("__preview__"))
                        {
                            Debug.Log($"[ZombieAnimatorSetup] Found animation: {clip.name} from {path}");
                            return clip;
                        }
                    }
                }
            }

            // Last resort: search by asset name
            string[] guids = AssetDatabase.FindAssets($"{clipName}", new[] { ZOMBIE_ANIM_PATH });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                {
                    Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                    foreach (Object asset in assets)
                    {
                        if (asset is AnimationClip clip && !clip.name.Contains("__preview__"))
                        {
                            Debug.Log($"[ZombieAnimatorSetup] Found animation: {clip.name} from {path}");
                            return clip;
                        }
                    }
                }
            }

            Debug.LogWarning($"[ZombieAnimatorSetup] Could not find animation: {clipName}");
            return null;
        }

        [MenuItem("Tools/Enemy/Setup Zombie Prefab from Selection")]
        public static void SetupZombiePrefabFromSelection()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Setup Zombie",
                    "Please select a zombie model in the Project window or a zombie instance in the Scene.", "OK");
                return;
            }

            // Check if this is a prefab asset or scene instance
            bool isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(selected);

            if (isPrefabAsset)
            {
                // Create an instance in scene to work with
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(selected);
                SetupZombieInstance(instance);

                // Save as new prefab variant
                string prefabPath = $"Assets/_Project/Prefabs/Enemies/EnemyZombie.prefab";

                // Ensure directory exists
                string directory = Path.GetDirectoryName(prefabPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                Object.DestroyImmediate(instance);

                Debug.Log($"[ZombieAnimatorSetup] Created zombie prefab at {prefabPath}");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }
            else
            {
                // Working with scene instance
                Undo.RegisterCompleteObjectUndo(selected, "Setup Zombie Prefab");
                SetupZombieInstance(selected);
                EditorUtility.SetDirty(selected);
            }
        }

        private static void SetupZombieInstance(GameObject zombie)
        {
            // Add CharacterController if missing
            var cc = zombie.GetComponent<CharacterController>();
            if (cc == null)
            {
                cc = zombie.AddComponent<CharacterController>();
                cc.height = 1.8f;
                cc.radius = 0.3f;
                cc.center = new Vector3(0, 0.9f, 0);
            }

            // Add EnemyHealth if missing
            var health = zombie.GetComponent<Enemy.EnemyHealth>();
            if (health == null)
            {
                health = zombie.AddComponent<Enemy.EnemyHealth>();
            }

            // Add EnemyAnimation if missing
            var animation = zombie.GetComponent<Enemy.EnemyAnimation>();
            if (animation == null)
            {
                animation = zombie.AddComponent<Enemy.EnemyAnimation>();
            }

            // Add EnemyAI if missing
            var ai = zombie.GetComponent<Enemy.EnemyAI>();
            if (ai == null)
            {
                ai = zombie.AddComponent<Enemy.EnemyAI>();
            }

            // Setup Animator
            var animator = zombie.GetComponent<Animator>();
            if (animator == null)
            {
                animator = zombie.GetComponentInChildren<Animator>();
            }

            if (animator != null)
            {
                // Try to assign the zombie animator controller
                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    $"{OUTPUT_PATH}/ZombieAnimator.controller");

                if (controller != null)
                {
                    animator.runtimeAnimatorController = controller;
                    Debug.Log("[ZombieAnimatorSetup] Assigned ZombieAnimator controller");
                }
                else
                {
                    Debug.LogWarning("[ZombieAnimatorSetup] ZombieAnimator.controller not found. " +
                        "Run 'Tools > Enemy > Create Zombie Animator Controller' first.");
                }
            }

            // Set layer to Enemy
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
            {
                zombie.layer = enemyLayer;
                SetLayerRecursively(zombie, enemyLayer);
            }

            // Add tag
            zombie.tag = "Enemy";

            Debug.Log($"[ZombieAnimatorSetup] Setup complete for {zombie.name}");
        }

        private static void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        [MenuItem("Tools/Enemy/Configure Selected FBX as Humanoid")]
        public static void ConfigureFBXAsHumanoid()
        {
            var selected = Selection.activeObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Configure Humanoid",
                    "Please select an FBX file in the Project window.", "OK");
                return;
            }

            string path = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Configure Humanoid",
                    "Selected asset is not an FBX file.", "OK");
                return;
            }

            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[ZombieAnimatorSetup] Could not get ModelImporter for " + path);
                return;
            }

            // Configure as Humanoid
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            // Apply changes
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            Debug.Log($"[ZombieAnimatorSetup] Configured {Path.GetFileName(path)} as Humanoid");
        }

        [MenuItem("Tools/Enemy/Configure All Zombie FBX as Humanoid")]
        public static void ConfigureAllZombieFBXAsHumanoid()
        {
            // First, ensure the main Zombie.fbx is configured as Humanoid and has an avatar
            string zombieModelPath = $"{ZOMBIE_ANIM_PATH}/Zombie.fbx";

            Debug.Log($"[ZombieAnimatorSetup] Looking for Zombie.fbx at: {zombieModelPath}");

            ModelImporter mainImporter = AssetImporter.GetAtPath(zombieModelPath) as ModelImporter;

            if (mainImporter == null)
            {
                Debug.LogError($"[ZombieAnimatorSetup] Could not find Zombie.fbx at {zombieModelPath}! Check the path.");
                return;
            }

            // Always reconfigure Zombie.fbx to ensure avatar exists
            Debug.Log($"[ZombieAnimatorSetup] Configuring main Zombie.fbx as Humanoid (current type: {mainImporter.animationType})...");
            mainImporter.animationType = ModelImporterAnimationType.Human;
            mainImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            // Write import settings to .meta file
            EditorUtility.SetDirty(mainImporter);
            AssetDatabase.WriteImportSettingsIfDirty(zombieModelPath);

            // Force reimport to apply changes and generate avatar
            AssetDatabase.ImportAsset(zombieModelPath, ImportAssetOptions.ForceUpdate);

            // Refresh to ensure avatar is available
            AssetDatabase.Refresh();

            // Load the avatar from Zombie.fbx
            Avatar zombieAvatar = null;
            Object[] zombieAssets = AssetDatabase.LoadAllAssetsAtPath(zombieModelPath);
            Debug.Log($"[ZombieAnimatorSetup] Found {zombieAssets.Length} assets in Zombie.fbx");

            foreach (Object asset in zombieAssets)
            {
                Debug.Log($"[ZombieAnimatorSetup] Asset: {asset.name} ({asset.GetType().Name})");
                if (asset is Avatar avatar)
                {
                    zombieAvatar = avatar;
                    Debug.Log($"[ZombieAnimatorSetup] Found avatar: {avatar.name}");
                    break;
                }
            }

            if (zombieAvatar == null)
            {
                Debug.LogError("[ZombieAnimatorSetup] Could not find Avatar in Zombie.fbx after configuration! The model may not have a valid humanoid skeleton.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { ZOMBIE_ANIM_PATH });
            int configured = 0;

            Debug.Log($"[ZombieAnimatorSetup] Found {guids.Length} models in {ZOMBIE_ANIM_PATH}");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                string fileName = Path.GetFileName(path);

                // Skip the main Zombie.fbx model (it creates its own avatar)
                // Also skip Parasite model if present
                if (fileName == "Zombie.fbx" || fileName.Contains("Parasite"))
                {
                    Debug.Log($"[ZombieAnimatorSetup] Skipping {fileName}");
                    continue;
                }

                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    Debug.LogWarning($"[ZombieAnimatorSetup] Could not get importer for {fileName}");
                    continue;
                }

                Debug.Log($"[ZombieAnimatorSetup] {fileName}: animationType={importer.animationType}, avatarSetup={importer.avatarSetup}");

                // ALWAYS reconfigure to ensure consistency
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = zombieAvatar;

                // Write import settings to .meta file
                EditorUtility.SetDirty(importer);
                AssetDatabase.WriteImportSettingsIfDirty(path);

                // Force reimport to apply changes
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                configured++;
                Debug.Log($"[ZombieAnimatorSetup] Configured {fileName} as Humanoid (copied avatar from Zombie.fbx)");
            }

            AssetDatabase.Refresh();
            Debug.Log($"[ZombieAnimatorSetup] Configured {configured} animation FBX files to use Zombie.fbx avatar");

            if (configured > 0)
            {
                EditorUtility.DisplayDialog("FBX Configuration Complete",
                    $"Configured {configured} animation files.\n\nNow run 'Tools > Enemy > Create Zombie Animator Controller' to rebuild the animator.",
                    "OK");
            }
        }
    }
}
#endif
