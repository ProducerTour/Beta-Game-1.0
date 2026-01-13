using UnityEngine;
using CreatorWorld.Config;
using CreatorWorld.Core;
using CreatorWorld.Interfaces;

namespace CreatorWorld.Player.Movement
{
    /// <summary>
    /// Handles running slide mechanic triggered by X key while moving fast (run/sprint speed).
    /// Requires speed >= MinSlideSpeed (default 3) to trigger.
    /// </summary>
    public class SlideHandler : MonoBehaviour
    {
        [SerializeField] private MovementConfig config;

        private CharacterController controller;
        private GroundChecker groundChecker;
        private MovementHandler movementHandler;
        private CrouchHandler crouchHandler;
        private IInputService input;


        // Slide state
        private bool isSliding;
        private float slideTimer;
        private float slideCooldownTimer;
        private Vector3 slideDirection;
        private float slideSpeed;
        private float currentHeight;
        private float targetHeight;

        // Properties
        public bool IsSliding => isSliding;
        public float SlideSpeed => slideSpeed;
        public Vector3 SlideDirection => slideDirection;

        // Events
        public event System.Action OnSlideStart;
        public event System.Action OnSlideEnd;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            groundChecker = GetComponent<GroundChecker>();
            movementHandler = GetComponent<MovementHandler>();
            crouchHandler = GetComponent<CrouchHandler>();

            if (controller == null) controller = GetComponentInParent<CharacterController>();
            if (groundChecker == null) groundChecker = GetComponentInParent<GroundChecker>();
            if (movementHandler == null) movementHandler = GetComponentInParent<MovementHandler>();
            if (crouchHandler == null) crouchHandler = GetComponentInParent<CrouchHandler>();

            currentHeight = config != null ? config.StandingHeight : 2f;
            targetHeight = currentHeight;
        }

        private void Start()
        {
            input = ServiceLocator.Get<IInputService>();
        }

        /// <summary>
        /// Call this every frame from PlayerController.
        /// Returns true if slide consumed the jump input (prevents normal jump).
        /// </summary>
        public bool UpdateSlide()
        {
            if (input == null)
            {
                input = ServiceLocator.Get<IInputService>();
                if (input == null) return false;
            }

            // Update cooldown
            if (slideCooldownTimer > 0)
            {
                slideCooldownTimer -= Time.deltaTime;
            }

            // Handle Ctrl+Sprint slide input
            bool consumedInput = HandleSlideInput();

            // Update active slide
            if (isSliding)
            {
                UpdateActiveSlide();
            }

            // Apply height transition
            ApplyHeightTransition();

            return consumedInput;
        }

        private bool HandleSlideInput()
        {
            // Debug: Check input service
            if (input == null)
            {
                Debug.Log("[SlideHandler] input service is NULL!");
                return false;
            }

            // Debug: Check if X was pressed (before any returns)
            if (input.SlidePressed)
            {
                Debug.Log($"[SlideHandler] X pressed! config={config != null}, isSliding={isSliding}, cooldown={slideCooldownTimer:F1}, grounded={groundChecker?.IsGrounded}, crouching={crouchHandler?.IsCrouching}");
            }

            if (config == null)
            {
                if (input.SlidePressed) Debug.Log("[SlideHandler] config is NULL!");
                return false;
            }

            // Don't detect new slide if already sliding or on cooldown
            if (isSliding || slideCooldownTimer > 0) return false;

            // Don't slide if not grounded
            if (groundChecker != null && !groundChecker.IsGrounded) return false;

            // Don't slide if already crouching
            if (crouchHandler != null && crouchHandler.IsCrouching) return false;

            // Slide triggers on X key while running fast enough
            if (input.SlidePressed && CanInitiateSlide())
            {
                StartSlide();
                Debug.Log("[SlideHandler] Slide triggered!");
                return true; // Consume the slide input
            }

            return false;
        }

