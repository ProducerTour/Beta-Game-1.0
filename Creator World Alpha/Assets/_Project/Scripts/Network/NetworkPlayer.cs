using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using CreatorWorld.Core;
using CreatorWorld.Interfaces;

namespace CreatorWorld.Network
{
    /// <summary>
    /// Core networked player component. Handles server-authoritative movement,
    /// client-side prediction, and state synchronization.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class NetworkPlayer : NetworkBehaviour
    {
        [Header("Movement Configuration")]
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float runSpeed = 7f;
        [SerializeField] private float crouchSpeed = 2f;
        [SerializeField] private float jumpForce = 8f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float rotationSpeed = 10f;

        [Header("Network Settings")]
        [SerializeField] private float positionSyncThreshold = 0.1f;
        [SerializeField] private float interpolationSpeed = 15f;
        [SerializeField] private int inputBufferSize = 64;

        [Header("Client Prediction")]
        [SerializeField] private float reconciliationThreshold = 0.5f;
        [SerializeField] private float snapThreshold = 3f;

        // Server-owned state (synced to all clients)
        private NetworkVariable<Vector3> serverPosition = new NetworkVariable<Vector3>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private NetworkVariable<Quaternion> serverRotation = new NetworkVariable<Quaternion>(
            Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private NetworkVariable<Vector3> serverVelocity = new NetworkVariable<Vector3>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private NetworkVariable<PlayerAnimState> animState = new NetworkVariable<PlayerAnimState>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private NetworkVariable<float> networkHealth = new NetworkVariable<float>(
            100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private NetworkVariable<bool> networkIsDead = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Components
        private CharacterController controller;
        private Animator animator;
        private IInputService inputService;

        // Client prediction state
        private Queue<PlayerInputPayload> inputBuffer = new Queue<PlayerInputPayload>();
        private Dictionary<uint, PlayerStateSnapshot> predictionHistory = new Dictionary<uint, PlayerStateSnapshot>();
        private uint currentTick;
        private Vector3 predictedPosition;
        private Vector3 predictedVelocity;
        private float verticalVelocity;

        // Remote player interpolation
        private Vector3 interpolationTarget;
        private Quaternion rotationTarget;

        // Local state
        private bool isCrouching;
        private bool isSprinting;

        // Animation hashes
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
        private static readonly int IsSprintingHash = Animator.StringToHash("IsSprinting");
        private static readonly int JumpHash = Animator.StringToHash("Jump");

        public float Health => networkHealth.Value;
        public bool IsDead => networkIsDead.Value;
        public Vector3 Velocity => IsOwner ? predictedVelocity : serverVelocity.Value;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            controller = GetComponent<CharacterController>();
            animator = GetComponentInChildren<Animator>();

            if (IsOwner)
            {
                // Local player setup
                inputService = ServiceLocator.Get<IInputService>();
                predictedPosition = transform.position;
                currentTick = 0;

                // Register with camera service
                var cameraService = ServiceLocator.Get<ICameraService>();
                if (cameraService != null && cameraService is MonoBehaviour cameraMono)
                {
                    // Camera should follow this player
                    Debug.Log($"[NetworkPlayer] Local player spawned at {transform.position}");
                }
            }
            else
            {
                // Remote player setup
                interpolationTarget = transform.position;
                rotationTarget = transform.rotation;

                // Subscribe to state changes for interpolation
                serverPosition.OnValueChanged += OnServerPositionChanged;
                serverRotation.OnValueChanged += OnServerRotationChanged;
            }

            // Subscribe to health changes
            networkHealth.OnValueChanged += OnHealthChanged;
            networkIsDead.OnValueChanged += OnDeathStateChanged;

            Debug.Log($"[NetworkPlayer] Spawned: IsOwner={IsOwner}, IsServer={IsServer}, ClientId={OwnerClientId}");
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (!IsOwner)
            {
                serverPosition.OnValueChanged -= OnServerPositionChanged;
                serverRotation.OnValueChanged -= OnServerRotationChanged;
            }

            networkHealth.OnValueChanged -= OnHealthChanged;
            networkIsDead.OnValueChanged -= OnDeathStateChanged;
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsOwner)
            {
                // Local player: handle input and prediction
                HandleLocalPlayer();
            }
            else
            {
                // Remote player: interpolate to server state
                HandleRemotePlayer();
            }

            // Update animator for all players
            UpdateAnimator();
        }

        private void FixedUpdate()
        {
            if (!IsSpawned) return;

            if (IsServer)
            {
                // Server: process buffered inputs
                ProcessServerMovement();
            }
        }

        #region Local Player (Owner)

        private void HandleLocalPlayer()
        {
            if (inputService == null) return;

            // Gather input
            var input = GatherInput();

            // Client-side prediction
            ApplyPredictedMovement(input);

            // Send input to server
            SendInputServerRpc(input);

            // Store for reconciliation
            predictionHistory[input.tick] = new PlayerStateSnapshot
            {
                position = predictedPosition,
                rotation = transform.rotation,
                velocity = predictedVelocity,
                tick = input.tick
            };

            currentTick++;

            // Cleanup old history
            CleanupPredictionHistory();
        }

        private PlayerInputPayload GatherInput()
        {
            var input = new PlayerInputPayload
            {
                moveInput = inputService.MoveInput,
                lookInput = inputService.LookInput,
                tick = currentTick
            };

            input.Jump = inputService.JumpPressed;
            input.Crouch = inputService.CrouchPressed;
            input.Sprint = inputService.SprintHeld;
            input.Fire = inputService.FireHeld;
            input.Reload = inputService.ReloadPressed;
            input.Interact = inputService.InteractPressed;
            input.Aim = inputService.AimHeld;

            return input;
        }

        private void ApplyPredictedMovement(PlayerInputPayload input)
        {
            // Calculate movement direction
            Vector3 moveDirection = CalculateMoveDirection(input.moveInput);

            // Determine speed
            float targetSpeed = 0f;
            if (moveDirection.magnitude > 0.1f)
            {
                if (input.Crouch)
                {
                    targetSpeed = crouchSpeed;
                    isCrouching = true;
                    isSprinting = false;
                }
                else if (input.Sprint && input.moveInput.y > 0.5f)
                {
                    targetSpeed = runSpeed;
                    isSprinting = true;
                    isCrouching = false;
                }
                else
                {
                    targetSpeed = walkSpeed;
                    isSprinting = false;
                    isCrouching = false;
                }
            }
            else
            {
                isSprinting = false;
            }

            // Apply horizontal movement
            Vector3 horizontalVelocity = moveDirection * targetSpeed;

            // Apply gravity
            if (controller.isGrounded)
            {
                verticalVelocity = -2f; // Small downward force to keep grounded

                if (input.Jump)
                {
                    verticalVelocity = jumpForce;
                }
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }

            // Combine velocities
            predictedVelocity = new Vector3(horizontalVelocity.x, verticalVelocity, horizontalVelocity.z);

            // Apply movement
            controller.Move(predictedVelocity * Time.deltaTime);
            predictedPosition = transform.position;

            // Apply rotation from look input
            if (input.lookInput.x != 0)
            {
                transform.Rotate(0f, input.lookInput.x * rotationSpeed * Time.deltaTime, 0f);
            }
        }

        private Vector3 CalculateMoveDirection(Vector2 input)
        {
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;

            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            return (forward * input.y + right * input.x).normalized;
        }

        private void ReconcileWithServer(PlayerStateSnapshot serverState)
        {
            if (!predictionHistory.TryGetValue(serverState.tick, out var predictedState))
            {
                return; // No prediction for this tick
            }

            float positionError = Vector3.Distance(serverState.position, predictedState.position);

            if (positionError > snapThreshold)
            {
                // Large error: snap to server position
                transform.position = serverState.position;
                predictedPosition = serverState.position;
                Debug.LogWarning($"[NetworkPlayer] Snapped to server position (error: {positionError:F2}m)");
            }
            else if (positionError > reconciliationThreshold)
            {
                // Small error: replay inputs from server state
                transform.position = serverState.position;
                predictedPosition = serverState.position;

                // Re-simulate from server state
                // (Simplified: in production, replay all inputs since serverState.tick)
                Debug.Log($"[NetworkPlayer] Reconciled (error: {positionError:F2}m)");
            }

            // Cleanup old predictions
            var keysToRemove = new List<uint>();
            foreach (var key in predictionHistory.Keys)
            {
                if (key <= serverState.tick)
                {
                    keysToRemove.Add(key);
                }
            }
            foreach (var key in keysToRemove)
            {
                predictionHistory.Remove(key);
            }
        }

        private void CleanupPredictionHistory()
        {
            // Keep only recent history
            while (predictionHistory.Count > inputBufferSize)
            {
                uint oldestTick = uint.MaxValue;
                foreach (var key in predictionHistory.Keys)
                {
                    if (key < oldestTick) oldestTick = key;
                }
                predictionHistory.Remove(oldestTick);
            }
        }

        #endregion

        #region Remote Player

        private void HandleRemotePlayer()
        {
            // Interpolate position
            transform.position = Vector3.Lerp(transform.position, interpolationTarget, interpolationSpeed * Time.deltaTime);

            // Interpolate rotation
            transform.rotation = Quaternion.Slerp(transform.rotation, rotationTarget, interpolationSpeed * Time.deltaTime);
        }

        private void OnServerPositionChanged(Vector3 oldValue, Vector3 newValue)
        {
            interpolationTarget = newValue;
        }

        private void OnServerRotationChanged(Quaternion oldValue, Quaternion newValue)
        {
            rotationTarget = newValue;
        }

        #endregion

        #region Server Processing

        [ServerRpc]
        private void SendInputServerRpc(PlayerInputPayload input, ServerRpcParams rpcParams = default)
        {
            // Buffer input for processing
            inputBuffer.Enqueue(input);

            // Limit buffer size
            while (inputBuffer.Count > inputBufferSize)
            {
                inputBuffer.Dequeue();
            }
        }

        private void ProcessServerMovement()
        {
            while (inputBuffer.Count > 0)
            {
                var input = inputBuffer.Dequeue();

                // Calculate movement on server
                Vector3 moveDirection = CalculateMoveDirection(input.moveInput);

                float targetSpeed = 0f;
                if (moveDirection.magnitude > 0.1f)
                {
                    if (input.Crouch)
                    {
                        targetSpeed = crouchSpeed;
                    }
                    else if (input.Sprint && input.moveInput.y > 0.5f)
                    {
                        targetSpeed = runSpeed;
                    }
                    else
                    {
                        targetSpeed = walkSpeed;
                    }
                }

                Vector3 horizontalVelocity = moveDirection * targetSpeed;

                // Apply gravity
                if (controller.isGrounded)
                {
                    verticalVelocity = -2f;
                    if (input.Jump)
                    {
                        verticalVelocity = jumpForce;
                    }
                }
                else
                {
                    verticalVelocity += gravity * Time.fixedDeltaTime;
                }

                Vector3 velocity = new Vector3(horizontalVelocity.x, verticalVelocity, horizontalVelocity.z);
                controller.Move(velocity * Time.fixedDeltaTime);

                // Apply rotation
                if (input.lookInput.x != 0)
                {
                    transform.Rotate(0f, input.lookInput.x * rotationSpeed * Time.fixedDeltaTime, 0f);
                }

                // Update network variables
                serverPosition.Value = transform.position;
                serverRotation.Value = transform.rotation;
                serverVelocity.Value = velocity;

                // Update animation state
                var newAnimState = new PlayerAnimState
                {
                    speed = velocity.magnitude / runSpeed,
                    moveX = input.moveInput.x,
                    moveZ = input.moveInput.y,
                    velocityY = verticalVelocity
                };
                newAnimState.IsGrounded = controller.isGrounded;
                newAnimState.IsCrouching = input.Crouch;
                newAnimState.IsSprinting = input.Sprint && input.moveInput.y > 0.5f;
                newAnimState.IsAiming = input.Aim;

                animState.Value = newAnimState;

                // Send state back to owning client for reconciliation
                SendStateToClientClientRpc(new PlayerStateSnapshot
                {
                    position = transform.position,
                    rotation = transform.rotation,
                    velocity = velocity,
                    tick = input.tick
                }, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new[] { OwnerClientId }
                    }
                });
            }
        }

        [ClientRpc]
        private void SendStateToClientClientRpc(PlayerStateSnapshot state, ClientRpcParams rpcParams = default)
        {
            if (IsOwner)
            {
                ReconcileWithServer(state);
            }
        }

        #endregion

        #region Health & Death

        /// <summary>
        /// Server-only: Apply damage to this player.
        /// </summary>
        public void TakeDamage(float damage, ulong attackerId = 0)
        {
            if (!IsServer) return;
            if (networkIsDead.Value) return;

            float newHealth = Mathf.Max(0f, networkHealth.Value - damage);
            networkHealth.Value = newHealth;

            Debug.Log($"[NetworkPlayer] Player {OwnerClientId} took {damage} damage. Health: {newHealth}");

            if (newHealth <= 0f)
            {
                Die(attackerId);
            }
        }

        /// <summary>
        /// Server-only: Heal this player.
        /// </summary>
        public void Heal(float amount)
        {
            if (!IsServer) return;
            if (networkIsDead.Value) return;

            networkHealth.Value = Mathf.Min(100f, networkHealth.Value + amount);
        }

        private void Die(ulong killerId)
        {
            if (!IsServer) return;

            networkIsDead.Value = true;

            // Broadcast death to all clients
            OnPlayerDeathClientRpc(OwnerClientId, killerId);

            Debug.Log($"[NetworkPlayer] Player {OwnerClientId} died (killed by {killerId})");
        }

        [ClientRpc]
        private void OnPlayerDeathClientRpc(ulong victimId, ulong killerId)
        {
            // Play death animation, show kill feed, etc.
            if (animator != null)
            {
                animator.SetTrigger("Death");
            }

            Debug.Log($"[NetworkPlayer] Death broadcast: {victimId} killed by {killerId}");
        }

        /// <summary>
        /// Server-only: Respawn this player.
        /// </summary>
        public void Respawn(Vector3 spawnPosition)
        {
            if (!IsServer) return;

            networkHealth.Value = 100f;
            networkIsDead.Value = false;
            serverPosition.Value = spawnPosition;

            // Teleport
            controller.enabled = false;
            transform.position = spawnPosition;
            controller.enabled = true;

            OnPlayerRespawnClientRpc(spawnPosition);
        }

        [ClientRpc]
        private void OnPlayerRespawnClientRpc(Vector3 position)
        {
            if (IsOwner)
            {
                controller.enabled = false;
                transform.position = position;
                predictedPosition = position;
                controller.enabled = true;
            }

            if (animator != null)
            {
                animator.SetTrigger("Respawn");
            }
        }

        private void OnHealthChanged(float oldValue, float newValue)
        {
            // Update UI, play damage effects, etc.
            Debug.Log($"[NetworkPlayer] Health changed: {oldValue} -> {newValue}");
        }

        private void OnDeathStateChanged(bool oldValue, bool newValue)
        {
            if (newValue && !oldValue)
            {
                // Just died
                if (controller != null)
                {
                    controller.enabled = false;
                }
            }
            else if (!newValue && oldValue)
            {
                // Just respawned
                if (controller != null)
                {
                    controller.enabled = true;
                }
            }
        }

        #endregion

        #region Animation

        private void UpdateAnimator()
        {
            if (animator == null) return;

            PlayerAnimState state;
            if (IsOwner)
            {
                // Local player: use predicted state
                state = new PlayerAnimState
                {
                    speed = predictedVelocity.magnitude / runSpeed,
                    moveX = inputService?.MoveInput.x ?? 0f,
                    moveZ = inputService?.MoveInput.y ?? 0f,
                    velocityY = verticalVelocity
                };
                state.IsGrounded = controller.isGrounded;
                state.IsCrouching = isCrouching;
                state.IsSprinting = isSprinting;
            }
            else
            {
                // Remote player: use synced state
                state = animState.Value;
            }

            animator.SetFloat(SpeedHash, state.speed);
            animator.SetFloat(MoveXHash, state.moveX);
            animator.SetFloat(MoveZHash, state.moveZ);
            animator.SetBool(IsGroundedHash, state.IsGrounded);
            animator.SetBool(IsCrouchingHash, state.IsCrouching);
            animator.SetBool(IsSprintingHash, state.IsSprinting);
        }

        #endregion
    }
}
