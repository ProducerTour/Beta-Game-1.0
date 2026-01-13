using UnityEngine;
using System.Collections;
using CreatorWorld.Config;
using CreatorWorld.World;

namespace CreatorWorld.Enemy
{
    /// <summary>
    /// Enemy spawner with weapon and patrol configuration.
    /// Spawns fully configured enemy NPCs with weapons and AI behavior.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private bool spawnOnStart = true;

        [Header("Weapon Configuration")]
        [Tooltip("Weapon prefab to give spawned enemies (e.g., AK47)")]
        [SerializeField] private GameObject weaponPrefab;

        [Header("AI Configuration")]
        [Tooltip("Enemy configuration asset")]
        [SerializeField] private EnemyConfig enemyConfig;

        [Tooltip("Patrol waypoints for this spawned enemy")]
        [SerializeField] private Transform[] patrolPoints;

        [Header("Terrain Sync")]
        [Tooltip("Wait for ChunkManager to finish loading before spawning")]
        [SerializeField] private bool waitForTerrain = true;
        [Tooltip("Fallback delay if ChunkManager not found")]
        [SerializeField] private float fallbackSpawnDelay = 3f;

        [Header("Ground Detection")]
        [SerializeField] private bool snapToGround = true;
        [SerializeField] private float groundCheckHeight = 50f;
        [SerializeField] private float groundCheckDistance = 100f;
        [SerializeField] private LayerMask groundLayers = ~0;

        [Header("Biome Restrictions")]
        [Tooltip("Only spawn in specific biomes")]
        [SerializeField] private bool restrictToBiomes = true;
        [Tooltip("Biomes where this enemy can spawn")]
        [SerializeField] private BiomeType[] allowedBiomes = new BiomeType[]
        {
            BiomeType.Grassland,
            BiomeType.Forest,
            BiomeType.Mountain
        };

        [Header("Debug")]
        [SerializeField] private Color gizmoColor = Color.red;
        [SerializeField] private bool showDebugInfo = false;

        private GameObject spawnedEnemy;
        private ChunkManager chunkManager;

        public GameObject SpawnedEnemy => spawnedEnemy;

        private void Start()
        {
            Debug.Log($"[EnemySpawner] Start called. SpawnOnStart: {spawnOnStart}, Prefab: {(enemyPrefab != null ? enemyPrefab.name : "NULL")}");

            if (spawnOnStart && enemyPrefab != null)
            {
                StartCoroutine(DelayedSpawnRoutine());
            }
            else if (enemyPrefab == null)
            {
                Debug.LogError("[EnemySpawner] Cannot spawn - enemyPrefab is not assigned!");
            }
        }

        private IEnumerator DelayedSpawnRoutine()
        {
            if (waitForTerrain)
            {
                chunkManager = FindFirstObjectByType<ChunkManager>();

                if (chunkManager != null)
                {
                    Debug.Log("[EnemySpawner] Waiting for terrain to finish loading...");
                    yield return new WaitUntil(() => chunkManager.IsInitialLoadComplete);
                    Debug.Log("[EnemySpawner] Terrain loaded! Spawning enemy.");
                }
                else
                {
                    Debug.LogWarning("[EnemySpawner] ChunkManager not found, using fallback delay");
                    yield return new WaitForSeconds(fallbackSpawnDelay);
                }
            }
            else
            {
                yield return new WaitForSeconds(fallbackSpawnDelay);
            }

            SpawnEnemy();
        }

