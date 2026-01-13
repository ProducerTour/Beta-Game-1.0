using Unity.Netcode;
using UnityEngine;
using System;
using System.Collections;

namespace CreatorWorld.Network
{
    /// <summary>
    /// Dedicated server controller for headless server builds.
    /// Handles server lifecycle, player management, and admin commands.
    /// </summary>
    public class DedicatedServer : MonoBehaviour
    {
        public static DedicatedServer Instance { get; private set; }

        [Header("Server Configuration")]
        [SerializeField] private int targetFrameRate = 30;
        [SerializeField] private float statusLogInterval = 60f;

        [Header("Player Management")]
        [SerializeField] private float afkTimeout = 300f; // 5 minutes
        [SerializeField] private bool enableAfkKick = true;

        [Header("World Settings")]
        [SerializeField] private Vector3[] spawnPoints = new Vector3[]
        {
            new Vector3(1024f, 5f, 256f),
            new Vector3(1050f, 5f, 256f),
            new Vector3(1024f, 5f, 280f),
            new Vector3(1050f, 5f, 280f)
        };

        public int ConnectedPlayers => NetworkManager.Singleton?.ConnectedClientsIds.Count ?? 0;
        public float Uptime => Time.realtimeSinceStartup;

        private float lastStatusLog;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Configure for server performance
            ConfigureServerSettings();
        }

        private void Start()
        {
            // Only run on server
            if (!IsRunningAsServer())
            {
                enabled = false;
                return;
            }

            LogServer("Dedicated server starting...");
            StartCoroutine(ServerLoop());
        }

        private void ConfigureServerSettings()
        {
#if UNITY_SERVER
            // Headless mode optimizations
            Application.targetFrameRate = targetFrameRate;
            QualitySettings.vSyncCount = 0;

            // Disable rendering
            Camera.main?.gameObject.SetActive(false);

            LogServer($"Server configured: TargetFPS={targetFrameRate}");
#endif
        }

        private IEnumerator ServerLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);

                // Periodic status logging
                if (Time.realtimeSinceStartup - lastStatusLog > statusLogInterval)
                {
                    LogStatus();
                    lastStatusLog = Time.realtimeSinceStartup;
                }

                // Process server tasks
                ProcessServerTasks();
            }
        }

        private void ProcessServerTasks()
        {
            // AFK kick
            if (enableAfkKick)
            {
                // TODO: Track player activity and kick AFK players
            }

            // Other periodic tasks
            // - Save world state
            // - Clean up despawned entities
            // - Update AI spawns
        }

        private void LogStatus()
        {
            int players = ConnectedPlayers;
            float uptime = Uptime;
            int hours = (int)(uptime / 3600);
            int minutes = (int)((uptime % 3600) / 60);

            LogServer($"Status: Players={players}, Uptime={hours}h {minutes}m");
        }

        /// <summary>
        /// Get a spawn point for a new player.
        /// </summary>
        public Vector3 GetSpawnPoint(int playerIndex)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                return new Vector3(1024f, 5f, 256f); // Default spawn
            }

            return spawnPoints[playerIndex % spawnPoints.Length];
        }

        /// <summary>
        /// Kick a player from the server.
        /// </summary>
        public void KickPlayer(ulong clientId, string reason = "Kicked by server")
        {
            if (!NetworkManager.Singleton.IsServer) return;

            LogServer($"Kicking player {clientId}: {reason}");
            NetworkManager.Singleton.DisconnectClient(clientId, reason);
        }

        /// <summary>
        /// Broadcast a message to all players.
        /// </summary>
        public void BroadcastMessage(string message)
        {
            if (!NetworkManager.Singleton.IsServer) return;

            LogServer($"Broadcast: {message}");
            BroadcastMessageClientRpc(message);
        }

        [ClientRpc]
        private void BroadcastMessageClientRpc(string message)
        {
            Debug.Log($"[Server Message] {message}");
            // TODO: Display in UI
        }

        /// <summary>
        /// Shutdown the server gracefully.
        /// </summary>
        public void Shutdown()
        {
            LogServer("Server shutting down...");

            // Notify all clients
            BroadcastMessage("Server shutting down...");

            // Give clients time to receive message
            StartCoroutine(ShutdownSequence());
        }

        private IEnumerator ShutdownSequence()
        {
            yield return new WaitForSeconds(3f);

            // Save world state
            // TODO: Implement world save

            // Disconnect all clients
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }

            yield return new WaitForSeconds(1f);

            LogServer("Server shutdown complete");
            Application.Quit();
        }

        private bool IsRunningAsServer()
        {
#if UNITY_SERVER
            return true;
#else
            // Check if NetworkManager is running as server
            return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsClient;
#endif
        }

        private void LogServer(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            Debug.Log($"[{timestamp}] [Server] {message}");

            // In production, also write to log file
#if UNITY_SERVER
            Console.WriteLine($"[{timestamp}] {message}");
#endif
        }

        private void OnApplicationQuit()
        {
            LogServer("Application quit requested");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #region Console Commands (for dedicated server)

#if UNITY_SERVER
        private void Update()
        {
            // Process console input
            if (Console.KeyAvailable)
            {
                ProcessConsoleCommand();
            }
        }

        private void ProcessConsoleCommand()
        {
            string command = Console.ReadLine()?.Trim().ToLower();
            if (string.IsNullOrEmpty(command)) return;

            string[] parts = command.Split(' ');
            string cmd = parts[0];

            switch (cmd)
            {
                case "status":
                    LogStatus();
                    break;

                case "players":
                    ListPlayers();
                    break;

                case "kick":
                    if (parts.Length > 1 && ulong.TryParse(parts[1], out ulong clientId))
                    {
                        string reason = parts.Length > 2 ? string.Join(" ", parts, 2, parts.Length - 2) : "Kicked";
                        KickPlayer(clientId, reason);
                    }
                    else
                    {
                        LogServer("Usage: kick <clientId> [reason]");
                    }
                    break;

                case "say":
                    if (parts.Length > 1)
                    {
                        string message = string.Join(" ", parts, 1, parts.Length - 1);
                        BroadcastMessage(message);
                    }
                    break;

                case "quit":
                case "exit":
                case "stop":
                    Shutdown();
                    break;

                case "help":
                    LogServer("Commands: status, players, kick <id> [reason], say <message>, quit");
                    break;

                default:
                    LogServer($"Unknown command: {cmd}. Type 'help' for available commands.");
                    break;
            }
        }

        private void ListPlayers()
        {
            if (NetworkManager.Singleton == null)
            {
                LogServer("No players connected");
                return;
            }

            LogServer($"Connected players ({ConnectedPlayers}):");
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                LogServer($"  - Client {clientId}");
            }
        }
#endif

        #endregion
    }
}
