#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace CreatorWorld.UI.Editor
{
    /// <summary>
    /// Editor utility to automatically create the Game Loading Screen UI.
    /// Access via menu: GameObject > UI > Create Game Loading Screen
    /// </summary>
    public static class GameLoadingScreenSetup
    {
        [MenuItem("GameObject/UI/Create Game Loading Screen", false, 10)]
        public static void CreateLoadingScreen()
        {
            // Find or create Canvas
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100; // Render on top
                var scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                canvasObj.AddComponent<GraphicRaycaster>();
                Undo.RegisterCreatedObjectUndo(canvasObj, "Create Loading Screen Canvas");
            }

            // Create main Loading Panel
            GameObject panelObj = new GameObject("GameLoadingScreen");
            panelObj.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // Background image (dark)
            Image bgImage = panelObj.AddComponent<Image>();
            bgImage.color = new Color(0.02f, 0.02f, 0.04f, 1f); // Very dark blue-black

            CanvasGroup canvasGroup = panelObj.AddComponent<CanvasGroup>();

            // Add the loading screen component
            GameLoadingScreen loadingScreen = panelObj.AddComponent<GameLoadingScreen>();

            // Create Logo placeholder (centered, top area)
            GameObject logoObj = new GameObject("Logo");
            logoObj.transform.SetParent(panelObj.transform, false);
            RectTransform logoRect = logoObj.AddComponent<RectTransform>();
            logoRect.anchorMin = new Vector2(0.5f, 1f);
            logoRect.anchorMax = new Vector2(0.5f, 1f);
            logoRect.pivot = new Vector2(0.5f, 1f);
            logoRect.sizeDelta = new Vector2(300, 150);
            logoRect.anchoredPosition = new Vector2(0, -80);

            Image logoImage = logoObj.AddComponent<Image>();
            logoImage.color = new Color(0.3f, 0.3f, 0.3f, 0.5f); // Placeholder gray

            // Create Title Text
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(panelObj.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(800, 80);
            titleRect.anchoredPosition = new Vector2(0, -250);

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "CREATOR WORLD";
            titleText.fontSize = 56;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;

            // Create bottom container for progress elements
            GameObject bottomContainer = new GameObject("BottomContainer");
            bottomContainer.transform.SetParent(panelObj.transform, false);
            RectTransform bottomRect = bottomContainer.AddComponent<RectTransform>();
            bottomRect.anchorMin = new Vector2(0, 0);
            bottomRect.anchorMax = new Vector2(1, 0);
            bottomRect.pivot = new Vector2(0.5f, 0);
            bottomRect.sizeDelta = new Vector2(0, 200);
            bottomRect.anchoredPosition = new Vector2(0, 50);

            // Create Status Text (current stage name)
            GameObject statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(bottomContainer.transform, false);
            RectTransform statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.5f, 1f);
            statusRect.anchorMax = new Vector2(0.5f, 1f);
            statusRect.pivot = new Vector2(0.5f, 1f);
            statusRect.sizeDelta = new Vector2(800, 40);
            statusRect.anchoredPosition = new Vector2(0, -10);

            TextMeshProUGUI statusText = statusObj.AddComponent<TextMeshProUGUI>();
            statusText.text = "Initializing...";
            statusText.fontSize = 28;
            statusText.fontStyle = FontStyles.Bold;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = new Color(0.9f, 0.9f, 0.9f, 1f);

            // Create Detail Text (specific operation)
            GameObject detailObj = new GameObject("DetailText");
            detailObj.transform.SetParent(bottomContainer.transform, false);
            RectTransform detailRect = detailObj.AddComponent<RectTransform>();
            detailRect.anchorMin = new Vector2(0.5f, 1f);
            detailRect.anchorMax = new Vector2(0.5f, 1f);
            detailRect.pivot = new Vector2(0.5f, 1f);
            detailRect.sizeDelta = new Vector2(800, 30);
            detailRect.anchoredPosition = new Vector2(0, -50);

            TextMeshProUGUI detailText = detailObj.AddComponent<TextMeshProUGUI>();
            detailText.text = "Preparing world generation...";
            detailText.fontSize = 18;
            detailText.alignment = TextAlignmentOptions.Center;
            detailText.color = new Color(0.6f, 0.6f, 0.6f, 1f);

            // Create Progress Bar Background
            GameObject progressBgObj = new GameObject("ProgressBarBackground");
            progressBgObj.transform.SetParent(bottomContainer.transform, false);
            RectTransform progressBgRect = progressBgObj.AddComponent<RectTransform>();
            progressBgRect.anchorMin = new Vector2(0.5f, 1f);
            progressBgRect.anchorMax = new Vector2(0.5f, 1f);
            progressBgRect.pivot = new Vector2(0.5f, 1f);
            progressBgRect.sizeDelta = new Vector2(600, 12);
            progressBgRect.anchoredPosition = new Vector2(0, -95);

            Image progressBgImage = progressBgObj.AddComponent<Image>();
            progressBgImage.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            // Create Progress Bar Fill
            GameObject progressFillObj = new GameObject("ProgressBarFill");
            progressFillObj.transform.SetParent(progressBgObj.transform, false);
            RectTransform progressFillRect = progressFillObj.AddComponent<RectTransform>();
            progressFillRect.anchorMin = Vector2.zero;
            progressFillRect.anchorMax = Vector2.one;
            progressFillRect.offsetMin = new Vector2(2, 2);
            progressFillRect.offsetMax = new Vector2(-2, -2);

            Image progressFillImage = progressFillObj.AddComponent<Image>();
            progressFillImage.color = new Color(0.85f, 0.55f, 0.2f, 1f); // Orange/rust color
            progressFillImage.type = Image.Type.Filled;
            progressFillImage.fillMethod = Image.FillMethod.Horizontal;
            progressFillImage.fillAmount = 0f;

            // Create Progress Percentage Text
            GameObject percentObj = new GameObject("ProgressPercentText");
            percentObj.transform.SetParent(bottomContainer.transform, false);
            RectTransform percentRect = percentObj.AddComponent<RectTransform>();
            percentRect.anchorMin = new Vector2(0.5f, 1f);
            percentRect.anchorMax = new Vector2(0.5f, 1f);
            percentRect.pivot = new Vector2(0.5f, 1f);
            percentRect.sizeDelta = new Vector2(100, 30);
            percentRect.anchoredPosition = new Vector2(0, -115);

            TextMeshProUGUI percentText = percentObj.AddComponent<TextMeshProUGUI>();
            percentText.text = "0%";
            percentText.fontSize = 20;
            percentText.alignment = TextAlignmentOptions.Center;
            percentText.color = new Color(0.7f, 0.7f, 0.7f, 1f);

            // Create Tip Text (bottom of screen)
            GameObject tipObj = new GameObject("TipText");
            tipObj.transform.SetParent(bottomContainer.transform, false);
            RectTransform tipRect = tipObj.AddComponent<RectTransform>();
            tipRect.anchorMin = new Vector2(0.5f, 0f);
            tipRect.anchorMax = new Vector2(0.5f, 0f);
            tipRect.pivot = new Vector2(0.5f, 0f);
            tipRect.sizeDelta = new Vector2(800, 40);
            tipRect.anchoredPosition = new Vector2(0, 10);

            TextMeshProUGUI tipText = tipObj.AddComponent<TextMeshProUGUI>();
            tipText.text = "Procedural terrain generates unique worlds every time";
            tipText.fontSize = 16;
            tipText.fontStyle = FontStyles.Italic;
            tipText.alignment = TextAlignmentOptions.Center;
            tipText.color = new Color(0.5f, 0.5f, 0.5f, 1f);

            // Wire up the references using SerializedObject
            SerializedObject serializedScreen = new SerializedObject(loadingScreen);
            serializedScreen.FindProperty("loadingPanel").objectReferenceValue = panelObj;
            serializedScreen.FindProperty("backgroundImage").objectReferenceValue = bgImage;
            serializedScreen.FindProperty("logoImage").objectReferenceValue = logoImage;
            serializedScreen.FindProperty("titleText").objectReferenceValue = titleText;
            serializedScreen.FindProperty("statusText").objectReferenceValue = statusText;
            serializedScreen.FindProperty("detailText").objectReferenceValue = detailText;
            serializedScreen.FindProperty("tipText").objectReferenceValue = tipText;
            serializedScreen.FindProperty("progressBarFill").objectReferenceValue = progressFillImage;
            serializedScreen.FindProperty("progressPercentText").objectReferenceValue = percentText;
            serializedScreen.ApplyModifiedProperties();

            // Register undo
            Undo.RegisterCreatedObjectUndo(panelObj, "Create Game Loading Screen");

            // Select the created object
            Selection.activeGameObject = panelObj;

            Debug.Log("[GameLoadingScreenSetup] Game loading screen created successfully!");
            Debug.Log("Usage: Call GameLoadingScreen.SetStageProgress(stageId, progress, detail) to update progress");
            Debug.Log("Stages: init, terrain, grass, trees, rocks, water, finalize");
        }

        [MenuItem("GameObject/UI/Create Game Loading Screen", true)]
        public static bool ValidateCreateLoadingScreen()
        {
            // Only enable if not in play mode
            return !Application.isPlaying;
        }
    }
}
#endif
