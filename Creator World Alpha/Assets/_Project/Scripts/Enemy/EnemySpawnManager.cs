using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using CreatorWorld.Config;
using CreatorWorld.World;

namespace CreatorWorld.Enemy
{
    /// <summary>
    /// Manages spawning multiple enemies across spawn points.
    /// Supports wave-based spawning, biome restrictions, and enemy pooling.
    /// </summary>
    public class EnemySpawnManager : MonoBehaviour
    {
        [System.Serializable]
        public class EnemyType
        {
            public string name = "Enemy";
            public GameObject prefab;
            public GameObject weaponPrefab; // Optional - leave null for melee enemies
            public EnemyConfig config;
            [Range(0f, 1f)]
            public float spawnWeight = 1f;
        }

        [Header("Enemy Types")]
        [SerializeField] private List<EnemyType> enemyTypes = new List<EnemyType>();

        [Header("Spawn Points")]
        [SerializeField] private Transform[] spawnPoints;

        [Header("Spawn Settings")]
        [SerializeField] private int maxEnemies = 10;
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private int initialSpawnCount = 3;

        [Header("Terrain Sync")]
        [SerializeField] private bool waitForTerrain = true;
        [SerializeField] private float fallbackSpawnDelay = 3f;

        [Header("Biome Restrictions")]
        [Tooltip("Only spawn in specific biomes")]
        [SerializeField] private bool restrictToBiomes = true;
        [Tooltip("Biomes where enemies can spawn")]
        [SerializeField] private BiomeType[] allowedBiomes = new BiomeType[]
        {
            BiomeType.Grassland,
            BiomeType.Forest,
            BiomeType.Mountain
        };

        [Header("Wave Settings")]
        [SerializeField] private bool useWaves = false;
        [SerializeField] private float timeBetweenWaves = 30f;
        [SerializeField] private int enemiesPerWave = 5;
        [SerializeField] private bool autoStartNextWave = true;

        [Header("Continuous Spawn")]
        [SerializeField] private bool continuousSpawn = false;
        [SerializeField] private float spawnInterval = 5f;
        [SerializeField] private float spawnIntervalVariation = 2f;

        [Header("Patrol")]
        [SerializeField] private Transform[] sharedPatrolPoints;

        [Header("Spawn Spread")]
        [SerializeField] private float spawnSpreadRadius = 3f;
        [SerializeField] private float minSpawnDistance = 2f;
        [SerializeField] private int maxSpawnAttempts = 10;

        [Header("Ground Detection")]
        [SerializeField] private bool snapToGround = true;
        [SerializeField] private float groundCheckHeight = 50f;
        [SerializeField] private float groundCheckDistance = 100f;
        [SerializeField] private float spawnHeightOffset = 1.0f;
        [SerializeField] private LayerMask groundLayers = ~0;

        [Header("Water Avoidance")]
        [SerializeField] private bool avoidWater = true;
        [SerializeField] private float waterMargin = 1f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] private Color spawnPointGizmoColor = Color.red;

        // Runtime state
        private List<GameObject> activeEnemies = new List<GameObject>();
        private int currentWave = 0;
        private bool isSpawning = false;
        private float totalWeight;
        private ChunkManager chunkManager;

        // Events
        public System.Action<GameObject> OnEnemySpawned;
        public System.Action<GameObject> OnEnemyDied;
        public System.Action<int> OnWaveStarted;
        public System.Action<int> OnWaveCleared;

        public int ActiveEnemyCount => activeEnemies.Count;
        public int CurrentWave => currentWave;
        public List<GameObject> ActiveEnemies => activeEnemies;

        private void Start()
        {
            if (showDebugInfo)
            {
                Debug.Log($"[EnemySpawnManager] Start() - spawnOnStart: {spawnOnStart}, enemyTypes: {enemyTypes.Count}, waitForTerrain: {waitForTerrain}");

                // Validate enemy types
                for (int i = 0; i < enemyTypes.Count; i++)
                {
                    var type = enemyTypes[i];
                    Debug.Log($"[EnemySpawnManager] Enemy type [{i}]: name='{type.name}', prefab={(type.prefab != null ? type.prefab.name : "NULL")}, weight={type.spawnWeight}");
                }
            }

            CalculateTotalWeight();

            if (spawnOnStart)
            {
                // Use delayed spawn to allow terrain to load first
                StartCoroutine(DelayedSpawnRoutine());
            }
            else if (continuousSpawn && !useWaves)
            {
                StartCoroutine(ContinuousSpawnRoutine());
            }
        }

