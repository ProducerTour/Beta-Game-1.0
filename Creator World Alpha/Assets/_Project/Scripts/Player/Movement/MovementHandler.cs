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
        /// Speed normalized to animator blend tree thresholds:
        /// 0 = idle, 0.35 = walk, 0.7 = run, 1.0 = sprint
        /// </summary>
        public float NormalizedSpeed
        {
            get
            {
                if (config == null) return 0f;

                // Map speed to animator thresholds
                if (currentSpeed < 0.1f) return 0f;

                if (currentSpeed <= config.WalkSpeed)
                {
                    // 0 to WalkSpeed maps to 0 to 0.35
                    return Mathf.Lerp(0f, 0.35f, currentSpeed / config.WalkSpeed);
                }
                else if (currentSpeed <= config.RunSpeed)
                {
                    // WalkSpeed to RunSpeed maps to 0.35 to 0.7
                    float t = (currentSpeed - config.WalkSpeed) / (config.RunSpeed - config.WalkSpeed);
                    return Mathf.Lerp(0.35f, 0.7f, t);
                }
                else
                {
                    // RunSpeed to SprintSpeed maps to 0.7 to 1.0
                    float t = (currentSpeed - config.RunSpeed) / (config.SprintSpeed - config.RunSpeed);
                    return Mathf.Lerp(0.7f, 1.0f, t);
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
            if (input == null)
            {
                input = ServiceLocator.Get<IInputService>();
                if (input == null) return;
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

            // Determine speed based on state
            if (crouchHandler != null && crouchHandler.IsCrouching)
            {
                targetSpeed = config.CrouchSpeed;
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

            // Pure strafing uses strafe speed
            if (IsStrafing)
            {
                targetSpeed = Mathf.Min(targetSpeed, config.StrafeSpeed);
            }
        }

        private void ApplySpeedChange()
        {
            if (config == null) return;

            // Reduce control in air
            float controlMultiplier = groundChecker != null && groundChecker.IsGrounded
                ? 1f
                : config.AirControlMultiplier;

            float accel = (input.MoveInput.magnitude > config.MoveDeadzone
                ? config.Acceleration
                : config.Deceleration) * controlMultiplier;

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
            if (moveDirection.magnitude > 0.1f && !IsStrafing)
            {
                return Quaternion.LookRotation(moveDirection);
            }
            return transform.rotation;
        }
    }
}
