using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

namespace CreatorWorld.Network
{
    /// <summary>
    /// Manages network interest/visibility for scalable multiplayer (100+ players).
    /// Uses spatial partitioning (grid) to efficiently determine which entities
    /// each client should receive updates for.
    /// </summary>
    public class NetworkInterestManager : NetworkBehaviour
    {
        public static NetworkInterestManager Instance { get; private set; }

        [Header("Grid Configuration")]
        [SerializeField] private float cellSize = 64f; // Match chunk size
        [SerializeField] private int viewRadiusCells = 3; // 3 cells = ~192m

        [Header("Visibility Ranges")]
        [SerializeField] private float playerVisibilityRange = 150f;
        [SerializeField] private float enemyVisibilityRange = 100f;
        [SerializeField] private float itemVisibilityRange = 50f;
        [SerializeField] private float structureVisibilityRange = 200f;

        [Header("Update Settings")]
        [SerializeField] private float updateInterval = 0.5f; // Update visibility every 500ms
        [SerializeField] private int maxUpdatesPerFrame = 10; // Limit to prevent spikes

        // Spatial grid: cell coordinate -> set of NetworkObjects in that cell
        private Dictionary<Vector2Int, HashSet<NetworkObject>> spatialGrid = new Dictionary<Vector2Int, HashSet<NetworkObject>>();

        // Per-client visibility sets
        private Dictionary<ulong, HashSet<NetworkObject>> clientVisibility = new Dictionary<ulong, HashSet<NetworkObject>>();

        // Player positions for visibility checks
        private Dictionary<ulong, Vector3> playerPositions = new Dictionary<ulong, Vector3>();

        // Tracked objects
        private HashSet<NetworkObject> trackedObjects = new HashSet<NetworkObject>();

        private float lastUpdateTime;
        private Queue<ulong> clientUpdateQueue = new Queue<ulong>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsServer)
            {
                enabled = false;
                return;
            }

            // Subscribe to client connect/disconnect
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            Debug.Log("[NetworkInterestManager] Initialized on server");
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        private void Update()
        {
            if (!IsServer) return;

            // Periodic visibility updates
            if (Time.time - lastUpdateTime > updateInterval)
            {
                lastUpdateTime = Time.time;
                QueueAllClientsForUpdate();
            }

            // Process queued updates (spread across frames)
            ProcessUpdateQueue();
        }

        #region Object Registration

        /// <summary>
        /// Register a NetworkObject with the interest manager.
        /// Call this when spawning networked entities.
        /// </summary>
        public void RegisterObject(NetworkObject obj)
        {
            if (!IsServer || obj == null) return;

            if (trackedObjects.Contains(obj)) return;

            trackedObjects.Add(obj);
            UpdateObjectCell(obj);

            Debug.Log($"[NetworkInterestManager] Registered: {obj.name}");
        }

        /// <summary>
        /// Unregister a NetworkObject from the interest manager.
        /// Call this when despawning networked entities.
        /// </summary>
        public void UnregisterObject(NetworkObject obj)
        {
            if (!IsServer || obj == null) return;

            trackedObjects.Remove(obj);

            // Remove from grid
            foreach (var cell in spatialGrid.Values)
            {
                cell.Remove(obj);
            }

            // Remove from all client visibility sets
            foreach (var visibility in clientVisibility.Values)
            {
                visibility.Remove(obj);
            }
        }

        /// <summary>
        /// Update the cell position of a NetworkObject.
        /// Call this when objects move significantly.
        /// </summary>
        public void UpdateObjectCell(NetworkObject obj)
        {
            if (!IsServer || obj == null) return;

            Vector2Int newCell = GetCellCoord(obj.transform.position);

            // Remove from old cells
            foreach (var kvp in spatialGrid)
            {
                if (kvp.Value.Contains(obj) && kvp.Key != newCell)
                {
                    kvp.Value.Remove(obj);
                }
            }

            // Add to new cell
            if (!spatialGrid.ContainsKey(newCell))
            {
                spatialGrid[newCell] = new HashSet<NetworkObject>();
            }
            spatialGrid[newCell].Add(obj);
        }

        #endregion

        #region Player Position Tracking

        /// <summary>
        /// Update a player's position for visibility calculations.
        /// Called by NetworkPlayer when position changes.
        /// </summary>
        public void UpdatePlayerPosition(ulong clientId, Vector3 position)
        {
            if (!IsServer) return;
            playerPositions[clientId] = position;
        }

        #endregion

        #region Visibility Updates

        private void OnClientConnected(ulong clientId)
        {
            clientVisibility[clientId] = new HashSet<NetworkObject>();
            playerPositions[clientId] = Vector3.zero;

            Debug.Log($"[NetworkInterestManager] Client {clientId} connected");
        }

        private void OnClientDisconnected(ulong clientId)
        {
            clientVisibility.Remove(clientId);
            playerPositions.Remove(clientId);

            Debug.Log($"[NetworkInterestManager] Client {clientId} disconnected");
        }

