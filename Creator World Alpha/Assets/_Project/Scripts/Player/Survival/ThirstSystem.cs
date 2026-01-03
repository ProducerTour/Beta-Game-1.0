using System;
using UnityEngine;

namespace CreatorWorld.Player.Survival
{
    /// <summary>
    /// Manages player thirst and dehydration damage.
    /// </summary>
    public class ThirstSystem : MonoBehaviour
    {
        [Header("Thirst Settings")]
        [SerializeField] private float maxThirst = 100f;
        [SerializeField] private float thirstDecayRate = 0.8f;
        [SerializeField] private float dehydrationDamageRate = 3f;

        [Header("Event Throttling")]
        [SerializeField] private float eventThreshold = 1f; // Only fire event if changed by this amount

        // State
        private float currentThirst;
        private float lastReportedThirst;

        // Properties
        public float CurrentThirst => currentThirst;
        public float MaxThirst => maxThirst;
        public float ThirstPercent => currentThirst / maxThirst;
        public bool IsDehydrated => currentThirst <= 0;
        public bool IsHydrated => ThirstPercent > 0.5f;

        // Events (throttled - only fire on significant change)
        public event Action<float, float> OnThirstChanged; // current, max
        public event Action OnStartedDehydration;
        public event Action OnStoppedDehydration;

        private bool wasDehydrated;

        private void Awake()
        {
            currentThirst = maxThirst;
            lastReportedThirst = currentThirst;
        }

        /// <summary>
        /// Process thirst decay. Call from SurvivalManager.
        /// </summary>
        public void ProcessDecay(float deltaTime)
        {
            float previousThirst = currentThirst;
            currentThirst = Mathf.Max(0, currentThirst - thirstDecayRate * deltaTime);

            // Check dehydration state change
            if (IsDehydrated && !wasDehydrated)
            {
                wasDehydrated = true;
                OnStartedDehydration?.Invoke();
            }
            else if (!IsDehydrated && wasDehydrated)
            {
                wasDehydrated = false;
                OnStoppedDehydration?.Invoke();
            }

            // Throttled event - only fire if changed significantly
            if (Mathf.Abs(currentThirst - lastReportedThirst) >= eventThreshold)
            {
                lastReportedThirst = currentThirst;
                OnThirstChanged?.Invoke(currentThirst, maxThirst);
            }
        }

        /// <summary>
        /// Get dehydration damage per second.
        /// </summary>
        public float GetDehydrationDamage()
        {
            return IsDehydrated ? dehydrationDamageRate : 0f;
        }

        /// <summary>
        /// Drink to restore thirst.
        /// </summary>
        public void Drink(float amount)
        {
            if (amount <= 0) return;

            currentThirst = Mathf.Min(maxThirst, currentThirst + amount);
            lastReportedThirst = currentThirst;
            OnThirstChanged?.Invoke(currentThirst, maxThirst);

            // Check if stopped dehydration
            if (!IsDehydrated && wasDehydrated)
            {
                wasDehydrated = false;
                OnStoppedDehydration?.Invoke();
            }
        }

        /// <summary>
        /// Reset thirst for respawn.
        /// </summary>
        public void Reset()
        {
            currentThirst = maxThirst;
            lastReportedThirst = currentThirst;
            wasDehydrated = false;
            OnThirstChanged?.Invoke(currentThirst, maxThirst);
        }

        /// <summary>
        /// Set thirst directly (for loading saves).
        /// </summary>
        public void SetThirst(float thirst)
        {
            currentThirst = Mathf.Clamp(thirst, 0, maxThirst);
            lastReportedThirst = currentThirst;
            wasDehydrated = IsDehydrated;
            OnThirstChanged?.Invoke(currentThirst, maxThirst);
        }
    }
}
