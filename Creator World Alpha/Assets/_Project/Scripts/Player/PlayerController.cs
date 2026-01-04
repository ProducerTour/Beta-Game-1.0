using UnityEngine;
using CreatorWorld.Config;
using CreatorWorld.Core;
using CreatorWorld.Interfaces;
using CreatorWorld.Player.Movement;

namespace CreatorWorld.Player
{
    /// <summary>
    /// Player controller - coordinates movement subsystems.
    /// All logic delegated to focused components.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(GroundChecker))]
    [RequireComponent(typeof(MovementHandler))]
    [RequireComponent(typeof(JumpController))]
    [RequireComponent(typeof(CrouchHandler))]
    [RequireComponent(typeof(PlayerStateMachine))]
    public class PlayerController : MonoBehaviour, IMoveable
    {
        [Header("Configuration")]
        [SerializeField] private MovementConfig config;

        [Header("Spawn Settings")]
        [SerializeField] private Vector3 spawnPosition = new Vector3(1024f, 3.236518f, 256f);
        [SerializeField] private bool useSpawnPosition = true;

        // Components
        private CharacterController controller;
        private GroundChecker groundChecker;
        private MovementHandler movementHandler;
        private JumpController jumpController;
        private CrouchHandler crouchHandler;
        private PlayerStateMachine stateMachine;
        private PlayerAnimation playerAnimation;

        // IMoveable implementation
        public Vector3 Velocity => GetTotalVelocity();
        public bool IsGrounded => groundChecker != null && groundChecker.IsGrounded;
        public bool IsMoving => movementHandler != null && movementHandler.IsMoving;
        public float CurrentSpeed => movementHandler != null ? movementHandler.CurrentSpeed : 0f;

        // State properties for animation/other systems
        public bool IsCrouching => crouchHandler != null && crouchHandler.IsCrouching;
        public bool IsSprinting => movementHandler != null && movementHandler.IsSprinting;
        public bool IsJumping => jumpController != null && jumpController.IsJumping;
        public bool IsFalling => jumpController != null && jumpController.IsFalling;
        public bool IsStrafing => movementHandler != null && movementHandler.IsStrafing;
        public float NormalizedSpeed => movementHandler != null ? movementHandler.NormalizedSpeed : 0f;
        public Vector2 MoveInput => ServiceLocator.Get<IInputService>()?.MoveInput ?? Vector2.zero;
        public PlayerState CurrentState => stateMachine != null ? stateMachine.CurrentState : PlayerState.Idle;

        private bool movementEnabled = true;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            groundChecker = GetComponent<GroundChecker>();
            movementHandler = GetComponent<MovementHandler>();
            jumpController = GetComponent<JumpController>();
            crouchHandler = GetComponent<CrouchHandler>();
            stateMachine = GetComponent<PlayerStateMachine>();
            playerAnimation = GetComponent<PlayerAnimation>();

            // Subscribe to events
            if (jumpController != null)
            {
                jumpController.OnJump += OnPlayerJump;
                jumpController.OnLand += OnPlayerLand;
            }
        }

        private void OnDestroy()
        {
            if (jumpController != null)
            {
                jumpController.OnJump -= OnPlayerJump;
                jumpController.OnLand -= OnPlayerLand;
            }
        }

        private void Start()
        {
            // Teleport to spawn position if enabled
            if (useSpawnPosition)
            {
                TeleportToSpawn();
            }

            // Set game to playing state if GameManager exists
            var gameState = ServiceLocator.Get<IGameStateService>();
            if (gameState != null)
            {
                gameState.SetGameState(Interfaces.GameState.Playing);
            }
        }

        /// <summary>
        /// Teleport player to the configured spawn position
        /// </summary>
        public void TeleportToSpawn()
        {
            TeleportTo(spawnPosition);
        }

        /// <summary>
        /// Teleport player to a specific world position
        /// </summary>
        public void TeleportTo(Vector3 position)
        {
            // Disable CharacterController to allow position change
            if (controller != null)
            {
                controller.enabled = false;
                transform.position = position;
                controller.enabled = true;
            }
            else
            {
                transform.position = position;
            }
        }

        private void Update()
        {
            if (!movementEnabled) return;

            // Check game state - only update when playing
            var gameState = ServiceLocator.Get<IGameStateService>();
            if (gameState != null && !gameState.IsPlaying) return;

            // Update all subsystems
            groundChecker.UpdateGroundCheck();
            crouchHandler.UpdateCrouch();
            movementHandler.UpdateMovement();
            jumpController.UpdateJump();
            stateMachine.UpdateState();

            // Apply final movement
            ApplyMovement();
            ApplyRotation();
        }

        private void ApplyMovement()
        {
            // Combine horizontal and vertical movement
            Vector3 horizontal = movementHandler.GetHorizontalMovement();
            Vector3 vertical = jumpController.GetVerticalMovement();
            Vector3 slopeSlide = movementHandler.GetSlopeSlideMovement();

            Vector3 finalMove = horizontal + vertical + slopeSlide;
            controller.Move(finalMove * Time.deltaTime);
        }

        private void ApplyRotation()
        {
            if (!movementHandler.IsMoving) return;

            // Only prevent rotation when both strafing AND aiming
            // Otherwise, always rotate to face movement direction
            var input = ServiceLocator.Get<IInputService>();
            bool isAiming = input?.AimHeld ?? false;
            bool shouldStrafe = movementHandler.IsStrafing && isAiming;

            if (!shouldStrafe)
            {
                Quaternion targetRotation = movementHandler.GetTargetRotation();
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    Time.deltaTime * config.RotationSpeed
                );
            }
        }

        private Vector3 GetTotalVelocity()
        {
            Vector3 horizontal = movementHandler != null
                ? movementHandler.GetHorizontalMovement()
                : Vector3.zero;

            float vertical = jumpController != null
                ? jumpController.VerticalVelocity
                : 0f;

            return new Vector3(horizontal.x, vertical, horizontal.z);
        }

        // Event handlers
        private void OnPlayerJump()
        {
            playerAnimation?.TriggerJump();

            // Cancel crouch when jumping
            if (crouchHandler != null && crouchHandler.IsCrouching)
            {
                crouchHandler.ForceCrouch(false);
            }
        }

        private void OnPlayerLand()
        {
            playerAnimation?.OnLanded();
        }

        // IMoveable
        public void SetMovementEnabled(bool enabled)
        {
            movementEnabled = enabled;
        }

        private void OnDrawGizmosSelected()
        {
            // Gizmos handled by GroundChecker
        }
    }
}
