using System;
using UnityEngine;

namespace CreatorWorld.Player.Survival
{
    /// <summary>
    /// Manages player hunger and starvation damage.
    /// </summary>
    public class HungerSystem : MonoBehaviour
    {
        [Header("Hunger Settings")]
        [SerializeField] private float maxHunger = 100f;
        [SerializeField] private float hungerDecayRate = 0.5f;
        [SerializeField] private float starvingDamageRate = 2f;

        [Header("Event Throttling")]
        [SerializeField] private float eventThreshold = 1f; // Only fire event if changed by this amount

        // State
        private float currentHunger;
        private float lastReportedHunger;

        // Properties
        public float CurrentHunger => currentHunger;
        public float MaxHunger => maxHunger;
        public float HungerPercent => currentHunger / maxHunger;
        public bool IsStarving => currentHunger <= 0;
        public bool IsWellFed => HungerPercent > 0.5f;

        // Events (throttled - only fire on significant change)
        public event Action<float, float> OnHungerChanged; // current, max
        public event Action OnStartedStarving;
        public event Action OnStoppedStarving;

        private bool wasStarving;

        private void Awake()
        {
            currentHunger = maxHunger;
            lastReportedHunger = currentHunger;
        }

        /// <summary>
        /// Process hunger decay. Call from SurvivalManager.
        /// </summary>
        public void ProcessDecay(float deltaTime)
        {
            float previousHunger = currentHunger;
            currentHunger = Mathf.Max(0, currentHunger - hungerDecayRate * deltaTime);

            // Check starvation state change
            if (IsStarving && !wasStarving)
            {
                wasStarving = true;
                OnStartedStarving?.Invoke();
            }
            else if (!IsStarving && wasStarving)
            {
                wasStarving = false;
                OnStoppedStarving?.Invoke();
            }

            // Throttled event - only fire if changed significantly
            if (Mathf.Abs(currentHunger - lastReportedHunger) >= eventThreshold)
            {
                lastReportedHunger = currentHunger;
                OnHungerChanged?.Invoke(currentHunger, maxHunger);
            }
        }

        /// <summary>
        /// Get starvation damage per second.
        /// </summary>
        public float GetStarvationDamage()
        {
            return IsStarving ? starvingDamageRate : 0f;
        }

        /// <summary>
        /// Eat food to restore hunger.
        /// </summary>
        public void Eat(float amount)
        {
            if (amount <= 0) return;

            currentHunger = Mathf.Min(maxHunger, currentHunger + amount);
            lastReportedHunger = currentHunger;
            OnHungerChanged?.Invoke(currentHunger, maxHunger);

            // Check if stopped starving
            if (!IsStarving && wasStarving)
            {
                wasStarving = false;
                OnStoppedStarving?.Invoke();
            }
        }

        /// <summary>
        /// Reset hunger for respawn.
        /// </summary>
        public void Reset()
        {
            currentHunger = maxHunger;
            lastReportedHunger = currentHunger;
            wasStarving = false;
            OnHungerChanged?.Invoke(currentHunger, maxHunger);
        }

        /// <summary>
        /// Set hunger directly (for loading saves).
        /// </summary>
        public void SetHunger(float hunger)
        {
            currentHunger = Mathf.Clamp(hunger, 0, maxHunger);
            lastReportedHunger = currentHunger;
            wasStarving = IsStarving;
            OnHungerChanged?.Invoke(currentHunger, maxHunger);
        }
    }
}
