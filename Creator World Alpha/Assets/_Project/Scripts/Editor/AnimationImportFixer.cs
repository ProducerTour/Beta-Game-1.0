using UnityEngine;
using UnityEditor;
using System.IO;

namespace CreatorWorld.Editor
{
    /// <summary>
    /// Fixes animation import settings for all FBX files.
    /// Configures looping, root motion baking, and rig type.
    /// </summary>
    public class AnimationImportFixer : EditorWindow
    {
        [MenuItem("Tools/Creator World/Fix Animation Imports", priority = 10)]
        public static void FixAllAnimations()
        {
            FixAnimations(false);
        }

        [MenuItem("Tools/Creator World/Force Reimport All Animations", priority = 11)]
        public static void ForceReimportAllAnimations()
        {
            FixAnimations(true);
        }

        [MenuItem("Tools/Creator World/Reimport Animations (Uses PostProcessor)", priority = 12)]
        public static void ReimportWithPostProcessor()
        {
            string[] folders = {
                "Assets/Art/Animations/Rifle Animations",
                "Assets/Art/Animations/Pistol_Handgun Locomotion Pack",
                "Assets/Art/Animations/basic Locomotion Animations"
            };

            int count = 0;
            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder.Replace("Assets/", Application.dataPath + "/")))
                {
                    Debug.LogWarning($"Folder not found: {folder}");
                    continue;
                }

                string[] guids = AssetDatabase.FindAssets("t:Model", new[] { folder });
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.EndsWith(".fbx")) continue;

                    string fileName = Path.GetFileNameWithoutExtension(path).ToLower();
                    if (fileName == "y bot" || fileName == "character") continue;

                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    count++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Reimported {count} animations with PostProcessor!");
            EditorUtility.DisplayDialog("Reimport Complete",
                $"Reimported {count} animation files.\n\n" +
                "The AnimationPostProcessor automatically applied:\n" +
                "- Generic rig type\n" +
                "- Y Bot avatar reference\n" +
                "- Loop settings (based on animation type)\n" +
                "- Root motion baking (Center of Mass)",
                "OK");
        }

        static void FixAnimations(bool forceReimport)
        {
            string[] folders = {
                "Assets/Art/Animations/Rifle Animations",
                "Assets/Art/Animations/Pistol_Handgun Locomotion Pack",
                "Assets/Art/Animations/basic Locomotion Animations"
            };

            int fixedCount = 0;

            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder.Replace("Assets/", Application.dataPath + "/")))
                {
                    Debug.LogWarning($"Folder not found: {folder}");
                    continue;
                }

                string[] guids = AssetDatabase.FindAssets("t:Model", new[] { folder });
                Debug.Log($"Found {guids.Length} models in {folder}");

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.EndsWith(".fbx")) continue;

                    if (FixAnimationImport(path, forceReimport))
                    {
                        fixedCount++;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Fixed {fixedCount} animation imports!");
            EditorUtility.DisplayDialog("Animation Import Fix",
                $"Fixed {fixedCount} animation files.\n\nAll animations now have:\n" +
                "- Loop Time enabled\n" +
                "- Root motion baked into pose\n" +
                "- Generic rig type", "OK");
        }

        static bool FixAnimationImport(string assetPath, bool forceReimport = false)
        {
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) return false;

            bool changed = forceReimport;
            string fileName = Path.GetFileNameWithoutExtension(assetPath).ToLower();

            // Skip character files - they're not animations
            if (fileName == "character" || fileName == "y bot") return false;

            // Set to Generic rig
            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                changed = true;
            }

            // Get or create clip animations
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            if (clips == null || clips.Length == 0)
            {
                Debug.LogWarning($"No clips found in {assetPath}");
                return false;
            }

            // Determine if this is a looping animation
            bool shouldLoop = IsLoopingAnimation(fileName);

            // Fix each clip
            for (int i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];

                // Enable looping for locomotion animations
                clip.loopTime = shouldLoop;

                // Bake root motion into pose to prevent character from moving
                // Root Transform Rotation
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;
                clip.rotationOffset = 0;

                // Root Transform Position (Y)
                clip.lockRootHeightY = true;
                clip.keepOriginalPositionY = true;
                clip.heightOffset = 0;

                // Root Transform Position (XZ) - Use Center of Mass for stable in-place animations
                clip.lockRootPositionXZ = true;
                clip.keepOriginalPositionXZ = false;  // false = Center of Mass (more stable)

                clips[i] = clip;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            Debug.Log($"Fixed: {assetPath} (loop={shouldLoop})");

            return true;
        }

        static bool IsLoopingAnimation(string fileName)
        {
            // These animations should NOT loop (one-shot)
            string[] nonLooping = {
                "jump", "death", "reload", "fire", "shoot",
                "hit", "kneel", "stand", "turn", "prone", "rapid"
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
