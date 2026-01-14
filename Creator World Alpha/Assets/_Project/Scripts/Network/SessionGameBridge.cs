using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;

namespace CreatorWorld.Network
{
    /// <summary>
    /// Bridges Unity Session Building Blocks with our game's NetworkPlayer system.
    /// Handles initialization, session events, and scene transitions.
    /// </summary>
    public class SessionGameBridge : MonoBehaviour
    {
        public static SessionGameBridge Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private string gameSceneName = "";
        [SerializeField] private bool autoInitialize = true;
        [SerializeField] private bool loadGameSceneOnJoin = false;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        public bool IsInitialized { get; private set; }
        public bool IsInSession { get; private set; }
        public string CurrentSessionCode { get; private set; }

        // Events for UI to subscribe to
        public event System.Action OnServicesInitialized;
        public event System.Action<string> OnSessionCreated; // passes join code
        public event System.Action OnSessionJoined;
        public event System.Action OnSessionLeft;
        public event System.Action<string> OnError;

        private ISession currentSession;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private async void Start()
        {
            if (autoInitialize)
            {
                await InitializeServices();
            }
        }

        /// <summary>
        /// Initialize Unity Gaming Services (required before creating/joining sessions).
        /// </summary>
        public async Task InitializeServices()
        {
            if (IsInitialized)
            {
                Log("Services already initialized");
                return;
            }

            try
            {
                Log("Initializing Unity Gaming Services...");

                // Initialize Unity Services
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    await UnityServices.InitializeAsync();
                }

                // Sign in anonymously (or use your own auth flow)
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    Log($"Signed in as: {AuthenticationService.Instance.PlayerId}");
                }

                IsInitialized = true;
                Log("Unity Gaming Services initialized successfully");
                OnServicesInitialized?.Invoke();
            }
            catch (System.Exception e)
            {
                LogError($"Failed to initialize services: {e.Message}");
                OnError?.Invoke($"Failed to initialize: {e.Message}");
            }
        }

        /// <summary>
        /// Create a new session as host. Returns the join code for friends.
        /// </summary>
        public async Task<string> CreateSession(int maxPlayers = 100, string sessionName = "Game Session")
        {
            if (!IsInitialized)
            {
                await InitializeServices();
            }

            try
            {
                Log($"Creating session: {sessionName} (max {maxPlayers} players)");

                var options = new SessionOptions
                {
                    MaxPlayers = maxPlayers,
                    Name = sessionName,
                }.WithRelayNetwork(); // Use Unity Relay for NAT traversal

                var session = await MultiplayerService.Instance.CreateSessionAsync(options);
                currentSession = session;
                CurrentSessionCode = session.Code;
                IsInSession = true;

                // Subscribe to session events
                session.PlayerJoined += OnPlayerJoined;
                session.PlayerLeaving += OnPlayerLeft;
                session.Deleted += OnSessionDeleted;

                Log($"Session created! Join code: {CurrentSessionCode}");
                OnSessionCreated?.Invoke(CurrentSessionCode);

                // Load game scene
                if (loadGameSceneOnJoin && !string.IsNullOrEmpty(gameSceneName))
                {
                    await LoadGameScene();
                }

                return CurrentSessionCode;
            }
            catch (System.Exception e)
            {
                LogError($"Failed to create session: {e.Message}");
                OnError?.Invoke($"Failed to create session: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Join an existing session using a join code.
        /// </summary>
        public async Task<bool> JoinSession(string joinCode)
        {
            if (!IsInitialized)
            {
                await InitializeServices();
            }

            if (string.IsNullOrEmpty(joinCode))
            {
                OnError?.Invoke("Join code cannot be empty");
                return false;
            }

            try
            {
                Log($"Joining session with code: {joinCode}");

                // JoinSessionOptions doesn't need WithRelayNetwork - the session already has that configured
                var options = new JoinSessionOptions();
                var session = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode, options);

                currentSession = session;
                CurrentSessionCode = session.Code;
                IsInSession = true;

                // Subscribe to session events
                session.PlayerJoined += OnPlayerJoined;
                session.PlayerLeaving += OnPlayerLeft;
                session.RemovedFromSession += OnRemovedFromSession;

                Log($"Joined session successfully! Players: {session.PlayerCount}/{session.MaxPlayers}");
                OnSessionJoined?.Invoke();

                // Load game scene
                if (loadGameSceneOnJoin && !string.IsNullOrEmpty(gameSceneName))
                {
                    await LoadGameScene();
                }

                return true;
            }
            catch (System.Exception e)
            {
                LogError($"Failed to join session: {e.Message}");
                OnError?.Invoke($"Failed to join: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Leave the current session.
        /// </summary>
        public async Task LeaveSession()
        {
            if (!IsInSession || currentSession == null)
            {
                Log("Not in a session");
                return;
            }

            try
            {
                Log("Leaving session...");

                // Unsubscribe from events
                currentSession.PlayerJoined -= OnPlayerJoined;
                currentSession.PlayerLeaving -= OnPlayerLeft;

                await currentSession.LeaveAsync();

                currentSession = null;
                CurrentSessionCode = null;
                IsInSession = false;

                // Shutdown NetworkManager
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    NetworkManager.Singleton.Shutdown();
                }

                Log("Left session");
                OnSessionLeft?.Invoke();
            }
            catch (System.Exception e)
            {
                LogError($"Error leaving session: {e.Message}");
            }
        }

        /// <summary>
        /// Quick join - creates a new session (simplified version).
        /// For proper matchmaking, use the Unity Building Blocks UI instead.
        /// </summary>
        public async Task<bool> QuickJoin(int maxPlayers = 100)
        {
            // Simplified: just create a new session
            // For proper matchmaking/browsing, use the Building Blocks UI
            Log("Quick join: Creating new session...");
            var code = await CreateSession(maxPlayers, "Quick Session");
            return !string.IsNullOrEmpty(code);
        }

        private async Task LoadGameScene()
        {
            Log($"Loading game scene: {gameSceneName}");

            // Use NetworkManager's scene management if we're the host/server
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            }
            else
            {
                // Client - just wait for server to load scene
                // NetworkManager handles this automatically
                await Task.Yield();
            }
        }

        #region Session Event Handlers

        private void OnPlayerJoined(string playerId)
        {
            Log($"Player joined: {playerId}");
        }

        private void OnPlayerLeft(string playerId)
        {
            Log($"Player left: {playerId}");
        }

        private void OnSessionDeleted()
        {
            Log("Session was deleted");
            IsInSession = false;
            currentSession = null;
            CurrentSessionCode = null;
            OnSessionLeft?.Invoke();
        }

        private void OnRemovedFromSession()
        {
            Log("Removed from session");
            IsInSession = false;
            currentSession = null;
            CurrentSessionCode = null;
            OnSessionLeft?.Invoke();
        }

        #endregion

        #region Utility

        private void Log(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[SessionGameBridge] {message}");
            }
        }

        private void LogError(string message)
        {
            Debug.LogError($"[SessionGameBridge] {message}");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion
    }
}
