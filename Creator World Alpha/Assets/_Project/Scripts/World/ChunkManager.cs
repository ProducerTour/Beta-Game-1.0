using System.Collections.Generic;
using UnityEngine;
using CreatorWorld.Config;
using CreatorWorld.UI;

namespace CreatorWorld.World
{
    /// <summary>
    /// Manages chunk loading/unloading based on player position.
    /// Rust-style procedural world with chunk streaming.
    /// </summary>
    public class ChunkManager : MonoBehaviour
    {
        [Header("World Settings")]
        [SerializeField] private BiomeSettings biomeSettings;
        [SerializeField] private int worldSeed = 12345;
        [SerializeField] private int chunkSize = 64; // meters
        [SerializeField] private int viewDistance = 3; // chunks in each direction

        [Header("LOD Settings")]
        [SerializeField] private int highDetailDistance = 1;
        [SerializeField] private int mediumDetailDistance = 2;

        [Header("Performance")]
        [SerializeField] private int chunksPerFrame = 2;
        [SerializeField] private float unloadDelay = 5f;

        [Header("References")]
        [SerializeField] private Transform player;
        [SerializeField] private Material terrainMaterial;

        [Header("Vegetation")]
        [SerializeField] private GameObject[] treePrefabs; // Multiple tree sizes (small, medium, tall)
        [SerializeField] private float treeYOffset = 0f; // Adjust if trees float or sink
        [SerializeField] private GameObject[] rockPrefabs; // Rock variations
        [SerializeField] private float rockYOffset = 0f; // Adjust if rocks float or sink

        [Header("Grass")]
        [SerializeField] private GrassManager grassManager;

        // Chunk storage
        private Dictionary<Vector2Int, Chunk> loadedChunks = new Dictionary<Vector2Int, Chunk>();
        private Queue<Vector2Int> loadQueue = new Queue<Vector2Int>();
        private Dictionary<Vector2Int, float> unloadTimers = new Dictionary<Vector2Int, float>();

        // State
        private Vector2Int currentPlayerChunk;
        private Vector2Int lastPlayerChunk;

        // Initial loading tracking
        private int initialChunksRequired;
        private int initialChunksLoaded;
        private bool initialLoadComplete;

        public int WorldSeed => worldSeed;
        public int ChunkSize => chunkSize;
        public int ActiveChunkCount => loadedChunks.Count;
        public bool IsInitialLoadComplete => initialLoadComplete;

        private void Start()
        {
            // Initialize terrain generator with biome settings
            if (biomeSettings != null)
            {
                TerrainGenerator.Initialize(biomeSettings);
            }
            else
            {
                Debug.LogError("[ChunkManager] BiomeSettings not assigned! Create a BiomeSettings asset and assign it.");
            }

            if (player == null)
            {
                var playerController = FindFirstObjectByType<Player.PlayerController>();
                if (playerController != null)
                {
                    player = playerController.transform;
                }
            }

            // Create default terrain material if not assigned
            if (terrainMaterial == null)
            {
                terrainMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                terrainMaterial.color = new Color(0.3f, 0.5f, 0.2f); // Green grass color
            }

            // Initialize world
            Random.InitState(worldSeed);
            UpdatePlayerChunk();

            // Calculate initial chunks needed (view distance grid)
            int gridSize = (viewDistance * 2 + 1);
            initialChunksRequired = gridSize * gridSize;
            initialChunksLoaded = 0;
            initialLoadComplete = false;

            Debug.Log($"[ChunkManager] Starting initial load: {initialChunksRequired} chunks required");
            GameLoadingScreen.SetStageProgress("terrain", 0f, "Preparing terrain...");

            QueueChunksAroundPlayer();
        }

        private void Update()
        {
            UpdatePlayerChunk();

            // Check if player moved to new chunk
            if (currentPlayerChunk != lastPlayerChunk)
            {
                QueueChunksAroundPlayer();
                lastPlayerChunk = currentPlayerChunk;
            }

            ProcessLoadQueue();
            ProcessUnloadTimers();
            UpdateChunkLODs();
        }

        private void UpdatePlayerChunk()
        {
            if (player == null) return;

            Vector3 pos = player.position;
            currentPlayerChunk = new Vector2Int(
                Mathf.FloorToInt(pos.x / chunkSize),
                Mathf.FloorToInt(pos.z / chunkSize)
            );
        }

        private void QueueChunksAroundPlayer()
        {
            // Mark all current chunks for potential unload
            foreach (var kvp in loadedChunks)
            {
                if (!unloadTimers.ContainsKey(kvp.Key))
                {
                    unloadTimers[kvp.Key] = unloadDelay;
                }
            }

            // Queue chunks in view distance
            for (int x = -viewDistance; x <= viewDistance; x++)
            {
                for (int z = -viewDistance; z <= viewDistance; z++)
                {
                    Vector2Int coord = currentPlayerChunk + new Vector2Int(x, z);

                    // Remove from unload list (player is near)
                    unloadTimers.Remove(coord);

                    // Queue if not loaded
                    if (!loadedChunks.ContainsKey(coord) && !loadQueue.Contains(coord))
                    {
                        loadQueue.Enqueue(coord);
                    }
                }
            }
        }

