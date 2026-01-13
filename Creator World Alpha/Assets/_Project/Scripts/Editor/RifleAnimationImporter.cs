using UnityEngine;
using UnityEditor;
using System.IO;

namespace CreatorWorld.Editor
{
    /// <summary>
    /// Batch imports rifle animations with correct settings.
    /// Menu: Tools > Animation > Import Rifle Animations
    /// </summary>
    public class RifleAnimationImporter : EditorWindow
    {
        private const string RIFLE_ANIM_PATH = "Assets/Art/Animations/Combat/Rifle/Rifle 8-Way Locomotion Pack";
        private const string YBOT_AVATAR_PATH = "Assets/Art/Models/Characters/Y Bot.fbx";

        private Avatar yBotAvatar;
        private bool loopLocomotion = true;
        private bool bakeRootMotion = false;

        [MenuItem("Tools/Animation/Import Rifle Animations")]
        public static void ShowWindow()
        {
            GetWindow<RifleAnimationImporter>("Rifle Animation Importer");
        }

        private void OnEnable()
        {
            // Try to find Y Bot avatar
            var yBotImporter = AssetImporter.GetAtPath(YBOT_AVATAR_PATH) as ModelImporter;
            if (yBotImporter != null)
            {
                var yBotGO = AssetDatabase.LoadAssetAtPath<GameObject>(YBOT_AVATAR_PATH);
                if (yBotGO != null)
                {
                    var animator = yBotGO.GetComponent<Animator>();
                    if (animator != null)
                    {
                        yBotAvatar = animator.avatar;
                    }
                }
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("Rifle Animation Batch Importer", EditorStyles.boldLabel);
            GUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "This will apply import settings to all FBX files in:\n" + RIFLE_ANIM_PATH,
                MessageType.Info
            );

            GUILayout.Space(10);

            yBotAvatar = (Avatar)EditorGUILayout.ObjectField("Y Bot Avatar", yBotAvatar, typeof(Avatar), false);
            loopLocomotion = EditorGUILayout.Toggle("Loop Locomotion Anims", loopLocomotion);
            bakeRootMotion = EditorGUILayout.Toggle("Bake Root Motion (XZ)", bakeRootMotion);

            GUILayout.Space(20);

            if (GUILayout.Button("Preview Files", GUILayout.Height(30)))
            {
                PreviewFiles();
            }

            GUILayout.Space(10);

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Apply Import Settings", GUILayout.Height(40)))
            {
                ApplyImportSettings();
            }
            GUI.backgroundColor = Color.white;
        }

        private void PreviewFiles()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { RIFLE_ANIM_PATH });
            Debug.Log($"[RifleImporter] Found {guids.Length} FBX files:");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileName(path);

                // Skip Y Bot model itself
                if (fileName.ToLower().Contains("y bot"))
                {
                    Debug.Log($"  [SKIP] {fileName} (character model)");
                    continue;
                }

                bool shouldLoop = ShouldLoop(fileName);
                Debug.Log($"  {fileName} - Loop: {shouldLoop}");
            }
        }

        private void ApplyImportSettings()
        {
            if (yBotAvatar == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign the Y Bot Avatar first!", "OK");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { RIFLE_ANIM_PATH });
            int processed = 0;
            int skipped = 0;

            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    string fileName = Path.GetFileName(path);

                    EditorUtility.DisplayProgressBar(
                        "Importing Rifle Animations",
                        $"Processing: {fileName}",
                        (float)i / guids.Length
                    );

                    // Skip Y Bot model
                    if (fileName.ToLower().Contains("y bot"))
                    {
                        skipped++;
                        continue;
                    }

                    ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (importer == null)
                    {
                        Debug.LogWarning($"[RifleImporter] Could not get importer for: {path}");
                        continue;
                    }

                    // === RIG SETTINGS ===
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.sourceAvatar = yBotAvatar;

                    // === ANIMATION SETTINGS ===
                    ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                    if (clips.Length == 0)
                    {
                        clips = importer.clipAnimations;
                    }

                    if (clips.Length > 0)
                    {
                        bool shouldLoop = loopLocomotion && ShouldLoop(fileName);

                        for (int j = 0; j < clips.Length; j++)
                        {
                            // Loop settings
                            clips[j].loopTime = shouldLoop;
                            clips[j].loopPose = shouldLoop;

                            // Root motion settings
                            // Rotation - bake based on body orientation
                            clips[j].lockRootRotation = true;
                            clips[j].keepOriginalOrientation = true;

                            // Position Y - bake (feet on ground)
                            clips[j].lockRootHeightY = true;
                            clips[j].keepOriginalPositionY = true;

                            // Position XZ - usually unchecked for in-place anims
                            clips[j].lockRootPositionXZ = bakeRootMotion;
                            clips[j].keepOriginalPositionXZ = !bakeRootMotion;
                        }

                        importer.clipAnimations = clips;
                    }

                    // Save and reimport
                    importer.SaveAndReimport();
                    processed++;

                    Debug.Log($"[RifleImporter] Processed: {fileName} (Loop: {ShouldLoop(fileName)})");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Import Complete",
                $"Processed: {processed} animations\nSkipped: {skipped}",
                "OK"
            );

            Debug.Log($"[RifleImporter] Complete! Processed {processed}, Skipped {skipped}");
        }

        /// <summary>
        /// Determines if an animation should loop based on filename.
        /// </summary>
        private bool ShouldLoop(string fileName)
        {
            string lower = fileName.ToLower();

            // These should NOT loop (one-shot animations)
            if (lower.Contains("death")) return false;
            if (lower.Contains("fire")) return false;
            if (lower.Contains("shoot")) return false;
            if (lower.Contains("reload")) return false;
            if (lower.Contains("equip")) return false;
            if (lower.Contains("holster")) return false;
            if (lower.Contains("draw")) return false;
            if (lower.Contains("turn")) return false;
            if (lower.Contains("jump up")) return false;
            if (lower.Contains("jump down")) return false;

            // These SHOULD loop
            if (lower.Contains("idle")) return true;
            if (lower.Contains("walk")) return true;
            if (lower.Contains("run")) return true;
            if (lower.Contains("sprint")) return true;
            if (lower.Contains("crouch")) return true;
            if (lower.Contains("loop")) return true;

            // Default to loop for locomotion pack
            return true;
        }
    }
}
