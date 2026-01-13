using System;
using System.Collections;
using UnityEngine;
using CreatorWorld.Core;
using CreatorWorld.Interfaces;
using CreatorWorld.Interaction;

namespace CreatorWorld.Player.Movement
{
    /// <summary>
    /// AAA GAME DEV LESSON: The Vault Handler
    ///
    /// This component handles the vault/jump-over mechanic.
    ///
    /// FLOW:
    /// 1. Player enters vaultable object's trigger zone
    /// 2. VaultHandler stores reference to that object
    /// 3. Player presses Jump (spacebar) while in zone
    /// 4. VaultHandler checks conditions (grounded, valid approach, not already vaulting)
    /// 5. If valid → Start vault coroutine
    /// 6. During vault: Disable normal movement, play animation, lerp position
    /// 7. After vault: Re-enable movement, land
    ///
    /// DESIGN PATTERN: Same pattern as SlideHandler/JumpController
    /// - Events for other systems to respond (OnVaultStart, OnVaultEnd)
    /// - Public properties for state queries (IsVaulting, CanVault)
    /// - Integration with PlayerController's update loop
    /// </summary>
    public class VaultHandler : MonoBehaviour
    {
        [Header("Vault Settings")]
        [Tooltip("Height of the vault arc above the obstacle")]
        [SerializeField] private float vaultArcHeight = 0.5f;

        [Tooltip("Cooldown between vaults")]
        [SerializeField] private float vaultCooldown = 0.3f;

        // Events for other systems
        public event Action OnVaultStart;
        public event Action OnVaultEnd;

        // State
        public bool IsVaulting { get; private set; }
        public bool CanVault => currentVaultable != null && !IsVaulting && cooldownTimer <= 0f;

        // References
        private CharacterController characterController;
        private GroundChecker groundChecker;
        private IInputService input;

        // Current vaultable object (set by trigger)
        private VaultableObject currentVaultable;
        private float cooldownTimer;

        // Vault execution
        private Coroutine activeVaultCoroutine;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            groundChecker = GetComponent<GroundChecker>();
        }

        private void Start()
        {
            input = ServiceLocator.Get<IInputService>();
        }

        /// <summary>
        /// Called by PlayerController each frame.
        /// Returns true if vault consumed the jump input.
        /// </summary>
        public bool UpdateVault()
        {
            // Update cooldown
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }

            // Don't process input if already vaulting
            if (IsVaulting) return true; // Consume input while vaulting

            // Check for vault input
            if (input != null && input.JumpPressed && CanVault)
            {
                if (TryStartVault())
                {
                    return true; // Consumed jump input
                }
            }

            return false; // Did not consume input
        }

        /// <summary>
        /// Attempt to start a vault. Returns true if successful.
        /// </summary>
        private bool TryStartVault()
        {
            if (currentVaultable == null) return false;
            if (!groundChecker.IsGrounded) return false;

            // Check if approaching from valid angle
            if (!currentVaultable.IsValidApproach(transform.position, transform.forward))
            {
                return false;
            }

            // Start the vault!
            StartVault();
            return true;
        }

        private void StartVault()
        {
            IsVaulting = true;
            OnVaultStart?.Invoke();

            // Start vault movement coroutine
            if (activeVaultCoroutine != null)
            {
                StopCoroutine(activeVaultCoroutine);
            }
            activeVaultCoroutine = StartCoroutine(VaultCoroutine());
        }

        /// <summary>
        /// The vault movement coroutine.
        ///
        /// AAA TECHNIQUE: Bezier-like arc movement
        /// Instead of linear interpolation, we use a parabolic arc
        /// to make the vault feel natural and dynamic.
        /// </summary>
        private IEnumerator VaultCoroutine()
        {
            // Disable CharacterController to allow direct position manipulation
            characterController.enabled = false;

            // Calculate vault parameters
            Vector3 startPos = transform.position;
            Vector3 endPos = currentVaultable.GetLandingPosition(startPos);
            Vector3 vaultDirection = currentVaultable.GetVaultDirection(startPos);
            float vaultDuration = currentVaultable.VaultDuration;
            float obstacleHeight = currentVaultable.VaultHeight;

            // Rotate to face vault direction
            Quaternion targetRotation = Quaternion.LookRotation(vaultDirection);

            float elapsed = 0f;

            while (elapsed < vaultDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / vaultDuration;

                // Smooth step for nicer acceleration/deceleration
                float smoothT = t * t * (3f - 2f * t); // Hermite interpolation

                // Horizontal position (linear interpolation)
                Vector3 horizontalPos = Vector3.Lerp(startPos, endPos, smoothT);

                // Vertical position (parabolic arc)
                // Peak at t=0.5, using -4(t-0.5)^2 + 1 for a nice arc
                float arcT = -4f * (t - 0.5f) * (t - 0.5f) + 1f;
                float heightOffset = arcT * (obstacleHeight + vaultArcHeight);

                // Combine horizontal and vertical
                Vector3 newPos = new Vector3(
                    horizontalPos.x,
                    startPos.y + heightOffset,
                    horizontalPos.z
                );

                transform.position = newPos;

                // Smoothly rotate during first half of vault
                if (t < 0.5f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t * 2f);
                }

                yield return null;
            }

            // Ensure we end at exact position
            transform.position = endPos;
            transform.rotation = targetRotation;

            // Re-enable CharacterController
            characterController.enabled = true;

            // End vault
            EndVault();
        }

        private void EndVault()
        {
            IsVaulting = false;
            cooldownTimer = vaultCooldown;
            activeVaultCoroutine = null;

            OnVaultEnd?.Invoke();
        }

        /// <summary>
        /// Force cancel the vault (e.g., if player takes damage)
        /// </summary>
        public void CancelVault()
        {
            if (!IsVaulting) return;

            if (activeVaultCoroutine != null)
            {
                StopCoroutine(activeVaultCoroutine);
            }

            characterController.enabled = true;
            EndVault();
        }

        // ========== TRIGGER DETECTION ==========
        // This is how we know when the player is near a vaultable object

        private void OnTriggerEnter(Collider other)
        {
            // Try to get VaultableObject from the trigger
            var vaultable = other.GetComponent<VaultableObject>();
            if (vaultable == null)
            {
                // Also check parent (in case trigger is a child object)
                vaultable = other.GetComponentInParent<VaultableObject>();
            }

            if (vaultable != null)
            {
                currentVaultable = vaultable;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var vaultable = other.GetComponent<VaultableObject>();
            if (vaultable == null)
            {
                vaultable = other.GetComponentInParent<VaultableObject>();
            }

            // Only clear if it's the same vaultable we're tracking
            if (vaultable != null && vaultable == currentVaultable)
            {
                currentVaultable = null;
            }
        }

        // ========== DEBUG ==========

        private void OnGUI()
        {
            #if UNITY_EDITOR
            if (currentVaultable != null)
            {
                GUI.Label(new Rect(10, 100, 300, 30), $"[SPACE] Vault over {currentVaultable.gameObject.name}");
            }
            #endif
        }
    }
}
