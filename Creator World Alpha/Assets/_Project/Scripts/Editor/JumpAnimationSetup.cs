using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;

namespace CreatorWorld.Editor
{
    /// <summary>
    /// Editor script to set up adaptive jump animations.
    /// Run via menu: Tools > CreatorWorld > Setup Jump Animations
    /// </summary>
    public class JumpAnimationSetup : EditorWindow
    {
        private const string ANIMATOR_PATH = "Assets/_Project/Settings/PlayerAnimator.controller";

        // Animation paths
        private const string UNARMED_JUMP_PATH = "Assets/Art/Animations/Jump/Unarmed/Running Jump.fbx";
        private const string RIFLE_JUMP_UP_PATH = "Assets/Art/Animations/Combat/Rifle/Rifle 8-Way Locomotion Pack/jump up.fbx";
        private const string RIFLE_JUMP_LOOP_PATH = "Assets/Art/Animations/Combat/Rifle/Rifle 8-Way Locomotion Pack/jump loop.fbx";
        private const string RIFLE_JUMP_DOWN_PATH = "Assets/Art/Animations/Combat/Rifle/Rifle 8-Way Locomotion Pack/jump down.fbx";

        [MenuItem("Tools/CreatorWorld/Setup Jump Animations")]
        public static void ShowWindow()
        {
            GetWindow<JumpAnimationSetup>("Jump Animation Setup");
        }

        private void OnGUI()
        {
            GUILayout.Label("Jump Animation Setup", EditorStyles.boldLabel);
            GUILayout.Space(10);

            GUILayout.Label("This will:", EditorStyles.wordWrappedLabel);
            GUILayout.Label("1. Configure FBX imports as Humanoid", EditorStyles.wordWrappedLabel);
            GUILayout.Label("2. Add missing animator parameters", EditorStyles.wordWrappedLabel);
            GUILayout.Label("3. Create jump states and transitions", EditorStyles.wordWrappedLabel);

            GUILayout.Space(20);

            if (GUILayout.Button("Setup Jump Animations", GUILayout.Height(40)))
            {
                SetupJumpAnimations();
            }

            GUILayout.Space(10);

            if (GUILayout.Button("1. Configure FBX Imports Only", GUILayout.Height(25)))
            {
                ConfigureFBXImports();
            }

            if (GUILayout.Button("2. Add Parameters Only", GUILayout.Height(25)))
            {
                AddMissingParameters();
            }

            if (GUILayout.Button("3. Create States & Transitions Only", GUILayout.Height(25)))
            {
                CreateStatesAndTransitions();
            }
        }

        private void SetupJumpAnimations()
        {
            Debug.Log("[JumpSetup] Starting full setup...");

            ConfigureFBXImports();
            AddMissingParameters();
            CreateStatesAndTransitions();

            Debug.Log("[JumpSetup] Setup complete!");
            EditorUtility.DisplayDialog("Jump Animation Setup", "Setup complete! Check console for details.", "OK");
        }

        private void ConfigureFBXImports()
        {
            Debug.Log("[JumpSetup] Configuring FBX imports...");

            string[] fbxPaths = new string[]
            {
                UNARMED_JUMP_PATH,
                RIFLE_JUMP_UP_PATH,
                RIFLE_JUMP_LOOP_PATH,
                RIFLE_JUMP_DOWN_PATH
            };

            foreach (string path in fbxPaths)
            {
                ConfigureFBX(path);
            }

            AssetDatabase.Refresh();
            Debug.Log("[JumpSetup] FBX imports configured.");
        }

        private void ConfigureFBX(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[JumpSetup] Could not find FBX at: {path}");
                return;
            }

            bool needsReimport = false;

            // Set to Humanoid
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                needsReimport = true;
                Debug.Log($"[JumpSetup] Set {System.IO.Path.GetFileName(path)} to Humanoid");
            }

