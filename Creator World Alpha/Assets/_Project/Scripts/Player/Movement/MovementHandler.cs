using UnityEngine;
using CreatorWorld.Config;
using CreatorWorld.Core;
using CreatorWorld.Interfaces;

namespace CreatorWorld.Player.Movement
{
    /// <summary>
    /// Handles horizontal movement, speed calculation, and direction.
    /// </summary>
    public class MovementHandler : MonoBehaviour
    {
        [SerializeField] private MovementConfig config;
        [SerializeField] private BlendTreeConfig blendConfig;

        private GroundChecker groundChecker;
        private CrouchHandler crouchHandler;
        private IInputService input;
        private ICameraService cameraService;
        private Transform cachedCamera;

        // State
        private Vector3 moveDirection;
        private float currentSpeed;
        private float targetSpeed;
        private bool isSprinting;

        // Properties
        public Vector3 MoveDirection => moveDirection;
        public float CurrentSpeed => currentSpeed;

        /// <summary>
        /// Speed normalized to animator blend tree thresholds.
        /// Uses BlendTreeConfig for threshold values, or defaults if not assigned.
        /// </summary>
        public float NormalizedSpeed
        {
            get
            {
                if (config == null) return 0f;

                // When crouching, use separate normalization (0 to 1 range for crouch blend tree)
                if (crouchHandler != null && crouchHandler.IsCrouching)
                {
                    if (currentSpeed < 0.1f) return 0f;
                    // Map 0 -> CrouchSpeed to 0 -> 1 for crouch blend tree
                    return Mathf.Clamp01(currentSpeed / config.CrouchSpeed);
                }

                // Use BlendTreeConfig if available, otherwise use hardcoded defaults
                if (blendConfig != null)
                {
                    return blendConfig.GetNormalizedSpeed(
                        currentSpeed,
                        config.WalkSpeed,
                        config.RunSpeed,
                        config.SprintSpeed
                    );
                }

                // Fallback: Map speed to blend tree positions
                // 0 = idle (0,0), 0.5 = walk (0,0.5), 1.0 = run/sprint (0,1)
                // These MUST match the blend tree Y positions in PlayerAnimator.controller
                if (currentSpeed < 0.1f) return 0f;

                if (currentSpeed <= config.WalkSpeed)
                {
                    // 0 to WalkSpeed → 0 to 0.5 (idle to walk position)
                    return Mathf.Lerp(0f, 0.5f, currentSpeed / config.WalkSpeed);
                }
                else if (currentSpeed <= config.RunSpeed)
                {
                    // WalkSpeed to RunSpeed → 0.5 to 1.0 (walk to run position)
                    float t = (currentSpeed - config.WalkSpeed) / (config.RunSpeed - config.WalkSpeed);
                    return Mathf.Lerp(0.5f, 1.0f, t);
                }
                else
                {
                    // Beyond RunSpeed (sprint) → cap at 1.0 (run position, no separate sprint animation)
                    return 1.0f;
                }
            }
        }

        public bool IsSprinting => isSprinting;
        public bool IsMoving => config != null && input != null && input.MoveInput.magnitude > config.MoveDeadzone;
        public bool IsStrafing => config != null && input != null &&
                                  Mathf.Abs(input.MoveInput.x) > config.StrafeThreshold &&
                                  Mathf.Abs(input.MoveInput.y) < config.StrafeThreshold;

        private void Awake()
        {
            groundChecker = GetComponent<GroundChecker>();
            crouchHandler = GetComponent<CrouchHandler>();

            if (groundChecker == null) groundChecker = GetComponentInParent<GroundChecker>();
            if (crouchHandler == null) crouchHandler = GetComponentInParent<CrouchHandler>();
        }

        private void Start()
        {
            input = ServiceLocator.Get<IInputService>();
            cameraService = ServiceLocator.Get<ICameraService>();

            // Fallback to Camera.main if no service
            if (cameraService == null && Camera.main != null)
            {
                cachedCamera = Camera.main.transform;
            }
        }

        /// <summary>
        /// Call this every frame from PlayerController.
        /// </summary>
        public void UpdateMovement()
        {
            // Lazy load input service
            if (input == null)
            {
                input = ServiceLocator.Get<IInputService>();
                if (input == null) return;
            }

            // Lazy load camera service (may not be registered on first frame)
            if (cameraService == null)
            {
                cameraService = ServiceLocator.Get<ICameraService>();
            }

            CalculateTargetSpeed();
            ApplySpeedChange();
            CalculateMoveDirection();
        }

