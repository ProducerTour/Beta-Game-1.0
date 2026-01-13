using UnityEngine;

namespace CreatorWorld.Config
{
    /// <summary>
    /// Centralized path constants for animation assets.
    /// Uses the reorganized folder structure: Locomotion/{Weapon}/, Combat/{Weapon}/, Actions/
    /// </summary>
    public static class AnimationPaths
    {
        // ===========================================
        // CONTROLLER PATHS
        // ===========================================

        public const string PlayerAnimatorController = "Assets/_Project/Settings/PlayerAnimator.controller";
        public const string XBotAnimatorController = "Assets/_Project/Settings/XBotAnimator.controller";

        // ===========================================
        // CHARACTER MODEL PATHS
        // ===========================================

        public const string YBotModel = "Assets/Art/Models/Characters/Y Bot.fbx";
        public const string XBotModel = "Assets/Art/Models/Characters/X Bot.fbx";

        // ===========================================
        // ANIMATION ROOT
        // ===========================================

        public const string AnimationsRoot = "Assets/Art/Animations";

        // ===========================================
        // FOLDER STRUCTURE
        // Organized by category, then by weapon type
        // ===========================================

        public static class Folders
        {
            // Shared assets
            public const string Shared = "Assets/Art/Animations/_Shared";
            public const string SharedMasks = "Assets/Art/Animations/_Shared/Masks";

            // Locomotion (movement animations)
            public const string LocomotionRoot = "Assets/Art/Animations/Locomotion";
            public const string LocomotionUnarmed = "Assets/Art/Animations/Locomotion/Unarmed";
            public const string LocomotionRifle = "Assets/Art/Animations/Locomotion/Rifle";
            public const string LocomotionPistol = "Assets/Art/Animations/Locomotion/Pistol";

            // Combat (fire, reload, aim animations)
            public const string CombatRoot = "Assets/Art/Animations/Combat";
            public const string CombatRifle = "Assets/Art/Animations/Combat/Rifle";
            public const string CombatPistol = "Assets/Art/Animations/Combat/Pistol";

            // Actions (parkour, interactions, etc.)
            public const string ActionsRoot = "Assets/Art/Animations/Actions";
            public const string ActionsParkour = "Assets/Art/Animations/Actions/Parkour";
            public const string ActionsInteractions = "Assets/Art/Animations/Actions/Interactions";

            // Legacy packs (not imported, kept for reference)
            public const string RawPacks = "Assets/Art/Animations/_Raw";

            /// <summary>
            /// All folders to process for import settings.
            /// </summary>
            public static readonly string[] AllFolders = {
                LocomotionUnarmed,
                LocomotionRifle,
                LocomotionPistol,
                CombatRifle,
                CombatPistol,
                ActionsParkour,
                ActionsInteractions
            };
        }

        // ===========================================
        // LOOP DETECTION KEYWORDS
        // ===========================================

        public static class LoopKeywords
        {
            /// <summary>
            /// Animations containing these keywords should NOT loop (one-shot).
            /// </summary>
            public static readonly string[] NonLooping = {
                "jump", "death", "reload", "fire", "shoot",
                "hit", "kneel", "stand", "turn", "prone", "rapid",
                "to stand", "to kneel", "to prone", "to crouch", "to sprint",
                "slide", "vault", "land"
            };

            /// <summary>
            /// Animations containing these keywords SHOULD loop.
            /// </summary>
            public static readonly string[] Looping = {
                "idle", "walk", "run", "sprint", "crouch", "strafe", "aim"
            };
        }

        // ===========================================
        // UTILITY METHODS
        // ===========================================

        /// <summary>
        /// Check if a path is in any valid animation folder.
        /// </summary>
        public static bool IsInAnimationFolder(string path)
        {
            foreach (var folder in Folders.AllFolders)
            {
                if (path.StartsWith(folder)) return true;
            }
            return false;
        }

