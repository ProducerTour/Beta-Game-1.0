using UnityEngine;

namespace CreatorWorld.Interaction
{
    /// <summary>
    /// AAA GAME DEV LESSON: Object-Triggered Animations
    ///
    /// This component marks an object as "vaultable" (can be jumped over).
    ///
    /// HOW IT WORKS:
    /// 1. A trigger collider surrounds the object
    /// 2. When player enters trigger → they can vault
    /// 3. When player presses jump while in trigger → vault animation plays
    /// 4. Object provides vault parameters (height, direction hint)
    ///
    /// SETUP REQUIREMENTS:
    /// - Add a Box Collider (or other collider) set to "Is Trigger"
    /// - The trigger should extend slightly beyond the object
    /// - Player needs the "Player" tag
    /// </summary>
    public class VaultableObject : MonoBehaviour
    {
        [Header("Vault Configuration")]
        [Tooltip("Height of this obstacle for animation selection")]
        [SerializeField] private float vaultHeight = 1.0f;

        [Tooltip("How far the player moves forward during the vault")]
        [SerializeField] private float vaultDistance = 2.0f;

        [Tooltip("Duration of the vault animation")]
        [SerializeField] private float vaultDuration = 0.8f;

        [Header("Approach Settings")]
        [Tooltip("Maximum angle from forward to allow vault (prevents side/back vaults)")]
        [SerializeField] private float maxApproachAngle = 60f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;

        // Properties for VaultHandler to read
        public float VaultHeight => vaultHeight;
        public float VaultDistance => vaultDistance;
        public float VaultDuration => vaultDuration;

        /// <summary>
        /// Get the optimal vault direction (perpendicular to obstacle)
        /// </summary>
        public Vector3 GetVaultDirection(Vector3 playerPosition)
        {
            // Calculate direction from player to obstacle center
            Vector3 toObstacle = transform.position - playerPosition;
            toObstacle.y = 0; // Keep horizontal

            // Return the forward direction player should vault
            return toObstacle.normalized;
        }

        /// <summary>
        /// Check if the player is approaching from a valid angle
        /// </summary>
        public bool IsValidApproach(Vector3 playerPosition, Vector3 playerForward)
        {
            Vector3 toObstacle = transform.position - playerPosition;
            toObstacle.y = 0;

            // Angle between player facing and direction to obstacle
            float angle = Vector3.Angle(playerForward, toObstacle);

            return angle <= maxApproachAngle;
        }

        /// <summary>
        /// Calculate the landing position after vault
        /// </summary>
        public Vector3 GetLandingPosition(Vector3 playerPosition)
        {
            Vector3 vaultDirection = GetVaultDirection(playerPosition);
            return playerPosition + vaultDirection * vaultDistance;
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebugInfo) return;

            // Draw vault height indicator
            Gizmos.color = Color.yellow;
            Vector3 heightPoint = transform.position + Vector3.up * vaultHeight;
            Gizmos.DrawLine(transform.position, heightPoint);
            Gizmos.DrawWireSphere(heightPoint, 0.1f);

            // Draw approach angle cone
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Vector3 forward = transform.forward;

            // Draw the valid approach arc
            int segments = 20;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = -maxApproachAngle + (maxApproachAngle * 2 * i / segments);
                float angle2 = -maxApproachAngle + (maxApproachAngle * 2 * (i + 1) / segments);

                Vector3 dir1 = Quaternion.Euler(0, angle1, 0) * -forward * 2f;
                Vector3 dir2 = Quaternion.Euler(0, angle2, 0) * -forward * 2f;

                Gizmos.DrawLine(transform.position + dir1, transform.position + dir2);
            }
        }
    }
}
