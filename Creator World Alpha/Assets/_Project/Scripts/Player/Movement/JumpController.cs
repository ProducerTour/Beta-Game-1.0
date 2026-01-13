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
        private float timeSinceJump; // Track time since last jump to prevent instant re-jump
        private bool isJumping;
        private bool isFalling;
        private bool hasEverBeenGrounded; // Spawn safety: don't apply gravity until first grounded

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

            // Track time since jump
            timeSinceJump += Time.deltaTime;

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
            }

            if (jumpBufferTimer > 0)
            {
                jumpBufferTimer -= Time.deltaTime;
            }
        }

        private void HandleJumpInput()
        {
            if (groundChecker == null || config == null) return;

            // Can jump when:
            // 1. Grounded
            // 2. Enough time has passed since last jump (prevents bunny hopping exploit)
            bool canJump = groundChecker.IsGrounded && timeSinceJump > 0.1f;

            // Execute jump if buffered and can jump
            if (jumpBufferTimer > 0 && canJump)
            {
                ExecuteJump();
            }
        }

        private void ExecuteJump()
        {
            if (config == null) return;

            // Apply jump force directly as velocity
            verticalVelocity = config.JumpForce;
            jumpBufferTimer = 0;
            timeSinceJump = 0; // Reset jump timer
            isJumping = true;
            isFalling = false;

            Debug.Log($"[JumpController] ExecuteJump - verticalVelocity: {verticalVelocity}, OnJump subscribers: {OnJump?.GetInvocationList()?.Length ?? 0}");
            OnJump?.Invoke();
        }

        private void ApplyGravity()
        {
            if (config == null || groundChecker == null) return;

            // SPAWN SAFETY: Don't apply gravity until we've been grounded at least once
            // This prevents falling through the world while waiting for terrain to load
            if (!hasEverBeenGrounded)
            {
                if (groundChecker.IsGrounded)
                {
                    hasEverBeenGrounded = true;
                }
                else
                {
                    // Hold position while waiting for ground
                    verticalVelocity = 0f;
                    return;
                }
            }

            // When grounded and not ascending, apply small downward force to stick to ground
            if (groundChecker.IsGrounded && verticalVelocity <= 0)
            {
                verticalVelocity = -2f;
                return;
            }

            // Apply gravity when airborne
            float gravityMultiplier = 1f;

            // FALLING: Extra gravity when falling for snappy, impactful landings
            if (verticalVelocity < 0)
            {
                gravityMultiplier = config.FallMultiplier;
            }
            // LOW JUMP: Faster fall if jump released early (variable jump height)
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

            // SPAWN SAFETY: Don't report airborne state while waiting for ground
            // This prevents animator from entering jump/fall states during spawn
            if (!hasEverBeenGrounded)
            {
                isJumping = false;
                isFalling = false;
                return;
            }

            if (groundChecker.IsGrounded && verticalVelocity <= 0)
            {
                // Truly grounded and not ascending
                isJumping = false;
                isFalling = false;
            }
            else
            {
                // Airborne
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

        /// <summary>
        /// Reset spawn safety. Call this after teleporting to prevent falling through unloaded terrain.
        /// </summary>
        public void ResetSpawnSafety()
        {
            hasEverBeenGrounded = false;
            verticalVelocity = 0f;
        }
    }
}
