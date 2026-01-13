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
    [RequireComponent(typeof(SlideHandler))]
    [RequireComponent(typeof(VaultHandler))]
    [RequireComponent(typeof(PlayerStateMachine))]
    public class PlayerController : MonoBehaviour, IMoveable
    {
        [Header("Configuration")]
        [SerializeField] private MovementConfig config;

        [Header("Spawn Settings")]
        [SerializeField] private Vector3 spawnPosition = new Vector3(1024f, 3.236518f, 256f);
        [SerializeField] private float spawnYRotation = 0f;
        [SerializeField] private bool useSpawnPosition = true;

        [Header("Stance")]
        [Tooltip("Character offset angle when armed (typical FPS: 15-25 degrees right)")]
        [SerializeField] private float armedStanceOffset = 20f;

        // Components
        private CharacterController controller;
        private GroundChecker groundChecker;
        private MovementHandler movementHandler;
        private JumpController jumpController;
        private CrouchHandler crouchHandler;
        private SlideHandler slideHandler;
        private VaultHandler vaultHandler;
        private PlayerStateMachine stateMachine;
        private PlayerAnimation playerAnimation;
        private ICameraService cameraService;

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
        public bool IsSliding => slideHandler != null && slideHandler.IsSliding;
        public bool IsVaulting => vaultHandler != null && vaultHandler.IsVaulting;
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
            slideHandler = GetComponent<SlideHandler>();
            vaultHandler = GetComponent<VaultHandler>();
            stateMachine = GetComponent<PlayerStateMachine>();
            playerAnimation = GetComponent<PlayerAnimation>();

            // Subscribe to events
            if (jumpController != null)
            {
                jumpController.OnJump += OnPlayerJump;
                jumpController.OnLand += OnPlayerLand;
            }
            if (slideHandler != null)
            {
                slideHandler.OnSlideStart += OnPlayerSlideStart;
                slideHandler.OnSlideEnd += OnPlayerSlideEnd;
            }
            if (vaultHandler != null)
            {
                vaultHandler.OnVaultStart += OnPlayerVaultStart;
                vaultHandler.OnVaultEnd += OnPlayerVaultEnd;
            }
        }

        private void OnDestroy()
        {
            if (jumpController != null)
            {
                jumpController.OnJump -= OnPlayerJump;
                jumpController.OnLand -= OnPlayerLand;
            }
            if (slideHandler != null)
            {
                slideHandler.OnSlideStart -= OnPlayerSlideStart;
                slideHandler.OnSlideEnd -= OnPlayerSlideEnd;
            }
            if (vaultHandler != null)
            {
                vaultHandler.OnVaultStart -= OnPlayerVaultStart;
                vaultHandler.OnVaultEnd -= OnPlayerVaultEnd;
            }
        }

        private void Start()
        {
            // Get camera service for aim rotation
            cameraService = ServiceLocator.Get<ICameraService>();

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
            transform.rotation = Quaternion.Euler(0f, spawnYRotation, 0f);
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

            // Reset spawn safety so player doesn't fall through unloaded terrain
            if (jumpController != null)
            {
                jumpController.ResetSpawnSafety();
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

            // Vault/Slide must update before jump to potentially consume the input
            // Priority: Vault > Slide > Jump (vault has highest priority for spacebar)
            bool vaultConsumedInput = vaultHandler != null && vaultHandler.UpdateVault();
            bool slideConsumedInput = !vaultConsumedInput && slideHandler != null && slideHandler.UpdateSlide();
            if (!vaultConsumedInput && !slideConsumedInput && !IsSliding && !IsVaulting)
            {
                jumpController.UpdateJump();
            }

            stateMachine.UpdateState();

            // Skip movement during vault (CharacterController is disabled, vault handles position)
            if (IsVaulting) return;

            // Apply final movement
            ApplyMovement();
            ApplyRotation();
        }

        private void ApplyMovement()
        {
            Vector3 horizontal;

            // Use slide movement when sliding, otherwise normal movement
            if (IsSliding && slideHandler != null)
            {
                horizontal = slideHandler.GetSlideMovement();
            }
            else
            {
                horizontal = movementHandler.GetHorizontalMovement();
            }

            Vector3 vertical = jumpController.GetVerticalMovement();
            Vector3 slopeSlide = movementHandler.GetSlopeSlideMovement();

            Vector3 finalMove = horizontal + vertical + slopeSlide;
            controller.Move(finalMove * Time.deltaTime);
        }

        private void ApplyRotation()
        {
            // Try to get camera service if not yet available
            if (cameraService == null)
            {
                cameraService = ServiceLocator.Get<ICameraService>();
            }

            Quaternion targetRotation;

            // When aiming: face camera direction (strafe mode)
            // When not aiming: face movement direction (turn-to-face mode)
            bool isAiming = cameraService != null && cameraService.IsAiming;

            // Check if armed for stance offset
            bool isArmed = playerAnimation != null &&
                           playerAnimation.GetComponent<Animator>().GetBool("HasRifle");

            if (isAiming)
            {
                // Face camera direction for aiming/shooting
                float targetYaw = cameraService.Yaw;
                targetRotation = Quaternion.Euler(0, targetYaw, 0);
            }
            else if (movementHandler != null && movementHandler.IsMoving)
            {
                // Face movement direction when walking around
                targetRotation = movementHandler.GetTargetRotation();
            }
            else
            {
                // Not moving - keep current rotation
                return;
            }

            // Apply armed stance offset (character angled right, left shoulder forward)
            if (isArmed)
            {
                targetRotation *= Quaternion.Euler(0, armedStanceOffset, 0);
            }

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.deltaTime * config.RotationSpeed
            );
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
            Debug.Log($"[PlayerController] OnPlayerJump called - playerAnimation is null: {playerAnimation == null}");
            if (playerAnimation != null)
            {
                playerAnimation.TriggerJump();
            }
            else
            {
                Debug.LogError("[PlayerController] playerAnimation is NULL! Cannot trigger jump animation.");
            }

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

        private void OnPlayerSlideStart()
        {
            playerAnimation?.TriggerSlide();

            // Cancel crouch when sliding
            if (crouchHandler != null && crouchHandler.IsCrouching)
            {
                crouchHandler.ForceCrouch(false);
            }
        }

        private void OnPlayerSlideEnd()
        {
            // Could trigger slide end animation here if needed
        }

        private void OnPlayerVaultStart()
        {
            playerAnimation?.TriggerVault();
        }

        private void OnPlayerVaultEnd()
        {
            // Could trigger vault end effects here if needed
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
