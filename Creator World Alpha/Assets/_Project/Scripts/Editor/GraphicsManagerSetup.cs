using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using CreatorWorld.Core;
using CreatorWorld.Config;
using CreatorWorld.UI;

namespace CreatorWorld.Editor
{
    /// <summary>
    /// Editor utilities for setting up graphics, pause menu, and grass systems.
    /// Access via: Tools > Creator World > Setup
    /// </summary>
    public static class GraphicsManagerSetup
    {
        private const string PRESETS_PATH = "Assets/_Project/ScriptableObjects/Graphics";
        private const string GRASS_SETTINGS_PATH = "Assets/_Project/ScriptableObjects";

        [MenuItem("Tools/Creator World/Setup/1. Create Graphics Presets", priority = 100)]
        public static void CreateGraphicsPresets()
        {
            // Ensure directory exists
            if (!AssetDatabase.IsValidFolder("Assets/_Project/ScriptableObjects"))
                AssetDatabase.CreateFolder("Assets/_Project", "ScriptableObjects");
            if (!AssetDatabase.IsValidFolder(PRESETS_PATH))
                AssetDatabase.CreateFolder("Assets/_Project/ScriptableObjects", "Graphics");

            // Create Low preset
            var low = ScriptableObject.CreateInstance<GameGraphicsSettings>();
            low.presetName = "Low";
            low.grassInstancesPerChunk = 8192;
            low.grassViewDistance = 50f;
            low.grassShadows = false;
            low.grassOcclusionCulling = false;
            low.viewDistanceChunks = 2;
            low.terrainHighDetailDistance = 1;
            low.terrainMediumDetailDistance = 1;
            low.shadowQuality = 1;
            low.shadowDistance = 30f;
            low.renderScale = 0.75f;
            low.ssaoEnabled = false;
            low.bloomEnabled = false;
            low.antiAliasing = 0;
            low.fogEnabled = true;
            low.ambientParticles = false;
            AssetDatabase.CreateAsset(low, $"{PRESETS_PATH}/LowGraphicsSettings.asset");

            // Create Medium preset
            var medium = ScriptableObject.CreateInstance<GameGraphicsSettings>();
            medium.presetName = "Medium";
            medium.grassInstancesPerChunk = 32768;
            medium.grassViewDistance = 80f;
            medium.grassShadows = false;
            medium.grassOcclusionCulling = true;
            medium.viewDistanceChunks = 3;
            medium.terrainHighDetailDistance = 1;
            medium.terrainMediumDetailDistance = 2;
            medium.shadowQuality = 2;
            medium.shadowDistance = 50f;
            medium.renderScale = 1.0f;
            medium.ssaoEnabled = false;
            medium.bloomEnabled = true;
            medium.antiAliasing = 1;
            medium.fogEnabled = true;
            medium.ambientParticles = true;
            AssetDatabase.CreateAsset(medium, $"{PRESETS_PATH}/MediumGraphicsSettings.asset");

            // Create High preset
            var high = ScriptableObject.CreateInstance<GameGraphicsSettings>();
            high.presetName = "High";
            high.grassInstancesPerChunk = 65536;
            high.grassViewDistance = 120f;
            high.grassShadows = false;
            high.grassOcclusionCulling = true;
            high.viewDistanceChunks = 4;
            high.terrainHighDetailDistance = 2;
            high.terrainMediumDetailDistance = 3;
            high.shadowQuality = 2;
            high.shadowDistance = 80f;
            high.renderScale = 1.0f;
            high.ssaoEnabled = true;
            high.bloomEnabled = true;
            high.antiAliasing = 2;
            high.fogEnabled = true;
            high.ambientParticles = true;
            AssetDatabase.CreateAsset(high, $"{PRESETS_PATH}/HighGraphicsSettings.asset");

            // Create Ultra preset
            var ultra = ScriptableObject.CreateInstance<GameGraphicsSettings>();
            ultra.presetName = "Ultra";
            ultra.grassInstancesPerChunk = 150000;
            ultra.grassViewDistance = 200f;
            ultra.grassShadows = true;
            ultra.grassOcclusionCulling = true;
            ultra.viewDistanceChunks = 5;
            ultra.terrainHighDetailDistance = 2;
            ultra.terrainMediumDetailDistance = 4;
            ultra.shadowQuality = 3;
            ultra.shadowDistance = 120f;
            ultra.renderScale = 1.0f;
            ultra.ssaoEnabled = true;
            ultra.bloomEnabled = true;
            ultra.antiAliasing = 3;
            ultra.fogEnabled = true;
            ultra.ambientParticles = true;
            AssetDatabase.CreateAsset(ultra, $"{PRESETS_PATH}/UltraGraphicsSettings.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Setup] Created 4 graphics presets in " + PRESETS_PATH);
            EditorUtility.DisplayDialog("Graphics Presets Created",
                "Created:\n• LowGraphicsSettings\n• MediumGraphicsSettings\n• HighGraphicsSettings\n• UltraGraphicsSettings\n\nLocation: " + PRESETS_PATH,
                "OK");
        }