        private IEnumerator DelayedSpawnRoutine()
        {
            if (waitForTerrain)
            {
                // Find ChunkManager
                chunkManager = FindFirstObjectByType<ChunkManager>();

                if (chunkManager != null)
                {
                    if (showDebugInfo)
                        Debug.Log($"[EnemySpawnManager] Waiting for terrain...");

                    // Wait until terrain is fully loaded
                    yield return new WaitUntil(() => chunkManager.IsInitialLoadComplete);

                    // Wait additional frames for physics colliders to settle
                    yield return new WaitForFixedUpdate();
                    yield return new WaitForFixedUpdate();
                    yield return new WaitForFixedUpdate();
                }
                else
                {
                    Debug.LogWarning("[EnemySpawnManager] ChunkManager not found, using fallback delay");
                    yield return new WaitForSeconds(fallbackSpawnDelay);
                }
            }
            else
            {
                yield return new WaitForSeconds(fallbackSpawnDelay);
            }

            if (useWaves)
            {
                StartWave(1);
            }
            else
            {
                SpawnEnemies(initialSpawnCount);
            }

            if (continuousSpawn && !useWaves)
            {
                StartCoroutine(ContinuousSpawnRoutine());
            }
        }

        private void CalculateTotalWeight()
        {
            totalWeight = 0f;
            foreach (var type in enemyTypes)
            {
                if (type.prefab != null)
                    totalWeight += type.spawnWeight;
            }
        }

        /// <summary>
        /// Spawn a specific number of enemies
        /// </summary>
        public void SpawnEnemies(int count)
        {
            if (showDebugInfo)
                Debug.Log($"[EnemySpawnManager] Spawning {count} enemies...");

            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                if (activeEnemies.Count >= maxEnemies)
                {
                    if (showDebugInfo)
                        Debug.Log("[EnemySpawnManager] Max enemies reached");
                    break;
                }

                if (SpawnRandomEnemy() != null)
                    spawned++;
            }

