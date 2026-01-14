using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;

namespace CreatorWorld.Network
{
    /// <summary>
    /// Bootstraps the network system for both dedicated servers and clients.
    /// Handles NetworkManager configuration, connection approval, and scene loading.
    /// </summary>
    public class ServerBootstrap : MonoBehaviour
    {
        public static ServerBootstrap Instance { get; private set; }

        [Header("Network Configuration")]
        [SerializeField] private int maxPlayers = 100;
        [SerializeField] private ushort port = 7777;
        [SerializeField] private string gameSceneName = "";

        [Header("Server Settings")]
        [SerializeField] private int tickRate = 30;
        [SerializeField] private bool autoStartServer = false;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        public int MaxPlayers => maxPlayers;
        public bool IsServerRunning => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        public bool IsClientConnected => NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient;

        public event Action OnServerStarted;
        public event Action OnClientConnected;
        public event Action OnClientDisconnected;
        public event Action<string> OnConnectionFailed;

        private NetworkManager networkManager;
        private UnityTransport transport;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Get references
            networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                networkManager = FindObjectOfType<NetworkManager>();
            }

            if (networkManager != null)
            {
                transport = networkManager.GetComponent<UnityTransport>();
                ConfigureNetworkManager();
            }
            else
            {
                Debug.LogError("[ServerBootstrap] NetworkManager not found!");
            }
        }

        private void Start()
        {
            // Check for dedicated server mode
            if (IsDedicatedServer())
            {
                Log("Dedicated server mode detected");
                StartDedicatedServer();
            }
            else if (autoStartServer)
            {
                Log("Auto-starting server in editor");
                StartServer();
            }
        }

        private void ConfigureNetworkManager()
        {
            // Set tick rate
            networkManager.NetworkConfig.TickRate = (uint)tickRate;

            // Connection approval for player limit
            networkManager.ConnectionApprovalCallback = ConnectionApproval;

            // Subscribe to events
            networkManager.OnServerStarted += HandleServerStarted;
            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            networkManager.OnTransportFailure += HandleTransportFailure;

            Log($"NetworkManager configured: TickRate={tickRate}, MaxPlayers={maxPlayers}");
        }

        private void ConnectionApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            // Check player limit
            int currentPlayers = networkManager.ConnectedClientsIds.Count;
            if (currentPlayers >= maxPlayers)
            {
                Log($"Connection rejected: Server full ({currentPlayers}/{maxPlayers})");
                response.Approved = false;
                response.Reason = "Server is full";
                return;
            }

            // Approve connection
            response.Approved = true;
            response.CreatePlayerObject = true;
            response.PlayerPrefabHash = null; // Use default player prefab

            Log($"Connection approved. Players: {currentPlayers + 1}/{maxPlayers}");
        }

        /// <summary>
        /// Start as dedicated server (headless mode).
        /// </summary>
        public void StartDedicatedServer()
        {
            ParseCommandLineArgs();
            StartServer();
        }

        /// <summary>
        /// Start as server/host.
        /// </summary>
        public void StartServer()
        {
            if (networkManager == null)
            {
                Debug.LogError("[ServerBootstrap] Cannot start server: NetworkManager is null");
                return;
            }

            // Configure transport
            if (transport != null)
            {
                transport.SetConnectionData("0.0.0.0", port);
                Log($"Server binding to 0.0.0.0:{port}");
            }

            // Start server
            bool success = networkManager.StartServer();
            if (success)
            {
                Log("Server started successfully");
            }
            else
            {
                Debug.LogError("[ServerBootstrap] Failed to start server");
            }
        }

        /// <summary>
        /// Start as host (server + client).
        /// </summary>
        public void StartHost()
        {
            if (networkManager == null)
            {
                Debug.LogError("[ServerBootstrap] Cannot start host: NetworkManager is null");
                return;
            }

            if (transport != null)
            {
                transport.SetConnectionData("127.0.0.1", port);
            }

            bool success = networkManager.StartHost();
            if (success)
            {
                Log("Host started successfully");
            }
            else
            {
                Debug.LogError("[ServerBootstrap] Failed to start host");
            }
        }

        /// <summary>
        /// Connect to a server as client.
        /// </summary>
        public void ConnectAsClient(string ipAddress, ushort serverPort)
        {
            if (networkManager == null)
            {
                Debug.LogError("[ServerBootstrap] Cannot connect: NetworkManager is null");
                return;
            }

            if (transport != null)
            {
                transport.SetConnectionData(ipAddress, serverPort);
                Log($"Connecting to {ipAddress}:{serverPort}");
            }

            bool success = networkManager.StartClient();
            if (!success)
            {
                Debug.LogError("[ServerBootstrap] Failed to start client");
                OnConnectionFailed?.Invoke("Failed to start network client");
            }
        }

        /// <summary>
        /// Disconnect from server or shutdown server.
        /// </summary>
        public void Disconnect()
        {
            if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();
                Log("Network shutdown");
            }
        }

        private void HandleServerStarted()
        {
            Log("Server fully started, loading game scene...");
            OnServerStarted?.Invoke();

            // Load the game scene on server
            if (!string.IsNullOrEmpty(gameSceneName))
            {
                NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (clientId == networkManager.LocalClientId)
            {
                Log($"Local client connected (ID: {clientId})");
                OnClientConnected?.Invoke();
            }
            else
            {
                Log($"Remote client connected (ID: {clientId})");
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (clientId == networkManager.LocalClientId)
            {
                Log("Local client disconnected");
                OnClientDisconnected?.Invoke();
            }
            else
            {
                Log($"Remote client disconnected (ID: {clientId})");
            }
        }

        private void HandleTransportFailure()
        {
            Debug.LogError("[ServerBootstrap] Transport failure!");
            OnConnectionFailed?.Invoke("Network transport failure");
        }

        private void ParseCommandLineArgs()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                switch (args[i])
                {
                    case "--port":
                        if (ushort.TryParse(args[i + 1], out ushort parsedPort))
                        {
                            port = parsedPort;
                            Log($"Port set from command line: {port}");
                        }
                        break;

                    case "--maxplayers":
                        if (int.TryParse(args[i + 1], out int parsedMax))
                        {
                            maxPlayers = parsedMax;
                            Log($"Max players set from command line: {maxPlayers}");
                        }
                        break;

                    case "--tickrate":
                        if (int.TryParse(args[i + 1], out int parsedTick))
                        {
                            tickRate = parsedTick;
                            Log($"Tick rate set from command line: {tickRate}");
                        }
                        break;
                }
            }
        }

        private bool IsDedicatedServer()
        {
#if UNITY_SERVER
            return true;
#else
            // Check command-line for -batchmode or --server flag
            string[] args = Environment.GetCommandLineArgs();
            foreach (string arg in args)
            {
                if (arg == "-batchmode" || arg == "--server" || arg == "-dedicated")
                {
                    return true;
                }
            }
            return false;
#endif
        }

        private void Log(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[ServerBootstrap] {message}");
            }
        }

        private void OnDestroy()
        {
            if (networkManager != null)
            {
                networkManager.OnServerStarted -= HandleServerStarted;
                networkManager.OnClientConnectedCallback -= HandleClientConnected;
                networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
                networkManager.OnTransportFailure -= HandleTransportFailure;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
