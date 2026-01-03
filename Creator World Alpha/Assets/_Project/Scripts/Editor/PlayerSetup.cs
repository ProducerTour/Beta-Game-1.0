using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Collections.Generic;

namespace CreatorWorld.Editor
{
    /// <summary>
    /// SINGLE consolidated setup script for player, animations, and weapons.
    /// Replaces all other setup scripts.
    /// </summary>
    public class PlayerSetup : EditorWindow
    {
        private const string CONTROLLER_PATH = "Assets/_Project/Settings/PlayerAnimator.controller";
        private const string LOCOMOTION_PATH = "Assets/Art/Animations/basic Locomotion Animations";
        private const string RIFLE_PATH = "Assets/Art/Animations/Rifle Animations";
        private const string PISTOL_PATH = "Assets/Art/Animations/Pistol_Handgun Locomotion Pack";
        private const string YBOT_PATH = "Assets/Art/Models/Characters/Y Bot.fbx";

        [MenuItem("Tools/Creator World/=== SETUP PLAYER (Use This) ===", priority = 0)]
        public static void SetupPlayer()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Error", "Stop Play mode first.", "OK");
                return;
            }

            Debug.Log("========== PLAYER SETUP START ==========");

            // Step 1: Create or find player
            var player = SetupPlayerGameObject();
            if (player == null) return;

            // Step 2: Add all components
            SetupPlayerComponents(player);

            // Step 3: Create animator controller
            var controller = CreateAnimatorController();

            // Step 4: Assign animator
            var animator = player.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Step 5: Setup weapons
            SetupWeapons(player);

            // Step 6: Add debug components
            AddComponentIfMissing<CreatorWorld.Debugging.AnimationDebugger>(player);
            AddComponentIfMissing<CreatorWorld.Debugging.DevPanel>(player);
            Debug.Log("DevPanel added - press ` (backtick) to toggle debug panel");

            // Step 7: Ensure services exist
            EnsureServicesExist();

            // Step 8: Setup camera
            SetupCamera(player);