        public GameObject SpawnEnemy()
        {
            if (enemyPrefab == null)
            {
                Debug.LogWarning("[EnemySpawner] No enemy prefab assigned!");
                return null;
            }

            // Get spawn position, snapped to ground if enabled
            Vector3 spawnPosition = snapToGround ? GetGroundPosition(transform.position) : transform.position;

            // Check biome restrictions
            if (!IsValidSpawnBiome(spawnPosition))
            {
                if (showDebugInfo)
                    Debug.LogWarning($"[EnemySpawner] Cannot spawn - position is in restricted biome");
                return null;
            }

            if (showDebugInfo)
                Debug.Log($"[EnemySpawner] Spawning {enemyPrefab.name} at {spawnPosition}");

            spawnedEnemy = Instantiate(enemyPrefab, spawnPosition, transform.rotation);

            Debug.Log($"[EnemySpawner] Successfully spawned: {spawnedEnemy.name}");

            // Configure weapon holder (only if weapon prefab is assigned)
            if (weaponPrefab != null)
            {
                var weaponHolder = spawnedEnemy.GetComponent<EnemyWeaponHolder>();
                if (weaponHolder == null)
                {
                    weaponHolder = spawnedEnemy.AddComponent<EnemyWeaponHolder>();
                }
                weaponHolder.SetWeaponPrefab(weaponPrefab);
                weaponHolder.SpawnWeapon();

                // Enable rifle animations
                var enemyAnim = spawnedEnemy.GetComponent<EnemyAnimation>();
                if (enemyAnim != null)
                {
                    enemyAnim.SetUseRifleAnimations(true);
                }
            }

            // Configure AI
            var enemyAI = spawnedEnemy.GetComponent<EnemyAI>();
            if (enemyAI == null)
            {
                enemyAI = spawnedEnemy.AddComponent<EnemyAI>();
            }

            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                enemyAI.SetPatrolPoints(patrolPoints);
            }

            // Configure health with config
            var health = spawnedEnemy.GetComponent<EnemyHealth>();
            if (health != null && enemyConfig != null)
            {
                health.SetConfig(enemyConfig);
            }

            Debug.Log($"[EnemySpawner] Spawned {spawnedEnemy.name} at {transform.position}" +
                (weaponPrefab != null ? $" with {weaponPrefab.name}" : ""));

            return spawnedEnemy;
        }

        /// <summary>
        /// Despawn the current enemy
        /// </summary>
        public void DespawnEnemy()
        {
            if (spawnedEnemy != null)
            {
                Destroy(spawnedEnemy);
                spawnedEnemy = null;
            }
        }

        /// <summary>
        /// Respawn the enemy (despawn then spawn)
        /// </summary>
        public void RespawnEnemy()
        {
            DespawnEnemy();
            SpawnEnemy();
        }

        /// <summary>
        /// Check if spawn position is in an allowed biome
        /// </summary>
        private bool IsValidSpawnBiome(Vector3 position)
        {
            if (!restrictToBiomes || !TerrainGenerator.IsInitialized)
                return true;

            if (allowedBiomes == null || allowedBiomes.Length == 0)
                return true;

            BiomeType biome = TerrainGenerator.GetBiomeAt(position.x, position.z, 0);

            foreach (var allowed in allowedBiomes)
            {
                if (biome == allowed) return true;
            }

            if (showDebugInfo)
                Debug.Log($"[EnemySpawner] Biome {biome} not allowed at ({position.x:F0}, {position.z:F0})");

            return false;
        }

        /// <summary>
        /// Raycast down from position to find solid ground
        /// </summary>
        private Vector3 GetGroundPosition(Vector3 position)
        {
            Vector3 rayStart = position + Vector3.up * groundCheckHeight;

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayers))
            {
                return hit.point + Vector3.up * 1.0f;
            }

            if (Physics.Raycast(position, Vector3.down, out hit, groundCheckDistance, groundLayers))
            {
                return hit.point + Vector3.up * 1.0f;
            }

            Debug.LogWarning($"[EnemySpawner] No ground found at {position}. Using original position.");
            return position;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2f);

            // Draw connection to patrol points
            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                Gizmos.color = Color.cyan;
                foreach (var point in patrolPoints)
                {
                    if (point != null)
                    {
                        Gizmos.DrawLine(transform.position, point.position);
                    }
                }
            }
        }
    }
}
