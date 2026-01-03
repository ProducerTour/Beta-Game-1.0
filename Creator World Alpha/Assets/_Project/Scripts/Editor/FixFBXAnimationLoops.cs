using UnityEngine;
using UnityEditor;
using System.IO;

namespace CreatorWorld.Editor
{
    /// <summary>
    /// Fixes FBX animation import settings to enable looping.
    /// Run: Tools > Creator World > Fix Animation Loops
    /// </summary>
    public class FixFBXAnimationLoops : EditorWindow
    {
        private const string RIFLE_FBX_PATH = "Assets/Art/Animations/Rifle_FBX";
        private const string LOCOMOTION_FBX_PATH = "Assets/Art/Animations/Locomotion_FBX";

        // Animations that should loop (idle, walk, run, etc.)
        private static readonly string[] LOOPING_ANIMATIONS = new string[]
        {
            "idle",
            "walk",
            "run",
            "sprint",
            "strafe",
            "crouching",
            "loop"
        };

        // Animations that should NOT loop (jump up, death, etc.)
        private static readonly string[] NON_LOOPING_ANIMATIONS = new string[]
        {
            "jump up",
            "jump down",
            "death",
            "reload",
            "fire",
            "hit"
        };

        [MenuItem("Tools/Creator World/Fix Animation Loops")]
        public static void FixLoops()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Error", "Stop Play mode first.", "OK");
                return;
            }

            int fixedCount = 0;

            // Fix animations in both folders
            fixedCount += FixAnimationsInFolder(RIFLE_FBX_PATH);
            fixedCount += FixAnimationsInFolder(LOCOMOTION_FBX_PATH);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Animation Loops Fixed",
                $"Updated {fixedCount} FBX animation import settings.\n\n" +
                "Looping animations: idle, walk, run, strafe, crouch\n" +
                "Non-looping: jump, death, reload",
                "OK");
        }

        static int FixAnimationsInFolder(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogWarning($"Folder not found: {folderPath}");
                return 0;
            }

            int count = 0;
            string fullPath = Path.Combine(Application.dataPath, folderPath.Substring("Assets/".Length));

            if (!Directory.Exists(fullPath))
            {
                Debug.LogWarning($"Directory not found: {fullPath}");
                return 0;
            }

            string[] fbxFiles = Directory.GetFiles(fullPath, "*.fbx", SearchOption.TopDirectoryOnly);

            foreach (var fbxFile in fbxFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(fbxFile).ToLower();

                // Skip character.fbx (T-pose)
                if (fileName == "character") continue;

                string assetPath = "Assets" + fbxFile.Substring(Application.dataPath.Length).Replace("\\", "/");
                var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;

                if (importer == null)
                {
                    Debug.LogWarning($"Could not get importer for: {assetPath}");
                    continue;
                }

                // Determine if this should loop
                bool shouldLoop = ShouldAnimationLoop(fileName);

                // Get current clip settings
                var clipAnimations = importer.clipAnimations;

                if (clipAnimations == null || clipAnimations.Length == 0)
                {
                    // Use default clip animations
                    clipAnimations = importer.defaultClipAnimations;
                }

                if (clipAnimations == null || clipAnimations.Length == 0)
                {
                    Debug.LogWarning($"No animation clips in: {fileName}");
                    continue;
                }

                bool changed = false;
                for (int i = 0; i < clipAnimations.Length; i++)
                {
                    if (clipAnimations[i].loopTime != shouldLoop)
                    {
                        clipAnimations[i].loopTime = shouldLoop;
                        clipAnimations[i].loopPose = shouldLoop;
                        changed = true;
                    }
                }

                if (changed)
                {
                    importer.clipAnimations = clipAnimations;
                    importer.SaveAndReimport();
                    Debug.Log($"Fixed loop={shouldLoop} for: {fileName}");
                    count++;
                }
            }

            return count;
        }

        static bool ShouldAnimationLoop(string fileName)
        {
            // Check if it's explicitly non-looping
            foreach (var nonLoop in NON_LOOPING_ANIMATIONS)
            {
                if (fileName.Contains(nonLoop))
                    return false;
            }

            // Check if it's explicitly looping
            foreach (var loop in LOOPING_ANIMATIONS)
            {
                if (fileName.Contains(loop))
                    return true;
            }

            // Default to looping for locomotion animations
            return true;
        }

        [MenuItem("Tools/Creator World/Debug: List Animation Clips")]
        public static void ListAnimationClips()
        {
            Debug.Log("=== Animation Clips in Rifle_FBX ===");
            ListClipsInFolder(RIFLE_FBX_PATH);

            Debug.Log("\n=== Animation Clips in Locomotion_FBX ===");
            ListClipsInFolder(LOCOMOTION_FBX_PATH);

            EditorUtility.DisplayDialog("Done", "Check Console for animation clip list.", "OK");
        }

        static void ListClipsInFolder(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath)) return;

            string fullPath = Path.Combine(Application.dataPath, folderPath.Substring("Assets/".Length));
            if (!Directory.Exists(fullPath)) return;

            string[] fbxFiles = Directory.GetFiles(fullPath, "*.fbx", SearchOption.TopDirectoryOnly);

            foreach (var fbxFile in fbxFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(fbxFile);
                if (fileName.ToLower() == "character") continue;

                string assetPath = "Assets" + fbxFile.Substring(Application.dataPath.Length).Replace("\\", "/");

                // Load clips from FBX
                var subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                foreach (var asset in subAssets)
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        Debug.Log($"  {fileName}: '{clip.name}' ({clip.length:F2}s, loop={clip.isLooping})");
                    }
                }
            }
        }
    }
}
