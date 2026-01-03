using UnityEngine;
using UnityEngine.InputSystem;
using CreatorWorld.Core;
using CreatorWorld.Interfaces;

namespace CreatorWorld.Player
{
    /// <summary>
    /// Third-person camera controller with smooth follow and collision.
    /// Can switch between third-person and first-person (ADS).
    /// Implements ICameraService for ServiceLocator registration.
    /// </summary>
    public class PlayerCamera : MonoBehaviour, ICameraService
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private float shoulderOffsetX = 0.5f;  // Always screen-right
        [SerializeField] private float shoulderHeight = 1.6f;   // Height above player pivot

        [Header("Third Person")]
        [SerializeField] private float defaultDistance = 3f;
        [SerializeField] private float minDistance = 1f;
        [SerializeField] private float maxDistance = 6f;
        [SerializeField] private float zoomSpeed = 2f;

        [Header("First Person (ADS)")]
        [SerializeField] private float adsDistance = 0.5f;
        [SerializeField] private float adsTransitionSpeed = 10f;

        [Header("Rotation")]
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float minPitch = -40f;
        [SerializeField] private float maxPitch = 80f;
        [SerializeField] private float rotationSmoothTime = 0.05f;

        [Header("Collision")]
        [SerializeField] private float collisionRadius = 0.2f;
        [SerializeField] private LayerMask collisionMask;
        [SerializeField] private float collisionSmoothTime = 0.1f;

        // State
        private float yaw;
        private float pitch;
        private float currentDistance;
        private float targetDistance;
        private float distanceVelocity;
        private Vector2 currentRotation;
        private Vector2 rotationVelocity;
        private bool isAiming;

        // Input
        private Vector2 lookInput;

        // ICameraService implementation
        public Transform CameraTransform => transform;
        public Vector3 Forward => transform.forward;
        public Vector3 Right => transform.right;
        public bool IsAiming => isAiming;

        // Additional properties
        public float Yaw => yaw;
        public float Pitch => pitch;

        private void Start()
        {
            if (target == null)
            {
                var player = FindFirstObjectByType<PlayerController>();
                if (player != null)
                {
                    target = player.transform;
                }
            }

            currentDistance = defaultDistance;
            targetDistance = defaultDistance;

            // Default collision mask if not set
            if (collisionMask == 0)
            {
                collisionMask = ~LayerMask.GetMask("Player", "Ignore Raycast");
            }

            // Initialize rotation from current camera rotation
            Vector3 angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = angles.x;

            // Lock cursor for gameplay
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Register with ServiceLocator
            ServiceLocator.Register<ICameraService>(this);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                // Try to find player again
                var player = FindFirstObjectByType<PlayerController>();
                if (player != null) target = player.transform;
                return;
            }

            // Allow camera if no GameManager or if in Playing state
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != Interfaces.GameState.Playing) return;

            HandleInput();
            HandleRotation();
            HandleDistance();
            HandleCollision();
            ApplyTransform();
        }

        #region Input Callbacks

        public void OnLook(InputValue value)
        {
            lookInput = value.Get<Vector2>();
        }

        public void OnAim(InputValue value)
        {
            isAiming = value.isPressed;
        }

        public void OnZoom(InputValue value)
        {
            if (!isAiming)
            {
                float scroll = value.Get<float>();
                targetDistance = Mathf.Clamp(targetDistance - scroll * zoomSpeed, minDistance, maxDistance);
            }
        }

        #endregion

        private void HandleInput()
        {
            // Use new Input System Mouse class
            var mouse = Mouse.current;
            if (mouse == null) return;

            // Read mouse delta directly
            Vector2 mouseDelta = mouse.delta.ReadValue();
            float mouseX = mouseDelta.x * mouseSensitivity * 0.1f;
            float mouseY = mouseDelta.y * mouseSensitivity * 0.1f;

            yaw += mouseX;
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            // Right-click to aim
            isAiming = mouse.rightButton.isPressed;
        }

        private void HandleRotation()
        {
            // Smooth rotation
            currentRotation.x = Mathf.SmoothDamp(currentRotation.x, pitch, ref rotationVelocity.x, rotationSmoothTime);
            currentRotation.y = Mathf.SmoothDamp(currentRotation.y, yaw, ref rotationVelocity.y, rotationSmoothTime);
        }

        private void HandleDistance()
        {
            // Switch between ADS and third-person distance
            float desiredDistance = isAiming ? adsDistance : targetDistance;
            currentDistance = Mathf.Lerp(currentDistance, desiredDistance, Time.deltaTime * adsTransitionSpeed);
        }

        private void HandleCollision()
        {
            // Calculate target position with camera-relative shoulder offset
            // This ensures the camera is always to the screen-right of the player
            Quaternion rotation = Quaternion.Euler(currentRotation.x, currentRotation.y, 0);
            Vector3 cameraRight = rotation * Vector3.right;
            Vector3 targetPosition = target.position + Vector3.up * shoulderHeight + cameraRight * shoulderOffsetX;

            Vector3 direction = rotation * Vector3.back;
            Vector3 desiredPosition = targetPosition + direction * currentDistance;

            // Raycast to check for obstacles
            if (Physics.SphereCast(targetPosition, collisionRadius, direction, out RaycastHit hit,
                currentDistance, collisionMask))
            {
                // Move camera in front of obstacle
                float adjustedDistance = hit.distance - collisionRadius;
                adjustedDistance = Mathf.Max(adjustedDistance, minDistance * 0.5f);
                currentDistance = Mathf.SmoothDamp(currentDistance, adjustedDistance, ref distanceVelocity, collisionSmoothTime);
            }
        }

        private void ApplyTransform()
        {
            // Calculate target position with camera-relative shoulder offset
            Quaternion rotation = Quaternion.Euler(currentRotation.x, currentRotation.y, 0);
            Vector3 cameraRight = rotation * Vector3.right;
            Vector3 targetPosition = target.position + Vector3.up * shoulderHeight + cameraRight * shoulderOffsetX;

            Vector3 direction = rotation * Vector3.back;

            transform.position = targetPosition + direction * currentDistance;
            transform.rotation = rotation;
        }

        /// <summary>
        /// Get the forward direction for aiming/shooting
        /// </summary>
        public Vector3 GetAimDirection()
        {
            return transform.forward;
        }

        /// <summary>
        /// Get the point the player is aiming at
        /// </summary>
        public bool GetAimPoint(out Vector3 point, float maxDistance = 100f)
        {
            Ray ray = new Ray(transform.position, transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }
            point = transform.position + transform.forward * maxDistance;
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            if (target == null) return;

            Gizmos.color = Color.cyan;
            // Use current camera rotation to compute shoulder offset for gizmo
            Quaternion rotation = Quaternion.Euler(currentRotation.x, currentRotation.y, 0);
            Vector3 cameraRight = rotation * Vector3.right;
            Vector3 targetPos = target.position + Vector3.up * shoulderHeight + cameraRight * shoulderOffsetX;
            Gizmos.DrawWireSphere(targetPos, 0.1f);
            Gizmos.DrawLine(targetPos, transform.position);
        }
    }
}
