using Unity.Netcode;
using UnityEngine;
using System;

namespace CreatorWorld.Network
{
    /// <summary>
    /// Synchronizes the world seed across all clients.
    /// Server generates seed on startup, clients receive it on connection.
    /// Ensures deterministic terrain generation for all players.
    /// </summary>
    public class WorldSeedSync : NetworkBehaviour
    {
        public static WorldSeedSync Instance { get; private set; }

        [Header("Seed Configuration")]
        [SerializeField] private int defaultSeed = 12345;
        [SerializeField] private bool useRandomSeed = true;

        /// <summary>
        /// The synchronized world seed. Only valid after OnSeedReceived fires.
        /// </summary>
        public int WorldSeed => worldSeed.Value;

        /// <summary>
        /// True when seed has been received and terrain can be generated.
        /// </summary>
        public bool IsSeedReady { get; private set; }

        /// <summary>
        /// Fires when the seed is received/set and terrain generation can begin.
        /// </summary>
        public event Action<int> OnSeedReceived;

        // NetworkVariable syncs seed to all clients
        private NetworkVariable<int> worldSeed = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                // Server sets the seed
                int seed = useRandomSeed ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : defaultSeed;
                SetSeed(seed);
                Debug.Log($"[WorldSeedSync] Server initialized with seed: {seed}");
            }
            else
            {
                // Client subscribes to seed changes
                worldSeed.OnValueChanged += OnWorldSeedChanged;

                // If seed is already set (late join), use it immediately
                if (worldSeed.Value != 0)
                {
                    OnWorldSeedChanged(0, worldSeed.Value);
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (!IsServer)
            {
                worldSeed.OnValueChanged -= OnWorldSeedChanged;
            }
        }

        /// <summary>
        /// Server-only: Set the world seed. Call this before clients connect
        /// or use command-line args for dedicated server.
        /// </summary>
        public void SetSeed(int seed)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[WorldSeedSync] Only server can set seed!");
                return;
            }

            worldSeed.Value = seed;
            IsSeedReady = true;
            OnSeedReceived?.Invoke(seed);

            Debug.Log($"[WorldSeedSync] Seed set to: {seed}");
        }

        /// <summary>
        /// Server-only: Set seed from command-line argument.
        /// </summary>
        public void SetSeedFromCommandLine()
        {
            if (!IsServer) return;

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--seed" && int.TryParse(args[i + 1], out int seed))
                {
                    SetSeed(seed);
                    return;
                }
            }

            // No command-line seed, use default behavior
            int defaultOrRandom = useRandomSeed ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : defaultSeed;
            SetSeed(defaultOrRandom);
        }

        private void OnWorldSeedChanged(int previousValue, int newValue)
        {
            if (newValue == 0) return; // Ignore initial zero

            Debug.Log($"[WorldSeedSync] Client received seed: {newValue}");
            IsSeedReady = true;
            OnSeedReceived?.Invoke(newValue);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
