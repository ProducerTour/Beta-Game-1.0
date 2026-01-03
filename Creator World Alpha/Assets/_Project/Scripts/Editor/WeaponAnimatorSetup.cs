using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Collections.Generic;

namespace CreatorWorld.Editor
{
    /// <summary>
    /// Creates a proper multi-weapon animator controller with layers for each weapon type.
    /// Run: Tools > Creator World > Setup Weapon Animations
    /// </summary>
    public class WeaponAnimatorSetup
    {
        private const string CONTROLLER_PATH = "Assets/_Project/Settings/XBotAnimator.controller";
        private const string RIFLE_FBX_PATH = "Assets/Art/Animations/Rifle_FBX";
        private const string PISTOL_FBX_PATH = "Assets/Art/Animations/Pistol_FBX";
        private const string LOCOMOTION_FBX_PATH = "Assets/Art/Animations/Locomotion_FBX";

        [MenuItem("Tools/Creator World/Setup Weapon Animations (Multi-Layer)")]
        public static void SetupWeaponAnimations()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Error", "Stop Play mode first.", "OK");
                return;
            }

            Debug.Log("=== Setting up Multi-Weapon Animator ===");

            // Load animation clips
            var rifleClips = LoadClipsFromFolder(RIFLE_FBX_PATH, "rifle");
            var pistolClips = LoadClipsFromFolder(PISTOL_FBX_PATH, "pistol");
            var locomotionClips = LoadClipsFromFolder(LOCOMOTION_FBX_PATH, "loco");

            Debug.Log($"Loaded: {rifleClips.Count} rifle, {pistolClips.Count} pistol, {locomotionClips.Count} locomotion clips");

