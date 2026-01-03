using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using CreatorWorld.Interfaces;

namespace CreatorWorld.Core
{
    /// <summary>
    /// Main game manager - singleton that persists across scenes.
    /// Handles game state, initialization, and scene management.
    /// Implements IGameStateService for ServiceLocator registration.
    /// </summary>
    public class GameManager : MonoBehaviour, IGameStateService
    {
        public static GameManager Instance { get; private set; }

        [Header("Game State")]
        [SerializeField] private GameState currentState = GameState.Playing;

        [Header("Configuration")]
        [SerializeField] private int targetFrameRate = 60;
        [SerializeField] private bool vSyncEnabled = true;

        public GameState CurrentState => currentState;
        public bool IsPlaying => currentState == GameState.Playing;

        public event Action<GameState> OnStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Move to root to avoid DontDestroyOnLoad warning
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            // Register with ServiceLocator
            ServiceLocator.Register<IGameStateService>(this);

            Initialize();
        }

        private void Initialize()
        {
            // Set target frame rate
            Application.targetFrameRate = targetFrameRate;
            QualitySettings.vSyncCount = vSyncEnabled ? 1 : 0;

            // Lock cursor for gameplay
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Debug.Log("[GameManager] Initialized");
        }

        public void SetGameState(GameState newState)
        {
            if (currentState == newState) return;

            currentState = newState;
            OnStateChanged?.Invoke(newState);

            Debug.Log($"[GameManager] State changed to: {newState}");

            // Handle state-specific logic
            switch (newState)
            {
                case GameState.MainMenu:
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    Time.timeScale = 1f;
                    break;

                case GameState.Playing:
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    Time.timeScale = 1f;
                    break;

                case GameState.Paused:
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    Time.timeScale = 0f;
                    break;

                case GameState.Dead:
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    break;

                case GameState.Inventory:
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    break;
            }
        }

        public void LoadScene(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }

        public void LoadSceneAsync(string sceneName)
        {
            SceneManager.LoadSceneAsync(sceneName);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnApplicationQuit()
        {
            // Save any necessary data before quitting
            Debug.Log("[GameManager] Application quitting...");
        }
    }
}