            // Save
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log("========== PLAYER SETUP COMPLETE ==========");
            EditorUtility.DisplayDialog("Player Setup Complete",
                "Setup completed successfully!\n\n" +
                "Controls:\n" +
                "• WASD - Move\n" +
                "• Space - Jump\n" +
                "• Shift - Sprint\n" +
                "• C - Crouch\n" +
                "• 1/2 - Switch Weapons\n" +
                "• H - Holster\n" +
                "• LMB - Fire\n" +
                "• RMB - Aim\n\n" +
                "Press Play to test!",
                "OK");
        }

        static GameObject SetupPlayerGameObject()
        {
            // Ensure Y Bot is set up as Generic rig before loading
            EnsureYBotRigSetup();

            // Try to find existing player
            var player = GameObject.Find("Player");

            if (player == null)
            {
                // Try to load Y Bot character model
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(YBOT_PATH);

                if (prefab == null)
                {
                    Debug.LogError($"No character model found at: {YBOT_PATH}");
                    return null;
                }

                player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                player.name = "Player";
                player.transform.position = new Vector3(0, 2, 0);
                Debug.Log($"Instantiated Y Bot from {YBOT_PATH}");
            }

            player.tag = "Player";

            // Setup CharacterController
            var cc = player.GetComponent<CharacterController>();
            if (cc == null) cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0, 0.9f, 0);

            // Setup Animator
            var animator = player.GetComponent<Animator>();
            if (animator == null) animator = player.AddComponent<Animator>();

            // Try to find avatar from Y Bot FBX (embedded in the model)
            var ybotAssets = AssetDatabase.LoadAllAssetsAtPath(YBOT_PATH);
            Avatar avatar = null;
            foreach (var asset in ybotAssets)
            {
                if (asset is Avatar a)
                {
                    avatar = a;
                    break;
                }
            }

            if (avatar != null)
            {
                animator.avatar = avatar;
                Debug.Log($"Using Y Bot avatar: {avatar.name}");
            }
            else
            {
                // Fallback to XBot avatar if Y Bot doesn't have one
                avatar = AssetDatabase.LoadAssetAtPath<Avatar>("Assets/_Project/Settings/XBot_GenericAvatar.asset");
                if (avatar != null)
                {
                    animator.avatar = avatar;
                    Debug.Log("Using XBot_GenericAvatar as fallback");
                }
                else
                {
                    Debug.LogWarning("No avatar found - animation retargeting may not work correctly");
                }
            }

            Debug.Log("Player GameObject ready");
            return player;
        }

        static void SetupPlayerComponents(GameObject player)
        {
            // Add movement subsystems
            AddComponentIfMissing<CreatorWorld.Player.Movement.GroundChecker>(player);
            AddComponentIfMissing<CreatorWorld.Player.Movement.CrouchHandler>(player);
            AddComponentIfMissing<CreatorWorld.Player.Movement.MovementHandler>(player);
            AddComponentIfMissing<CreatorWorld.Player.Movement.JumpController>(player);
            AddComponentIfMissing<CreatorWorld.Player.PlayerStateMachine>(player);

            // Add core player scripts
            AddComponentIfMissing<CreatorWorld.Player.PlayerController>(player);
            AddComponentIfMissing<CreatorWorld.Player.PlayerAnimation>(player);
            // HipPositionFixer removed - no longer needed with In-Place animations

            // Add combat scripts
            var inventory = AddComponentIfMissing<CreatorWorld.Combat.WeaponInventory>(player);
            var stateMachine = AddComponentIfMissing<CreatorWorld.Combat.WeaponStateMachine>(player);
            var manager = AddComponentIfMissing<CreatorWorld.Combat.WeaponManager>(player);

            // Connect combat references
            SetField(manager, "inventory", inventory);
            SetField(manager, "stateMachine", stateMachine);

            // Assign MovementConfig to all components
            var config = AssetDatabase.LoadAssetAtPath<CreatorWorld.Config.MovementConfig>(
                "Assets/_Project/ScriptableObjects/DefaultMovementConfig.asset");
            if (config != null)
            {
                SetField(player.GetComponent<CreatorWorld.Player.PlayerController>(), "config", config);
                SetField(player.GetComponent<CreatorWorld.Player.Movement.GroundChecker>(), "config", config);
                SetField(player.GetComponent<CreatorWorld.Player.Movement.MovementHandler>(), "config", config);
                SetField(player.GetComponent<CreatorWorld.Player.Movement.JumpController>(), "config", config);
                SetField(player.GetComponent<CreatorWorld.Player.Movement.CrouchHandler>(), "config", config);
                Debug.Log("MovementConfig assigned to all components");
            }

            Debug.Log("Player components ready");
        }

        static RuntimeAnimatorController CreateAnimatorController()
        {
            // Delete old controller
            if (File.Exists(CONTROLLER_PATH.Replace("Assets/", Application.dataPath + "/")))
            {
                AssetDatabase.DeleteAsset(CONTROLLER_PATH);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(CONTROLLER_PATH);

            // Add ALL parameters
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
            controller.AddParameter("VelocityY", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsJumping", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsFalling", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsCrouching", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsSprinting", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsStrafing", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsAiming", AnimatorControllerParameterType.Bool);
            controller.AddParameter("WeaponType", AnimatorControllerParameterType.Int);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Land", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Fire", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Reload", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

            var rootSM = controller.layers[0].stateMachine;

            // Load animation clips
            var rifleClips = LoadRifleClips();
            var pistolClips = LoadPistolClips();
            var locoClips = LoadLocomotionClips();

            Debug.Log($"Clips loaded - Rifle: {rifleClips.Count}, Pistol: {pistolClips.Count}, Locomotion: {locoClips.Count}");

            // Create weapon sub-state machines
            var rifleSM = rootSM.AddStateMachine("Rifle", new Vector3(300, 0, 0));
            var pistolSM = rootSM.AddStateMachine("Pistol", new Vector3(300, 100, 0));
            var unarmedSM = rootSM.AddStateMachine("Unarmed", new Vector3(300, -100, 0));

            // Setup each weapon's states
            SetupWeaponStates(rifleSM, rifleClips, controller, "Rifle");
            SetupWeaponStates(pistolSM, pistolClips, controller, "Pistol");
            SetupWeaponStates(unarmedSM, locoClips, controller, "Unarmed");

            // Default to rifle (WeaponType = 1)
            rootSM.defaultState = rifleSM.defaultState;

            // Weapon switching transitions
            AddWeaponTransition(rootSM, unarmedSM, 0);
            AddWeaponTransition(rootSM, rifleSM, 1);
            AddWeaponTransition(rootSM, pistolSM, 2);

            AssetDatabase.SaveAssets();
            Debug.Log("Animator controller created");
            return controller;
        }

        static Dictionary<string, AnimationClip> LoadRifleClips()
        {
            var clips = new Dictionary<string, AnimationClip>();
            TryLoadClip(clips, "idle", RIFLE_PATH, "Rifle Idle (1).fbx");
            TryLoadClip(clips, "walk", RIFLE_PATH, "Rifle Walk.fbx");
            TryLoadClip(clips, "run", RIFLE_PATH, "Rifle Run.fbx");
            TryLoadClip(clips, "sprint", RIFLE_PATH, "Rifle Run.fbx");  // fallback to run
            TryLoadClip(clips, "crouch_idle", RIFLE_PATH, "Idle Rifle Crouching.fbx");
            TryLoadClip(clips, "crouch_walk", RIFLE_PATH, "Crouch Walking Firing Rifle.fbx");
            TryLoadClip(clips, "jump", RIFLE_PATH, "Rifle Jump.fbx");
            return clips;
        }

        static Dictionary<string, AnimationClip> LoadPistolClips()
        {
            var clips = new Dictionary<string, AnimationClip>();
            TryLoadClip(clips, "idle", PISTOL_PATH, "pistol idle.fbx");
            TryLoadClip(clips, "walk", PISTOL_PATH, "pistol walk.fbx");
            TryLoadClip(clips, "run", PISTOL_PATH, "pistol run.fbx");
            TryLoadClip(clips, "sprint", PISTOL_PATH, "pistol run.fbx");  // fallback to run
            TryLoadClip(clips, "crouch_idle", PISTOL_PATH, "pistol kneeling idle.fbx");
            TryLoadClip(clips, "crouch_walk", PISTOL_PATH, "pistol walk.fbx");  // fallback to walk
            TryLoadClip(clips, "jump", PISTOL_PATH, "pistol jump.fbx");
            return clips;
        }

        static Dictionary<string, AnimationClip> LoadLocomotionClips()
        {
            var clips = new Dictionary<string, AnimationClip>();
            TryLoadClip(clips, "idle", LOCOMOTION_PATH, "Idle.fbx");
            TryLoadClip(clips, "walk", LOCOMOTION_PATH, "Walking.fbx");
            TryLoadClip(clips, "run", LOCOMOTION_PATH, "Running.fbx");
            TryLoadClip(clips, "sprint", LOCOMOTION_PATH, "Sprint.fbx");
            TryLoadClip(clips, "crouch_idle", LOCOMOTION_PATH, "Crouching Idle (1).fbx");
            TryLoadClip(clips, "crouch_walk", LOCOMOTION_PATH, "Crouched Walking (1).fbx");
            TryLoadClip(clips, "jump", LOCOMOTION_PATH, "Jumping.fbx");
            return clips;
        }

        static void TryLoadClip(Dictionary<string, AnimationClip> dict, string key, string folder, string fileName)
        {
            string path = $"{folder}/{fileName}";
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);

            foreach (var asset in assets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__"))
                {
                    dict[key] = clip;
                    Debug.Log($"  Loaded: {key} <- {fileName}");
                    return;
                }
            }
            Debug.LogWarning($"  NOT FOUND: {path}");
        }

        static void SetupWeaponStates(AnimatorStateMachine sm, Dictionary<string, AnimationClip> clips,
            AnimatorController controller, string name)
        {
            // Get clips with fallbacks
            clips.TryGetValue("idle", out var idle);
            clips.TryGetValue("walk", out var walk);
            clips.TryGetValue("run", out var run);
            clips.TryGetValue("sprint", out var sprint);

            // Log what we have
            Debug.Log($"{name} Blend Tree Setup:");
            Debug.Log($"  idle: {(idle != null ? idle.name : "MISSING")}");
            Debug.Log($"  walk: {(walk != null ? walk.name : "MISSING")}");
            Debug.Log($"  run: {(run != null ? run.name : "MISSING")}");
            Debug.Log($"  sprint: {(sprint != null ? sprint.name : "MISSING")}");

            // Create fallback chain: idle < walk < run < sprint
            var effectiveIdle = idle ?? walk ?? run ?? sprint;
            var effectiveWalk = walk ?? idle ?? run ?? sprint;
            var effectiveRun = run ?? walk ?? idle ?? sprint;
            var effectiveSprint = sprint ?? run ?? walk ?? idle;

            if (effectiveIdle == null)
            {
                Debug.LogError($"No animation clips found for {name}! Blend tree will be empty.");
            }

            // LOCOMOTION blend tree - ALWAYS add 4 entries for proper blending
            var locoState = sm.AddState("Locomotion", new Vector3(0, 0, 0));
            var locoTree = new BlendTree { name = $"{name}_Loco", blendType = BlendTreeType.Simple1D };
            locoTree.blendParameter = "Speed";
            locoTree.useAutomaticThresholds = false;

            if (effectiveIdle != null) locoTree.AddChild(effectiveIdle, 0f);
            if (effectiveWalk != null) locoTree.AddChild(effectiveWalk, 0.35f);
            if (effectiveRun != null) locoTree.AddChild(effectiveRun, 0.7f);
            if (effectiveSprint != null) locoTree.AddChild(effectiveSprint, 1.0f);

            locoState.motion = locoTree;
            AssetDatabase.AddObjectToAsset(locoTree, CONTROLLER_PATH);

            // CROUCH blend tree
            var crouchState = sm.AddState("Crouch", new Vector3(0, 100, 0));
            var crouchTree = new BlendTree { name = $"{name}_Crouch", blendType = BlendTreeType.Simple1D };
            crouchTree.blendParameter = "Speed";
            crouchTree.useAutomaticThresholds = false;

            if (clips.TryGetValue("crouch_idle", out var crouchIdle))
                crouchTree.AddChild(crouchIdle, 0f);
            else if (idle != null)
                crouchTree.AddChild(idle, 0f);

            if (clips.TryGetValue("crouch_walk", out var crouchWalk))
                crouchTree.AddChild(crouchWalk, 0.5f);
            else if (crouchIdle != null)
                crouchTree.AddChild(crouchIdle, 0.5f);

            crouchState.motion = crouchTree;
            AssetDatabase.AddObjectToAsset(crouchTree, CONTROLLER_PATH);

            // JUMP state
            var jumpState = sm.AddState("Jump", new Vector3(200, 0, 0));
            if (clips.TryGetValue("jump", out var jump))
                jumpState.motion = jump;

            sm.defaultState = locoState;

            // TRANSITIONS
            // Loco <-> Crouch
            var toCrouch = locoState.AddTransition(crouchState);
            toCrouch.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
            toCrouch.hasExitTime = false;
            toCrouch.duration = 0.15f;

            var fromCrouch = crouchState.AddTransition(locoState);
            fromCrouch.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
            fromCrouch.hasExitTime = false;
            fromCrouch.duration = 0.15f;

            // Loco <-> Jump
            var toJump = locoState.AddTransition(jumpState);
            toJump.AddCondition(AnimatorConditionMode.If, 0, "IsJumping");
            toJump.hasExitTime = false;
            toJump.duration = 0.1f;

            var fromJump = jumpState.AddTransition(locoState);
            fromJump.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
            fromJump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsJumping");
            fromJump.hasExitTime = false;
            fromJump.duration = 0.15f;
        }

        static void AddWeaponTransition(AnimatorStateMachine root, AnimatorStateMachine weaponSM, int weaponType)
        {
            var t = root.AddAnyStateTransition(weaponSM.defaultState);
            t.AddCondition(AnimatorConditionMode.Equals, weaponType, "WeaponType");
            // Prevent AnyState from interrupting Crouch/Jump states (causes animation replaying)
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsJumping");
            t.hasExitTime = false;
            t.duration = 0.2f;
            t.canTransitionToSelf = false;
        }

        static void SetupWeapons(GameObject player)
        {
            // Find or create weapon holder
            Transform rightHand = FindBoneRecursive(player.transform, "mixamorig:RightHand");
            Transform holder = player.transform.Find("WeaponHolder");

            if (holder == null)
            {
                var holderGO = new GameObject("WeaponHolder");
                if (rightHand != null)
                {
                    holderGO.transform.SetParent(rightHand);
                    holderGO.transform.localPosition = new Vector3(0.1f, 0, 0);
                    holderGO.transform.localRotation = Quaternion.Euler(0, 0, -90);
                }
                else
                {
                    holderGO.transform.SetParent(player.transform);
                    holderGO.transform.localPosition = new Vector3(0.5f, 1.2f, 0.3f);
                }
                holder = holderGO.transform;
            }

            var inventory = player.GetComponent<CreatorWorld.Combat.WeaponInventory>();
            SetField(inventory, "weaponHolder", holder);

            // Try to load weapon prefabs
            var riflePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Weapons/AK47.prefab");
            var pistolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Weapons/Pistol.prefab");

            if (riflePrefab != null)
            {
                var rifle = InstantiateWeapon(riflePrefab, holder);
                var rifleWeapon = rifle.GetComponent<CreatorWorld.Combat.WeaponBase>();
                if (rifleWeapon != null)
                {
                    SetField(inventory, "primaryWeapon", rifleWeapon);
                    Debug.Log($"Rifle added to inventory: {rifleWeapon.name}");
                }
                else
                {
                    Debug.LogWarning("Rifle prefab has no WeaponBase component!");
                }
            }
            else
            {
                Debug.LogWarning("Rifle prefab not found at Assets/_Project/Prefabs/Weapons/AK47.prefab");
            }

            if (pistolPrefab != null)
            {
                var pistol = InstantiateWeapon(pistolPrefab, holder);
                var pistolWeapon = pistol.GetComponent<CreatorWorld.Combat.WeaponBase>();
                if (pistolWeapon != null)
                {
                    SetField(inventory, "secondaryWeapon", pistolWeapon);
                    Debug.Log($"Pistol added to inventory: {pistolWeapon.name}");
                }
                else
                {
                    Debug.LogWarning("Pistol prefab has no WeaponBase component!");
                }
            }
            else
            {
                Debug.LogWarning("Pistol prefab not found at Assets/_Project/Prefabs/Weapons/Pistol.prefab");
            }
        }

        static GameObject InstantiateWeapon(GameObject prefab, Transform parent)
        {
            var weapon = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            weapon.transform.SetParent(parent);
            weapon.transform.localPosition = Vector3.zero;
            weapon.transform.localRotation = Quaternion.identity;
            weapon.SetActive(false);
            return weapon;
        }

        static void EnsureServicesExist()
        {
            if (Object.FindFirstObjectByType<CreatorWorld.Input.InputService>() == null)
            {
                new GameObject("InputService").AddComponent<CreatorWorld.Input.InputService>();
                Debug.Log("Created InputService");
            }

            if (Object.FindFirstObjectByType<CreatorWorld.Core.GameManager>() == null)
            {
                new GameObject("GameManager").AddComponent<CreatorWorld.Core.GameManager>();
                Debug.Log("Created GameManager");
            }

            // Add ChunkManager for procedural terrain
            if (Object.FindFirstObjectByType<CreatorWorld.World.ChunkManager>() == null)
            {
                new GameObject("ChunkManager").AddComponent<CreatorWorld.World.ChunkManager>();
                Debug.Log("Created ChunkManager - terrain will generate at runtime");
            }

            // Add directional light if none exists
            if (Object.FindFirstObjectByType<Light>() == null)
            {
                var lightGO = new GameObject("Directional Light");
                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.95f, 0.85f);
                light.intensity = 1.5f;
                light.shadows = LightShadows.Soft;
                lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                Debug.Log("Created Directional Light");
            }

            // Add fallback ground plane
            if (GameObject.Find("TemporaryGround") == null && GameObject.Find("Ground") == null)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "TemporaryGround";
                ground.transform.position = Vector3.zero;
                ground.transform.localScale = new Vector3(100, 1, 100);
                var renderer = ground.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    if (mat != null)
                    {
                        mat.color = new Color(0.2f, 0.5f, 0.2f);
                        renderer.material = mat;
                    }
                }
                Debug.Log("Created temporary ground plane");
            }
        }

        static void SetupCamera(GameObject player)
        {
            // Find or create main camera
            var mainCam = Camera.main;
            if (mainCam == null)
            {
                var camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
                mainCam = camGO.AddComponent<Camera>();
                camGO.AddComponent<AudioListener>();
                Debug.Log("Created Main Camera");
            }

            // Add PlayerCamera component
            var playerCam = mainCam.GetComponent<CreatorWorld.Player.PlayerCamera>();
            if (playerCam == null)
            {
                playerCam = mainCam.gameObject.AddComponent<CreatorWorld.Player.PlayerCamera>();
                Debug.Log("Added PlayerCamera component");
            }

            // Set the target to player
            var so = new SerializedObject(playerCam);
            var targetProp = so.FindProperty("target");
            if (targetProp != null)
            {
                targetProp.objectReferenceValue = player.transform;
                so.ApplyModifiedProperties();
            }

            // Position camera behind player
            mainCam.transform.position = player.transform.position + new Vector3(0, 2, -4);
            mainCam.transform.LookAt(player.transform.position + Vector3.up * 1.5f);

            Debug.Log("Camera setup complete - targeting player");
        }

        static T AddComponentIfMissing<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            if (comp == null) comp = go.AddComponent<T>();
            return comp;
        }

        static void SetField(Component component, string fieldName, Object value)
        {
            if (component == null)
            {
                Debug.LogWarning($"SetField: Component is null for field '{fieldName}'");
                return;
            }
            var so = new SerializedObject(component);
            var prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
                so.ApplyModifiedProperties();
                Debug.Log($"SetField: Set '{fieldName}' on {component.GetType().Name}");
            }
            else
            {
                Debug.LogWarning($"SetField: Property '{fieldName}' not found on {component.GetType().Name}");
            }
        }

        static Transform FindBoneRecursive(Transform parent, string boneName)
        {
            // Check exact match
            if (parent.name == boneName) return parent;

            // Check variations (Mixamo, Mixamo with underscore, no prefix)
            string[] variations = {
                boneName,
                boneName.Replace("mixamorig:", ""),
                boneName.Replace("mixamorig:", "mixamorig_"),
                boneName.Replace(":", "_")
            };

            foreach (var variant in variations)
            {
                if (parent.name.Equals(variant, System.StringComparison.OrdinalIgnoreCase))
                    return parent;
            }

            foreach (Transform child in parent)
            {
                var result = FindBoneRecursive(child, boneName);
                if (result != null) return result;
            }
            return null;
        }

        static void EnsureYBotRigSetup()
        {
            var importer = AssetImporter.GetAtPath(YBOT_PATH) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"Could not find ModelImporter for {YBOT_PATH}");
                return;
            }

            bool needsReimport = false;

            // Ensure Generic rig (required for Mixamo animations)
            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                needsReimport = true;
                Debug.Log("Set Y Bot to Generic rig type");
            }

            // Ensure avatar is created from this model
            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                needsReimport = true;
            }

            if (needsReimport)
            {
                importer.SaveAndReimport();
                Debug.Log("Y Bot model reimported with correct settings");
            }
            else
            {
                Debug.Log("Y Bot model already configured correctly");
            }
        }
    }
}