        [MenuItem("Tools/Creator World/Setup/2. Create Grass Settings", priority = 101)]
        public static void CreateGrassSettings()
        {
            // Ensure directory exists
            if (!AssetDatabase.IsValidFolder("Assets/_Project/ScriptableObjects"))
                AssetDatabase.CreateFolder("Assets/_Project", "ScriptableObjects");

            var grassSettings = ScriptableObject.CreateInstance<GrassSettings>();
            grassSettings.instancesPerChunk = 65536;
            grassSettings.minDensityThreshold = 0.1f;
            grassSettings.scaleMin = new Vector3(0.8f, 0.5f, 0.8f);
            grassSettings.scaleMax = new Vector3(1.2f, 1.5f, 1.2f);
            grassSettings.maxViewDistance = 150f;
            grassSettings.lod1Threshold = 0.25f;
            grassSettings.lod2Threshold = 0.5f;
            grassSettings.fadeStart = 0.7f;
            grassSettings.fadeEnd = 1.0f;
            grassSettings.windStrength = 0.5f;
            grassSettings.windSpeed = 1.0f;
            grassSettings.windDirection = new Vector2(1f, 0.5f);
            grassSettings.windNoiseScale = 0.1f;
            grassSettings.baseColor = new Color(0.2f, 0.4f, 0.1f);
            grassSettings.tipColor = new Color(0.4f, 0.6f, 0.2f);
            grassSettings.aoColor = new Color(0.1f, 0.15f, 0.05f);
            grassSettings.alphaCutoff = 0.5f;
            grassSettings.castShadows = false;
            grassSettings.useOcclusionCulling = true;
            grassSettings.subChunkSize = 16;
            grassSettings.densityScaleInfluence = 0.3f;

            AssetDatabase.CreateAsset(grassSettings, $"{GRASS_SETTINGS_PATH}/DefaultGrassSettings.asset");
            AssetDatabase.SaveAssets();

            Debug.Log("[Setup] Created DefaultGrassSettings.asset");
            EditorUtility.DisplayDialog("Grass Settings Created",
                "Created DefaultGrassSettings.asset\n\nLocation: " + GRASS_SETTINGS_PATH + "\n\nNote: You'll need to assign LOD meshes (simple billboard quads).",
                "OK");
        }