            // Create or load controller
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER_PATH);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(CONTROLLER_PATH);
            }

            // Clear existing layers (except base)
            while (controller.layers.Length > 1)
            {
                controller.RemoveLayer(1);
            }

            // Ensure parameters exist
            EnsureParameters(controller);

            // Setup Base Layer (unarmed/locomotion)
            SetupBaseLayer(controller, locomotionClips);

            // Add Rifle Layer
            AddWeaponLayer(controller, "Rifle", 1, rifleClips);

            // Add Pistol Layer
            AddWeaponLayer(controller, "Pistol", 2, pistolClips);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("=== Weapon Animator Setup Complete ===");
            EditorUtility.DisplayDialog("Weapon Animations Setup",
                "Created multi-layer animator:\n\n" +
                "• Base Layer: Unarmed locomotion\n" +
                $"• Rifle Layer: {rifleClips.Count} animations\n" +
                $"• Pistol Layer: {pistolClips.Count} animations\n\n" +
                "WeaponType parameter controls which layer plays:\n" +
                "0 = None (base), 1 = Rifle, 2 = Pistol",
                "OK");
        }

        static void EnsureParameters(AnimatorController controller)
        {
            string[] floatParams = { "Speed", "MoveX", "MoveZ", "VelocityY" };
            string[] boolParams = { "IsGrounded", "IsCrouching", "IsSprinting", "IsStrafing", "IsJumping", "IsFalling", "IsAiming" };
            string[] triggerParams = { "Jump", "Land", "Fire", "Reload", "Death" };

            foreach (var p in floatParams)
                AddParameterIfMissing(controller, p, AnimatorControllerParameterType.Float);
            foreach (var p in boolParams)
                AddParameterIfMissing(controller, p, AnimatorControllerParameterType.Bool);
            foreach (var p in triggerParams)
                AddParameterIfMissing(controller, p, AnimatorControllerParameterType.Trigger);

            AddParameterIfMissing(controller, "WeaponType", AnimatorControllerParameterType.Int);
        }

        static void AddParameterIfMissing(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            foreach (var p in controller.parameters)
            {
                if (p.name == name) return;
            }
            controller.AddParameter(name, type);
        }

        static void SetupBaseLayer(AnimatorController controller, Dictionary<string, AnimationClip> clips)
        {
            var layer = controller.layers[0];
            var sm = layer.stateMachine;

            // Clear existing states
            foreach (var state in sm.states)
            {
                sm.RemoveState(state.state);
            }

            // Create locomotion blend tree
            var locomotionState = sm.AddState("Locomotion", new Vector3(300, 0, 0));
            var blendTree = CreateLocomotionBlendTree(controller, clips, "Base");
            locomotionState.motion = blendTree;
            sm.defaultState = locomotionState;

            // Jump state
            var jumpState = sm.AddState("Jump", new Vector3(300, -120, 0));
            if (clips.TryGetValue("jump", out var jumpClip))
                jumpState.motion = jumpClip;

            // Transitions
            var anyToJump = sm.AddAnyStateTransition(jumpState);
            anyToJump.AddCondition(AnimatorConditionMode.If, 0, "IsJumping");
            anyToJump.hasExitTime = false;
            anyToJump.duration = 0.1f;
            anyToJump.canTransitionToSelf = false;

            var jumpToLoco = jumpState.AddTransition(locomotionState);
            jumpToLoco.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
            jumpToLoco.AddCondition(AnimatorConditionMode.IfNot, 0, "IsJumping");
            jumpToLoco.hasExitTime = false;
            jumpToLoco.duration = 0.15f;
        }

        static void AddWeaponLayer(AnimatorController controller, string weaponName, int weaponTypeValue, Dictionary<string, AnimationClip> clips)
        {
            // Create new layer
            var newLayer = new AnimatorControllerLayer
            {
                name = weaponName,
                defaultWeight = 1f,
                stateMachine = new AnimatorStateMachine()
            };
            newLayer.stateMachine.name = weaponName;
            newLayer.stateMachine.hideFlags = HideFlags.HideInHierarchy;

            // Save state machine as sub-asset
            AssetDatabase.AddObjectToAsset(newLayer.stateMachine, CONTROLLER_PATH);

            controller.AddLayer(newLayer);

            var sm = newLayer.stateMachine;

            // Empty state (when this weapon type is not active)
            var emptyState = sm.AddState("Empty", new Vector3(100, 0, 0));
            sm.defaultState = emptyState;

            // Active state with locomotion
            var activeState = sm.AddState($"{weaponName}_Locomotion", new Vector3(300, 0, 0));
            var blendTree = CreateLocomotionBlendTree(controller, clips, weaponName);
            activeState.motion = blendTree;

            // Crouch idle
            var crouchState = sm.AddState($"{weaponName}_CrouchIdle", new Vector3(300, 120, 0));
            if (clips.TryGetValue("crouch_idle", out var crouchClip) ||
                clips.TryGetValue("idle_crouching", out crouchClip) ||
                clips.TryGetValue("kneeling_idle", out crouchClip))
            {
                crouchState.motion = crouchClip;
            }

            // Jump
            var jumpState = sm.AddState($"{weaponName}_Jump", new Vector3(300, -120, 0));
            if (clips.TryGetValue("jump", out var jumpClip))
                jumpState.motion = jumpClip;

            // Transitions: Empty <-> Active based on WeaponType
            var toActive = emptyState.AddTransition(activeState);
            toActive.AddCondition(AnimatorConditionMode.Equals, weaponTypeValue, "WeaponType");
            toActive.hasExitTime = false;
            toActive.duration = 0.1f;

            var toEmpty = activeState.AddTransition(emptyState);
            toEmpty.AddCondition(AnimatorConditionMode.NotEqual, weaponTypeValue, "WeaponType");
            toEmpty.hasExitTime = false;
            toEmpty.duration = 0.1f;

            // Crouch transitions
            var toCrouch = activeState.AddTransition(crouchState);
            toCrouch.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
            toCrouch.hasExitTime = false;
            toCrouch.duration = 0.2f;

            var fromCrouch = crouchState.AddTransition(activeState);
            fromCrouch.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
            fromCrouch.hasExitTime = false;
            fromCrouch.duration = 0.2f;

            // Crouch to empty when weapon changes
            var crouchToEmpty = crouchState.AddTransition(emptyState);
            crouchToEmpty.AddCondition(AnimatorConditionMode.NotEqual, weaponTypeValue, "WeaponType");
            crouchToEmpty.hasExitTime = false;
            crouchToEmpty.duration = 0.1f;

            // Jump transitions from active
            var toJump = activeState.AddTransition(jumpState);
            toJump.AddCondition(AnimatorConditionMode.If, 0, "IsJumping");
            toJump.hasExitTime = false;
            toJump.duration = 0.1f;

            var fromJump = jumpState.AddTransition(activeState);
            fromJump.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
            fromJump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsJumping");
            fromJump.hasExitTime = false;
            fromJump.duration = 0.15f;

            // Jump to empty when weapon changes
            var jumpToEmpty = jumpState.AddTransition(emptyState);
            jumpToEmpty.AddCondition(AnimatorConditionMode.NotEqual, weaponTypeValue, "WeaponType");
            jumpToEmpty.hasExitTime = false;
            jumpToEmpty.duration = 0.1f;

            Debug.Log($"Created {weaponName} layer with {clips.Count} animations");
        }

        static BlendTree CreateLocomotionBlendTree(AnimatorController controller, Dictionary<string, AnimationClip> clips, string prefix)
        {
            BlendTree tree = new BlendTree();
            tree.name = $"{prefix}_Locomotion";
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;

            // Find clips with various naming conventions
            AnimationClip idle = FindClip(clips, "idle");
            AnimationClip walk = FindClip(clips, "walk", "walk_forward");
            AnimationClip run = FindClip(clips, "run", "run_forward", "standard_run");

            if (idle != null) tree.AddChild(idle, 0f);
            if (walk != null) tree.AddChild(walk, 0.4f);
            if (run != null)
            {
                tree.AddChild(run, 0.75f);
                tree.AddChild(run, 1.0f); // Sprint uses run
            }

            AssetDatabase.AddObjectToAsset(tree, CONTROLLER_PATH);
            return tree;
        }

        static AnimationClip FindClip(Dictionary<string, AnimationClip> clips, params string[] names)
        {
            foreach (var name in names)
            {
                if (clips.TryGetValue(name, out var clip))
                    return clip;
            }
            return null;
        }

        static Dictionary<string, AnimationClip> LoadClipsFromFolder(string folderPath, string debugPrefix)
        {
            var clips = new Dictionary<string, AnimationClip>();

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogWarning($"Folder not found: {folderPath}");
                return clips;
            }

            string fullPath = Path.Combine(Application.dataPath, folderPath.Substring("Assets/".Length));
            if (!Directory.Exists(fullPath))
                return clips;

            string[] fbxFiles = Directory.GetFiles(fullPath, "*.fbx", SearchOption.TopDirectoryOnly);
            foreach (var fbxFile in fbxFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(fbxFile);

                // Skip character/model files
                if (fileName.ToLower().Contains("character") || fileName.ToLower().Contains("x bot"))
                    continue;

                string assetPath = "Assets" + fbxFile.Substring(Application.dataPath.Length).Replace("\\", "/");

                var subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                foreach (var asset in subAssets)
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        // Normalize the key name
                        string key = NormalizeClipName(fileName);

                        if (!clips.ContainsKey(key))
                        {
                            clips[key] = clip;
                        }
                    }
                }
            }

            return clips;
        }

        static string NormalizeClipName(string fileName)
        {
            // Convert to lowercase, replace spaces with underscores
            string key = fileName.ToLower().Replace(" ", "_");

            // Remove common prefixes
            string[] prefixes = { "pistol_", "rifle_" };
            foreach (var prefix in prefixes)
            {
                if (key.StartsWith(prefix))
                {
                    key = key.Substring(prefix.Length);
                    break;
                }
            }

            // Map common variations
            if (key.Contains("kneeling")) key = key.Replace("kneeling", "crouch");
            if (key.Contains("crouching")) key = key.Replace("crouching", "crouch");

            return key;
        }
    }
}