        private void ProcessLoadQueue()
        {
            int processed = 0;
            while (loadQueue.Count > 0 && processed < chunksPerFrame)
            {
                Vector2Int coord = loadQueue.Dequeue();

                // Skip if already loaded or too far
                if (loadedChunks.ContainsKey(coord)) continue;
                if (GetChunkDistance(coord) > viewDistance) continue;

                LoadChunk(coord);
                processed++;
            }
        }

        private void ProcessUnloadTimers()
        {
            List<Vector2Int> toRemove = new List<Vector2Int>();
            List<KeyValuePair<Vector2Int, float>> toUpdate = new List<KeyValuePair<Vector2Int, float>>();

            foreach (var kvp in unloadTimers)
            {
                float timer = kvp.Value - Time.deltaTime;

                if (timer <= 0)
                {
                    UnloadChunk(kvp.Key);
                    toRemove.Add(kvp.Key);
                }
                else
                {
                    toUpdate.Add(new KeyValuePair<Vector2Int, float>(kvp.Key, timer));
                }
            }

            // Apply updates after enumeration
            foreach (var update in toUpdate)
            {
                unloadTimers[update.Key] = update.Value;
            }

            foreach (var coord in toRemove)
            {
                unloadTimers.Remove(coord);
            }
        }

        private void UpdateChunkLODs()
        {
            foreach (var kvp in loadedChunks)
            {
                int distance = GetChunkDistance(kvp.Key);
                int lod = distance <= highDetailDistance ? 0 :
                          distance <= mediumDetailDistance ? 1 : 2;

                kvp.Value.SetLOD(lod);
            }
        }

        private void LoadChunk(Vector2Int coord)
        {
            GameObject chunkGO = new GameObject($"Chunk_{coord.x}_{coord.y}");
            chunkGO.transform.parent = transform;
            chunkGO.transform.position = new Vector3(coord.x * chunkSize, 0, coord.y * chunkSize);

            Chunk chunk = chunkGO.AddComponent<Chunk>();

            // Wire up grass generation when chunk is ready
            if (grassManager != null)
            {
                chunk.OnChunkReady += grassManager.GenerateGrassForChunk;
            }

            chunk.Initialize(coord, chunkSize, worldSeed, terrainMaterial, treePrefabs, treeYOffset, rockPrefabs, rockYOffset);

            loadedChunks[coord] = chunk;

            // Track initial loading progress
            if (!initialLoadComplete)
            {
                initialChunksLoaded++;
                float progress = (float)initialChunksLoaded / initialChunksRequired;
                GameLoadingScreen.SetStageProgress("terrain", progress, $"Loading chunk {initialChunksLoaded}/{initialChunksRequired}");

                // Per-chunk logging removed to reduce console spam

                if (initialChunksLoaded >= initialChunksRequired)
                {
                    initialLoadComplete = true;
                    GameLoadingScreen.FinishStage("terrain");
                    Debug.Log("[ChunkManager] Initial terrain load complete!");
                }
            }
        }

        private void UnloadChunk(Vector2Int coord)
        {
            if (loadedChunks.TryGetValue(coord, out Chunk chunk))
            {
                // Remove grass for this chunk
                if (grassManager != null)
                {
                    grassManager.RemoveGrassForChunk(coord);
                }

                Destroy(chunk.gameObject);
                loadedChunks.Remove(coord);
            }
        }

        private int GetChunkDistance(Vector2Int coord)
        {
            return Mathf.Max(
                Mathf.Abs(coord.x - currentPlayerChunk.x),
                Mathf.Abs(coord.y - currentPlayerChunk.y)
            );
        }

        /// <summary>
        /// Get world height at a position
        /// </summary>
        public float GetHeightAt(Vector3 worldPosition)
        {
            Vector2Int coord = new Vector2Int(
                Mathf.FloorToInt(worldPosition.x / chunkSize),
                Mathf.FloorToInt(worldPosition.z / chunkSize)
            );

            if (loadedChunks.TryGetValue(coord, out Chunk chunk))
            {
                return chunk.GetHeightAt(worldPosition);
            }

            // Fallback: generate height on the fly
            return TerrainGenerator.GetHeightAt(worldPosition.x, worldPosition.z, worldSeed);
        }

        private void OnDrawGizmosSelected()
        {
            // Draw chunk grid
            Gizmos.color = Color.green;
            for (int x = -viewDistance; x <= viewDistance; x++)
            {
                for (int z = -viewDistance; z <= viewDistance; z++)
                {
                    Vector2Int coord = currentPlayerChunk + new Vector2Int(x, z);
                    Vector3 center = new Vector3(
                        (coord.x + 0.5f) * chunkSize,
                        0,
                        (coord.y + 0.5f) * chunkSize
                    );
                    Gizmos.DrawWireCube(center, new Vector3(chunkSize, 1, chunkSize));
                }
            }
        }
    }
}