        [MenuItem("Tools/Creator World/Setup/3. Create GraphicsManager GameObject", priority = 102)]
        public static void CreateGraphicsManager()
        {
            // Check if already exists
            var existing = Object.FindFirstObjectByType<GraphicsManager>();
            if (existing != null)
            {
                EditorUtility.DisplayDialog("Already Exists",
                    "GraphicsManager already exists in the scene.\nSelect: " + existing.gameObject.name,
                    "OK");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            // Create GameObject
            GameObject go = new GameObject("GraphicsManager");
            var manager = go.AddComponent<GraphicsManager>();

            // Try to find and assign presets
            var low = AssetDatabase.LoadAssetAtPath<GameGraphicsSettings>($"{PRESETS_PATH}/LowGraphicsSettings.asset");
            var medium = AssetDatabase.LoadAssetAtPath<GameGraphicsSettings>($"{PRESETS_PATH}/MediumGraphicsSettings.asset");
            var high = AssetDatabase.LoadAssetAtPath<GameGraphicsSettings>($"{PRESETS_PATH}/HighGraphicsSettings.asset");
            var ultra = AssetDatabase.LoadAssetAtPath<GameGraphicsSettings>($"{PRESETS_PATH}/UltraGraphicsSettings.asset");

            SerializedObject so = new SerializedObject(manager);
            so.FindProperty("lowPreset").objectReferenceValue = low;
            so.FindProperty("mediumPreset").objectReferenceValue = medium;
            so.FindProperty("highPreset").objectReferenceValue = high;
            so.FindProperty("ultraPreset").objectReferenceValue = ultra;
            so.ApplyModifiedProperties();

            Undo.RegisterCreatedObjectUndo(go, "Create GraphicsManager");
            Selection.activeGameObject = go;

            string message = "Created GraphicsManager GameObject.";
            if (low == null || medium == null || high == null || ultra == null)
            {
                message += "\n\n⚠️ Some presets not found!\nRun 'Create Graphics Presets' first.";
            }
            else
            {
                message += "\n\n✓ All presets assigned.";
            }
            message += "\n\nManually assign:\n• URP Asset\n• Volume Profile";

            Debug.Log("[Setup] Created GraphicsManager");
            EditorUtility.DisplayDialog("GraphicsManager Created", message, "OK");
        }

        [MenuItem("Tools/Creator World/Setup/4. Create Pause Menu UI", priority = 103)]
        public static void CreatePauseMenuUI()
        {
            // Check if Canvas exists
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                // Create Canvas
                GameObject canvasGO = new GameObject("Canvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
                Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");
            }

            // Check if EventSystem exists
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
            }

            // Create PauseMenu parent
            GameObject pauseMenuGO = new GameObject("PauseMenu");
            pauseMenuGO.transform.SetParent(canvas.transform, false);
            var pauseMenu = pauseMenuGO.AddComponent<PauseMenu>();
            var pauseRect = pauseMenuGO.AddComponent<RectTransform>();
            pauseRect.anchorMin = Vector2.zero;
            pauseRect.anchorMax = Vector2.one;
            pauseRect.offsetMin = Vector2.zero;
            pauseRect.offsetMax = Vector2.zero;

            // Create Panel (background)
            GameObject panelGO = CreateUIPanel("PauseMenuPanel", pauseMenuGO.transform);
            panelGO.SetActive(false);

            // Create Main Buttons Panel
            GameObject mainButtonsGO = new GameObject("MainButtonsPanel");
            mainButtonsGO.transform.SetParent(panelGO.transform, false);
            var mainButtonsRect = mainButtonsGO.AddComponent<RectTransform>();
            mainButtonsRect.anchorMin = new Vector2(0.5f, 0.5f);
            mainButtonsRect.anchorMax = new Vector2(0.5f, 0.5f);
            mainButtonsRect.sizeDelta = new Vector2(300, 200);
            var mainButtonsLayout = mainButtonsGO.AddComponent<VerticalLayoutGroup>();
            mainButtonsLayout.spacing = 10;
            mainButtonsLayout.childAlignment = TextAnchor.MiddleCenter;
            mainButtonsLayout.childControlWidth = true;
            mainButtonsLayout.childControlHeight = false;
            mainButtonsLayout.childForceExpandWidth = true;

            // Create buttons
            var resumeBtn = CreateUIButton("ResumeButton", "Resume", mainButtonsGO.transform);
            var settingsBtn = CreateUIButton("SettingsButton", "Settings", mainButtonsGO.transform);
            var quitBtn = CreateUIButton("QuitButton", "Quit", mainButtonsGO.transform);

            // Create Settings Panel
            GameObject settingsPanelGO = CreateUIPanel("SettingsPanel", panelGO.transform);
            settingsPanelGO.SetActive(false);

            // Add GraphicsSettingsUI to settings panel
            var graphicsUI = settingsPanelGO.AddComponent<GraphicsSettingsUI>();

            // Create Back button in settings
            var backBtn = CreateUIButton("BackButton", "Back", settingsPanelGO.transform);
            var backRect = backBtn.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0, 0);
            backRect.anchorMax = new Vector2(0, 0);
            backRect.pivot = new Vector2(0, 0);
            backRect.anchoredPosition = new Vector2(20, 20);

