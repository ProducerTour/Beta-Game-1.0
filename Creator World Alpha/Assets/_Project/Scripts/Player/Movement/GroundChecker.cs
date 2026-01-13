using UnityEngine;
using CreatorWorld.Config;

namespace CreatorWorld.Player.Movement
{
    /// <summary>
    /// Handles ground detection, slope checking, and coyote time tracking.
    /// Uses CharacterController.isGrounded as primary source with raycast supplement.
    /// </summary>
    public class GroundChecker : MonoBehaviour
    {
        [SerializeField] private MovementConfig config;

        [Header("Slope Handling - Procedural Terrain Optimized")]
        [Tooltip("DIAGNOSTIC: Disable all slope adjustment to test if it causes drift")]
        [SerializeField] private bool disableSlopeAdjustment = false;

        [Tooltip("Minimum slope angle to apply adjustment. Higher = ignores terrain noise. For procedural terrain: 10-15 degrees recommended.")]
        [SerializeField] private float minSlopeAngleForAdjustment = 12f;

        [Tooltip("How quickly slope normal smooths. Lower = more stable on noisy terrain. Range: 3-10 for procedural terrain.")]
        [SerializeField] private float slopeSmoothSpeed = 5f;

        private CharacterController controller;

        // State
        private bool isGrounded;
        private bool wasGrounded;
        private float lastGroundedTime;
        private Vector3 slopeNormal;
        private Vector3 smoothedSlopeNormal; // Smoothed to prevent jitter
        private float slopeAngle;
        private bool isOnSlope;

        // Properties
        public bool IsGrounded => isGrounded;
        public bool WasGrounded => wasGrounded;
        public bool JustLanded => isGrounded && !wasGrounded;
        public bool JustLeftGround => !isGrounded && wasGrounded;
        public float TimeSinceGrounded => Time.time - lastGroundedTime;
        public bool WithinCoyoteTime => config != null && TimeSinceGrounded <= config.CoyoteTime;
        public Vector3 SlopeNormal => slopeNormal;
        public float SlopeAngle => slopeAngle;
        public bool IsOnSlope => isOnSlope;
        public bool IsOnSteepSlope => config != null && slopeAngle > config.MaxSlopeAngle;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (controller == null)
            {
                controller = GetComponentInParent<CharacterController>();
            }
            smoothedSlopeNormal = Vector3.up;
        }

        /// <summary>
        /// Call this every frame from the PlayerController.
        /// </summary>
        public void UpdateGroundCheck()
        {
            wasGrounded = isGrounded;
            CheckGrounded();
            CheckSlope();
        }

        private void CheckGrounded()
        {
            if (controller == null) return;

            // PRIMARY: Use CharacterController's built-in ground detection
            // This is the most reliable because it's based on actual collision during Move()
            isGrounded = controller.isGrounded;

            // SECONDARY: Supplement with a simple raycast for edge cases
            // This helps detect ground slightly before landing for smoother transitions
            if (!isGrounded && config != null)
            {
                float rayDistance = controller.skinWidth + config.GroundCheckDistance;
                Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;

                if (Physics.Raycast(rayOrigin, Vector3.down, rayDistance + 0.1f, config.GroundMask))
                {
                    isGrounded = true;
                }
            }

            // Track last grounded time for coyote time
            if (isGrounded)
            {
                lastGroundedTime = Time.time;
            }
        }

        private void CheckSlope()
        {
            isOnSlope = false;
            slopeAngle = 0f;
            slopeNormal = Vector3.up;

            if (config == null) return;

            if (Physics.Raycast(
                transform.position + Vector3.up * 0.1f,
                Vector3.down,
                out RaycastHit hit,
                1.5f,
                config.GroundMask))
            {
                slopeNormal = hit.normal;
                slopeAngle = Vector3.Angle(Vector3.up, slopeNormal);

                // Only consider it a meaningful slope if above threshold
                isOnSlope = slopeAngle >= minSlopeAngleForAdjustment && slopeAngle <= config.MaxSlopeAngle;

                // Smooth the slope normal to prevent jittery movement on uneven terrain
                smoothedSlopeNormal = Vector3.Slerp(smoothedSlopeNormal, slopeNormal, Time.deltaTime * slopeSmoothSpeed);
            }
            else
            {
                // No ground hit, smoothly return to flat
                smoothedSlopeNormal = Vector3.Slerp(smoothedSlopeNormal, Vector3.up, Time.deltaTime * slopeSmoothSpeed);
            }
        }

        /// <summary>
        /// Get the slope-adjusted movement direction.
        /// Only applies adjustment on meaningful slopes to prevent drift on near-flat terrain.
        /// </summary>
        public Vector3 GetSlopeAdjustedDirection(Vector3 moveDirection)
        {
            // DIAGNOSTIC: Skip all slope adjustment if disabled
            if (disableSlopeAdjustment) return moveDirection;

            // Only adjust on meaningful slopes (prevents drift on micro-bumps)
            if (isOnSlope && isGrounded && slopeAngle >= minSlopeAngleForAdjustment)
            {
                // Use smoothed normal to prevent jitter
                Vector3 adjusted = Vector3.ProjectOnPlane(moveDirection, smoothedSlopeNormal);

                // Preserve original speed magnitude
                float originalMagnitude = moveDirection.magnitude;
                if (adjusted.magnitude > 0.001f && originalMagnitude > 0.001f)
                {
                    adjusted = adjusted.normalized * originalMagnitude;
                }

                return adjusted;
            }
            return moveDirection;
        }

        /// <summary>
        /// Get the slide direction for steep slopes.
        /// </summary>
        public Vector3 GetSlopeSlideDirection()
        {
            if (IsOnSteepSlope)
            {
                // Use smoothed normal for consistency
                return Vector3.ProjectOnPlane(Vector3.down, smoothedSlopeNormal).normalized;
            }
            return Vector3.zero;
        }

        private void OnDrawGizmosSelected()
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            if (controller == null) return;

            // Ground check visualization
            Gizmos.color = isGrounded ? Color.green : Color.red;

            float rayDistance = controller.skinWidth + (config != null ? config.GroundCheckDistance : 0.15f);
            Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
            Gizmos.DrawLine(rayOrigin, rayOrigin + Vector3.down * (rayDistance + 0.1f));

            // Draw a small sphere at feet
            Gizmos.DrawWireSphere(transform.position, 0.1f);

            // Slope normal
            if (isOnSlope)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(transform.position, slopeNormal);
            }
        }
    }
}
