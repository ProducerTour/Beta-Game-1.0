#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace CreatorWorld.Editor
{
    /// <summary>
    /// Editor tool to configure FBX animation import settings.
    /// Sets up animations as Humanoid with proper loop settings.
    ///
    /// Industry Standard Mixamo Import Settings:
    /// - Rig Type: Humanoid
    /// - Avatar: Copy from character model
    /// - Loop Time: true for locomotion, false for actions
    /// - Root Transform: Bake Into Pose
    /// </summary>
    public class AnimationImportSetup : EditorWindow
    {
        private const string CHARACTER_MODEL_PATH = "Assets/Art/Models/Characters/Y Bot.fbx";
        private const string ANIMATIONS_ROOT = "Assets/Art/Animations";

        private Avatar sourceAvatar;
        private bool setLoopTime = true;
        private bool bakeRootTransform = true;

        [MenuItem("Tools/Creator World/Setup Animation Imports")]
        public static void ShowWindow()
        {
            GetWindow<AnimationImportSetup>("Animation Import Setup");
        }

        private void OnEnable()
        {
            // Try to load Y Bot avatar
            LoadSourceAvatar();
        }

        private void LoadSourceAvatar()
        {
            var modelImporter = AssetImporter.GetAtPath(CHARACTER_MODEL_PATH) as ModelImporter;
            if (modelImporter != null)
            {
                // Force reimport to ensure avatar exists
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(CHARACTER_MODEL_PATH);
                if (model != null)
                {
                    var animator = model.GetComponent<Animator>();
                    if (animator != null)
                    {
                        sourceAvatar = animator.avatar;
                    }
                }
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("Animation Import Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "This tool configures FBX animation files with:\n" +
                "• Humanoid rig type\n" +
                "• Avatar copied from Y Bot character\n" +
                "• Correct loop settings\n" +
                "• Root transform baked into pose",
                MessageType.Info);

            EditorGUILayout.Space();

            // Avatar source
            EditorGUILayout.LabelField("Source Avatar", EditorStyles.boldLabel);
            sourceAvatar = EditorGUILayout.ObjectField("Avatar", sourceAvatar, typeof(Avatar), false) as Avatar;

            if (sourceAvatar == null)
            {
                EditorGUILayout.HelpBox("No avatar found. Make sure Y Bot.fbx is imported as Humanoid first.", MessageType.Warning);

                if (GUILayout.Button("Setup Y Bot as Humanoid"))
                {
                    SetupCharacterModel();
                }
            }

            EditorGUILayout.Space();

            // Options
            EditorGUILayout.LabelField("Import Settings", EditorStyles.boldLabel);
            setLoopTime = EditorGUILayout.Toggle("Loop Locomotion Animations", setLoopTime);
            bakeRootTransform = EditorGUILayout.Toggle("Bake Root Transform", bakeRootTransform);

            EditorGUILayout.Space();

            // Actions
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            if (GUILayout.Button("Configure All Animations in Locomotion Folder", GUILayout.Height(30)))
            {
                ConfigureAnimationsInFolder($"{ANIMATIONS_ROOT}/Locomotion");
            }

            if (GUILayout.Button("Configure All Animations in Crouch Folder", GUILayout.Height(30)))
            {
                ConfigureAnimationsInFolder($"{ANIMATIONS_ROOT}/Crouch");
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Configure Selected FBX Files"))
            {
                ConfigureSelectedAnimations();
            }
        }

        private void SetupCharacterModel()
        {
            var modelImporter = AssetImporter.GetAtPath(CHARACTER_MODEL_PATH) as ModelImporter;
            if (modelImporter == null)
            {
                Debug.LogError($"Could not find model importer at {CHARACTER_MODEL_PATH}");
                return;
            }

            // Set to Humanoid
            modelImporter.animationType = ModelImporterAnimationType.Human;
            modelImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            // Save and reimport
            EditorUtility.SetDirty(modelImporter);
            modelImporter.SaveAndReimport();

            // Reload avatar
            LoadSourceAvatar();

            Debug.Log("Y Bot configured as Humanoid. Avatar created.");
        }

        private void ConfigureAnimationsInFolder(string folderPath)
        {
            if (sourceAvatar == null)
            {
                Debug.LogError("No source avatar set. Please configure Y Bot as Humanoid first.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { folderPath });
            int configured = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                {
                    ConfigureAnimationFBX(path);
                    configured++;
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"Configured {configured} animation files in {folderPath}");
        }

        private void ConfigureSelectedAnimations()
        {
            if (sourceAvatar == null)
            {
                Debug.LogError("No source avatar set. Please configure Y Bot as Humanoid first.");
                return;
            }

            var selected = Selection.objects;
            int configured = 0;

            foreach (var obj in selected)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                {
                    ConfigureAnimationFBX(path);
                    configured++;
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"Configured {configured} selected animation files");
        }

        private void ConfigureAnimationFBX(string path)
        {
            var modelImporter = AssetImporter.GetAtPath(path) as ModelImporter;
            if (modelImporter == null)
            {
                Debug.LogWarning($"Could not load importer for {path}");
                return;
            }

            bool isLocomotion = IsLocomotionAnimation(path);
            string fileName = Path.GetFileName(path);

            // Set up as Humanoid, copying avatar from Y Bot for proper retargeting
            modelImporter.animationType = ModelImporterAnimationType.Human;
            modelImporter.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            modelImporter.sourceAvatar = sourceAvatar;

            // Configure animation clips
            ConfigureClipSettings(modelImporter, isLocomotion);

            // Save and reimport
            EditorUtility.SetDirty(modelImporter);
            modelImporter.SaveAndReimport();

            // Check if import succeeded
            bool hasClips = CheckForAnimationClips(path);

            if (hasClips)
            {
                Debug.Log($"Configured: {fileName} as Humanoid (Loop: {isLocomotion && setLoopTime})");
            }
            else
            {
                Debug.LogError($"FAILED: {fileName} - Avatar copy failed. Make sure Y Bot is set up as Humanoid first.");
            }
        }

        private void ConfigureClipSettings(ModelImporter modelImporter, bool isLocomotion)
        {
            var clipAnimations = modelImporter.clipAnimations;
            if (clipAnimations.Length == 0)
            {
                clipAnimations = modelImporter.defaultClipAnimations;
            }

            for (int i = 0; i < clipAnimations.Length; i++)
            {
                var clip = clipAnimations[i];

                // Set loop time based on animation type
                clip.loopTime = isLocomotion && setLoopTime;
                clip.loopPose = isLocomotion && setLoopTime;

                if (bakeRootTransform)
                {
                    // Bake root motion into pose - character stays in place
                    // Script controls movement, not animation root motion
                    clip.lockRootRotation = true;
                    clip.lockRootHeightY = true;
                    clip.lockRootPositionXZ = true;

                    // Don't keep original offsets - start at origin
                    clip.keepOriginalOrientation = false;
                    clip.keepOriginalPositionY = true;  // Keep feet on ground
                    clip.keepOriginalPositionXZ = false; // Remove lateral drift
                }

                clipAnimations[i] = clip;
            }

            modelImporter.clipAnimations = clipAnimations;
        }

        private bool CheckForAnimationClips(string path)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var asset in assets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsLocomotionAnimation(string path)
        {
            string fileName = Path.GetFileNameWithoutExtension(path).ToLower();

            // These are looping locomotion animations
            string[] locomotionKeywords = { "idle", "walk", "run", "sprint", "strafe", "crouch" };
            foreach (string keyword in locomotionKeywords)
            {
                if (fileName.Contains(keyword))
                    return true;
            }

            return false;
        }
    }
}
#endif