            // Wire up PauseMenu references
            SerializedObject so = new SerializedObject(pauseMenu);
            so.FindProperty("pauseMenuPanel").objectReferenceValue = panelGO;
            so.FindProperty("settingsPanel").objectReferenceValue = settingsPanelGO;
            so.FindProperty("mainButtonsPanel").objectReferenceValue = mainButtonsGO;
            so.FindProperty("resumeButton").objectReferenceValue = resumeBtn;
            so.FindProperty("settingsButton").objectReferenceValue = settingsBtn;
            so.FindProperty("quitButton").objectReferenceValue = quitBtn;
            so.FindProperty("backButton").objectReferenceValue = backBtn;
            so.FindProperty("graphicsSettingsUI").objectReferenceValue = graphicsUI;
            so.ApplyModifiedProperties();

            Undo.RegisterCreatedObjectUndo(pauseMenuGO, "Create Pause Menu");
            Selection.activeGameObject = pauseMenuGO;

            Debug.Log("[Setup] Created Pause Menu UI structure");
            EditorUtility.DisplayDialog("Pause Menu Created",
                "Created basic Pause Menu UI structure.\n\nPress ESC to toggle pause menu.\n\nNote: GraphicsSettingsUI needs sliders/dropdowns wired up manually for full functionality.",
                "OK");
        }

        [MenuItem("Tools/Creator World/Setup/5. Wire Up GrassManager", priority = 104)]
        public static void WireUpGrassManager()
        {
            var grassManager = Object.FindFirstObjectByType<CreatorWorld.World.GrassManager>();
            if (grassManager == null)
            {
                EditorUtility.DisplayDialog("GrassManager Not Found",
                    "GrassManager component not found in scene.\n\nMake sure ChunkManager has a GrassManager reference.",
                    "OK");
                return;
            }

            // Find grass settings
            var grassSettings = AssetDatabase.LoadAssetAtPath<GrassSettings>($"{GRASS_SETTINGS_PATH}/DefaultGrassSettings.asset");

            // Find grass material
            var grassMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/GrassInstanced.mat");

            SerializedObject so = new SerializedObject(grassManager);

            if (grassSettings != null)
                so.FindProperty("settings").objectReferenceValue = grassSettings;

            if (grassMaterial != null)
                so.FindProperty("grassMaterial").objectReferenceValue = grassMaterial;

            so.ApplyModifiedProperties();

            Selection.activeGameObject = grassManager.gameObject;

            string message = "GrassManager wired up.\n\n";
            message += grassSettings != null ? "✓ GrassSettings assigned\n" : "⚠️ GrassSettings not found\n";
            message += grassMaterial != null ? "✓ GrassMaterial assigned\n" : "⚠️ GrassMaterial not found - create it\n";
            message += "\nStill needed:\n• Assign LOD meshes (simple quads)\n• Assign ChunkManager reference";

            Debug.Log("[Setup] Wired up GrassManager");
            EditorUtility.DisplayDialog("GrassManager Setup", message, "OK");
        }

        [MenuItem("Tools/Creator World/Setup/Run All Setup Steps", priority = 200)]
        public static void RunAllSetup()
        {
            CreateGraphicsPresets();
            CreateGrassSettings();
            CreateGraphicsManager();
            CreatePauseMenuUI();
            WireUpGrassManager();

            EditorUtility.DisplayDialog("Setup Complete",
                "All setup steps completed!\n\nCheck the console for any warnings.\n\nManual steps remaining:\n• Assign URP Asset to GraphicsManager\n• Assign Volume Profile to GraphicsManager\n• Create LOD meshes for grass\n• Style the Pause Menu UI",
                "OK");
        }

        #region UI Helpers

        private static GameObject CreateUIPanel(string name, Transform parent)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = panel.AddComponent<Image>();
            image.color = new Color(0, 0, 0, 0.8f);

            return panel;
        }

        private static Button CreateUIButton(string name, string text, Transform parent)
        {
            GameObject buttonGO = new GameObject(name);
            buttonGO.transform.SetParent(parent, false);

            var rect = buttonGO.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 40);

            var image = buttonGO.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f);

            var button = buttonGO.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.3f, 0.5f, 0.8f);
            colors.pressedColor = new Color(0.2f, 0.4f, 0.7f);
            button.colors = colors;

            // Create text child
            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(buttonGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmpText = textGO.AddComponent<TextMeshProUGUI>();
            tmpText.text = text;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.fontSize = 18;
            tmpText.color = Color.white;

            return button;
        }

        #endregion
    }
}
