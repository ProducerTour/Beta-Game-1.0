using UnityEngine;
using CreatorWorld.Config;

namespace CreatorWorld.Player.Movement
{
    /// <summary>
    /// Handles ground detection, slope checking, and coyote time tracking.
    /// </summary>
    public class GroundChecker : MonoBehaviour
    {
        [SerializeField] private MovementConfig config;

        private CharacterController controller;

        // State
        private bool isGrounded;
        private bool wasGrounded;
        private float lastGroundedTime;
        private Vector3 slopeNormal;
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
            if (controller == null || config == null) return;

            // Sphere cast from bottom of character
            Vector3 spherePosition = transform.position + Vector3.up * controller.radius;
            isGrounded = Physics.CheckSphere(
                spherePosition,
                controller.radius + config.GroundCheckDistance,
                config.GroundMask
            );

            // Track last grounded time
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
                isOnSlope = slopeAngle > 0.1f && slopeAngle <= config.MaxSlopeAngle;
            }
        }

        /// <summary>
        /// Get the slope-adjusted movement direction.
        /// </summary>
        public Vector3 GetSlopeAdjustedDirection(Vector3 moveDirection)
        {
            if (isOnSlope && isGrounded)
            {
                return Vector3.ProjectOnPlane(moveDirection, slopeNormal);
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
                return Vector3.ProjectOnPlane(Vector3.down, slopeNormal).normalized;
            }
            return Vector3.zero;
        }

        private void OnDrawGizmosSelected()
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            if (controller == null) return;

            // Ground check sphere
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Vector3 spherePosition = transform.position + Vector3.up * controller.radius;
            float checkRadius = controller.radius + (config != null ? config.GroundCheckDistance : 0.2f);
            Gizmos.DrawWireSphere(spherePosition, checkRadius);

            // Slope normal
            if (isOnSlope)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(transform.position, slopeNormal);
            }
        }
    }
}
