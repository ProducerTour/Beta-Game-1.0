using UnityEngine;
using UnityEditor;
using System.IO;
using System.Reflection;

namespace CreatorWorld.Editor
{
    /// <summary>
    /// Automatically fixes animation imports when FBX files are imported.
    /// This runs BEFORE the animation is imported, ensuring correct settings.
    /// Based on best practices for Mixamo animation importing.
    /// </summary>
    public class AnimationPostProcessor : AssetPostprocessor
    {
        // Reference paths
        private const string YBOT_PATH = "Assets/Art/Models/Characters/Y Bot.fbx";

        // Animation folders to process
        private static readonly string[] AnimationFolders = {
            "Assets/Art/Animations/Rifle Animations",
            "Assets/Art/Animations/Pistol_Handgun Locomotion Pack",
            "Assets/Art/Animations/basic Locomotion Animations"
        };

        // Static references loaded from reference model
        private static Avatar referenceAvatar;
        private static ModelImporter referenceImporter;

        /// <summary>
        /// Called BEFORE the model is imported - set up rig and avatar
        /// </summary>
        void OnPreprocessModel()
        {
            // Only process files in our animation folders
            if (!IsInAnimationFolder(assetPath)) return;

            // Skip character models
            string fileName = Path.GetFileNameWithoutExtension(assetPath).ToLower();
            if (fileName == "y bot" || fileName == "character" || fileName == "xbot") return;

            Debug.Log($"[AnimationPostProcessor] Pre-processing model: {assetPath}");

            // Load reference avatar from Y Bot
            LoadReferenceAvatar();

            ModelImporter importer = assetImporter as ModelImporter;
            if (importer == null) return;

            // Set to Generic rig (required for Mixamo animations)
            importer.animationType = ModelImporterAnimationType.Generic;

            // Use avatar from reference model for consistent bone mapping
            if (referenceAvatar != null)
            {
                importer.sourceAvatar = referenceAvatar;
                Debug.Log($"[AnimationPostProcessor] Set avatar from Y Bot");
            }

            // Copy bone mapping from reference model if available
            if (referenceImporter != null)
            {
                CopyBoneMapping(referenceImporter, importer);
            }
        }

        /// <summary>
        /// Called BEFORE animation clips are imported - set up loop, root motion, etc.
        /// </summary>
        void OnPreprocessAnimation()
        {
            // Only process files in our animation folders
            if (!IsInAnimationFolder(assetPath)) return;

            string fileName = Path.GetFileNameWithoutExtension(assetPath).ToLower();
            if (fileName == "y bot" || fileName == "character" || fileName == "xbot") return;

            Debug.Log($"[AnimationPostProcessor] Pre-processing animation: {assetPath}");

            ModelImporter importer = assetImporter as ModelImporter;
            if (importer == null) return;

            // Determine if this animation should loop
            bool shouldLoop = IsLoopingAnimation(fileName);

            // Get existing clip animations or create from defaults
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            if (clips == null || clips.Length == 0)
            {
                Debug.LogWarning($"[AnimationPostProcessor] No clips found in {assetPath}");
                return;
            }

            // Fix each clip
            for (int i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];

                // Rename clip to match filename (cleaner in animator)
                clip.name = Path.GetFileNameWithoutExtension(assetPath);

                // Set loop time based on animation type
                clip.loopTime = shouldLoop;
                clip.loopPose = shouldLoop;

                // === ROOT MOTION BAKING (CRITICAL FOR IN-PLACE) ===

                // Root Transform Rotation - Bake into Pose
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;
                clip.rotationOffset = 0;

                // Root Transform Position (Y) - Bake into Pose
                clip.lockRootHeightY = true;
                clip.keepOriginalPositionY = true;
                clip.heightOffset = 0;
                clip.heightFromFeet = false;

                // Root Transform Position (XZ) - Bake into Pose with Center of Mass
                // This is the KEY setting to prevent drift!
                clip.lockRootPositionXZ = true;
                clip.keepOriginalPositionXZ = false; // FALSE = Use Center of Mass (prevents drift)

                // Curves and events
                clip.hasAdditiveReferencePose = false;

                clips[i] = clip;
            }

            importer.clipAnimations = clips;

            Debug.Log($"[AnimationPostProcessor] Fixed: {assetPath} (loop={shouldLoop})");
        }

        /// <summary>
        /// Load the reference avatar from Y Bot model
        /// </summary>
        private static void LoadReferenceAvatar()
        {
            if (referenceAvatar != null) return;

            // Load all assets from Y Bot FBX
            var assets = AssetDatabase.LoadAllAssetsAtPath(YBOT_PATH);
            foreach (var asset in assets)
            {
                if (asset is Avatar avatar)
                {
                    referenceAvatar = avatar;
                    Debug.Log($"[AnimationPostProcessor] Loaded reference avatar: {avatar.name}");
                    break;
                }
            }

            // Also load the model importer for bone mapping
            referenceImporter = AssetImporter.GetAtPath(YBOT_PATH) as ModelImporter;
        }

        /// <summary>
        /// Copy bone mapping from reference model to target using SerializedObject
        /// </summary>
        private static void CopyBoneMapping(ModelImporter source, ModelImporter target)
        {
            try
            {
                SerializedObject sourceObj = new SerializedObject(source);
                SerializedObject targetObj = new SerializedObject(target);

                // Copy the human description (bone mapping)
                SerializedProperty humanDescription = sourceObj.FindProperty("m_HumanDescription");
                if (humanDescription != null)
                {
                    targetObj.CopyFromSerializedProperty(humanDescription);
                    targetObj.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AnimationPostProcessor] Could not copy bone mapping: {e.Message}");
            }
        }

        /// <summary>
        /// Check if the asset is in one of our animation folders
        /// </summary>
        private bool IsInAnimationFolder(string path)
        {
            foreach (var folder in AnimationFolders)
            {
                if (path.StartsWith(folder)) return true;
            }
            return false;
        }

        /// <summary>
        /// Determine if an animation should loop based on filename
        /// </summary>
        private static bool IsLoopingAnimation(string fileName)
        {
            // These animations should NOT loop (one-shot)
            string[] nonLooping = {
                "jump", "death", "reload", "fire", "shoot",
                "hit", "kneel", "stand", "turn", "prone", "rapid",
                "to stand", "to kneel", "to prone", "to crouch", "to sprint"
            };

            foreach (var keyword in nonLooping)
            {
                if (fileName.Contains(keyword)) return false;
            }

            // Everything else loops (idle, walk, run, sprint, crouch, strafe)
            return true;
        }
    }
}
