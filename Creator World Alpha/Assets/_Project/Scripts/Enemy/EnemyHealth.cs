using System;
using UnityEngine;
using CreatorWorld.Interfaces;
using CreatorWorld.Config;

namespace CreatorWorld.Enemy
{
    /// <summary>
    /// Enemy health component implementing IDamageable.
    /// AAA Pattern: Consistent damage interface across all damageable entities.
    /// This allows weapons to damage any IDamageable without knowing the specific type.
    ///
    /// Events are fired for hit feedback systems to react to damage.
    /// </summary>
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        [Header("Configuration")]
        [Tooltip("Enemy configuration asset")]
        [SerializeField] private EnemyConfig config;

        [Header("Debug")]
        [SerializeField] private bool showDamageLog = false;

        // State
        private float currentHealth;
        private float lastDamageTime;
        private bool isDead;

        // Events - AAA Pattern: Event-driven feedback allows multiple systems to react
        /// <summary>
        /// Fired when damage is received. Parameters: damage, damageType, hitPoint, hitDirection, isHeadshot
        /// </summary>
        public event Action<float, DamageType, Vector3, Vector3, bool> OnDamageReceived;

        /// <summary>
        /// Fired when health changes. Parameters: currentHealth, maxHealth
        /// </summary>
        public event Action<float, float> OnHealthChanged;

        /// <summary>
        /// Fired when the enemy dies.
        /// </summary>
        public event Action OnDeath;

        /// <summary>
        /// Fired when healed. Parameters: amount
        /// </summary>
        public event Action<float> OnHealed;

        // IDamageable implementation
        public float CurrentHealth => currentHealth;
        public float MaxHealth => config != null ? config.MaxHealth : 100f;
        public bool IsDead => isDead;

        // Additional properties
        public EnemyConfig Config => config;

        private void Awake()
        {
            if (config == null)
            {
                Debug.LogWarning($"[EnemyHealth] No EnemyConfig assigned to {gameObject.name}. Using default values.");
            }

            currentHealth = MaxHealth;
        }

        private void Update()
        {
            // Health regeneration (if configured)
            if (!isDead && config != null && config.HealthRegen > 0)
            {
                if (Time.time >= lastDamageTime + config.RegenDelay)
                {
                    if (currentHealth < MaxHealth)
                    {
                        float healAmount = config.HealthRegen * Time.deltaTime;
                        Heal(healAmount);
                    }
                }
            }
        }

        #region IDamageable Implementation

        /// <summary>
        /// Take damage from any source. Implements IDamageable.
        /// For hitbox-aware damage, use TakeDamageFromHitbox instead.
        /// </summary>
        public void TakeDamage(float amount, DamageType type = DamageType.Generic)
        {
            TakeDamageInternal(amount, type, transform.position, Vector3.zero, false);
        }

        /// <summary>
        /// Heal the enemy. Implements IDamageable.
        /// </summary>
        public void Heal(float amount)
        {
            if (isDead || amount <= 0) return;

            float previousHealth = currentHealth;
            currentHealth = Mathf.Min(currentHealth + amount, MaxHealth);

            if (currentHealth != previousHealth)
            {
                OnHealed?.Invoke(amount);
                OnHealthChanged?.Invoke(currentHealth, MaxHealth);
            }
        }

        #endregion

        #region Damage Methods

        /// <summary>
        /// Take damage from a specific hitbox. Called by EnemyHitbox.
        /// Provides full context for hit feedback systems.
        /// </summary>
        /// <param name="damage">Final damage after hitbox multiplier</param>
        /// <param name="type">Type of damage</param>
        /// <param name="hitPoint">World position of the hit</param>
        /// <param name="hitDirection">Direction the damage came from</param>
        /// <param name="hitboxType">Which hitbox was hit</param>
        public void TakeDamageFromHitbox(float damage, DamageType type, Vector3 hitPoint, Vector3 hitDirection, HitboxType hitboxType)
        {
            bool isHeadshot = hitboxType == HitboxType.Head;
            TakeDamageInternal(damage, type, hitPoint, hitDirection, isHeadshot);
        }

        /// <summary>
        /// Internal damage handling with full context.
        /// </summary>
        private void TakeDamageInternal(float damage, DamageType type, Vector3 hitPoint, Vector3 hitDirection, bool isHeadshot)
        {
            if (isDead || damage <= 0) return;

            currentHealth = Mathf.Max(0, currentHealth - damage);
            lastDamageTime = Time.time;

            if (showDamageLog)
            {
                string headshotText = isHeadshot ? " [HEADSHOT]" : "";
                Debug.Log($"[EnemyHealth] {gameObject.name} took {damage:F1} {type} damage{headshotText}. " +
                    $"Health: {currentHealth:F1}/{MaxHealth}");
            }

            // Fire events for feedback systems
            OnDamageReceived?.Invoke(damage, type, hitPoint, hitDirection, isHeadshot);
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);

            // Check for death
            if (currentHealth <= 0)
            {
                Die();
            }
        }

        /// <summary>
        /// Handle enemy death.
        /// </summary>
        private void Die()
        {
            if (isDead) return;

            isDead = true;

            if (showDamageLog)
            {
                Debug.Log($"[EnemyHealth] {gameObject.name} has died!");
            }

            OnDeath?.Invoke();

            // Note: Don't destroy here - let other systems (ragdoll, loot, etc.) handle cleanup
            // The EnemyController or a death handler will manage what happens next
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Set the enemy config and reinitialize health.
        /// Use this instead of reflection when spawning enemies.
        /// </summary>
        public void SetConfig(EnemyConfig newConfig)
        {
            config = newConfig;
            currentHealth = MaxHealth;
            isDead = false;
            lastDamageTime = 0f;
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        }

        /// <summary>
        /// Reset health to maximum. Used for respawning or pooled enemies.
        /// </summary>
        public void ResetHealth()
        {
            currentHealth = MaxHealth;
            isDead = false;
            lastDamageTime = 0f;
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        }

        /// <summary>
        /// Set health to a specific value. Useful for spawning damaged enemies.
        /// </summary>
        public void SetHealth(float health)
        {
            currentHealth = Mathf.Clamp(health, 0f, MaxHealth);
            isDead = currentHealth <= 0;
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        }

        /// <summary>
        /// Get health as a normalized 0-1 value.
        /// </summary>
        public float GetHealthNormalized()
        {
            return MaxHealth > 0 ? currentHealth / MaxHealth : 0f;
        }

        #endregion

        #region Editor Helpers

        private void OnValidate()
        {
            // Ensure current health doesn't exceed max when config changes
            if (Application.isPlaying && config != null)
            {
                currentHealth = Mathf.Min(currentHealth, MaxHealth);
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw health bar above enemy in scene view
            Vector3 barPos = transform.position + Vector3.up * 2.5f;
            float healthPercent = Application.isPlaying ? GetHealthNormalized() : 1f;

            Gizmos.color = Color.Lerp(Color.red, Color.green, healthPercent);
            Gizmos.DrawCube(barPos, new Vector3(healthPercent, 0.1f, 0.1f));

            Gizmos.color = Color.gray;
            Gizmos.DrawWireCube(barPos, new Vector3(1f, 0.1f, 0.1f));
        }

        #endregion
    }
}
