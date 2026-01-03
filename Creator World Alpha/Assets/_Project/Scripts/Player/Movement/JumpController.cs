using UnityEngine;
using CreatorWorld.Config;
using CreatorWorld.Core;
using CreatorWorld.Interfaces;

namespace CreatorWorld.Player.Movement
{
    /// <summary>
    /// Handles jumping, gravity, and vertical movement.
    /// </summary>
    public class JumpController : MonoBehaviour
    {
        [SerializeField] private MovementConfig config;

        private GroundChecker groundChecker;
        private IInputService input;

        // State
        private float verticalVelocity;
        private float jumpBufferTimer;
        private bool jumpConsumed;
        private bool isJumping;
        private bool isFalling;

        // Properties
        public float VerticalVelocity => verticalVelocity;
        public bool IsJumping => isJumping;
        public bool IsFalling => isFalling;
        public bool IsAirborne => groundChecker != null && !groundChecker.IsGrounded;

        // Events
        public event System.Action OnJump;
        public event System.Action OnLand;

        private void Awake()
        {
            groundChecker = GetComponent<GroundChecker>();
            if (groundChecker == null)
            {
                groundChecker = GetComponentInParent<GroundChecker>();
            }
        }

        private void Start()
        {
            input = ServiceLocator.Get<IInputService>();
        }

        /// <summary>
        /// Call this every frame from PlayerController.
        /// </summary>
        public void UpdateJump()
        {
            if (input == null)
            {
                input = ServiceLocator.Get<IInputService>();
                if (input == null) return;
            }

            HandleJumpBuffer();
            HandleJumpInput();
            ApplyGravity();
            UpdateAirState();
            HandleLanding();
        }

        private void HandleJumpBuffer()
        {
            if (config == null) return;

            // Buffer jump input
            if (input.JumpPressed)
            {
                jumpBufferTimer = config.JumpBufferTime;
                Debug.Log(">>> JUMP BUFFERED <<<");
            }

            if (jumpBufferTimer > 0)
            {
                jumpBufferTimer -= Time.deltaTime;
            }
        }

        private void HandleJumpInput()
        {
            if (groundChecker == null) return;

            // Check if we can jump (grounded OR within coyote time)
            bool canJump = groundChecker.IsGrounded ||
                          (groundChecker.WithinCoyoteTime && !jumpConsumed);

            // Execute jump if buffered and can jump
            if (jumpBufferTimer > 0 && canJump)
            {
                ExecuteJump();
            }

            // Reset jump consumed when grounded
            if (groundChecker.IsGrounded)
            {
                jumpConsumed = false;
            }
        }

        private void ExecuteJump()
        {
            if (config == null) return;

            // Apply jump force directly as velocity (Creator World style)
            verticalVelocity = config.JumpForce;
            jumpBufferTimer = 0;
            jumpConsumed = true;
            isJumping = true;
            isFalling = false;

            OnJump?.Invoke();
        }

        private void ApplyGravity()
        {
            if (config == null || groundChecker == null) return;

            if (groundChecker.IsGrounded && verticalVelocity < 0)
            {
                // Small negative to keep grounded
                verticalVelocity = -2f;
                return;
            }

            // Apply gravity (Creator World style - consistent gravity)
            float gravityMultiplier = 1f;

            // Slight extra gravity when falling for snappier feel
            if (verticalVelocity < 0)
            {
                gravityMultiplier = config.FallMultiplier;
            }
            // Variable jump height - faster fall if jump released early
            else if (verticalVelocity > 0 && !input.JumpHeld)
            {
                gravityMultiplier = config.LowJumpMultiplier;
            }

            verticalVelocity += config.Gravity * gravityMultiplier * Time.deltaTime;

            // Terminal velocity
            verticalVelocity = Mathf.Max(verticalVelocity, -config.TerminalVelocity);
        }

        private void UpdateAirState()
        {
            if (groundChecker == null) return;

            if (groundChecker.IsGrounded)
            {
                // Reset air states when grounded
                isJumping = false;
                isFalling = false;
            }
            else
            {
                if (verticalVelocity > 0)
                {
                    isJumping = true;
                    isFalling = false;
                }
                else
                {
                    isJumping = false;
                    isFalling = true;
                }
            }
        }

        private void HandleLanding()
        {
            if (groundChecker == null) return;

            if (groundChecker.JustLanded)
            {
                isJumping = false;
                isFalling = false;
                OnLand?.Invoke();
            }
        }

        /// <summary>
        /// Get the vertical velocity component for movement.
        /// </summary>
        public Vector3 GetVerticalMovement()
        {
            return new Vector3(0, verticalVelocity, 0);
        }

        /// <summary>
        /// Force set vertical velocity (e.g., for knockback).
        /// </summary>
        public void SetVerticalVelocity(float velocity)
        {
            verticalVelocity = velocity;
        }

        /// <summary>
        /// Add to vertical velocity (e.g., for bounce pads).
        /// </summary>
        public void AddVerticalVelocity(float amount)
        {
            verticalVelocity += amount;
        }
    }
}