            if (showDebugInfo)
                Debug.Log($"[EnemySpawnManager] Spawned {spawned}/{count} enemies. Total active: {activeEnemies.Count}");
        }

        /// <summary>
        /// Spawn a single random enemy at a random spawn point
        /// </summary>
        public GameObject SpawnRandomEnemy()
        {
            if (enemyTypes.Count == 0 || totalWeight <= 0)
            {
                Debug.LogWarning("[EnemySpawnManager] No enemy types configured!");
                return null;
            }

            if (activeEnemies.Count >= maxEnemies)
                return null;

            // Select random enemy type based on weight
            EnemyType selectedType = SelectRandomEnemyType();
            if (selectedType == null || selectedType.prefab == null)
            {
                Debug.LogWarning("[EnemySpawnManager] Selected enemy type has no prefab");
                return null;
            }

            // Select random spawn point
            Vector3 spawnPos = GetRandomSpawnPosition();

            // Check if we got a valid spawn position
            if (spawnPos == Vector3.zero)
            {
                if (showDebugInfo)
                    Debug.LogWarning($"[EnemySpawnManager] Could not find valid spawn position for '{selectedType.name}'");
                return null;
            }

            Quaternion spawnRot = GetRandomSpawnRotation();

            return SpawnEnemy(selectedType, spawnPos, spawnRot);
        }

        /// <summary>
        /// Spawn a specific enemy type at a position
        /// </summary>
        public GameObject SpawnEnemy(EnemyType enemyType, Vector3 position, Quaternion rotation)
        {
            if (enemyType == null || enemyType.prefab == null)
                return null;

            GameObject enemy = Instantiate(enemyType.prefab, position, rotation);

            // Configure weapon if specified
            if (enemyType.weaponPrefab != null)
            {
                var weaponHolder = enemy.GetComponent<EnemyWeaponHolder>();
                if (weaponHolder == null)
                {
                    weaponHolder = enemy.AddComponent<EnemyWeaponHolder>();
                }
                weaponHolder.SetWeaponPrefab(enemyType.weaponPrefab);
                weaponHolder.SpawnWeapon();

                var enemyAnim = enemy.GetComponent<EnemyAnimation>();
                if (enemyAnim != null)
                {
                    enemyAnim.SetUseRifleAnimations(true);
                }
            }

            // Configure AI
            var enemyAI = enemy.GetComponent<EnemyAI>();
            if (enemyAI == null)
            {
                enemyAI = enemy.AddComponent<EnemyAI>();
            }

            if (sharedPatrolPoints != null && sharedPatrolPoints.Length > 0)
            {
                enemyAI.SetPatrolPoints(sharedPatrolPoints);
            }

            // Configure health
            var health = enemy.GetComponent<EnemyHealth>();
            if (health != null)
            {
                if (enemyType.config != null)
                {
                    health.SetConfig(enemyType.config);
                }

                // Subscribe to death event
                health.OnDeath += () => OnEnemyDeath(enemy);
            }

            activeEnemies.Add(enemy);
            OnEnemySpawned?.Invoke(enemy);

            if (showDebugInfo)
                Debug.Log($"[EnemySpawnManager] Spawned {enemyType.name} at {position}. Active: {activeEnemies.Count}");

            return enemy;
        }

        private EnemyType SelectRandomEnemyType()
        {
            float random = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var type in enemyTypes)
            {
                if (type.prefab == null) continue;
                cumulative += type.spawnWeight;
                if (random <= cumulative)
                    return type;
            }

            return enemyTypes[0];
        }

        private Vector3 GetRandomSpawnPosition()
        {
            Vector3 basePosition;

            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                basePosition = transform.position;
            }
            else
            {
                Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
                basePosition = point != null ? point.position : transform.position;
            }

            // Try to find a position that doesn't overlap with existing enemies
            for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
            {
                // Add random offset within spread radius
                Vector2 randomOffset = Random.insideUnitCircle * spawnSpreadRadius;
                Vector3 candidatePosition = basePosition + new Vector3(randomOffset.x, 0, randomOffset.y);

                // Snap to ground if enabled
                if (snapToGround)
                {
                    candidatePosition = SnapToGround(candidatePosition);
                }

                // Check if position is far enough from existing enemies
                if (IsPositionClear(candidatePosition))
                {
                    return candidatePosition;
                }
            }

            // If we couldn't find a valid position, search in expanding rings around the base position
            if (avoidWater && TerrainGenerator.IsInitialized)
            {
                Vector3 validPos = FindValidSpawnPositionInRadius(basePosition, spawnSpreadRadius * 3f);
                if (validPos != Vector3.zero)
                {
                    if (showDebugInfo)
                        Debug.Log($"[EnemySpawnManager] Found valid spawn at {validPos} via expanded search");
                    return validPos;
                }
            }

            // Fallback: just use base position with small random offset
            Vector2 fallbackOffset = Random.insideUnitCircle * spawnSpreadRadius;
            Vector3 fallbackPosition = basePosition + new Vector3(fallbackOffset.x, 0, fallbackOffset.y);

            if (snapToGround)
            {
                fallbackPosition = SnapToGround(fallbackPosition);
            }

            // CRITICAL: Check if fallback position is underwater - if so, reject it
            if (avoidWater && !IsPositionAboveWater(fallbackPosition))
            {
                // One more attempt: search in a larger radius
                Vector3 validPos = FindValidSpawnPositionInRadius(basePosition, spawnSpreadRadius * 5f);
                if (validPos != Vector3.zero)
                {
                    return validPos;
                }

                if (showDebugInfo)
                    Debug.LogWarning("[EnemySpawnManager] Could not find any valid spawn position above water!");
                return Vector3.zero; // Signal spawn failure
            }

            return fallbackPosition;
        }

        /// <summary>
        /// Search in expanding rings for a valid spawn position above water
        /// </summary>
        private Vector3 FindValidSpawnPositionInRadius(Vector3 center, float maxRadius)
        {
            float waterLevel = TerrainGenerator.WaterLevel;

            for (float radius = 5f; radius <= maxRadius; radius += 5f)
            {
                for (int angle = 0; angle < 360; angle += 30)
                {
                    float rad = angle * Mathf.Deg2Rad;
                    Vector3 candidatePos = center + new Vector3(Mathf.Cos(rad) * radius, 0, Mathf.Sin(rad) * radius);

                    // Get ground height at this position
                    Vector3 rayStart = new Vector3(candidatePos.x, 200f, candidatePos.z);
                    if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 400f, groundLayers))
                    {
                        if (hit.point.y > waterLevel + waterMargin)
                        {
                            Vector3 validPos = hit.point + Vector3.up * spawnHeightOffset;
                            if (IsPositionClear(validPos))
                            {
                                return validPos;
                            }
                        }
                    }
                }
            }

            return Vector3.zero;
        }

        /// <summary>
        /// Check if a position is valid for spawning (biome, water, distance checks)
        /// </summary>
        private bool IsPositionClear(Vector3 position)
        {
            // Check biome restrictions
            if (!IsValidSpawnBiome(position))
            {
                if (showDebugInfo)
                    Debug.Log($"[EnemySpawnManager] Position {position} invalid biome, skipping");
                return false;
            }

            // Check water avoidance
            if (avoidWater && !IsPositionAboveWater(position))
            {
                if (showDebugInfo)
                    Debug.Log($"[EnemySpawnManager] Position {position} is in water, skipping");
                return false;
            }

            // Check distance from other enemies
            foreach (var enemy in activeEnemies)
            {
                if (enemy == null) continue;

                float distance = Vector3.Distance(position, enemy.transform.position);
                if (distance < minSpawnDistance)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Check if a position is in an allowed biome for spawning
        /// </summary>
        private bool IsValidSpawnBiome(Vector3 position)
        {
            if (!restrictToBiomes)
                return true;

            // If terrain not initialized, REJECT spawn instead of bypassing restrictions
            if (!TerrainGenerator.IsInitialized)
            {
                if (showDebugInfo)
                    Debug.Log("[EnemySpawnManager] Terrain not initialized, rejecting spawn position");
                return false;
            }

            if (allowedBiomes == null || allowedBiomes.Length == 0)
                return true;

            BiomeType biome = TerrainGenerator.GetBiomeAt(position.x, position.z, 0);

            foreach (var allowed in allowedBiomes)
            {
                if (biome == allowed) return true;
            }

            if (showDebugInfo)
                Debug.Log($"[EnemySpawnManager] Biome {biome} not allowed at ({position.x:F0}, {position.z:F0})");

            return false;
        }

        /// <summary>
        /// Check if a position is above water level using multiple detection methods
        /// </summary>
        private bool IsPositionAboveWater(Vector3 position)
        {
            if (!TerrainGenerator.IsInitialized)
                return true; // Assume safe if terrain not initialized

            float waterLevel = TerrainGenerator.WaterLevel;

            // First check: if position Y is already well above water, it's safe
            if (position.y > waterLevel + waterMargin)
            {
                return true;
            }

            // Second check: raycast down to find actual ground height at this XZ position
            Vector3 rayStart = new Vector3(position.x, position.y + 100f, position.z);
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 200f, groundLayers))
            {
                float groundHeight = hit.point.y;
                bool aboveWater = groundHeight > (waterLevel + waterMargin);

                if (showDebugInfo && !aboveWater)
                    Debug.Log($"[EnemySpawnManager] Position ({position.x:F1}, {position.z:F1}) ground={groundHeight:F1}, water={waterLevel:F1}, aboveWater={aboveWater}");

                return aboveWater;
            }

            // Fallback: use terrain generator height
            float terrainHeight = TerrainGenerator.GetHeightAt(position.x, position.z, 0);
            return terrainHeight > (waterLevel + waterMargin);
        }

        /// <summary>
        /// Raycast down from position to find solid ground
        /// </summary>
        private Vector3 SnapToGround(Vector3 position)
        {
            // Start raycast from above the position
            Vector3 rayStart = position + Vector3.up * groundCheckHeight;

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayers))
            {
                if (showDebugInfo)
                    Debug.Log($"[EnemySpawnManager] Found ground at {hit.point.y} (original: {position.y})");

                return hit.point + Vector3.up * spawnHeightOffset;
            }

            // No ground found, try from the original position going down
            if (Physics.Raycast(position, Vector3.down, out hit, groundCheckDistance, groundLayers))
            {
                return hit.point + Vector3.up * spawnHeightOffset;
            }

            // Still no ground - warn and return original
            Debug.LogWarning($"[EnemySpawnManager] No ground found at {position}. Using original position.");
            return position;
        }

        /// <summary>
        /// Check if a position has valid ground beneath it
        /// </summary>
        public bool HasGroundAt(Vector3 position)
        {
            Vector3 rayStart = position + Vector3.up * groundCheckHeight;
            return Physics.Raycast(rayStart, Vector3.down, groundCheckDistance, groundLayers);
        }

        private Quaternion GetRandomSpawnRotation()
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
                return transform.rotation;

            Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
            return point != null ? point.rotation : Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        }

        private void OnEnemyDeath(GameObject enemy)
        {
            activeEnemies.Remove(enemy);
            OnEnemyDied?.Invoke(enemy);

            if (showDebugInfo)
                Debug.Log($"[EnemySpawnManager] Enemy died. Active: {activeEnemies.Count}");

            // Check if wave is cleared
            if (useWaves && activeEnemies.Count == 0 && !isSpawning)
            {
                OnWaveCleared?.Invoke(currentWave);

                if (autoStartNextWave)
                {
                    StartCoroutine(StartNextWaveAfterDelay());
                }
            }
        }

        #region Wave System

        /// <summary>
        /// Start a specific wave
        /// </summary>
        public void StartWave(int waveNumber)
        {
            currentWave = waveNumber;
            OnWaveStarted?.Invoke(currentWave);

            if (showDebugInfo)
                Debug.Log($"[EnemySpawnManager] Starting wave {currentWave}");

            StartCoroutine(SpawnWaveRoutine());
        }

        private IEnumerator SpawnWaveRoutine()
        {
            isSpawning = true;

            int enemiesToSpawn = enemiesPerWave + (currentWave - 1) * 2; // Scale with waves

            for (int i = 0; i < enemiesToSpawn; i++)
            {
                if (activeEnemies.Count >= maxEnemies)
                {
                    yield return new WaitUntil(() => activeEnemies.Count < maxEnemies);
                }

                SpawnRandomEnemy();
                yield return new WaitForSeconds(0.5f); // Stagger spawns
            }

            isSpawning = false;
        }

        private IEnumerator StartNextWaveAfterDelay()
        {
            yield return new WaitForSeconds(timeBetweenWaves);
            StartWave(currentWave + 1);
        }

        #endregion

        #region Continuous Spawn

        private IEnumerator ContinuousSpawnRoutine()
        {
            while (true)
            {
                float waitTime = spawnInterval + Random.Range(-spawnIntervalVariation, spawnIntervalVariation);
                yield return new WaitForSeconds(Mathf.Max(0.5f, waitTime));

                if (activeEnemies.Count < maxEnemies)
                {
                    SpawnRandomEnemy();
                }
            }
        }

        #endregion

        /// <summary>
        /// Kill all active enemies
        /// </summary>
        public void KillAllEnemies()
        {
            foreach (var enemy in activeEnemies.ToArray())
            {
                if (enemy != null)
                {
                    var health = enemy.GetComponent<EnemyHealth>();
                    if (health != null)
                    {
                        health.TakeDamage(99999f);
                    }
                    else
                    {
                        Destroy(enemy);
                    }
                }
            }
            activeEnemies.Clear();
        }

        /// <summary>
        /// Despawn all enemies without death effects
        /// </summary>
        public void DespawnAllEnemies()
        {
            foreach (var enemy in activeEnemies)
            {
                if (enemy != null)
                    Destroy(enemy);
            }
            activeEnemies.Clear();
        }

        private void OnDrawGizmos()
        {
            // Draw spawn points
            if (spawnPoints != null)
            {
                Gizmos.color = spawnPointGizmoColor;
                foreach (var point in spawnPoints)
                {
                    if (point != null)
                    {
                        Gizmos.DrawWireSphere(point.position, 0.5f);
                        Gizmos.DrawLine(point.position, point.position + Vector3.up * 2f);
                        Gizmos.DrawRay(point.position + Vector3.up, point.forward);
                    }
                }
            }

            // Draw patrol route
            if (sharedPatrolPoints != null && sharedPatrolPoints.Length > 1)
            {
                Gizmos.color = Color.cyan;
                for (int i = 0; i < sharedPatrolPoints.Length; i++)
                {
                    if (sharedPatrolPoints[i] == null) continue;

                    Gizmos.DrawWireSphere(sharedPatrolPoints[i].position, 0.3f);

                    int next = (i + 1) % sharedPatrolPoints.Length;
                    if (sharedPatrolPoints[next] != null)
                    {
                        Gizmos.DrawLine(sharedPatrolPoints[i].position, sharedPatrolPoints[next].position);
                    }
                }
            }
        }
    }
}
