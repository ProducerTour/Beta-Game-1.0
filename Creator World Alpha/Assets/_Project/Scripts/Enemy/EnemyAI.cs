using UnityEngine;
using CreatorWorld.Config;
using CreatorWorld.World;
using CreatorWorld.Combat;
using CreatorWorld.Player;

namespace CreatorWorld.Enemy
{
    /// <summary>
    /// Zombie AI with working player detection, chase, and attack behaviors.
    /// States: Idle, Wander, Patrol, Chase, Combat, Dead
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(EnemyHealth))]
    [RequireComponent(typeof(EnemyAnimation))]
    public class EnemyAI : MonoBehaviour
    {
        public enum AIState
        {
            Idle,
            Wander,
            Patrol,
            Chase,
            Combat,
            Dead
        }

        [Header("Configuration")]
        [SerializeField] private EnemyConfig config;

        [Header("Player Detection")]
        [Tooltip("Layer mask for player detection")]
        [SerializeField] private LayerMask playerLayer = 1 << 6; // Default to layer 6

        [Tooltip("Tag to identify player")]
        [SerializeField] private string playerTag = "Player";

        [Header("Proximity & Hearing")]
        [Tooltip("Distance at which player is detected regardless of view angle")]
        [SerializeField] private float proximityDetectionRange = 5f;

        [Tooltip("Range at which gunshots can be heard")]
        [SerializeField] private float hearingRange = 40f;

        [Tooltip("Whether this enemy responds to gunshot sounds")]
        [SerializeField] private bool canHearGunshots = true;

        [Header("Wander Settings")]
        [SerializeField] private bool enableWander = true;
        [SerializeField] private float wanderRadius = 10f;
        [SerializeField] private float minWanderTime = 2f;
        [SerializeField] private float maxWanderTime = 5f;
        [SerializeField] private float wanderPauseTime = 1f;
        [SerializeField] private bool avoidWater = true;
        [SerializeField] private float waterMargin = 0.5f;
        [SerializeField] private float minHeightRestriction = 0f;
        [SerializeField] private int maxWanderAttempts = 10;

        [Header("Patrol Settings")]
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField] private bool loopPatrol = true;
        [SerializeField] private float idleTimeVariation = 1f;

        [Header("Death Settings")]
        [Tooltip("Time before corpse despawns after death")]
        [SerializeField] private float despawnDelay = 7f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] private bool logStateChanges = false;
        [SerializeField] private AIState currentState = AIState.Idle;

        // Components
        private CharacterController characterController;
        private EnemyHealth health;
        private EnemyAnimation enemyAnimation;

        // Player tracking
        private Transform playerTarget;
        private Transform cachedPlayerTransform; // Fallback for CharacterController detection
        private float detectionCheckTimer;
        private const float DETECTION_CHECK_INTERVAL = 0.2f;

        // Combat state
        private float attackCooldownTimer;
        private float attackAnimationTimer;
        private const float ATTACK_ANIMATION_DURATION = 1.0f;

        // Patrol state
        private int currentPatrolIndex;
        private int patrolDirection = 1;
        private float waitTimer;
        private bool isWaiting;

        // Wander state
        private Vector3 spawnPosition;
        private Vector3 wanderTarget;
        private float wanderTimer;
        private bool isWanderPausing;

        // Fall detection
        private int fallResetCount;
        private const int MAX_FALL_RESETS = 3;

        // Movement
        private Vector3 moveDirection;
        private float gravity = -9.81f;
        private float verticalVelocity;

        // Terrain sync
        private bool terrainReady = false;

        // Properties
        public AIState CurrentState => currentState;
        public EnemyConfig Config => config;
        public Transform PlayerTarget => playerTarget;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            health = GetComponent<EnemyHealth>();
            enemyAnimation = GetComponent<EnemyAnimation>();
        }

        private void OnEnable()
        {
            if (health != null)
                health.OnDeath += HandleDeath;

            // Subscribe to gunshot events
            if (canHearGunshots)
                WeaponBase.OnGunfired += OnGunfired;
        }

        private void OnDisable()
        {
            if (health != null)
                health.OnDeath -= HandleDeath;

            // Unsubscribe from gunshot events
            WeaponBase.OnGunfired -= OnGunfired;
        }

        private void OnDestroy()
        {
            // Log destruction to help debug disappearing zombies
            if (showDebugInfo)
                Debug.Log($"[EnemyAI] {gameObject.name} is being destroyed. State={currentState}, Position={transform.position}, FallResets={fallResetCount}");
        }

        private void Start()
        {
            // Remember spawn position for wander bounds
            spawnPosition = transform.position;

            if (showDebugInfo)
                Debug.Log($"[EnemyAI] {gameObject.name} Start() - Initial position: {transform.position}, TerrainReady: {TerrainGenerator.IsInitialized}");

            // If terrain isn't ready yet, disable CharacterController to prevent falling through world
            if (!TerrainGenerator.IsInitialized || !HasGroundBelow())
            {
                characterController.enabled = false;
                if (showDebugInfo)
                    Debug.Log($"[EnemyAI] {gameObject.name} - Terrain not ready, disabling CharacterController until terrain loads");
            }
            else
            {
                terrainReady = true;
            }

            // Set minimum height restriction based on water level
            UpdateMinHeightRestriction();

            // Only validate spawn position if terrain is already ready
            if (terrainReady)
            {
                ValidateAndFixSpawnPosition();
            }

            // Start patrolling if we have waypoints, otherwise wander or idle
            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                currentState = AIState.Patrol;
            }
            else if (enableWander)
            {
                currentState = AIState.Wander;
                if (terrainReady)
                {
                    PickNewWanderTarget();
                }
            }
            else
            {
                currentState = AIState.Idle;
            }

            if (showDebugInfo)
                Debug.Log($"[EnemyAI] {gameObject.name} initialized with state: {currentState}, avoidWater: {avoidWater}, minHeight: {minHeightRestriction:F1}, terrainReady: {terrainReady}");

            // Cache player reference for fallback detection
            CachePlayerReference();
        }

        /// <summary>
        /// Cache the player transform for fallback detection when Physics.OverlapSphere fails.
        /// This is needed because CharacterController is not detected by OverlapSphere.
        /// </summary>
        private void CachePlayerReference()
        {
            if (cachedPlayerTransform != null) return;

            var playerController = FindFirstObjectByType<PlayerController>();
            if (playerController != null)
            {
                cachedPlayerTransform = playerController.transform;
                if (showDebugInfo)
                    Debug.Log($"[EnemyAI] {gameObject.name} cached player reference: {cachedPlayerTransform.name}");
            }
        }

        /// <summary>
        /// Check if spawn position is valid, find a better one if not
        /// </summary>
        private void ValidateAndFixSpawnPosition()
        {
            if (!avoidWater || minHeightRestriction <= 0f)
                return;

            // Check if current position is valid
            float currentGroundHeight = GetGroundHeightAt(transform.position);
            if (currentGroundHeight > minHeightRestriction)
            {
                // Current position is fine, update spawn position to use actual ground height
                spawnPosition = new Vector3(transform.position.x, currentGroundHeight, transform.position.z);
                return;
            }

            if (showDebugInfo)
                Debug.LogWarning($"[EnemyAI] {gameObject.name} spawned underwater (ground={currentGroundHeight:F1}, threshold={minHeightRestriction:F1}), searching for valid position...");

            // Search in expanding rings for valid position
            for (float radius = 5f; radius <= wanderRadius * 2f; radius += 5f)
            {
                for (int angle = 0; angle < 360; angle += 45)
                {
                    float rad = angle * Mathf.Deg2Rad;
                    Vector3 candidatePos = transform.position + new Vector3(Mathf.Cos(rad) * radius, 0, Mathf.Sin(rad) * radius);
                    float groundHeight = GetGroundHeightAt(candidatePos);

                    if (groundHeight > minHeightRestriction)
                    {
                        // Found valid position - teleport there (add 1.0 to ensure character is above ground)
                        Vector3 validPosition = new Vector3(candidatePos.x, groundHeight + 1.0f, candidatePos.z);

                        if (showDebugInfo)
                            Debug.Log($"[EnemyAI] {gameObject.name} found valid position at {validPosition} (ground={groundHeight:F1})");

                        characterController.enabled = false;
                        transform.position = validPosition;
                        spawnPosition = validPosition;
                        characterController.enabled = true;
                        return;
                    }
                }
            }

            // Couldn't find valid position - disable water avoidance for this enemy
            if (showDebugInfo)
                Debug.LogWarning($"[EnemyAI] {gameObject.name} could not find valid spawn position, disabling water avoidance");

            avoidWater = false;
        }

        /// <summary>
        /// Update min height restriction when terrain becomes available
        /// </summary>
        private void UpdateMinHeightRestriction()
        {
            if (avoidWater && TerrainGenerator.IsInitialized && minHeightRestriction <= 0f)
            {
                minHeightRestriction = TerrainGenerator.WaterLevel + waterMargin;
            }
        }

        private void Update()
        {
            if (currentState == AIState.Dead) return;

            // Wait for terrain to be ready
            if (!terrainReady)
            {
                if (TerrainGenerator.IsInitialized && HasGroundBelow())
                {
                    terrainReady = true;

                    // Re-enable CharacterController now that terrain is ready
                    if (!characterController.enabled)
                    {
                        characterController.enabled = true;
                        if (showDebugInfo)
                            Debug.Log($"[EnemyAI] {gameObject.name} - Terrain ready, enabling CharacterController");
                    }

                    // Now validate and fix spawn position
                    ValidateAndFixSpawnPosition();

                    // Pick wander target if we're in wander state
                    if (currentState == AIState.Wander)
                    {
                        PickNewWanderTarget();
                    }
                }
                else
                {
                    return;
                }
            }

            // Update water avoidance threshold
            if (avoidWater && minHeightRestriction <= 0f)
            {
                UpdateMinHeightRestriction();
            }

            // Enforce minimum height
            if (avoidWater && minHeightRestriction > 0f)
            {
                EnforceMinimumHeight();
            }

            // Player detection (not every frame for performance)
            detectionCheckTimer -= Time.deltaTime;
            if (detectionCheckTimer <= 0f)
            {
                detectionCheckTimer = DETECTION_CHECK_INTERVAL;
                DetectPlayer();
            }

            // State machine
            switch (currentState)
            {
                case AIState.Idle:
                    UpdateIdle();
                    break;
                case AIState.Wander:
                    UpdateWander();
                    break;
                case AIState.Patrol:
                    UpdatePatrol();
                    break;
                case AIState.Chase:
                    UpdateChase();
                    break;
                case AIState.Combat:
                    UpdateCombat();
                    break;
            }

            ApplyGravity();

            if (logStateChanges && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[EnemyAI] {gameObject.name} State={currentState}, Target={playerTarget?.name ?? "none"}");
            }
        }

        /// <summary>
        /// Enforce minimum height - if enemy is below water level AND actually falling, find a valid position
        /// </summary>
        private void EnforceMinimumHeight()
        {
            // Only trigger if BOTH conditions are met:
            // 1. Below the threshold (with buffer)
            // 2. Actually falling (not grounded) - prevents false positives on low but valid terrain
            bool isBelowThreshold = transform.position.y < minHeightRestriction - 1f;
            bool isActuallyFalling = !characterController.isGrounded && verticalVelocity < -1f;

            // Also check if we're standing on valid ground despite being "low"
            float groundHeightHere = GetGroundHeightAt(transform.position);
            bool isOnValidGround = groundHeightHere > minHeightRestriction - 0.5f &&
                                   Mathf.Abs(transform.position.y - groundHeightHere) < 2f;

            if (isBelowThreshold && isActuallyFalling && !isOnValidGround)
            {
                fallResetCount++;

                // If stuck in infinite fall loop, destroy this zombie
                if (fallResetCount > MAX_FALL_RESETS)
                {
                    Debug.LogWarning($"[EnemyAI] {gameObject.name} stuck in fall loop (reset {fallResetCount} times) at Y={transform.position.y:F1}, destroying...");
                    Destroy(gameObject);
                    return;
                }

                if (showDebugInfo)
                    Debug.Log($"[EnemyAI] {gameObject.name} fell below minimum height ({transform.position.y:F1} < {minHeightRestriction:F1}), finding valid position... (reset #{fallResetCount})");

                // First check if spawn position is valid
                float spawnGroundHeight = GetGroundHeightAt(spawnPosition);
                Vector3 targetPosition;

                if (spawnGroundHeight > minHeightRestriction)
                {
                    // Spawn is valid, teleport there (add 1.0 to ensure character is above ground)
                    targetPosition = new Vector3(spawnPosition.x, spawnGroundHeight + 1.0f, spawnPosition.z);
                }
                else
                {
                    // Spawn is also underwater - search for valid position
                    targetPosition = FindNearestValidPosition();
                    if (targetPosition == Vector3.zero)
                    {
                        // No valid position found - disable water avoidance and let it fall
                        Debug.LogWarning($"[EnemyAI] {gameObject.name} could not find valid position, disabling water avoidance");
                        avoidWater = false;
                        return;
                    }
                }

                // Teleport to valid position
                characterController.enabled = false;
                transform.position = targetPosition;
                spawnPosition = targetPosition; // Update spawn to valid location
                characterController.enabled = true;

                // Reset wander state
                if (currentState == AIState.Wander)
                {
                    isWanderPausing = true;
                    wanderTimer = 0.5f;
                    moveDirection = Vector3.zero;
                }
            }
            else if (fallResetCount > 0 && characterController.isGrounded)
            {
                // Reset fall counter once properly grounded
                fallResetCount = 0;
            }
        }

        /// <summary>
        /// Search for the nearest valid position above water
        /// </summary>
        private Vector3 FindNearestValidPosition()
        {
            // Search in expanding rings
            for (float radius = 2f; radius <= wanderRadius * 2f; radius += 3f)
            {
                for (int angle = 0; angle < 360; angle += 30)
                {
                    float rad = angle * Mathf.Deg2Rad;
                    Vector3 candidatePos = transform.position + new Vector3(Mathf.Cos(rad) * radius, 0, Mathf.Sin(rad) * radius);
                    float groundHeight = GetGroundHeightAt(candidatePos);

                    if (groundHeight > minHeightRestriction)
                    {
                        return new Vector3(candidatePos.x, groundHeight + 1.0f, candidatePos.z);
                    }
                }
            }

            return Vector3.zero; // No valid position found
        }

        private void UpdateIdle()
        {
            // Just stand in place with rifle ready
            // Animation will show idle with rifle based on Speed = 0
        }

        private void UpdateWander()
        {
            // Handle pause between movements
            if (isWanderPausing)
            {
                wanderTimer -= Time.deltaTime;
                moveDirection = Vector3.zero; // Ensure we're stopped during pause
                if (wanderTimer <= 0f)
                {
                    isWanderPausing = false;
                    PickNewWanderTarget();
                }
                return;
            }

            // Move towards wander target
            Vector3 directionToTarget = wanderTarget - transform.position;
            directionToTarget.y = 0;
            float distanceToTarget = directionToTarget.magnitude;

            // Count down wander timer
            wanderTimer -= Time.deltaTime;

            // Check if we've reached target or timer expired
            if (distanceToTarget < 0.5f || wanderTimer <= 0f)
            {
                // Start pause before next wander
                isWanderPausing = true;
                wanderTimer = wanderPauseTime + Random.Range(-0.5f, 0.5f);
                moveDirection = Vector3.zero;
                return;
            }

            // Check if next step would put us in water (only check ahead, not current position)
            float speed = config != null ? config.WalkSpeed : 2f;
            Vector3 nextPosition = transform.position + directionToTarget.normalized * speed * 0.5f; // Look ahead 0.5 seconds
            if (avoidWater && !IsPositionAboveWater(nextPosition))
            {
                if (showDebugInfo)
                    Debug.Log("[EnemyAI] Water ahead! Picking new target.");

                // Stop and pick a new target
                isWanderPausing = true;
                wanderTimer = 0.2f; // Short pause before retrying
                moveDirection = Vector3.zero;
                return;
            }

            // Move towards target
            moveDirection = directionToTarget.normalized;

            // Rotate towards movement direction
            RotateTowards(moveDirection);

            // Apply movement
            Vector3 movement = moveDirection * speed * Time.deltaTime;
            movement.y = verticalVelocity * Time.deltaTime;
            characterController.Move(movement);

            if (showDebugInfo)
                Debug.Log($"[EnemyAI] Wandering to {wanderTarget}, distance: {distanceToTarget:F2}, speed: {speed:F2}");
        }

        private void PickNewWanderTarget()
        {
            // Try to find a valid wander target that isn't in water
            for (int attempt = 0; attempt < maxWanderAttempts; attempt++)
            {
                // Pick random point within wander radius from spawn
                Vector2 randomCircle = Random.insideUnitCircle * wanderRadius;
                Vector3 candidateTarget = spawnPosition + new Vector3(randomCircle.x, 0, randomCircle.y);

                // Check if target is above water
                if (avoidWater && !IsPositionAboveWater(candidateTarget))
                {
                    if (showDebugInfo)
                        Debug.Log($"[EnemyAI] Wander target {candidateTarget} is in water, trying again...");
                    continue;
                }

                // Valid target found
                wanderTarget = candidateTarget;
                wanderTimer = Random.Range(minWanderTime, maxWanderTime);

                if (showDebugInfo)
                    Debug.Log($"[EnemyAI] New wander target: {wanderTarget}");
                return;
            }

            // Couldn't find valid target above water, try to find any walkable direction
            // Pick a direction toward higher ground (away from spawn if spawn might be near water)
            if (showDebugInfo)
                Debug.LogWarning($"[EnemyAI] Could not find valid wander target after {maxWanderAttempts} attempts, trying to walk toward higher ground");

            // Try walking in a random direction for a short distance
            // The real-time water checks will stop us if we head toward water
            Vector2 randomDir = Random.insideUnitCircle.normalized * (wanderRadius * 0.5f);
            wanderTarget = transform.position + new Vector3(randomDir.x, 0, randomDir.y);
            wanderTimer = Random.Range(minWanderTime * 0.5f, maxWanderTime * 0.5f);
        }

        /// <summary>
        /// Check if a position is above water level using height restriction
        /// </summary>
        private bool IsPositionAboveWater(Vector3 position)
        {
            // If water avoidance is disabled or terrain not ready, allow all positions
            if (!avoidWater)
                return true;

            // If minHeightRestriction not set and terrain not ready, allow movement
            if (minHeightRestriction <= 0f && !TerrainGenerator.IsInitialized)
                return true;

            // Determine the effective water threshold
            float waterThreshold = minHeightRestriction > 0f
                ? minHeightRestriction
                : (TerrainGenerator.IsInitialized ? TerrainGenerator.WaterLevel + waterMargin : 0f);

            // If no valid threshold, allow all positions
            if (waterThreshold <= 0f)
                return true;

            // Quick check: if we're checking the current enemy position, use actual Y
            float distanceFromEnemy = Vector3.Distance(new Vector3(position.x, 0, position.z),
                                                        new Vector3(transform.position.x, 0, transform.position.z));
            if (distanceFromEnemy < 0.5f)
            {
                // This is checking our current position - use actual Y value
                return transform.position.y > waterThreshold;
            }

            // For distant positions (wander targets), need to find ground height at that XZ
            float groundHeight = GetGroundHeightAt(position);
            bool aboveWater = groundHeight > waterThreshold;

            if (showDebugInfo && !aboveWater)
                Debug.Log($"[EnemyAI] Position ({position.x:F1}, {position.z:F1}) ground={groundHeight:F1}, threshold={waterThreshold:F1}, valid={aboveWater}");

            return aboveWater;
        }

        /// <summary>
        /// Check if there's valid ground below this enemy (terrain loaded)
        /// </summary>
        private bool HasGroundBelow()
        {
            Vector3 rayStart = transform.position + Vector3.up * 2f;
            return Physics.Raycast(rayStart, Vector3.down, 50f);
        }

        /// <summary>
        /// Get the ground height at an XZ position using raycast
        /// </summary>
        private float GetGroundHeightAt(Vector3 position)
        {
            // Always raycast to find actual ground height at this XZ coordinate
            Vector3 rayStart = new Vector3(position.x, 200f, position.z);
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 400f))
            {
                return hit.point.y;
            }

            // Fallback: use terrain generator
            if (TerrainGenerator.IsInitialized)
            {
                return TerrainGenerator.GetHeightAt(position.x, position.z, 0);
            }

            // Last fallback: use the input position Y
            return position.y;
        }

        private void UpdatePatrol()
        {
            if (patrolPoints == null || patrolPoints.Length == 0)
            {
                currentState = AIState.Idle;
                return;
            }

            // Check if we're waiting at a patrol point
            if (isWaiting)
            {
                waitTimer -= Time.deltaTime;
                if (waitTimer <= 0f)
                {
                    isWaiting = false;
                    MoveToNextPatrolPoint();
                }
                return;
            }

            // Get current target
            Transform target = patrolPoints[currentPatrolIndex];
            if (target == null)
            {
                MoveToNextPatrolPoint();
                return;
            }

            // Move towards target
            Vector3 directionToTarget = target.position - transform.position;
            directionToTarget.y = 0; // Keep horizontal
            float distanceToTarget = directionToTarget.magnitude;

            if (distanceToTarget > 0.5f)
            {
                // Move towards target
                moveDirection = directionToTarget.normalized;

                // Rotate towards movement direction
                RotateTowards(moveDirection);

                // Apply movement
                float speed = config != null ? config.WalkSpeed : 2f;
                Vector3 movement = moveDirection * speed * Time.deltaTime;
                movement.y = verticalVelocity * Time.deltaTime;
                characterController.Move(movement);

                if (showDebugInfo)
                    Debug.Log($"[EnemyAI] Patrolling to point {currentPatrolIndex}, distance: {distanceToTarget:F2}");
            }
            else
            {
                // Arrived at patrol point, wait
                StartWaiting();
            }
        }

        private void StartWaiting()
        {
            isWaiting = true;
            float baseWait = config != null ? config.PatrolWaitTime : 2f;
            waitTimer = baseWait + Random.Range(-idleTimeVariation, idleTimeVariation);
            moveDirection = Vector3.zero;
        }

        private void MoveToNextPatrolPoint()
        {
            if (loopPatrol)
            {
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            }
            else
            {
                // Ping-pong
                currentPatrolIndex += patrolDirection;
                if (currentPatrolIndex >= patrolPoints.Length - 1 || currentPatrolIndex <= 0)
                {
                    patrolDirection *= -1;
                }
            }
        }

        /// <summary>
        /// Detect player within view distance/angle OR within proximity range (even from behind)
        /// </summary>
        private void DetectPlayer()
        {
            // Don't detect if already in chase/combat (handled by those states)
            if (currentState == AIState.Chase || currentState == AIState.Combat)
                return;

            // Ensure we have cached player reference
            if (cachedPlayerTransform == null)
                CachePlayerReference();

            Transform targetToCheck = null;

            float viewDistance = config != null ? config.ViewDistance : 20f;
            float viewAngle = config != null ? config.ViewAngle : 60f;

            // Find player colliders in range (use max of view distance and proximity range)
            float searchRadius = Mathf.Max(viewDistance, proximityDetectionRange);
            Collider[] hits = Physics.OverlapSphere(transform.position, searchRadius, playerLayer);

            // Try to find player via physics first
            foreach (var hit in hits)
            {
                if (hit.CompareTag(playerTag))
                {
                    targetToCheck = hit.transform;
                    break;
                }
            }

            // Fallback: use cached player transform if physics detection failed
            // This handles the case where player only has CharacterController (no Collider)
            if (targetToCheck == null && cachedPlayerTransform != null)
            {
                float distToPlayer = Vector3.Distance(transform.position, cachedPlayerTransform.position);
                if (distToPlayer <= searchRadius)
                {
                    targetToCheck = cachedPlayerTransform;
                }
            }

            // No target found
            if (targetToCheck == null) return;

            // Calculate direction and distance to target
            Vector3 dirToPlayer = (targetToCheck.position - transform.position);
            dirToPlayer.y = 0;
            float distance = dirToPlayer.magnitude;

            if (distance < 0.1f) return;

            // PROXIMITY CHECK: Detect player if very close, regardless of view angle
            // This means sneaking up on a zombie is not safe!
            if (distance <= proximityDetectionRange)
            {
                playerTarget = targetToCheck;
                currentState = AIState.Chase;

                if (showDebugInfo)
                    Debug.Log($"[EnemyAI] {gameObject.name} detected player by PROXIMITY at distance {distance:F1}");

                return;
            }

            // VIEW ANGLE CHECK: Detect player in front within view distance
            float angle = Vector3.Angle(transform.forward, dirToPlayer.normalized);
            if (distance <= viewDistance && angle < viewAngle / 2f)
            {
                // Player detected - start chasing
                playerTarget = targetToCheck;
                currentState = AIState.Chase;

                if (showDebugInfo)
                    Debug.Log($"[EnemyAI] {gameObject.name} detected player by SIGHT at distance {distance:F1}");

                return;
            }
        }

        /// <summary>
        /// Called when any weapon fires. Zombies will investigate/chase if within hearing range.
        /// </summary>
        private void OnGunfired(Vector3 shooterPosition, float loudness)
        {
            // Dead enemies don't respond
            if (currentState == AIState.Dead) return;

            // Already chasing/fighting? Don't need to respond
            if (currentState == AIState.Chase || currentState == AIState.Combat) return;

            // Check if gunshot is within hearing range
            float distance = Vector3.Distance(transform.position, shooterPosition);
            float effectiveRange = hearingRange * loudness;

            if (distance <= effectiveRange)
            {
                // Try to find the player transform to chase via physics first
                Collider[] hits = Physics.OverlapSphere(shooterPosition, 2f, playerLayer);
                foreach (var hit in hits)
                {
                    if (hit.CompareTag(playerTag))
                    {
                        playerTarget = hit.transform;
                        currentState = AIState.Chase;

                        if (showDebugInfo)
                            Debug.Log($"[EnemyAI] {gameObject.name} HEARD gunshot at distance {distance:F1}, chasing!");

                        return;
                    }
                }

                // Fallback: use cached player reference (for CharacterController players)
                if (cachedPlayerTransform == null)
                    CachePlayerReference();

                if (cachedPlayerTransform != null)
                {
                    playerTarget = cachedPlayerTransform;
                    currentState = AIState.Chase;

                    if (showDebugInfo)
                        Debug.Log($"[EnemyAI] {gameObject.name} HEARD gunshot at distance {distance:F1}, chasing (via cached ref)!");

                    return;
                }

                // If we still can't find player, log it
                if (showDebugInfo)
                    Debug.Log($"[EnemyAI] {gameObject.name} heard gunshot but couldn't locate shooter");
            }
        }

        private void UpdateChase()
        {
            // Lost target check
            if (playerTarget == null)
            {
                ReturnToPatrol();
                return;
            }

            float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);
            float attackRange = config != null ? config.AttackRange : 2f;
            float viewDistance = config != null ? config.ViewDistance : 20f;

            // If in attack range, switch to combat
            if (distanceToPlayer <= attackRange)
            {
                currentState = AIState.Combat;
                attackCooldownTimer = 0f; // Attack immediately
                return;
            }

            // If player too far, lose target
            if (distanceToPlayer > viewDistance * 1.5f)
            {
                if (showDebugInfo)
                    Debug.Log($"[EnemyAI] {gameObject.name} lost player (too far: {distanceToPlayer:F1})");

                playerTarget = null;
                ReturnToPatrol();
                return;
            }

            // Move towards player
            Vector3 direction = (playerTarget.position - transform.position);
            direction.y = 0;
            direction.Normalize();

            RotateTowards(direction);

            // Check water avoidance for next step
            float speed = config != null ? config.RunSpeed : 4.5f;
            Vector3 nextPos = transform.position + direction * speed * 0.5f;

            if (avoidWater && !IsPositionAboveWater(nextPos))
            {
                // Water ahead - try to go around or stop
                moveDirection = Vector3.zero;
                return;
            }

            // Move
            moveDirection = direction;
            Vector3 movement = moveDirection * speed * Time.deltaTime;
            movement.y = verticalVelocity * Time.deltaTime;
            characterController.Move(movement);
        }

        private void UpdateCombat()
        {
            // Lost target check
            if (playerTarget == null)
            {
                ReturnToPatrol();
                return;
            }

            float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);
            float attackRange = config != null ? config.AttackRange : 2f;

            // If player moved out of attack range, chase
            if (distanceToPlayer > attackRange * 1.2f)
            {
                currentState = AIState.Chase;
                return;
            }

            // Face the player
            Vector3 direction = (playerTarget.position - transform.position);
            direction.y = 0;
            if (direction.sqrMagnitude > 0.01f)
            {
                RotateTowards(direction.normalized);
            }

            // Stop moving during combat
            moveDirection = Vector3.zero;

            // Handle attack animation timing
            if (attackAnimationTimer > 0)
            {
                attackAnimationTimer -= Time.deltaTime;
                if (attackAnimationTimer <= 0)
                {
                    enemyAnimation.OnAttackFinished();
                }
                return;
            }

            // Attack on cooldown
            attackCooldownTimer -= Time.deltaTime;
            if (attackCooldownTimer <= 0f)
            {
                PerformAttack();
                float cooldown = config != null ? config.AttackCooldown : 1.5f;
                attackCooldownTimer = cooldown;
            }
        }

        private void PerformAttack()
        {
            if (enemyAnimation != null)
            {
                enemyAnimation.TriggerAttack();
                attackAnimationTimer = ATTACK_ANIMATION_DURATION;

                if (showDebugInfo)
                    Debug.Log($"[EnemyAI] {gameObject.name} attacking!");
            }

            // Note: Actual damage dealing would go here when implemented
            // For now, just plays the animation
        }

        private void RotateTowards(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.001f) return;

            float rotationSpeed = config != null ? config.RotationSpeed : 360f;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        private void ApplyGravity()
        {
            if (characterController.isGrounded)
            {
                verticalVelocity = -2f; // Small downward force to keep grounded
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }
        }

        private void HandleDeath()
        {
            currentState = AIState.Dead;
            playerTarget = null;

            // Trigger death animation
            if (enemyAnimation != null)
            {
                enemyAnimation.TriggerDeath(0);
            }

            // Disable movement
            if (characterController != null)
                characterController.enabled = false;

            if (showDebugInfo)
                Debug.Log($"[EnemyAI] {gameObject.name} has died, despawning in {despawnDelay}s");

            // Start despawn timer
            StartCoroutine(DespawnAfterDelay());
        }

        private System.Collections.IEnumerator DespawnAfterDelay()
        {
            yield return new WaitForSeconds(despawnDelay);

            if (showDebugInfo)
                Debug.Log($"[EnemyAI] {gameObject.name} despawning");

            Destroy(gameObject);
        }

        /// <summary>
        /// Set patrol points at runtime
        /// </summary>
        public void SetPatrolPoints(Transform[] points)
        {
            patrolPoints = points;
            currentPatrolIndex = 0;
            currentState = points != null && points.Length > 0 ? AIState.Patrol : AIState.Idle;
        }

        /// <summary>
        /// Force the enemy to chase a target
        /// </summary>
        public void StartChasing(Transform target)
        {
            currentState = AIState.Chase;
            // Future: store target and implement chase logic
        }

        /// <summary>
        /// Return to patrol, wander, or idle state
        /// </summary>
        public void ReturnToPatrol()
        {
            if (currentState == AIState.Dead) return;

            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                currentState = AIState.Patrol;
            }
            else if (enableWander)
            {
                currentState = AIState.Wander;
                PickNewWanderTarget();
            }
            else
            {
                currentState = AIState.Idle;
            }
        }

        /// <summary>
        /// Enable or disable wandering behavior
        /// </summary>
        public void SetWanderEnabled(bool enabled)
        {
            enableWander = enabled;
            if (enabled && currentState == AIState.Idle)
            {
                currentState = AIState.Wander;
                PickNewWanderTarget();
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw wander radius
            if (enableWander)
            {
                Gizmos.color = new Color(0.5f, 0.8f, 0.5f, 0.3f);
                Vector3 center = Application.isPlaying ? spawnPosition : transform.position;
                Gizmos.DrawWireSphere(center, wanderRadius);

                // Draw current wander target in play mode
                if (Application.isPlaying && currentState == AIState.Wander)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(wanderTarget, 0.4f);
                    Gizmos.DrawLine(transform.position, wanderTarget);
                }
            }

            // Draw patrol route
            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                Gizmos.color = Color.cyan;
                for (int i = 0; i < patrolPoints.Length; i++)
                {
                    if (patrolPoints[i] == null) continue;

                    Gizmos.DrawWireSphere(patrolPoints[i].position, 0.3f);

                    // Draw line to next point
                    int nextIndex = (i + 1) % patrolPoints.Length;
                    if (patrolPoints[nextIndex] != null)
                    {
                        Gizmos.DrawLine(patrolPoints[i].position, patrolPoints[nextIndex].position);
                    }
                }

                // Highlight current target
                if (Application.isPlaying && currentPatrolIndex < patrolPoints.Length && patrolPoints[currentPatrolIndex] != null)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(patrolPoints[currentPatrolIndex].position, 0.5f);
                }
            }

            // Draw movement direction
            if (Application.isPlaying && moveDirection.sqrMagnitude > 0.01f)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawRay(transform.position + Vector3.up, moveDirection * 2f);
            }
        }
    }
}