        private bool CanInitiateSlide()
        {
            if (config == null || movementHandler == null)
            {
                Debug.Log($"[SlideHandler] Can't slide: config={config != null}, movementHandler={movementHandler != null}");
                return false;
            }

            // Need to be moving fast enough
            float currentSpeed = movementHandler.CurrentSpeed;
            if (currentSpeed < config.MinSlideSpeed)
            {
                Debug.Log($"[SlideHandler] Can't slide: speed {currentSpeed:F1} < {config.MinSlideSpeed}");
                return false;
            }

            // Need to be moving forward (not strafing only)
            Vector3 moveDir = movementHandler.MoveDirection;
            if (moveDir.magnitude < 0.1f)
            {
                Debug.Log($"[SlideHandler] Can't slide: moveDir magnitude {moveDir.magnitude:F2} too low");
                return false;
            }

            return true;
        }

        private void StartSlide()
        {
            if (config == null) return;

            isSliding = true;
            slideTimer = config.SlideDuration;

            // Capture current movement direction and boost speed
            slideDirection = movementHandler.MoveDirection.normalized;
            slideSpeed = movementHandler.CurrentSpeed * config.SlideSpeedBoost;

            // Set target height to slide height
            targetHeight = config.SlideHeight;

            Debug.Log($"[SlideHandler] Slide started! Speed: {slideSpeed:F1}, Duration: {config.SlideDuration}s");
            OnSlideStart?.Invoke();
        }

        private void UpdateActiveSlide()
        {
            if (config == null) return;

            // Countdown timer
            slideTimer -= Time.deltaTime;

            // Decelerate
            slideSpeed = Mathf.Max(0, slideSpeed - config.SlideDeceleration * Time.deltaTime);

            // End slide conditions
            bool shouldEnd = false;

            // Timer expired
            if (slideTimer <= 0)
            {
                shouldEnd = true;
            }

            // Speed too low
            if (slideSpeed < 0.5f)
            {
                shouldEnd = true;
            }

            // Jump pressed (cancel slide and allow jump)
            if (input.JumpPressed && slideTimer < config.SlideDuration - 0.1f) // Small buffer to prevent instant cancel
            {
                shouldEnd = true;
            }

            // Left ground (e.g., slid off edge)
            if (groundChecker != null && !groundChecker.IsGrounded)
            {
                shouldEnd = true;
            }

            if (shouldEnd)
            {
                EndSlide();
            }
        }

        private void EndSlide()
        {
            if (!isSliding) return;

            isSliding = false;
            slideCooldownTimer = config != null ? config.SlideCooldown : 0.5f;

            // Return to standing height
            targetHeight = config != null ? config.StandingHeight : 2f;

            Debug.Log("[SlideHandler] Slide ended");
            OnSlideEnd?.Invoke();
        }

        private void ApplyHeightTransition()
        {
            if (controller == null || config == null) return;

            // Smooth height transition
            float previousHeight = currentHeight;
            currentHeight = Mathf.Lerp(
                currentHeight,
                targetHeight,
                Time.deltaTime * config.CrouchTransitionSpeed * 1.5f // Faster for slide
            );

            // Apply to controller
            float heightDelta = currentHeight - previousHeight;
            controller.height = currentHeight;
            controller.center = new Vector3(0, currentHeight / 2f, 0);

            // Move player up/down with height change
            if (Mathf.Abs(heightDelta) > 0.001f)
            {
                transform.position += Vector3.up * (heightDelta / 2f);
            }
        }

        /// <summary>
        /// Get the slide movement vector for this frame.
        /// </summary>
        public Vector3 GetSlideMovement()
        {
            if (!isSliding) return Vector3.zero;
            return slideDirection * slideSpeed;
        }

        /// <summary>
        /// Force end the slide (e.g., when taking damage).
        /// </summary>
        public void ForceEndSlide()
        {
            if (isSliding)
            {
                EndSlide();
            }
        }

        /// <summary>
        /// Check if currently transitioning heights.
        /// </summary>
        public bool IsTransitioning()
        {
            return Mathf.Abs(currentHeight - targetHeight) > 0.01f;
        }
    }
}
