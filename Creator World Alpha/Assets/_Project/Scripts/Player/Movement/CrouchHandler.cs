using UnityEngine;
using CreatorWorld.Config;
using CreatorWorld.Core;
using CreatorWorld.Interfaces;

namespace CreatorWorld.Player.Movement
{
    /// <summary>
    /// Handles crouching state and CharacterController height adjustments.
    /// </summary>
    public class CrouchHandler : MonoBehaviour
    {
        [SerializeField] private MovementConfig config;

        private CharacterController controller;
        private IInputService input;

        // State
        private bool isCrouching;
        private bool wantsToCrouch;
        private float currentHeight;
        private float targetHeight;

        // Properties
        public bool IsCrouching => isCrouching;
        public float CurrentHeight => currentHeight;
        public float HeightPercent {
            get {
                float crouchHeight = config != null ? config.CrouchHeight : 1.0f;
                float standingHeight = config != null ? config.StandingHeight : 1.8f;
                float range = standingHeight - crouchHeight;
                return range > 0 ? (currentHeight - crouchHeight) / range : 1f;
            }
        }

        // Events
        public event System.Action OnCrouchStart;
        public event System.Action OnCrouchEnd;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (controller == null)
            {
                controller = GetComponentInParent<CharacterController>();
            }

            // Use config value or default
            currentHeight = config != null ? config.StandingHeight : 1.8f;
            targetHeight = currentHeight;

            Debug.Log($"CrouchHandler initialized - config: {(config != null ? config.name : "NULL")}, height: {currentHeight}");
        }

        private void Start()
        {
            input = ServiceLocator.Get<IInputService>();
        }

        /// <summary>
        /// Call this every frame from PlayerController.
        /// </summary>
        public void UpdateCrouch()
        {
            if (input == null)
            {
                input = ServiceLocator.Get<IInputService>();
                if (input == null)
                {
                    Debug.LogWarning("CrouchHandler: InputService not found!");
                    return;
                }
            }

            HandleCrouchInput();
            UpdateCrouchState();
            ApplyHeightTransition();
        }

        private void HandleCrouchInput()
        {
            // Toggle crouch on input
            if (input.CrouchPressed)
            {
                wantsToCrouch = !wantsToCrouch;
                Debug.Log($">>> CROUCH TOGGLED - wantsToCrouch: {wantsToCrouch} <<<");
            }
        }

        private void UpdateCrouchState()
        {
            // Use config values or defaults
            float crouchHeight = config != null ? config.CrouchHeight : 1.0f;
            float standingHeight = config != null ? config.StandingHeight : 1.8f;

            if (wantsToCrouch)
            {
                if (!isCrouching)
                {
                    isCrouching = true;
                    OnCrouchStart?.Invoke();
                    Debug.Log("Crouch started");
                }
            }
            else
            {
                // Check if we can stand up (no ceiling)
                if (isCrouching && CanStandUp())
                {
                    isCrouching = false;
                    OnCrouchEnd?.Invoke();
                    Debug.Log("Crouch ended");
                }
            }

            targetHeight = isCrouching ? crouchHeight : standingHeight;
        }

        private bool CanStandUp()
        {
            if (controller == null) return true;

            // Use config values or defaults
            float standingHeight = config != null ? config.StandingHeight : 1.8f;
            LayerMask groundMask = config != null ? config.GroundMask : ~0; // Default: check all layers

            // Check for ceiling above player
            Vector3 checkPosition = transform.position + Vector3.up * standingHeight;
            return !Physics.CheckSphere(checkPosition, controller.radius, groundMask);
        }

        private void ApplyHeightTransition()
        {
            if (controller == null) return;

            // Use config value or default
            float transitionSpeed = config != null ? config.CrouchTransitionSpeed : 10f;

            // Smooth height transition
            float previousHeight = currentHeight;
            currentHeight = Mathf.Lerp(
                currentHeight,
                targetHeight,
                Time.deltaTime * transitionSpeed
            );

            // Apply to controller
            float heightDelta = currentHeight - previousHeight;
            controller.height = currentHeight;
            controller.center = new Vector3(0, currentHeight / 2f, 0);

            // Move player up/down with height change to prevent clipping
            if (Mathf.Abs(heightDelta) > 0.001f)
            {
                transform.position += Vector3.up * (heightDelta / 2f);
            }
        }

        /// <summary>
        /// Force crouch state (e.g., when entering small spaces).
        /// </summary>
        public void ForceCrouch(bool crouch)
        {
            wantsToCrouch = crouch;
            if (crouch && !isCrouching)
            {
                isCrouching = true;
                OnCrouchStart?.Invoke();
            }
        }

        /// <summary>
        /// Check if currently transitioning between heights.
        /// </summary>
        public bool IsTransitioning()
        {
            return Mathf.Abs(currentHeight - targetHeight) > 0.01f;
        }
    }
}
