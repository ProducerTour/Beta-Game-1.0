using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

namespace CreatorWorld.Editor
{
    /// <summary>
    /// Sets up the Crouch Blend Tree with 2D Freeform Directional blending.
    ///
    /// Run: Tools > Creator World > Setup Crouch Blend Tree
    /// </summary>
    public class CrouchBlendTreeSetup : EditorWindow
    {
        private const string ControllerPath = "Assets/_Project/Settings/PlayerAnimator.controller";
        private const string CrouchAnimationFolder = "Assets/Art/Animations/Crouch/Unarmed";

        [MenuItem("Tools/Creator World/Setup Crouch Blend Tree")]
        public static void Setup()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Error", "Stop Play mode first.", "OK");
                return;
            }

            Debug.Log("=== Setting Up Crouch Blend Tree ===");

            // Load animator controller
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"Controller not found at {ControllerPath}");
                return;
            }

            // Find the Crouch state with the blend tree
            var rootStateMachine = controller.layers[0].stateMachine;
            AnimatorState crouchState = null;
            BlendTree crouchBlendTree = null;

            foreach (var childState in rootStateMachine.states)
            {
                if (childState.state.name == "Crouch")
                {
                    crouchState = childState.state;
                    crouchBlendTree = crouchState.motion as BlendTree;
                    break;
                }
            }

            if (crouchState == null)
            {
                Debug.LogError("Crouch state not found in animator controller!");
                return;
            }

            if (crouchBlendTree == null)
            {
                Debug.LogError("Crouch state does not have a BlendTree motion!");
                return;
            }

            // Load animation clips from FBX files
            var clips = LoadCrouchClips();
            if (clips.idle == null)
            {
                Debug.LogError("Could not find crouch idle animation!");
                return;
            }

            // Configure the blend tree as 2D Freeform Directional
            crouchBlendTree.blendType = BlendTreeType.FreeformDirectional2D;
            crouchBlendTree.blendParameter = "MoveX";
            crouchBlendTree.blendParameterY = "MoveZ";

            // Clear existing children
            crouchBlendTree.children = new ChildMotion[0];

            // Add motions at correct positions
            // Center (0, 0) = Idle
            AddMotion(crouchBlendTree, clips.idle, 0f, 0f);

            // Forward (0, 0.5) = Walk Forward
            if (clips.walkForward != null)
                AddMotion(crouchBlendTree, clips.walkForward, 0f, 0.5f);

            // Backward (0, -0.5) = Walk Backward
            if (clips.walkBackward != null)
                AddMotion(crouchBlendTree, clips.walkBackward, 0f, -0.5f);

            // Left (-0.5, 0) = Strafe Left
            if (clips.walkLeft != null)
                AddMotion(crouchBlendTree, clips.walkLeft, -0.5f, 0f);

            // Right (0.5, 0) = Strafe Right
            if (clips.walkRight != null)
                AddMotion(crouchBlendTree, clips.walkRight, 0.5f, 0f);

            // Mark as dirty and save
            EditorUtility.SetDirty(crouchBlendTree);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Report results
            int clipCount = 1; // idle
            if (clips.walkForward != null) clipCount++;
            if (clips.walkBackward != null) clipCount++;
            if (clips.walkLeft != null) clipCount++;
            if (clips.walkRight != null) clipCount++;

            Debug.Log($"Crouch Blend Tree configured with {clipCount} animations:");
            Debug.Log($"  Idle: {clips.idle?.name ?? "MISSING"}");
            Debug.Log($"  Forward: {clips.walkForward?.name ?? "MISSING"}");
            Debug.Log($"  Backward: {clips.walkBackward?.name ?? "MISSING"}");
            Debug.Log($"  Left: {clips.walkLeft?.name ?? "MISSING"}");
            Debug.Log($"  Right: {clips.walkRight?.name ?? "MISSING"}");

            EditorUtility.DisplayDialog("Crouch Blend Tree Setup",
                $"Successfully configured crouch blend tree with {clipCount} animations.\n\n" +
                "The blend tree uses MoveX and MoveZ parameters for 8-directional movement.\n\n" +
                "Press Play to test!",
                "OK");
        }

        private static void AddMotion(BlendTree blendTree, AnimationClip clip, float x, float y)
        {
            var children = blendTree.children;
            var newChildren = new ChildMotion[children.Length + 1];
            children.CopyTo(newChildren, 0);

            newChildren[children.Length] = new ChildMotion
            {
                motion = clip,
                position = new Vector2(x, y),
                timeScale = 1f
            };

            blendTree.children = newChildren;
            Debug.Log($"  Added: {clip.name} at ({x}, {y})");
        }

        private static CrouchClips LoadCrouchClips()
        {
            var clips = new CrouchClips();

            string fullPath = Path.Combine(Application.dataPath, CrouchAnimationFolder.Substring("Assets/".Length));
            if (!Directory.Exists(fullPath))
            {
                Debug.LogError($"Crouch animation folder not found: {CrouchAnimationFolder}");
                return clips;
            }

            string[] fbxFiles = Directory.GetFiles(fullPath, "*.fbx", SearchOption.TopDirectoryOnly);
            Debug.Log($"Found {fbxFiles.Length} FBX files in {CrouchAnimationFolder}");

            foreach (var fbxFile in fbxFiles)
            {
                string assetPath = "Assets" + fbxFile.Substring(Application.dataPath.Length).Replace("\\", "/");
                string fileName = Path.GetFileNameWithoutExtension(fbxFile).ToLower();

                // Load the animation clip from the FBX
                var subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                AnimationClip clip = null;

                foreach (var asset in subAssets)
                {
                    if (asset is AnimationClip c && !c.name.StartsWith("__preview__"))
                    {
                        clip = c;
                        break;
                    }
                }

                if (clip == null)
                {
                    Debug.LogWarning($"No animation clip found in: {assetPath}");
                    continue;
                }

                Debug.Log($"Processing: {fileName} -> {clip.name}");

                // Match file names to clip slots
                if (fileName.Contains("idle"))
                {
                    clips.idle = clip;
                }
                else if (fileName.Contains("backward") || fileName.Contains("back"))
                {
                    clips.walkBackward = clip;
                }
                else if (fileName.Contains("left"))
                {
                    clips.walkLeft = clip;
                }
                else if (fileName.Contains("right"))
                {
                    clips.walkRight = clip;
                }
                else if (fileName.Contains("walk") || fileName.Contains("walking") || fileName.Contains("sneak"))
                {
                    // Forward walk - only if not already matched as directional
                    if (clips.walkForward == null)
                    {
                        clips.walkForward = clip;
                    }
                }
            }

            return clips;
        }

        private struct CrouchClips
        {
            public AnimationClip idle;
            public AnimationClip walkForward;
            public AnimationClip walkBackward;
            public AnimationClip walkLeft;
            public AnimationClip walkRight;
        }

        [MenuItem("Tools/Creator World/Debug: List Crouch FBX Files")]
        public static void DebugListFiles()
        {
            string fullPath = Path.Combine(Application.dataPath, CrouchAnimationFolder.Substring("Assets/".Length));
            if (!Directory.Exists(fullPath))
            {
                Debug.LogError($"Folder not found: {fullPath}");
                return;
            }

            string[] fbxFiles = Directory.GetFiles(fullPath, "*.fbx", SearchOption.TopDirectoryOnly);
            Debug.Log($"=== Crouch FBX Files ({fbxFiles.Length}) ===");

            foreach (var fbxFile in fbxFiles)
            {
                string assetPath = "Assets" + fbxFile.Substring(Application.dataPath.Length).Replace("\\", "/");
                string fileName = Path.GetFileNameWithoutExtension(fbxFile);

                var subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                string clipName = "NO CLIP";

                foreach (var asset in subAssets)
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        clipName = clip.name;
                        break;
                    }
                }

                Debug.Log($"  {fileName} -> {clipName}");
            }
        }
    }
}
