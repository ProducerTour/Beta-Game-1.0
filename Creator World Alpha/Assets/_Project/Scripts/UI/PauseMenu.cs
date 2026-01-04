using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;
using CreatorWorld.Core;

namespace CreatorWorld.UI
{
    /// <summary>
    /// Pause menu with settings access.
    /// Toggle with ESC key.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject pauseMenuPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject mainButtonsPanel;

        [Header("Main Menu Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Settings")]
        [SerializeField] private GraphicsSettingsUI graphicsSettingsUI;

        [Header("Settings Tabs")]
        [SerializeField] private Button graphicsTabButton;
        [SerializeField] private Button audioTabButton;
        [SerializeField] private Button controlsTabButton;
        [SerializeField] private Button backButton;

        [Header("Audio")]
        [SerializeField] private AudioSource uiAudioSource;
        [SerializeField] private AudioClip clickSound;

        public static bool IsPaused { get; private set; }
        public static PauseMenu Instance { get; private set; }

        private bool wasLockedCursor;

        private void Awake()
        {
            Instance = this;

            // Initially hidden
            if (pauseMenuPanel != null)
                pauseMenuPanel.SetActive(false);
            if (settingsPanel != null)
                settingsPanel.SetActive(false);

            IsPaused = false;
        }

        private void Start()
        {
            // Setup button listeners
            if (resumeButton != null)
                resumeButton.onClick.AddListener(Resume);
            if (settingsButton != null)
                settingsButton.onClick.AddListener(OpenSettings);
            if (quitButton != null)
                quitButton.onClick.AddListener(QuitGame);
            if (backButton != null)
                backButton.onClick.AddListener(CloseSettings);

            // Tab buttons
            if (graphicsTabButton != null)
                graphicsTabButton.onClick.AddListener(ShowGraphicsTab);
        }

        private void Update()
        {
            // Toggle pause with ESC (using new Input System)
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (IsPaused)
                {
                    if (settingsPanel != null && settingsPanel.activeSelf)
                    {
                        CloseSettings();
                    }
                    else
                    {
                        Resume();
                    }
                }
                else
                {
                    Pause();
                }
            }
        }

        public void Pause()
        {
            IsPaused = true;
            Time.timeScale = 0f;

            // Store cursor state
            wasLockedCursor = Cursor.lockState == CursorLockMode.Locked;

            // Show cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Show pause menu
            if (pauseMenuPanel != null)
                pauseMenuPanel.SetActive(true);
            if (mainButtonsPanel != null)
                mainButtonsPanel.SetActive(true);
            if (settingsPanel != null)
                settingsPanel.SetActive(false);

            PlayClickSound();
        }

        public void Resume()
        {
            IsPaused = false;
            Time.timeScale = 1f;

            // Restore cursor state
            if (wasLockedCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // Hide all panels
            if (pauseMenuPanel != null)
                pauseMenuPanel.SetActive(false);
            if (settingsPanel != null)
                settingsPanel.SetActive(false);

            PlayClickSound();
        }

        public void OpenSettings()
        {
            if (mainButtonsPanel != null)
                mainButtonsPanel.SetActive(false);
            if (settingsPanel != null)
                settingsPanel.SetActive(true);

            // Show graphics tab by default
            ShowGraphicsTab();

            PlayClickSound();
        }

        public void CloseSettings()
        {
            if (settingsPanel != null)
                settingsPanel.SetActive(false);
            if (mainButtonsPanel != null)
                mainButtonsPanel.SetActive(true);

            // Apply any pending settings
            if (graphicsSettingsUI != null)
                graphicsSettingsUI.ApplySettings();

            PlayClickSound();
        }

        private void ShowGraphicsTab()
        {
            // Activate graphics settings UI
            if (graphicsSettingsUI != null)
                graphicsSettingsUI.gameObject.SetActive(true);

            // Highlight tab button
            HighlightTab(graphicsTabButton);

            PlayClickSound();
        }

        private void HighlightTab(Button activeTab)
        {
            // Reset all tabs
            SetTabHighlight(graphicsTabButton, false);
            SetTabHighlight(audioTabButton, false);
            SetTabHighlight(controlsTabButton, false);

            // Highlight active
            SetTabHighlight(activeTab, true);
        }

        private void SetTabHighlight(Button tab, bool highlighted)
        {
            if (tab == null) return;

            var colors = tab.colors;
            colors.normalColor = highlighted ? new Color(0.3f, 0.6f, 0.9f) : Color.white;
            tab.colors = colors;
        }

        public void QuitGame()
        {
            PlayClickSound();

            // Return to main menu or quit application
            Time.timeScale = 1f;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void PlayClickSound()
        {
            if (uiAudioSource != null && clickSound != null)
            {
                uiAudioSource.PlayOneShot(clickSound);
            }
        }

        private void OnDestroy()
        {
            // Ensure time is restored if destroyed while paused
            Time.timeScale = 1f;
            IsPaused = false;
        }
    }
}
