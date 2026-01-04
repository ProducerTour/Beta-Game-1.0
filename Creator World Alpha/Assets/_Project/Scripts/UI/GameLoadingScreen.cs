using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace CreatorWorld.UI
{
    /// <summary>
    /// Full game loading screen similar to Rust.
    /// Tracks multiple loading stages and displays progress.
    /// </summary>
    public class GameLoadingScreen : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image logoImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI detailText;
        [SerializeField] private TextMeshProUGUI tipText;
        [SerializeField] private Image progressBarFill;
        [SerializeField] private TextMeshProUGUI progressPercentText;

        [Header("Settings")]
        [SerializeField] private string gameTitle = "CREATOR WORLD";
        [SerializeField] private float minimumDisplayTime = 2f;
        [SerializeField] private float fadeOutDuration = 0.5f;
        [SerializeField] private float tipRotateInterval = 5f;
        [Tooltip("Automatically hide when all stages are complete")]
        [SerializeField] private bool autoHideOnComplete = true;

        [Header("Loading Tips")]
        [SerializeField] private string[] loadingTips = new string[]
        {
            "Procedural terrain generates unique worlds every time",
            "Rivers carve through the landscape naturally",
            "Trees and rocks spawn based on terrain slope",
            "The world continues beyond what you can see",
            "Weather systems affect the environment",
            "Explore to discover hidden areas"
        };

        // Singleton for easy access
        private static GameLoadingScreen instance;
        public static GameLoadingScreen Instance => instance;

        // Loading state
        private Dictionary<string, LoadingStage> loadingStages = new Dictionary<string, LoadingStage>();
        private float overallProgress = 0f;
        private bool isLoading = true;
        private float showTime;
        private CanvasGroup canvasGroup;
        private Coroutine tipCoroutine;

        private class LoadingStage
        {
            public string displayName;
            public float weight;
            public float progress;
            public bool isComplete;
        }

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private void Awake()
        {
            Debug.Log("[LoadingScreen] Awake called");

            // Singleton setup
            if (instance != null && instance != this)
            {
                Debug.Log("[LoadingScreen] Duplicate instance destroyed");
                Destroy(gameObject);
                return;
            }
            instance = this;

            // Get canvas group for fading
            canvasGroup = loadingPanel?.GetComponent<CanvasGroup>();
            if (canvasGroup == null && loadingPanel != null)
            {
                canvasGroup = loadingPanel.AddComponent<CanvasGroup>();
            }

            // Initialize default loading stages
            InitializeDefaultStages();

            // Show immediately
            Show();

            Debug.Log("[LoadingScreen] Initialized, waiting for terrain stage to complete");
        }

        private void Start()
        {
            // Set title
            if (titleText != null)
            {
                titleText.text = gameTitle;
            }

            // Start tip rotation
            if (tipText != null && loadingTips.Length > 0)
            {
                tipCoroutine = StartCoroutine(RotateTips());
            }

            showTime = Time.realtimeSinceStartup;
        }

        private void InitializeDefaultStages()
        {
            // Define loading stages with weights (total should = 1.0)
            // Only register stages that are actually being tracked
            // Add more stages here when other systems integrate with the loading screen
            RegisterStage("terrain", "Generating Terrain", 1.0f);

            // Future stages (uncomment when integrated):
            // RegisterStage("init", "Initializing", 0.05f);
            // RegisterStage("terrain", "Generating Terrain", 0.25f);
            // RegisterStage("vegetation", "Growing Vegetation", 0.30f);
            // RegisterStage("trees", "Placing Trees", 0.15f);
            // RegisterStage("rocks", "Spawning Rocks", 0.10f);
            // RegisterStage("water", "Filling Lakes & Rivers", 0.05f);
            // RegisterStage("finalize", "Finalizing World", 0.10f);
        }

        /// <summary>
        /// Register a custom loading stage
        /// </summary>
        public void RegisterStage(string stageId, string displayName, float weight)
        {
            loadingStages[stageId] = new LoadingStage
            {
                displayName = displayName,
                weight = weight,
                progress = 0f,
                isComplete = false
            };
        }

        /// <summary>
        /// Update progress for a specific stage (0-1)
        /// </summary>
        public void UpdateStageProgress(string stageId, float progress, string detail = null)
        {
            if (!loadingStages.ContainsKey(stageId))
            {
                if (showDebugLogs) Debug.LogWarning($"[LoadingScreen] Unknown stage: {stageId}");
                return;
            }

            var stage = loadingStages[stageId];
            stage.progress = Mathf.Clamp01(progress);

            if (showDebugLogs) Debug.Log($"[LoadingScreen] Stage '{stageId}' progress: {progress:P0} - {detail}");

            // Update UI
            if (statusText != null)
            {
                statusText.text = stage.displayName;
            }

            if (detailText != null && !string.IsNullOrEmpty(detail))
            {
                detailText.text = detail;
            }

            // Recalculate overall progress
            CalculateOverallProgress();
        }

        /// <summary>
        /// Mark a stage as complete
        /// </summary>
        public void CompleteStage(string stageId)
        {
            if (!loadingStages.ContainsKey(stageId))
            {
                if (showDebugLogs) Debug.LogWarning($"[LoadingScreen] Cannot complete unknown stage: {stageId}");
                return;
            }

            var stage = loadingStages[stageId];
            stage.progress = 1f;
            stage.isComplete = true;

            if (showDebugLogs) Debug.Log($"[LoadingScreen] Stage '{stageId}' COMPLETE");

            CalculateOverallProgress();

            // Check if all stages are complete
            if (autoHideOnComplete && AreAllStagesComplete())
            {
                if (showDebugLogs) Debug.Log("[LoadingScreen] All stages complete, hiding...");
                Hide();
            }
        }

        /// <summary>
        /// Check if all registered stages are complete
        /// </summary>
        private bool AreAllStagesComplete()
        {
            foreach (var stage in loadingStages.Values)
            {
                if (!stage.isComplete) return false;
            }
            return true;
        }

        private void CalculateOverallProgress()
        {
            float totalProgress = 0f;

            foreach (var stage in loadingStages.Values)
            {
                totalProgress += stage.progress * stage.weight;
            }

            overallProgress = totalProgress;

            // Update progress bar
            if (progressBarFill != null)
            {
                progressBarFill.fillAmount = overallProgress;
            }

            if (progressPercentText != null)
            {
                progressPercentText.text = $"{Mathf.RoundToInt(overallProgress * 100)}%";
            }
        }

        private IEnumerator RotateTips()
        {
            int currentTip = 0;

            while (isLoading)
            {
                if (loadingTips.Length > 0)
                {
                    tipText.text = loadingTips[currentTip];
                    currentTip = (currentTip + 1) % loadingTips.Length;
                }

                yield return new WaitForSecondsRealtime(tipRotateInterval);
            }
        }

        /// <summary>
        /// Show the loading screen
        /// </summary>
        public void Show()
        {
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(true);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            isLoading = true;
            showTime = Time.realtimeSinceStartup;

            // Reset stages
            foreach (var stage in loadingStages.Values)
            {
                stage.progress = 0f;
                stage.isComplete = false;
            }

            CalculateOverallProgress();
        }

        /// <summary>
        /// Hide the loading screen with fade
        /// </summary>
        public void Hide()
        {
            if (showDebugLogs) Debug.Log("[LoadingScreen] Hide() called, starting fade out...");
            StartCoroutine(HideRoutine());
        }

        private IEnumerator HideRoutine()
        {
            // Ensure minimum display time
            float elapsed = Time.realtimeSinceStartup - showTime;
            if (elapsed < minimumDisplayTime)
            {
                yield return new WaitForSecondsRealtime(minimumDisplayTime - elapsed);
            }

            // Update final state
            if (statusText != null) statusText.text = "Ready";
            if (detailText != null) detailText.text = "Entering world...";
            if (progressBarFill != null) progressBarFill.fillAmount = 1f;
            if (progressPercentText != null) progressPercentText.text = "100%";

            isLoading = false;

            // Stop tip rotation
            if (tipCoroutine != null)
            {
                StopCoroutine(tipCoroutine);
            }

            // Small delay before fade
            yield return new WaitForSecondsRealtime(0.3f);

            // Fade out
            if (canvasGroup != null && fadeOutDuration > 0)
            {
                float startAlpha = canvasGroup.alpha;
                float fadeElapsed = 0f;

                while (fadeElapsed < fadeOutDuration)
                {
                    fadeElapsed += Time.deltaTime;
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, fadeElapsed / fadeOutDuration);
                    yield return null;
                }
            }

            if (loadingPanel != null)
            {
                loadingPanel.SetActive(false);
            }

            if (showDebugLogs) Debug.Log("[LoadingScreen] Fade out complete, panel hidden");
        }

        // ============ STATIC HELPER METHODS ============

        /// <summary>
        /// Static method to update a loading stage
        /// </summary>
        public static void SetStageProgress(string stageId, float progress, string detail = null)
        {
            if (instance != null)
            {
                instance.UpdateStageProgress(stageId, progress, detail);
            }
        }

        /// <summary>
        /// Static method to complete a stage
        /// </summary>
        public static void FinishStage(string stageId)
        {
            if (instance != null)
            {
                instance.CompleteStage(stageId);
            }
        }

        /// <summary>
        /// Static method to hide the loading screen
        /// </summary>
        public static void HideScreen()
        {
            if (instance != null)
            {
                instance.Hide();
            }
        }

        /// <summary>
        /// Static method to check if loading is complete
        /// </summary>
        public static bool IsComplete()
        {
            return instance == null || !instance.isLoading;
        }
    }
}
