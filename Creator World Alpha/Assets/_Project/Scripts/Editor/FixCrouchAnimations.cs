using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace CreatorWorld.Editor
{
    /// <summary>
    /// Fixes crouch idle animation by using clips directly from GLB files
    /// instead of extracted .anim files which may have issues.
    ///
    /// Run: Tools > Creator World > Fix Crouch Animations
    /// </summary>
    public class FixCrouchAnimations : EditorWindow
    {
        private const string CONTROLLER_PATH = "Assets/_Project/Settings/XBotAnimator.controller";
        private const string LOCOMOTION_GLB_PATH = "Assets/Art/Animations/Locomotion";

        [MenuItem("Tools/Creator World/Fix Crouch Animations")]
        public static void Fix()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Error", "Stop Play mode first.", "OK");
                return;
            }

            Debug.Log("=== Fixing Crouch Animations ===");

            // Load animator controller
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER_PATH);
            if (controller == null)
            {
                Debug.LogError($"Controller not found at {CONTROLLER_PATH}");
                return;
            }

            // Load clips directly from GLB files
            var glbClips = LoadGLBClips();
            Debug.Log($"Found {glbClips.Count} clips from GLB files");

            foreach (var kvp in glbClips)
            {
                Debug.Log($"  {kvp.Key}: {kvp.Value.length:F2}s, loop: {kvp.Value.isLooping}");
            }

            // Update the animator controller states to use GLB clips
            UpdateControllerStates(controller, glbClips);

            // Verify the Player animator
            VerifyPlayerAnimator(glbClips);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Crouch Animations Fixed",
                "Updated animator controller to use GLB clips directly.\n\n" +
                "This avoids issues with extracted .anim files.\n\n" +
                "Press Play to test!",
                "OK");
        }

        static Dictionary<string, AnimationClip> LoadGLBClips()
        {
            var clips = new Dictionary<string, AnimationClip>();

            if (!AssetDatabase.IsValidFolder(LOCOMOTION_GLB_PATH))
            {
                Debug.LogError($"GLB folder not found: {LOCOMOTION_GLB_PATH}");
                return clips;
            }

            // Find all GLB files
            string[] glbGuids = AssetDatabase.FindAssets("t:AnimationClip", new[] { LOCOMOTION_GLB_PATH });
            foreach (var guid in glbGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip != null && !clip.name.StartsWith("__"))
                {
                    // Use the file name as key
                    string fileName = Path.GetFileNameWithoutExtension(path).ToLower();

                    // Also try to extract from the clip name
                    string clipKey = clip.name.ToLower();
                    if (clipKey.Contains("|"))
                    {
                        // Remove animation name prefix like "Armature|mixamo.com|Layer0"
                        clipKey = fileName;
                    }

                    if (!clips.ContainsKey(clipKey))
                    {
                        clips[clipKey] = clip;
                    }
                }
            }

            // Also search for clips inside GLB files
            string[] glbFiles = Directory.GetFiles(
                Path.Combine(Application.dataPath, "Art/Animations/Locomotion"),
                "*.glb",
                SearchOption.AllDirectories);

            foreach (var glbFile in glbFiles)
            {
                string assetPath = "Assets" + glbFile.Substring(Application.dataPath.Length).Replace("\\", "/");
                string fileName = Path.GetFileNameWithoutExtension(glbFile).ToLower();

                var subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                foreach (var asset in subAssets)
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__"))
                    {
                        if (!clips.ContainsKey(fileName))
                        {
                            clips[fileName] = clip;
                            Debug.Log($"Found clip in GLB: {fileName} from {assetPath}");
                        }
                    }
                }
            }

            return clips;
        }

        static void UpdateControllerStates(AnimatorController controller, Dictionary<string, AnimationClip> clips)
        {
            var rootStateMachine = controller.layers[0].stateMachine;

            foreach (var childState in rootStateMachine.states)
            {
                var state = childState.state;
                string stateName = state.name.ToLower();

                Debug.Log($"Checking state: {state.name}");

                // Map state names to clip names
                string clipName = null;
                if (stateName.Contains("crouchidle") || stateName == "crouch_idle")
                {
                    clipName = "crouch_idle";
                }
                else if (stateName.Contains("crouchwalk") || stateName == "crouch_walk")
                {
                    clipName = "crouch_walk";
                }
                else if (stateName == "idle")
                {
                    clipName = "idle";
                }
                else if (stateName == "walk")
                {
                    clipName = "walk";
                }
                else if (stateName == "run")
                {
                    clipName = "run";
                }
                else if (stateName == "jump")
                {
                    clipName = "jump";
                }
                else if (stateName.Contains("strafeleft"))
                {
                    clipName = "strafe_left_walk";
                }
                else if (stateName.Contains("straferight"))
                {
                    clipName = "strafe_right_walk";
                }

                if (clipName != null && clips.TryGetValue(clipName, out var clip))
                {
                    // Check if current motion is different
                    var currentMotion = state.motion as AnimationClip;
                    if (currentMotion != clip)
                    {
                        Debug.Log($"  Updating {state.name}: {currentMotion?.name ?? "null"} -> {clip.name}");
                        state.motion = clip;
                        EditorUtility.SetDirty(controller);
                    }
                    else
                    {
                        Debug.Log($"  {state.name}: Already using correct clip");
                    }
                }
                else if (state.motion is BlendTree bt)
                {
                    // Handle blend trees
                    UpdateBlendTree(bt, clips);
                    EditorUtility.SetDirty(controller);
                }
            }
        }

        static void UpdateBlendTree(BlendTree blendTree, Dictionary<string, AnimationClip> clips)
        {
            Debug.Log($"  Updating BlendTree: {blendTree.name}");

            var children = blendTree.children;
            for (int i = 0; i < children.Length; i++)
            {
                var child = children[i];
                if (child.motion is AnimationClip clip)
                {
                    // Try to find a matching clip from GLB
                    string clipName = clip.name.ToLower();

                    // Extract base name
                    if (clipName.Contains("|"))
                    {
                        // Parse "Armature|mixamo.com|Layer0" style names
                        var parts = clipName.Split('|');
                        if (parts.Length > 0)
                        {
                            clipName = parts[0].ToLower();
                        }
                    }

                    // Try exact match first
                    if (clips.TryGetValue(clipName, out var newClip))
                    {
                        if (child.motion != newClip)
                        {
                            Debug.Log($"    Updating child {i}: {clip.name} -> {newClip.name}");
                            children[i].motion = newClip;
                        }
                    }
                }
            }

            blendTree.children = children;
        }

        static void VerifyPlayerAnimator(Dictionary<string, AnimationClip> clips)
        {
            var player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogWarning("Player not found in scene");
                return;
            }

            var animator = player.GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogWarning("No Animator on Player");
                return;
            }

            // Check skeleton structure
            var skinnedMesh = player.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skinnedMesh == null)
            {
                Debug.LogWarning("No SkinnedMeshRenderer on Player - may be capsule, not XBot");
                return;
            }

            // Check root bone
            Transform rootBone = skinnedMesh.rootBone;
            if (rootBone == null)
            {
                Debug.LogWarning("No root bone set on SkinnedMeshRenderer");
                return;
            }

            Debug.Log($"Root bone: {rootBone.name}");
            Debug.Log($"Root bone path from Player: {GetPath(rootBone, player.transform)}");

            // Check if path matches animation targets
            string expectedAnimPath = "mixamorig:Hips";
            string actualPath = GetPath(rootBone, player.transform);

            if (actualPath != expectedAnimPath)
            {
                Debug.LogWarning($"Path mismatch!\n  Animation targets: {expectedAnimPath}\n  Actual path: {actualPath}");
                Debug.Log("This may cause animations to not work. The skeleton needs to be at the correct hierarchy level.");
            }
            else
            {
                Debug.Log("Path matches - animations should work correctly");
            }
        }

        static string GetPath(Transform target, Transform root)
        {
            if (target == root) return "";

            string path = target.name;
            Transform current = target.parent;

            while (current != null && current != root)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        [MenuItem("Tools/Creator World/Debug: Check Animation Clip Info")]
        public static void DebugClipInfo()
        {
            // Check a specific clip
            string clipPath = "Assets/_Project/Animations/crouch_idle.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

            if (clip == null)
            {
                Debug.LogError($"Clip not found: {clipPath}");
                return;
            }

            string report = $"=== Animation Clip: {clip.name} ===\n\n";
            report += $"Length: {clip.length:F2}s\n";
            report += $"Frame Rate: {clip.frameRate}\n";
            report += $"Loop: {clip.isLooping}\n";
            report += $"Legacy: {clip.legacy}\n";
            report += $"Humanoid: {clip.humanMotion}\n";

            // Get bindings
            var bindings = AnimationUtility.GetCurveBindings(clip);
            report += $"\nCurve Bindings: {bindings.Length}\n";

            // Group by path
            var pathGroups = bindings.GroupBy(b => b.path).OrderBy(g => g.Key);
            foreach (var group in pathGroups.Take(10))
            {
                report += $"  {group.Key}: {group.Count()} curves\n";
            }

            if (pathGroups.Count() > 10)
            {
                report += $"  ... and {pathGroups.Count() - 10} more paths\n";
            }

            Debug.Log(report);

            // Now compare with GLB clip
            string glbPath = "Assets/Art/Animations/Locomotion/crouch_idle.glb";
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(glbPath);
            foreach (var asset in subAssets)
            {
                if (asset is AnimationClip glbClip)
                {
                    var glbBindings = AnimationUtility.GetCurveBindings(glbClip);
                    string glbReport = $"\n=== GLB Clip: {glbClip.name} ===\n";
                    glbReport += $"Length: {glbClip.length:F2}s\n";
                    glbReport += $"Curve Bindings: {glbBindings.Length}\n";

                    var glbPathGroups = glbBindings.GroupBy(b => b.path).OrderBy(g => g.Key);
                    foreach (var group in glbPathGroups.Take(10))
                    {
                        glbReport += $"  {group.Key}: {group.Count()} curves\n";
                    }

                    Debug.Log(glbReport);
                    break;
                }
            }

            EditorUtility.DisplayDialog("Clip Info", "Check console for details.", "OK");
        }
    }
}