        private void CalculateTargetSpeed()
        {
            if (config == null) return;

            Vector2 moveInput = input.MoveInput;

            if (moveInput.magnitude < config.MoveDeadzone)
            {
                targetSpeed = 0f;
                isSprinting = false;
                return;
            }

            // Check if aiming or firing - force walk speed when ADS or hip-firing
            bool isAiming = cameraService != null && cameraService.IsAiming;
            bool isFiring = input.FireHeld;

            // Determine speed based on state
            if (crouchHandler != null && crouchHandler.IsCrouching)
            {
                targetSpeed = config.CrouchSpeed;
                isSprinting = false;
            }
            else if (isAiming)
            {
                // Aiming forces slower walk speed - no sprinting or running while ADS
                targetSpeed = config.AimWalkSpeed;
                isSprinting = false;
            }
            else if (isFiring)
            {
                // Hip-firing forces slower walk speed - can't sprint while shooting
                targetSpeed = config.HipFireWalkSpeed;
                isSprinting = false;
            }
            else if (input.SprintHeld && moveInput.y > config.SprintForwardThreshold)
            {
                // Can only sprint when moving forward
                targetSpeed = config.SprintSpeed;
                isSprinting = true;
            }
            else if (input.SprintHeld)
            {
                // Shift held but not moving forward enough = run
                targetSpeed = config.RunSpeed;
                isSprinting = false;
            }
            else
            {
                targetSpeed = config.WalkSpeed;
                isSprinting = false;
            }

            // Pure strafing uses strafe speed - only when aiming (unarmed can run any direction)
            if (IsStrafing && isAiming)
            {
                targetSpeed = Mathf.Min(targetSpeed, config.StrafeSpeed);
            }
        }

        private void ApplySpeedChange()
        {
            if (config == null) return;

            // When aiming or hip-firing, instantly cap speed (no gradual slowdown)
            bool isAiming = cameraService != null && cameraService.IsAiming;
            bool isFiring = input.FireHeld;

            if (isAiming && currentSpeed > config.AimWalkSpeed)
            {
                currentSpeed = config.AimWalkSpeed;
            }
            else if (isFiring && currentSpeed > config.HipFireWalkSpeed)
            {
                currentSpeed = config.HipFireWalkSpeed;
            }

            // Reduce control in air
            float controlMultiplier = groundChecker != null && groundChecker.IsGrounded
                ? 1f
                : config.AirControlMultiplier;

            // Determine whether to accelerate or decelerate
            float accel;
            if (input.MoveInput.magnitude < config.MoveDeadzone)
            {
                // No input - decelerate to stop
                accel = config.Deceleration;
            }
            else if (targetSpeed < currentSpeed)
            {
                // Moving but slowing down (e.g., from run to walk when aiming)
                accel = config.Deceleration;
            }
            else
            {
                // Speeding up
                accel = config.Acceleration;
            }

            accel *= controlMultiplier;
            currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * accel);
        }

        private void CalculateMoveDirection()
        {
            if (config == null) return;

            Vector2 moveInput = input.MoveInput;

            if (moveInput.magnitude < config.MoveDeadzone)
            {
                moveDirection = Vector3.zero;
                return;
            }

            // Get camera-relative directions
            Vector3 forward, right;

            if (cameraService != null)
            {
                forward = cameraService.Forward;
                right = cameraService.Right;
            }
            else if (cachedCamera != null)
            {
                forward = cachedCamera.forward;
                right = cachedCamera.right;
            }
            else if (Camera.main != null)
            {
                cachedCamera = Camera.main.transform;
                forward = cachedCamera.forward;
                right = cachedCamera.right;
            }
            else
            {
                forward = transform.forward;
                right = transform.right;
            }

            // Flatten to horizontal plane
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            moveDirection = (forward * moveInput.y + right * moveInput.x).normalized;
        }

        /// <summary>
        /// Get the final horizontal movement vector (direction * speed).
        /// </summary>
        public Vector3 GetHorizontalMovement()
        {
            Vector3 movement = moveDirection * currentSpeed;

            // Clamp to max velocity (Creator World style)
            if (config != null)
            {
                float speed = movement.magnitude;
                if (speed > config.MaxHorizontalVelocity)
                {
                    movement = movement.normalized * config.MaxHorizontalVelocity;
                }
            }

            // Adjust for slopes if grounded
            if (groundChecker != null && groundChecker.IsGrounded && groundChecker.IsOnSlope)
            {
                movement = groundChecker.GetSlopeAdjustedDirection(movement);
            }

            return movement;
        }

        /// <summary>
        /// Get slope slide movement for steep slopes.
        /// </summary>
        public Vector3 GetSlopeSlideMovement()
        {
            if (config != null && groundChecker != null && groundChecker.IsOnSteepSlope && config.SlideDownSlopes)
            {
                return groundChecker.GetSlopeSlideDirection() * config.SlopeSlideSpeed;
            }
            return Vector3.zero;
        }

        /// <summary>
        /// Get the rotation the player should face based on movement.
        /// </summary>
        public Quaternion GetTargetRotation()
        {
            if (moveDirection.magnitude > 0.1f)
            {
                return Quaternion.LookRotation(moveDirection);
            }
            return transform.rotation;
        }
    }
}
