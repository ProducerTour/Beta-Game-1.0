#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

namespace CreatorWorld.Editor
{
    /// <summary>
    /// Editor utility to create and setup the Enemy Animator Controller.
    /// Creates a simple rifle locomotion blend tree for enemy NPCs.
    /// </summary>
    public static class EnemyAnimatorSetup
    {
        private const string ANIMATION_PATH = "Assets/Art/Animations/Combat/Rifle/Rifle 8-Way Locomotion Pack";
        private const string OUTPUT_PATH = "Assets/_Project/Settings/EnemyAnimator.controller";

        [MenuItem("Tools/Enemy/Create Enemy Animator Controller")]
        public static void CreateEnemyAnimatorController()
        {
            // Create controller
            var controller = AnimatorController.CreateAnimatorControllerAtPath(OUTPUT_PATH);

            // Add parameters
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsCrouching", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsSprinting", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("HasRifle", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsAiming", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("DeathType", AnimatorControllerParameterType.Int);

            // Get the base layer
            var rootStateMachine = controller.layers[0].stateMachine;

            // Create locomotion blend tree state
            var locomotionState = rootStateMachine.AddState("Locomotion", new Vector3(300, 100, 0));

            // Create 1D blend tree for speed-based locomotion
            BlendTree locomotionTree;
            controller.CreateBlendTreeInController("LocomotionBlend", out locomotionTree);
            locomotionTree.blendType = BlendTreeType.Simple1D;
            locomotionTree.blendParameter = "Speed";
            locomotionState.motion = locomotionTree;

            // Load animations
            var idleClip = LoadAnimation("idle.fbx");
            var walkClip = LoadAnimation("walk forward.fbx");
            var runClip = LoadAnimation("run forward.fbx");

            int loadedCount = 0;

            // Add motion clips to blend tree with speed thresholds
            // Speed 0 = idle, Speed 2 = walk, Speed 5 = run
            if (idleClip != null)
            {
                locomotionTree.AddChild(idleClip, 0f);
                loadedCount++;
                Debug.Log($"[EnemyAnimatorSetup] Loaded idle: {idleClip.name} at threshold 0");
            }
            if (walkClip != null)
            {
                locomotionTree.AddChild(walkClip, 2f);
                loadedCount++;
                Debug.Log($"[EnemyAnimatorSetup] Loaded walk: {walkClip.name} at threshold 2");
            }
            if (runClip != null)
            {
                locomotionTree.AddChild(runClip, 5f);
                loadedCount++;
                Debug.Log($"[EnemyAnimatorSetup] Loaded run: {runClip.name} at threshold 5");
            }

            if (loadedCount == 0)
            {
                Debug.LogError($"[EnemyAnimatorSetup] NO ANIMATIONS LOADED! Check if animations exist at: {ANIMATION_PATH}");
            }
            else if (loadedCount < 3)
            {
                Debug.LogWarning($"[EnemyAnimatorSetup] Only {loadedCount}/3 animations loaded. Blend tree may not work correctly.");
            }
            else
            {
                Debug.Log($"[EnemyAnimatorSetup] All {loadedCount} locomotion animations loaded successfully!");
            }

            // Set default state
            rootStateMachine.defaultState = locomotionState;

            // Create death state
            var deathState = rootStateMachine.AddState("Death", new Vector3(300, 250, 0));
            var deathClip = LoadAnimation("death from the front.fbx");
            if (deathClip != null)
                deathState.motion = deathClip;

            // Add transition from Any State to Death
            var deathTransition = rootStateMachine.AddAnyStateTransition(deathState);
            deathTransition.AddCondition(AnimatorConditionMode.If, 0, "Death");
            deathTransition.hasExitTime = false;
            deathTransition.duration = 0.1f;

            // Save
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EnemyAnimatorSetup] Created Enemy Animator Controller at: {OUTPUT_PATH}");
            Selection.activeObject = controller;
        }

        private static AnimationClip LoadAnimation(string fileName)
        {
            string fullPath = $"{ANIMATION_PATH}/{fileName}";
            var clips = AssetDatabase.LoadAllAssetsAtPath(fullPath);

            foreach (var clip in clips)
            {
                if (clip is AnimationClip animClip && !animClip.name.Contains("__preview__"))
                {
                    return animClip;
                }
            }

            Debug.LogWarning($"[EnemyAnimatorSetup] Could not find animation: {fileName}");
            return null;
        }

        [MenuItem("Tools/Enemy/Setup Enemy Prefab from Selection")]
        public static void SetupEnemyFromSelection()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Setup Enemy", "Please select a GameObject in the scene to convert to an enemy.", "OK");
                return;
            }

            Undo.RegisterCompleteObjectUndo(selected, "Setup Enemy");

            // Add required components
            if (selected.GetComponent<Animator>() == null)
            {
                var animator = selected.AddComponent<Animator>();

                // Try to load the enemy animator controller
                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(OUTPUT_PATH);
                if (controller != null)
                    animator.runtimeAnimatorController = controller;
            }

            if (selected.GetComponent<Enemy.EnemyHealth>() == null)
                selected.AddComponent<Enemy.EnemyHealth>();

            if (selected.GetComponent<Enemy.EnemyAnimation>() == null)
                selected.AddComponent<Enemy.EnemyAnimation>();

            if (selected.GetComponent<Enemy.EnemyWeaponHolder>() == null)
                selected.AddComponent<Enemy.EnemyWeaponHolder>();

            if (selected.GetComponent<CharacterController>() == null)
            {
                var cc = selected.AddComponent<CharacterController>();
                cc.height = 1.8f;
                cc.radius = 0.3f;
                cc.center = new Vector3(0, 0.9f, 0);
            }

            EditorUtility.SetDirty(selected);
            Debug.Log($"[EnemyAnimatorSetup] Set up enemy components on: {selected.name}");
        }
    }
}
#endif