        /// <summary>
        /// Check if an animation should loop based on filename.
        /// </summary>
        public static bool ShouldLoop(string fileName)
        {
            string lowerName = fileName.ToLower();

            // Check non-looping keywords first (higher priority)
            foreach (var keyword in LoopKeywords.NonLooping)
            {
                if (lowerName.Contains(keyword)) return false;
            }

            // Default: loop locomotion-style animations
            return true;
        }

        /// <summary>
        /// Get the weapon type from a file path.
        /// </summary>
        public static string GetWeaponTypeFromPath(string path)
        {
            if (path.Contains("/Rifle/")) return "Rifle";
            if (path.Contains("/Pistol/")) return "Pistol";
            if (path.Contains("/Unarmed/")) return "Unarmed";
            return "Unknown";
        }

        /// <summary>
        /// Get the category from a file path.
        /// </summary>
        public static string GetCategoryFromPath(string path)
        {
            if (path.Contains("/Locomotion/")) return "Locomotion";
            if (path.Contains("/Combat/")) return "Combat";
            if (path.Contains("/Actions/")) return "Actions";
            return "Unknown";
        }

#if UNITY_EDITOR
        /// <summary>
        /// Validate that a folder exists in the project.
        /// </summary>
        public static bool FolderExists(string path)
        {
            return UnityEditor.AssetDatabase.IsValidFolder(path);
        }

        /// <summary>
        /// Log warnings for any expected folders that don't exist.
        /// </summary>
        public static void ValidateFolders()
        {
            Debug.Log("[AnimationPaths] Validating folder structure...");
            int missing = 0;

            foreach (var folder in Folders.AllFolders)
            {
                if (!FolderExists(folder))
                {
                    Debug.LogWarning($"[AnimationPaths] Missing folder: {folder}");
                    missing++;
                }
                else
                {
                    Debug.Log($"[AnimationPaths] OK: {folder}");
                }
            }

            if (missing == 0)
            {
                Debug.Log("[AnimationPaths] All folders present.");
            }
            else
            {
                Debug.LogWarning($"[AnimationPaths] {missing} folders missing.");
            }
        }

        /// <summary>
        /// Backward compatibility alias for ValidateFolders.
        /// </summary>
        public static void ValidateCurrentFolders() => ValidateFolders();
#endif

        // ===========================================
        // BACKWARD COMPATIBILITY
        // Legacy aliases for old editor scripts
        // ===========================================

        /// <summary>
        /// Legacy "Current" folder structure (maps to new Folders).
        /// </summary>
        public static class Current
        {
            public const string BasicLocomotion = "Assets/Art/Animations/Locomotion/Unarmed";
            public const string RifleAnimations = "Assets/Art/Animations/Locomotion/Rifle";
            public const string PistolAnimations = "Assets/Art/Animations/Locomotion/Pistol";
            public const string ShooterPack = "Assets/Art/Animations/Shooter Pack";
            public const string Parkour = "Assets/Art/Animations/Actions/Parkour";

            public static readonly string[] AllFolders = Folders.AllFolders;
        }

        /// <summary>
        /// Legacy "Reorganized" folder structure (maps to new Folders).
        /// </summary>
        public static class Reorganized
        {
            public const string LocomotionRoot = "Assets/Art/Animations/Locomotion";
            public const string LocomotionUnarmed = "Assets/Art/Animations/Locomotion/Unarmed";
            public const string LocomotionRifle = "Assets/Art/Animations/Locomotion/Rifle";
            public const string LocomotionPistol = "Assets/Art/Animations/Locomotion/Pistol";
            public const string CombatRoot = "Assets/Art/Animations/Combat";
            public const string CombatRifle = "Assets/Art/Animations/Combat/Rifle";
            public const string CombatPistol = "Assets/Art/Animations/Combat/Pistol";

            public static readonly string[] AllFolders = Folders.AllFolders;
        }
    }
}
