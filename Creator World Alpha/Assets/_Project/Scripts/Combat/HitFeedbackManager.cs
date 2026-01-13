using UnityEngine;
using CreatorWorld.Interfaces;
using CreatorWorld.UI;
using CreatorWorld.Core;
using CreatorWorld.Audio;

namespace CreatorWorld.Combat
{
    /// <summary>
    /// Coordinates hit feedback between weapons and UI/audio systems.
    /// AAA Pattern: Single point of control for all hit feedback.
    /// Subscribes to weapon OnTargetHit events and routes to appropriate systems.
    /// </summary>
    public class HitFeedbackManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HitMarkerUI hitMarkerUI;

        [Header("Auto Setup")]
        [Tooltip("If true, automatically creates HitMarkerUI if not assigned")]
        [SerializeField] private bool autoCreateUI = true;

        // Cached references
        private WeaponManager weaponManager;
        private WeaponBase currentWeapon;
        private WeaponInventory weaponInventory;

        private void Start()
        {
            // Find weapon system
            weaponManager = FindFirstObjectByType<WeaponManager>();
            if (weaponManager == null)
            {
                Debug.LogWarning("[HitFeedbackManager] No WeaponManager found. Hit feedback disabled.");
                return;
            }

            // Find inventory for weapon change events
            weaponInventory = weaponManager.GetComponent<WeaponInventory>();
            if (weaponInventory != null)
            {
                weaponInventory.OnWeaponChanged += OnWeaponChanged;
            }

            // Subscribe to current weapon if one is equipped
            SubscribeToWeapon(weaponManager.CurrentWeapon);

            // Setup UI
            SetupHitMarkerUI();

            Debug.Log("[HitFeedbackManager] Initialized successfully");
        }

        private void OnDestroy()
        {
            // Unsubscribe from current weapon
            UnsubscribeFromWeapon();

            // Unsubscribe from inventory
            if (weaponInventory != null)
            {
                weaponInventory.OnWeaponChanged -= OnWeaponChanged;
            }
        }

        private void SetupHitMarkerUI()
        {
            if (hitMarkerUI != null) return;

            if (autoCreateUI)
            {
                // Create HitMarkerUI
                GameObject uiGO = new GameObject("HitMarkerUI");
                hitMarkerUI = uiGO.AddComponent<HitMarkerUI>();
                Debug.Log("[HitFeedbackManager] Auto-created HitMarkerUI");
            }
            else
            {
                Debug.LogWarning("[HitFeedbackManager] No HitMarkerUI assigned and autoCreate is disabled.");
            }
        }

        private void OnWeaponChanged(WeaponBase weapon, int slot)
        {
            // Unsubscribe from old weapon
            UnsubscribeFromWeapon();

            // Subscribe to new weapon
            SubscribeToWeapon(weapon);
        }

        private void SubscribeToWeapon(WeaponBase weapon)
        {
            if (weapon == null) return;

            currentWeapon = weapon;
            currentWeapon.OnTargetHit += OnTargetHit;
            Debug.Log($"[HitFeedbackManager] Subscribed to {weapon.WeaponName}");
        }

        private void UnsubscribeFromWeapon()
        {
            if (currentWeapon != null)
            {
                currentWeapon.OnTargetHit -= OnTargetHit;
                currentWeapon = null;
            }
        }

        /// <summary>
        /// Called when a weapon hits a target.
        /// Routes to appropriate feedback systems.
        /// </summary>
        private void OnTargetHit(Vector3 hitPoint, bool isHeadshot, bool isKill)
        {
            // Determine feedback type
            HitFeedbackType feedbackType;
            if (isKill)
            {
                feedbackType = HitFeedbackType.Kill;
            }
            else if (isHeadshot)
            {
                feedbackType = HitFeedbackType.Headshot;
            }
            else
            {
                feedbackType = HitFeedbackType.Normal;
            }

            // Show hit marker (visual)
            if (hitMarkerUI != null)
            {
                hitMarkerUI.ShowHitMarker(feedbackType);
            }

            // Play hit sound (audio) via centralized AudioManager
            var audioManager = ServiceLocator.Get<IAudioService>() as AudioManager;
            if (audioManager != null)
            {
                audioManager.PlayHitMarker(feedbackType);
            }

            // Log for debugging
            string typeText = feedbackType.ToString().ToUpper();
            Debug.Log($"[HitFeedbackManager] {typeText} at {hitPoint}");
        }

        /// <summary>
        /// Manually trigger a hit marker (for testing or special effects).
        /// </summary>
        public void TriggerHitMarker(HitFeedbackType type)
        {
            if (hitMarkerUI != null)
            {
                hitMarkerUI.ShowHitMarker(type);
            }
        }
    }
}