        private void QueueAllClientsForUpdate()
        {
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (!clientUpdateQueue.Contains(clientId))
                {
                    clientUpdateQueue.Enqueue(clientId);
                }
            }
        }

        private void ProcessUpdateQueue()
        {
            int processed = 0;

            while (clientUpdateQueue.Count > 0 && processed < maxUpdatesPerFrame)
            {
                ulong clientId = clientUpdateQueue.Dequeue();
                UpdateClientVisibility(clientId);
                processed++;
            }
        }

        private void UpdateClientVisibility(ulong clientId)
        {
            if (!playerPositions.TryGetValue(clientId, out Vector3 clientPos))
            {
                return;
            }

            if (!clientVisibility.TryGetValue(clientId, out HashSet<NetworkObject> currentVisibility))
            {
                currentVisibility = new HashSet<NetworkObject>();
                clientVisibility[clientId] = currentVisibility;
            }

            Vector2Int clientCell = GetCellCoord(clientPos);
            HashSet<NetworkObject> newVisibility = new HashSet<NetworkObject>();

            // Get all objects in visible cells
            for (int x = -viewRadiusCells; x <= viewRadiusCells; x++)
            {
                for (int z = -viewRadiusCells; z <= viewRadiusCells; z++)
                {
                    Vector2Int cell = new Vector2Int(clientCell.x + x, clientCell.y + z);

                    if (spatialGrid.TryGetValue(cell, out HashSet<NetworkObject> objects))
                    {
                        foreach (var obj in objects)
                        {
                            if (obj == null || !obj.IsSpawned) continue;

                            // Check distance-based visibility
                            float distance = Vector3.Distance(clientPos, obj.transform.position);
                            float visRange = GetVisibilityRange(obj);

                            if (distance <= visRange)
                            {
                                newVisibility.Add(obj);
                            }
                        }
                    }
                }
            }

            // Determine what to show/hide
            foreach (var obj in newVisibility)
            {
                if (!currentVisibility.Contains(obj))
                {
                    // New visibility - show to client
                    ShowToClient(obj, clientId);
                }
            }

            foreach (var obj in currentVisibility)
            {
                if (!newVisibility.Contains(obj))
                {
                    // Lost visibility - hide from client
                    HideFromClient(obj, clientId);
                }
            }

            // Update visibility set
            clientVisibility[clientId] = newVisibility;
        }

        private float GetVisibilityRange(NetworkObject obj)
        {
            // Determine range based on object type
            // This could be extended with a component that specifies the type

            if (obj.TryGetComponent<NetworkPlayer>(out _))
            {
                return playerVisibilityRange;
            }

            // Check for enemy, item, structure components
            // For now, use player range as default
            return playerVisibilityRange;
        }

        private void ShowToClient(NetworkObject obj, ulong clientId)
        {
            if (obj == null || !obj.IsSpawned) return;

            // Use NetworkObject's built-in visibility system if available
            // Otherwise, use custom RPC to notify client

            // Note: Unity Netcode's NetworkShow/NetworkHide require
            // object-level visibility support. For now, we track visibility
            // and let objects handle their own sync based on it.

            Debug.Log($"[NetworkInterestManager] Show {obj.name} to client {clientId}");
        }

        private void HideFromClient(NetworkObject obj, ulong clientId)
        {
            if (obj == null) return;

            Debug.Log($"[NetworkInterestManager] Hide {obj.name} from client {clientId}");
        }

        #endregion

        #region Utility

        private Vector2Int GetCellCoord(Vector3 worldPos)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPos.x / cellSize),
                Mathf.FloorToInt(worldPos.z / cellSize)
            );
        }

        /// <summary>
        /// Check if a client can see a specific position.
        /// </summary>
        public bool CanClientSee(ulong clientId, Vector3 position, float customRange = -1f)
        {
            if (!playerPositions.TryGetValue(clientId, out Vector3 clientPos))
            {
                return false;
            }

            float range = customRange > 0 ? customRange : playerVisibilityRange;
            return Vector3.Distance(clientPos, position) <= range;
        }

        /// <summary>
        /// Get all clients that can see a position.
        /// </summary>
        public List<ulong> GetClientsInRange(Vector3 position, float range)
        {
            List<ulong> result = new List<ulong>();

            foreach (var kvp in playerPositions)
            {
                if (Vector3.Distance(position, kvp.Value) <= range)
                {
                    result.Add(kvp.Key);
                }
            }

            return result;
        }

        /// <summary>
        /// Get the number of objects being tracked.
        /// </summary>
        public int TrackedObjectCount => trackedObjects.Count;

        /// <summary>
        /// Get the number of grid cells in use.
        /// </summary>
        public int ActiveCellCount => spatialGrid.Count;

        #endregion

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            // Draw grid cells with objects
            Gizmos.color = Color.green;
            foreach (var kvp in spatialGrid)
            {
                if (kvp.Value.Count > 0)
                {
                    Vector3 center = new Vector3(
                        (kvp.Key.x + 0.5f) * cellSize,
                        5f,
                        (kvp.Key.y + 0.5f) * cellSize
                    );
                    Gizmos.DrawWireCube(center, new Vector3(cellSize, 10f, cellSize));
                }
            }

            // Draw player visibility ranges
            Gizmos.color = Color.blue;
            foreach (var kvp in playerPositions)
            {
                Gizmos.DrawWireSphere(kvp.Value, playerVisibilityRange);
            }
        }
    }
}
