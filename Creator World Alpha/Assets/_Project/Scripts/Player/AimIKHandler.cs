using UnityEngine;
using CreatorWorld.Core;
using CreatorWorld.Interfaces;

namespace CreatorWorld.Player
{
    /// <summary>
    /// Handles upper body IK to aim the weapon toward the camera's look direction.
    /// Uses Unity's built-in Animator IK system (requires IK Pass enabled on animation layer).
    ///
    /// When aiming:
    /// - Raycasts from camera to find aim point
    /// - Uses LookAt IK to tilt spine/head toward that point
    /// - Weight smoothly blends in/out for natural feel
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class AimIKHandler : MonoBehaviour
    {
        [Header("IK Settings")]
        [Tooltip("How much the body rotates to look at aim point (0-1)")]
        [Range(0f, 1f)]
        [SerializeField] private float bodyWeight = 0.4f;

        [Tooltip("How much the head rotates to look at aim point (0-1)")]
        [Range(0f, 1f)]
        [SerializeField] private float headWeight = 0.6f;

        [Tooltip("How much the eyes look at aim point (0-1)")]
        [Range(0f, 1f)]
        [SerializeField] private float eyesWeight = 0.8f;

        [Tooltip("Clamp weight - prevents over-rotation (0-1)")]
        [Range(0f, 1f)]
        [SerializeField] private float clampWeight = 0.5f;

        [Header("Blend Settings")]
        [Tooltip("How fast IK blends in when starting to aim")]
        [SerializeField] private float blendInSpeed = 8f;

        [Tooltip("How fast IK blends out when stopping aim")]
        [SerializeField] private float blendOutSpeed = 5f;

        [Header("Aim Point")]
        [Tooltip("Max distance for aim raycast")]
        [SerializeField] private float maxAimDistance = 100f;

        [Tooltip("Default aim distance when raycast hits nothing")]
        [SerializeField] private float defaultAimDistance = 50f;

        // Components
        private Animator animator;
        private ICameraService cameraService;
        private PlayerAnimation playerAnimation;

        // State
        private float currentIKWeight;
        private Vector3 lookAtPosition;
        private bool hasRifle;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            playerAnimation = GetComponent<PlayerAnimation>();
        }

        private void Start()
        {
            cameraService = ServiceLocator.Get<ICameraService>();
        }

        private void Update()
        {
            // Lazy load camera service
            if (cameraService == null)
            {
                cameraService = ServiceLocator.Get<ICameraService>();
            }

            UpdateAimPoint();
            UpdateIKWeight();
        }

        private void UpdateAimPoint()
        {
            if (cameraService == null) return;

            Transform cam = cameraService.CameraTransform;
            if (cam == null) return;

            // Raycast from camera center to find what we're aiming at
            Ray aimRay = new Ray(cam.position, cam.forward);

            if (Physics.Raycast(aimRay, out RaycastHit hit, maxAimDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                lookAtPosition = hit.point;
            }
            else
            {
                // No hit - aim at a point far in front of camera
                lookAtPosition = cam.position + cam.forward * defaultAimDistance;
            }
        }

        private void UpdateIKWeight()
        {
            // Determine if we should be using IK (aiming with weapon)
            bool shouldAim = cameraService != null && cameraService.IsAiming;

            // Check if we have a rifle equipped (IK only makes sense with weapon)
            // We check the animator parameter since PlayerAnimation manages it
            if (animator != null)
            {
                hasRifle = animator.GetBool("HasRifle");
            }

            float targetWeight = (shouldAim && hasRifle) ? 1f : 0f;

            // Blend toward target weight
            float blendSpeed = targetWeight > currentIKWeight ? blendInSpeed : blendOutSpeed;
            currentIKWeight = Mathf.MoveTowards(currentIKWeight, targetWeight, blendSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Called by Unity's animation system when IK Pass is enabled on the layer.
        /// This is where we apply the look-at IK.
        /// </summary>
        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null) return;
            if (currentIKWeight < 0.01f) return;

            // Apply look-at IK with current weight
            animator.SetLookAtWeight(
                currentIKWeight,           // Overall weight
                bodyWeight * currentIKWeight,  // Body
                headWeight * currentIKWeight,  // Head
                eyesWeight * currentIKWeight,  // Eyes
                clampWeight                    // Clamp (prevents extreme rotations)
            );

            animator.SetLookAtPosition(lookAtPosition);
        }

        /// <summary>
        /// Debug visualization of aim point.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (currentIKWeight < 0.01f) return;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(lookAtPosition, 0.2f);
            Gizmos.DrawLine(transform.position + Vector3.up * 1.5f, lookAtPosition);
        }
    }
}