            // Configure animation clips
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips.Length > 0)
            {
                ModelImporterClipAnimation[] newClips = new ModelImporterClipAnimation[clips.Length];
                for (int i = 0; i < clips.Length; i++)
                {
                    newClips[i] = clips[i];

                    // Set loop time based on animation type
                    bool shouldLoop = path.Contains("jump loop");
                    if (newClips[i].loopTime != shouldLoop)
                    {
                        newClips[i].loopTime = shouldLoop;
                        needsReimport = true;
                    }
                }
                importer.clipAnimations = newClips;
            }

            if (needsReimport)
            {
                importer.SaveAndReimport();
            }
        }

        private void AddMissingParameters()
        {
            Debug.Log("[JumpSetup] Adding missing parameters...");

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ANIMATOR_PATH);
            if (controller == null)
            {
                Debug.LogError($"[JumpSetup] Could not find animator at: {ANIMATOR_PATH}");
                return;
            }

            // Get existing parameter names
            HashSet<string> existingParams = new HashSet<string>();
            foreach (var param in controller.parameters)
            {
                existingParams.Add(param.name);
            }

            // Add missing Bool parameters
            string[] boolParams = { "IsJumping", "IsFalling", "HasRifle" };
            foreach (string paramName in boolParams)
            {
                if (!existingParams.Contains(paramName))
                {
                    controller.AddParameter(paramName, AnimatorControllerParameterType.Bool);
                    Debug.Log($"[JumpSetup] Added Bool parameter: {paramName}");
                }
            }

            // Add missing Float parameters
            string[] floatParams = { "NormalizedVelocityY" };
            foreach (string paramName in floatParams)
            {
                if (!existingParams.Contains(paramName))
                {
                    controller.AddParameter(paramName, AnimatorControllerParameterType.Float);
                    Debug.Log($"[JumpSetup] Added Float parameter: {paramName}");
                }
            }

            // Add missing Trigger parameters
            string[] triggerParams = { "Jump", "Land" };
            foreach (string paramName in triggerParams)
            {
                if (!existingParams.Contains(paramName))
                {
                    controller.AddParameter(paramName, AnimatorControllerParameterType.Trigger);
                    Debug.Log($"[JumpSetup] Added Trigger parameter: {paramName}");
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("[JumpSetup] Parameters added.");
        }

        private void CreateStatesAndTransitions()
        {
            Debug.Log("[JumpSetup] Creating states and transitions...");

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ANIMATOR_PATH);
            if (controller == null)
            {
                Debug.LogError($"[JumpSetup] Could not find animator at: {ANIMATOR_PATH}");
                return;
            }

            AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;

            // Find the Locomotion state (default state)
            AnimatorState locomotionState = FindState(rootStateMachine, "Blend Locomotion");
            if (locomotionState == null)
            {
                Debug.LogError("[JumpSetup] Could not find 'Blend Locomotion' state!");
                return;
            }

            // Load animation clips
            AnimationClip unarmedJumpClip = LoadAnimationClip(UNARMED_JUMP_PATH);
            AnimationClip rifleJumpUpClip = LoadAnimationClip(RIFLE_JUMP_UP_PATH);
            AnimationClip rifleJumpLoopClip = LoadAnimationClip(RIFLE_JUMP_LOOP_PATH);
            AnimationClip rifleJumpDownClip = LoadAnimationClip(RIFLE_JUMP_DOWN_PATH);

            // Create or find states
            AnimatorState unarmedJumpState = FindOrCreateState(rootStateMachine, "UnarmedJump", unarmedJumpClip, new Vector3(500, 300, 0));
            AnimatorState rifleJumpUpState = FindOrCreateState(rootStateMachine, "RifleJumpUp", rifleJumpUpClip, new Vector3(-200, 300, 0));
            AnimatorState rifleJumpLoopState = FindOrCreateState(rootStateMachine, "RifleJumpLoop", rifleJumpLoopClip, new Vector3(-200, 400, 0));
            AnimatorState rifleJumpDownState = FindOrCreateState(rootStateMachine, "RifleJumpDown", rifleJumpDownClip, new Vector3(-200, 500, 0));

            // Clear existing transitions from locomotion to ALL jump states to avoid conflicts
            ClearTransitionsFromState(locomotionState, "UnarmedJump");
            ClearTransitionsFromState(locomotionState, "RifleJumpUp");
            ClearTransitionsFromState(locomotionState, "RifleJumpLoop");
            ClearTransitionsFromState(locomotionState, "JumpRunning"); // Clear old jump transitions

            // Clear AnyState transitions to old jump states
            ClearAnyStateTransitionsTo(rootStateMachine, "JumpRunning");

            Debug.Log("[JumpSetup] Cleared old jump transitions");

            // === UNARMED JUMP TRANSITIONS ===

            // Locomotion -> UnarmedJump (Jump trigger, HasRifle = false)
            var toUnarmedJump = locomotionState.AddTransition(unarmedJumpState);
            toUnarmedJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            toUnarmedJump.AddCondition(AnimatorConditionMode.IfNot, 0, "HasRifle");
            toUnarmedJump.duration = 0.1f;
            toUnarmedJump.hasExitTime = false;
            toUnarmedJump.canTransitionToSelf = false;

            // Locomotion -> UnarmedJump (falling off cliff, no rifle)
            var toUnarmedFall = locomotionState.AddTransition(unarmedJumpState);
            toUnarmedFall.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
            toUnarmedFall.AddCondition(AnimatorConditionMode.If, 0, "IsFalling");
            toUnarmedFall.AddCondition(AnimatorConditionMode.IfNot, 0, "HasRifle");
            toUnarmedFall.duration = 0.15f;
            toUnarmedFall.hasExitTime = false;
            toUnarmedFall.canTransitionToSelf = false;

            // UnarmedJump -> Locomotion (grounded)
            ClearTransitionsFromState(unarmedJumpState, "Blend Locomotion");
            var unarmedToLoco = unarmedJumpState.AddTransition(locomotionState);
            unarmedToLoco.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
            unarmedToLoco.duration = 0.1f;
            unarmedToLoco.hasExitTime = false;
            unarmedToLoco.canTransitionToSelf = false;

            // === RIFLE JUMP TRANSITIONS ===

            // Locomotion -> RifleJumpUp (Jump trigger, HasRifle = true)
            var toRifleJumpUp = locomotionState.AddTransition(rifleJumpUpState);
            toRifleJumpUp.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            toRifleJumpUp.AddCondition(AnimatorConditionMode.If, 0, "HasRifle");
            toRifleJumpUp.duration = 0.05f;
            toRifleJumpUp.hasExitTime = false;
            toRifleJumpUp.canTransitionToSelf = false;

            // Locomotion -> RifleJumpLoop (falling off cliff, with rifle)
            var toRifleFall = locomotionState.AddTransition(rifleJumpLoopState);
            toRifleFall.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
            toRifleFall.AddCondition(AnimatorConditionMode.If, 0, "IsFalling");
            toRifleFall.AddCondition(AnimatorConditionMode.If, 0, "HasRifle");
            toRifleFall.duration = 0.15f;
            toRifleFall.hasExitTime = false;
            toRifleFall.canTransitionToSelf = false;

            // RifleJumpUp -> RifleJumpLoop (IsFalling)
            ClearTransitionsFromState(rifleJumpUpState, "RifleJumpLoop");
            var upToLoop = rifleJumpUpState.AddTransition(rifleJumpLoopState);
            upToLoop.AddCondition(AnimatorConditionMode.If, 0, "IsFalling");
            upToLoop.duration = 0.1f;
            upToLoop.hasExitTime = false;
            upToLoop.canTransitionToSelf = false;

            // RifleJumpLoop -> RifleJumpDown (Land trigger)
            ClearTransitionsFromState(rifleJumpLoopState, "RifleJumpDown");
            var loopToDown = rifleJumpLoopState.AddTransition(rifleJumpDownState);
            loopToDown.AddCondition(AnimatorConditionMode.If, 0, "Land");
            loopToDown.duration = 0.1f;
            loopToDown.hasExitTime = false;
            loopToDown.canTransitionToSelf = false;

            // RifleJumpDown -> Locomotion (exit time)
            ClearTransitionsFromState(rifleJumpDownState, "Blend Locomotion");
            var downToLoco = rifleJumpDownState.AddTransition(locomotionState);
            downToLoco.hasExitTime = true;
            downToLoco.exitTime = 0.9f;
            downToLoco.duration = 0.1f;
            downToLoco.canTransitionToSelf = false;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log("[JumpSetup] States and transitions created.");
        }

        private AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
        {
            foreach (var childState in stateMachine.states)
            {
                if (childState.state.name == stateName)
                {
                    return childState.state;
                }
            }
            return null;
        }

        private AnimatorState FindOrCreateState(AnimatorStateMachine stateMachine, string stateName, AnimationClip clip, Vector3 position)
        {
            // Check if state already exists
            AnimatorState existingState = FindState(stateMachine, stateName);
            if (existingState != null)
            {
                // Update the motion if needed
                if (clip != null && existingState.motion != clip)
                {
                    existingState.motion = clip;
                }
                Debug.Log($"[JumpSetup] Found existing state: {stateName}");
                return existingState;
            }

            // Create new state
            AnimatorState newState = stateMachine.AddState(stateName, position);
            if (clip != null)
            {
                newState.motion = clip;
            }
            Debug.Log($"[JumpSetup] Created new state: {stateName}");
            return newState;
        }

        private AnimationClip LoadAnimationClip(string fbxPath)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (Object asset in assets)
            {
                if (asset is AnimationClip clip && !clip.name.Contains("__preview__"))
                {
                    return clip;
                }
            }
            Debug.LogWarning($"[JumpSetup] Could not load animation clip from: {fbxPath}");
            return null;
        }

        private void ClearTransitionsFromState(AnimatorState state, string targetStateName)
        {
            // Get transitions to remove
            List<AnimatorStateTransition> toRemove = new List<AnimatorStateTransition>();
            foreach (var transition in state.transitions)
            {
                if (transition.destinationState != null &&
                    transition.destinationState.name == targetStateName)
                {
                    toRemove.Add(transition);
                }
            }

            // Remove them
            foreach (var transition in toRemove)
            {
                state.RemoveTransition(transition);
            }
        }

        private void ClearAnyStateTransitionsTo(AnimatorStateMachine stateMachine, string targetStateName)
        {
            // Get AnyState transitions to remove
            List<AnimatorStateTransition> toRemove = new List<AnimatorStateTransition>();
            foreach (var transition in stateMachine.anyStateTransitions)
            {
                if (transition.destinationState != null &&
                    transition.destinationState.name == targetStateName)
                {
                    toRemove.Add(transition);
                    Debug.Log($"[JumpSetup] Removing AnyState transition to: {targetStateName}");
                }
            }

            // Remove them
            foreach (var transition in toRemove)
            {
                stateMachine.RemoveAnyStateTransition(transition);
            }
        }
    }
}
