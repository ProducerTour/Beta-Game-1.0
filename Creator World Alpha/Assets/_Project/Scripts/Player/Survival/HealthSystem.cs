using System;
using UnityEngine;
using CreatorWorld.Interfaces;

namespace CreatorWorld.Player.Survival
{
    /// <summary>
    /// Manages player health, damage, healing, and regeneration.
    /// </summary>
    public class HealthSystem : MonoBehaviour, IDamageable
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float healthRegenRate = 1f;
        [SerializeField] private float healthRegenDelay = 5f;

        // State
        private float currentHealth;
        private float lastDamageTime;
        private bool isDead;
        private bool canRegen = true;

        // Properties
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public float HealthPercent => currentHealth / maxHealth;
        public bool IsDead => isDead;
        public bool CanRegenerate => canRegen && Time.time - lastDamageTime > healthRegenDelay;

        // Events (fire on change, not every frame)
        public event Action<float, float> OnHealthChanged; // current, max
        public event Action<float, DamageType> OnDamaged; // amount, type
        public event Action<float> OnHealed; // amount
        public event Action OnDeath;

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        /// <summary>
        /// Apply damage to the player.
        /// </summary>
        public void TakeDamage(float amount, DamageType type = DamageType.Generic)
        {
            if (isDead || amount <= 0) return;

            float previousHealth = currentHealth;
            currentHealth = Mathf.Max(0, currentHealth - amount);
            lastDamageTime = Time.time;

            if (currentHealth != previousHealth)
            {
                OnDamaged?.Invoke(amount, type);
                OnHealthChanged?.Invoke(currentHealth, maxHealth);
            }

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        /// <summary>
        /// Heal the player.
        /// </summary>
        public void Heal(float amount)
        {
            if (isDead || amount <= 0) return;

            float previousHealth = currentHealth;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);

            if (currentHealth != previousHealth)
            {
                OnHealed?.Invoke(currentHealth - previousHealth);
                OnHealthChanged?.Invoke(currentHealth, maxHealth);
            }
        }

        /// <summary>
        /// Process health regeneration. Call from SurvivalManager.
        /// </summary>
        public void ProcessRegen(float deltaTime)
        {
            if (!CanRegenerate || currentHealth >= maxHealth) return;

            float regenAmount = healthRegenRate * deltaTime;
            Heal(regenAmount);
        }

        /// <summary>
        /// Set whether regen is allowed (disabled when starving/dehydrated).
        /// </summary>
        public void SetCanRegenerate(bool canRegen)
        {
            this.canRegen = canRegen;
        }

        private void Die()
        {
            if (isDead) return;

            isDead = true;
            OnDeath?.Invoke();
        }

        /// <summary>
        /// Reset health for respawn.
        /// </summary>
        public void Reset()
        {
            currentHealth = maxHealth;
            isDead = false;
            lastDamageTime = 0f;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        /// <summary>
        /// Set health directly (for loading saves).
        /// </summary>
        public void SetHealth(float health)
        {
            currentHealth = Mathf.Clamp(health, 0, maxHealth);
            isDead = currentHealth <= 0;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }
}
