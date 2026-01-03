using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;

namespace CreatorWorld.Editor
{
    /// <summary>
    /// Automated game setup - creates scene, player, terrain, everything.
    /// Run from menu: Tools > Creator World > Setup Game
    /// </summary>
    public class GameSetup : EditorWindow
    {
        [MenuItem("Tools/Creator World/Setup Game (Full Auto)")]
        public static void SetupGame()
        {
            Debug.Log("=== Creator World Setup Starting ===");

            // Step 1: Create and save the scene
            CreateGameScene();

            // Step 2: Setup lighting
            SetupLighting();

            // Step 3: Create player
            CreatePlayer();

            // Step 4: Create managers
            CreateManagers();

            // Step 5: Setup camera
            SetupCamera();

            // Step 6: Add ground plane (temporary until terrain works)
            CreateTemporaryGround();

            // Save the scene
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

            Debug.Log("=== Creator World Setup Complete! Press Play to test. ===");
            EditorUtility.DisplayDialog("Setup Complete",
                "Game scene created!\n\n" +
                "Press PLAY to test.\n\n" +
                "Controls:\n" +
                "- WASD: Move\n" +
                "- Mouse: Look\n" +
                "- Shift: Sprint\n" +
                "- Space: Jump\n" +
                "- C: Crouch",
                "OK");
        }

        static void CreateGameScene()
        {
            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Ensure Scenes directory exists
            string scenePath = "Assets/_Project/Scenes";
            if (!AssetDatabase.IsValidFolder(scenePath))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Scenes");
            }

            // Save scene
            string fullPath = scenePath + "/Game.unity";
            EditorSceneManager.SaveScene(scene, fullPath);
            Debug.Log($"Created scene: {fullPath}");
        }

        static void SetupLighting()
        {
            // Create directional light (sun)
            var sunGO = new GameObject("Sun");
            var light = sunGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.intensity = 1.5f;
            light.shadows = LightShadows.Soft;
            sunGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Debug.Log("Created lighting");
        }

        static void CreatePlayer()
        {
            // Try to find XBot model
            string[] xbotPaths = new string[]
            {
                "Assets/Art/Models/Characters/xbot.glb",
                "Assets/Art/Models/Characters/xbot.fbx",
            };

            GameObject xbotPrefab = null;
            foreach (var path in xbotPaths)
            {
                xbotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (xbotPrefab != null) break;
            }

            GameObject player;
            if (xbotPrefab != null)
            {
                player = (GameObject)PrefabUtility.InstantiatePrefab(xbotPrefab);
                player.name = "Player";
                Debug.Log("Created player from XBot model");
            }
            else
            {
                // Fallback: create capsule placeholder
                player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.name = "Player";
                Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
                Debug.LogWarning("XBot not found - created capsule placeholder. Import xbot.glb to Assets/Art/Models/Characters/");
            }

            player.transform.position = new Vector3(0, 2, 0);
            player.tag = "Player";

            // Add CharacterController
            var cc = player.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0, 1f, 0);

            // Add our scripts
            AddComponentIfExists(player, "CreatorWorld.Player.PlayerController");
            AddComponentIfExists(player, "CreatorWorld.Player.PlayerAnimation");
            AddComponentIfExists(player, "CreatorWorld.Player.PlayerHealth");

            // Add PlayerInput component
            var playerInput = player.AddComponent<UnityEngine.InputSystem.PlayerInput>();

            // Try to find and assign input actions
            var inputActions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(
                "Assets/_Project/Settings/Input/PlayerInputActions.inputactions");
            if (inputActions != null)
            {
                playerInput.actions = inputActions;
                playerInput.defaultActionMap = "Player";
                Debug.Log("Assigned input actions");
            }

            Debug.Log("Player created with all components");
        }

        static void CreateManagers()
        {
            // Create Managers parent
            var managers = new GameObject("--- MANAGERS ---");

            // Game Manager
            var gameManagerGO = new GameObject("GameManager");
            gameManagerGO.transform.parent = managers.transform;
            AddComponentIfExists(gameManagerGO, "CreatorWorld.Core.GameManager");

            // Chunk Manager
            var chunkManagerGO = new GameObject("ChunkManager");
            chunkManagerGO.transform.parent = managers.transform;
            AddComponentIfExists(chunkManagerGO, "CreatorWorld.World.ChunkManager");

            Debug.Log("Created managers");
        }

        static void SetupCamera()
        {
            // Create main camera
            var cameraGO = new GameObject("Main Camera");
            cameraGO.tag = "MainCamera";

            var camera = cameraGO.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;

            cameraGO.AddComponent<AudioListener>();

            // Add our camera controller
            AddComponentIfExists(cameraGO, "CreatorWorld.Player.PlayerCamera");

            // Position behind player
            cameraGO.transform.position = new Vector3(0, 3, -5);
            cameraGO.transform.LookAt(Vector3.up * 1.5f);

            Debug.Log("Created camera");
        }

        static void CreateTemporaryGround()
        {
            // Create a simple ground plane for testing
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "TemporaryGround";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(100, 1, 100);

            // Set layer to Ground
            ground.layer = LayerMask.NameToLayer("Default");

            // Make it green
            var renderer = ground.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.2f, 0.5f, 0.2f);
                renderer.material = mat;
            }

            Debug.Log("Created temporary ground plane");
        }

        static void AddComponentIfExists(GameObject go, string typeName)
        {
            var type = System.Type.GetType(typeName + ", Assembly-CSharp");
            if (type != null)
            {
                go.AddComponent(type);
                Debug.Log($"Added component: {typeName}");
            }
            else
            {
                Debug.LogWarning($"Component not found: {typeName} (scripts may need to compile)");
            }
        }

        // Additional utilities
        [MenuItem("Tools/Creator World/Setup XBot Rig (Humanoid)")]
        public static void SetupXBotRig()
        {
            string path = "Assets/Art/Models/Characters/xbot.glb";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;

            if (importer != null)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
                Debug.Log("XBot configured as Humanoid");
                EditorUtility.DisplayDialog("Success", "XBot rig set to Humanoid!\nYou may need to click 'Configure' to verify bone mapping.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "XBot model not found at:\n" + path, "OK");
            }
        }

        [MenuItem("Tools/Creator World/Create Player Prefab")]
        public static void CreatePlayerPrefab()
        {
            var player = GameObject.Find("Player");
            if (player == null)
            {
                EditorUtility.DisplayDialog("Error", "No 'Player' object in scene. Run Setup Game first.", "OK");
                return;
            }

            string prefabPath = "Assets/_Project/Prefabs/Player";
            if (!AssetDatabase.IsValidFolder(prefabPath))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Player");
            }

            string fullPath = prefabPath + "/Player.prefab";
            PrefabUtility.SaveAsPrefabAsset(player, fullPath);
            Debug.Log($"Created prefab: {fullPath}");
            EditorUtility.DisplayDialog("Success", "Player prefab created!", "OK");
        }
    }
}
