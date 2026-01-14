using Unity.Netcode;
using UnityEngine;

namespace CreatorWorld.Network
{
    /// <summary>
    /// Simplified networked player for initial testing.
    /// Uses owner-authoritative movement with position sync.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class NetworkPlayer : NetworkBehaviour
    {
        [Header("Movement Configuration")]
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float runSpeed = 7f;
        [SerializeField] private float jumpForce = 8f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float rotationSpeed = 100f;

        [Header("Network Settings")]
        [SerializeField] private float interpolationSpeed = 15f;

        // Position sync (owner writes, others read)
        private NetworkVariable<Vector3> syncedPosition = new NetworkVariable<Vector3>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private NetworkVariable<Quaternion> syncedRotation = new NetworkVariable<Quaternion>(
            Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private NetworkVariable<float> syncedSpeed = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // Components
        private CharacterController controller;
        private Animator animator;

        // Local state
        private float verticalVelocity;
        private Vector3 interpolationTarget;
        private Quaternion rotationTarget;

        // Animation hashes
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            controller = GetComponent<CharacterController>();
            animator = GetComponentInChildren<Animator>();

            // Spawn players at offset positions so they don't overlap
            Vector3 spawnOffset = GetSpawnOffset();

            if (IsOwner)
            {
                // Teleport to spawn position
                controller.enabled = false;
                transform.position = spawnOffset;
                controller.enabled = true;

                // Initialize synced position
                syncedPosition.Value = transform.position;
                syncedRotation.Value = transform.rotation;

                Debug.Log($"[NetworkPlayer] LOCAL player spawned at {transform.position} (ClientId: {OwnerClientId})");
            }
            else
            {
                // Remote player - start at their synced position or spawn offset
                interpolationTarget = syncedPosition.Value != Vector3.zero ? syncedPosition.Value : spawnOffset;
                rotationTarget = syncedRotation.Value;
                transform.position = interpolationTarget;
                transform.rotation = rotationTarget;

                // Subscribe to position changes
                syncedPosition.OnValueChanged += OnPositionChanged;
                syncedRotation.OnValueChanged += OnRotationChanged;

                Debug.Log($"[NetworkPlayer] REMOTE player spawned at {transform.position} (ClientId: {OwnerClientId})");
            }
        }

        private Vector3 GetSpawnOffset()
        {
            // Spawn players in a circle around origin
            float angle = OwnerClientId * 60f * Mathf.Deg2Rad; // 60 degrees apart
            float radius = 3f;
            return new Vector3(
                Mathf.Cos(angle) * radius,
                1f, // Slightly above ground
                Mathf.Sin(angle) * radius
            );
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (!IsOwner)
            {
                syncedPosition.OnValueChanged -= OnPositionChanged;
                syncedRotation.OnValueChanged -= OnRotationChanged;
            }
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsOwner)
            {
                HandleLocalMovement();
            }
            else
            {
                HandleRemoteInterpolation();
            }

            UpdateAnimator();
        }

        private void HandleLocalMovement()
        {
            // Get input
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            bool jump = Input.GetButtonDown("Jump");
            bool sprint = Input.GetKey(KeyCode.LeftShift);

            // Calculate movement direction
            Vector3 moveDirection = (transform.forward * vertical + transform.right * horizontal).normalized;

            // Determine speed
            float currentSpeed = 0f;
            if (moveDirection.magnitude > 0.1f)
            {
                currentSpeed = sprint ? runSpeed : walkSpeed;
            }

            // Apply horizontal movement
            Vector3 horizontalVelocity = moveDirection * currentSpeed;

            // Apply gravity
            if (controller.isGrounded)
            {
                verticalVelocity = -2f;
                if (jump)
                {
                    verticalVelocity = jumpForce;
                }
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }

            // Combine and move
            Vector3 velocity = new Vector3(horizontalVelocity.x, verticalVelocity, horizontalVelocity.z);
            controller.Move(velocity * Time.deltaTime);

            // Mouse rotation
            float mouseX = Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
            transform.Rotate(0f, mouseX, 0f);

            // Sync position to network
            syncedPosition.Value = transform.position;
            syncedRotation.Value = transform.rotation;
            syncedSpeed.Value = currentSpeed / runSpeed;
        }

        private void HandleRemoteInterpolation()
        {
            // Smoothly interpolate to target position
            transform.position = Vector3.Lerp(transform.position, interpolationTarget, interpolationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotationTarget, interpolationSpeed * Time.deltaTime);
        }

        private void OnPositionChanged(Vector3 oldValue, Vector3 newValue)
        {
            interpolationTarget = newValue;
        }

        private void OnRotationChanged(Quaternion oldValue, Quaternion newValue)
        {
            rotationTarget = newValue;
        }

        private void UpdateAnimator()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;

            float speed = IsOwner ? syncedSpeed.Value : syncedSpeed.Value;
            animator.SetFloat(SpeedHash, speed);

            if (IsOwner && controller != null)
            {
                animator.SetBool(IsGroundedHash, controller.isGrounded);
            }
        }
    }
}
