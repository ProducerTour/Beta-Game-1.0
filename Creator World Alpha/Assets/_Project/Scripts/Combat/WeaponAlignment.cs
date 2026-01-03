using UnityEngine;

namespace CreatorWorld.Combat
{
    /// <summary>
    /// Handles weapon positioning relative to the character's hand.
    /// Attach to each weapon prefab to define its grip offset.
    /// </summary>
    public class WeaponAlignment : MonoBehaviour
    {
        [Header("Grip Position (Right Hand)")]
        [Tooltip("Local position offset from hand bone")]
        public Vector3 gripPosition = Vector3.zero;

        [Tooltip("Local rotation offset from hand bone")]
        public Vector3 gripRotation = Vector3.zero;

        [Header("Left Hand IK Target (Optional)")]
        [Tooltip("Transform for left hand to grip (foregrip/handguard)")]
        public Transform leftHandTarget;

        [Header("Aim Down Sights")]
        [Tooltip("Position offset when aiming")]
        public Vector3 adsPositionOffset = new Vector3(0, 0.05f, 0.1f);

        [Tooltip("Rotation offset when aiming")]
        public Vector3 adsRotationOffset = Vector3.zero;

        [Header("Debug")]
        [SerializeField] private bool showGizmos = true;

        private Vector3 defaultLocalPosition;
        private Quaternion defaultLocalRotation;
        private bool isInitialized;

        private void Awake()
        {
            // Store defaults
            defaultLocalPosition = transform.localPosition;
            defaultLocalRotation = transform.localRotation;
        }

        /// <summary>
        /// Apply the grip offset. Call after parenting to hand.
        /// </summary>
        public void ApplyGripOffset()
        {
            transform.localPosition = gripPosition;
            transform.localRotation = Quaternion.Euler(gripRotation);
            isInitialized = true;
        }

        /// <summary>
        /// Blend to ADS position.
        /// </summary>
        public void SetADSBlend(float blend)
        {
            if (!isInitialized) return;

            Vector3 targetPos = Vector3.Lerp(gripPosition, gripPosition + adsPositionOffset, blend);
            Quaternion targetRot = Quaternion.Slerp(
                Quaternion.Euler(gripRotation),
                Quaternion.Euler(gripRotation + adsRotationOffset),
                blend
            );

            transform.localPosition = targetPos;
            transform.localRotation = targetRot;
        }

        /// <summary>
        /// Get the left hand IK position (for two-handed weapons).
        /// </summary>
        public Vector3? GetLeftHandPosition()
        {
            return leftHandTarget != null ? leftHandTarget.position : null;
        }

        /// <summary>
        /// Get the left hand IK rotation.
        /// </summary>
        public Quaternion? GetLeftHandRotation()
        {
            return leftHandTarget != null ? leftHandTarget.rotation : null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;

            // Draw grip position
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.02f);

            // Draw forward direction
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, transform.forward * 0.3f);

            // Draw up direction
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, transform.up * 0.1f);

            // Draw left hand target
            if (leftHandTarget != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(leftHandTarget.position, 0.03f);
                Gizmos.DrawLine(transform.position, leftHandTarget.position);
            }
        }
#endif
    }
}
