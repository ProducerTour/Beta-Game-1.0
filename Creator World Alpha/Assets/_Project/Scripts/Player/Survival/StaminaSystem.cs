using System;
using UnityEngine;

namespace CreatorWorld.Player.Survival
{
    /// <summary>
    /// Manages player stamina for sprinting and other actions.
    /// </summary>
    public class StaminaSystem : MonoBehaviour
    {
        [Header("Stamina Settings")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float staminaRegenRate = 15f;
        [SerializeField] private float sprintDrainRate = 10f;
        [SerializeField] private float jumpCost = 15f;

        [Header("Event Throttling")]
        [SerializeField] private float eventThreshold = 2f; // Only fire event if changed by this amount

        // State
        private float currentStamina;
        private float lastReportedStamina;
        private bool canRegen = true;

        // Properties
        public float CurrentStamina => currentStamina;
        public float MaxStamina => maxStamina;
        public float StaminaPercent => currentStamina / maxStamina;
        public bool IsExhausted => currentStamina <= 0;
        public bool CanSprint => currentStamina > 0;
        public bool CanJump => currentStamina >= jumpCost;

        // Events (throttled)
        public event Action<float, float> OnStaminaChanged; // current, max
        public event Action OnExhausted;
        public event Action OnRecovered;

        private bool wasExhausted;

        private void Awake()
        {
            currentStamina = maxStamina;
            lastReportedStamina = currentStamina;
        }

        /// <summary>
        /// Process stamina drain for sprinting. Call from SurvivalManager.
        /// </summary>
        public void ProcessSprintDrain(bool isSprinting, float deltaTime)
        {
            if (isSprinting)
            {
                DrainStamina(sprintDrainRate * deltaTime);
            }
            else if (canRegen)
            {
                RegenerateStamina(staminaRegenRate * deltaTime);
            }
        }

        /// <summary>
        /// Use stamina for jumping.
        /// </summary>
        public bool TryUseJumpStamina()
        {
            if (currentStamina < jumpCost) return false;

            DrainStamina(jumpCost);
            return true;
        }

        /// <summary>
        /// Use a specific amount of stamina.
        /// </summary>
        public bool TryUseStamina(float amount)
        {
            if (currentStamina < amount) return false;

            DrainStamina(amount);
            return true;
        }

        private void DrainStamina(float amount)
        {
            float previousStamina = currentStamina;
            currentStamina = Mathf.Max(0, currentStamina - amount);

            // Check exhaustion state
            if (IsExhausted && !wasExhausted)
            {
                wasExhausted = true;
                OnExhausted?.Invoke();
            }

            // Throttled event
            if (Mathf.Abs(currentStamina - lastReportedStamina) >= eventThreshold)
            {
                lastReportedStamina = currentStamina;
                OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            }
        }

        private void RegenerateStamina(float amount)
        {
            if (currentStamina >= maxStamina) return;

            float previousStamina = currentStamina;
            currentStamina = Mathf.Min(maxStamina, currentStamina + amount);

            // Check recovery state
            if (!IsExhausted && wasExhausted)
            {
                wasExhausted = false;
                OnRecovered?.Invoke();
            }

            // Throttled event
            if (Mathf.Abs(currentStamina - lastReportedStamina) >= eventThreshold)
            {
                lastReportedStamina = currentStamina;
                OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            }
        }

        /// <summary>
        /// Set whether stamina can regenerate (disabled when starving/dehydrated).
        /// </summary>
        public void SetCanRegenerate(bool canRegen)
        {
            this.canRegen = canRegen;
        }

        /// <summary>
        /// Reset stamina for respawn.
        /// </summary>
        public void Reset()
        {
            currentStamina = maxStamina;
            lastReportedStamina = currentStamina;
            wasExhausted = false;
            canRegen = true;
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        }

        /// <summary>
        /// Set stamina directly (for loading saves).
        /// </summary>
        public void SetStamina(float stamina)
        {
            currentStamina = Mathf.Clamp(stamina, 0, maxStamina);
            lastReportedStamina = currentStamina;
            wasExhausted = IsExhausted;
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        }
    }
}
