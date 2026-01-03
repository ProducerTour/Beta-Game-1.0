using UnityEngine;
using CreatorWorld.Core;
using CreatorWorld.Interfaces;

namespace CreatorWorld.Player
{
    /// <summary>
    /// Player health, hunger, and thirst system.
    /// Rust-style survival mechanics.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;
        [SerializeField] private float healthRegenRate = 1f; // per second when conditions met
        [SerializeField] private float healthRegenDelay = 5f; // seconds after taking damage

        [Header("Hunger")]
        [SerializeField] private float maxHunger = 100f;
        [SerializeField] private float currentHunger = 100f;
        [SerializeField] private float hungerDecayRate = 0.5f; // per second
        [SerializeField] private float starvingDamageRate = 2f; // damage per second when starving

        [Header("Thirst")]
        [SerializeField] private float maxThirst = 100f;
        [SerializeField] private float currentThirst = 100f;
        [SerializeField] private float thirstDecayRate = 0.8f; // per second
        [SerializeField] private float dehydrationDamageRate = 3f; // damage per second when dehydrated

        [Header("Stamina")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float currentStamina = 100f;
        [SerializeField] private float staminaRegenRate = 15f;
        [SerializeField] private float sprintStaminaCost = 10f; // per second
        [SerializeField] private float jumpStaminaCost = 15f;

        // State
        private float lastDamageTime;
        private bool isDead;

        // Events
        public delegate void HealthChanged(float current, float max);
        public delegate void StatChanged(float current, float max);
        public delegate void PlayerDied();

        public event HealthChanged OnHealthChanged;
        public event StatChanged OnHungerChanged;
        public event StatChanged OnThirstChanged;
        public event StatChanged OnStaminaChanged;
        public event PlayerDied OnDeath;

        // Properties
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public float HealthPercent => currentHealth / maxHealth;
        public float CurrentHunger => currentHunger;
        public float HungerPercent => currentHunger / maxHunger;
        public float CurrentThirst => currentThirst;
        public float ThirstPercent => currentThirst / maxThirst;
        public float CurrentStamina => currentStamina;
        public float StaminaPercent => currentStamina / maxStamina;
        public bool IsDead => isDead;
        public bool IsStarving => currentHunger <= 0;
        public bool IsDehydrated => currentThirst <= 0;

        private PlayerController playerController;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
        }

        private void Update()
        {
            if (isDead) return;

            UpdateHunger();
            UpdateThirst();
            UpdateStamina();
            UpdateHealthRegen();
            CheckSurvivalDamage();
        }

        #region Damage & Healing

        public void TakeDamage(float amount, DamageType type = DamageType.Generic)
        {
            if (isDead) return;

            currentHealth = Mathf.Max(0, currentHealth - amount);
            lastDamageTime = Time.time;

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            Debug.Log($"[PlayerHealth] Took {amount} {type} damage. Health: {currentHealth}/{maxHealth}");

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        public void Heal(float amount)
        {
            if (isDead) return;

            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        private void Die()
        {
            isDead = true;
            Debug.Log("[PlayerHealth] Player died!");

            // Trigger death animation
            var animation = GetComponent<PlayerAnimation>();
            animation?.TriggerDeath();

            // Change game state
            GameManager.Instance?.SetGameState(Interfaces.GameState.Dead);

            OnDeath?.Invoke();
        }

        public void Respawn(Vector3 position)
        {
            isDead = false;
            currentHealth = maxHealth;
            currentHunger = maxHunger;
            currentThirst = maxThirst;
            currentStamina = maxStamina;

            transform.position = position;

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnHungerChanged?.Invoke(currentHunger, maxHunger);
            OnThirstChanged?.Invoke(currentThirst, maxThirst);
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);

            GameManager.Instance?.SetGameState(Interfaces.GameState.Playing);
        }

        #endregion

        #region Survival Stats

        private void UpdateHunger()
        {
            currentHunger = Mathf.Max(0, currentHunger - hungerDecayRate * Time.deltaTime);
            OnHungerChanged?.Invoke(currentHunger, maxHunger);
        }

        private void UpdateThirst()
        {
            currentThirst = Mathf.Max(0, currentThirst - thirstDecayRate * Time.deltaTime);
            OnThirstChanged?.Invoke(currentThirst, maxThirst);
        }

        private void UpdateStamina()
        {
            bool isSprinting = playerController != null && playerController.IsSprinting;

            if (isSprinting)
            {
                currentStamina = Mathf.Max(0, currentStamina - sprintStaminaCost * Time.deltaTime);
            }
            else
            {
                // Regen stamina when not sprinting (and not exhausted from hunger/thirst)
                if (!IsStarving && !IsDehydrated)
                {
                    currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenRate * Time.deltaTime);
                }
            }

            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        }

        private void UpdateHealthRegen()
        {
            // Only regen health if well-fed and hydrated
            if (HungerPercent > 0.5f && ThirstPercent > 0.5f)
            {
                // Wait for delay after taking damage
                if (Time.time - lastDamageTime > healthRegenDelay)
                {
                    currentHealth = Mathf.Min(maxHealth, currentHealth + healthRegenRate * Time.deltaTime);
                    OnHealthChanged?.Invoke(currentHealth, maxHealth);
                }
            }
        }

        private void CheckSurvivalDamage()
        {
            // Take damage when starving
            if (IsStarving)
            {
                TakeDamage(starvingDamageRate * Time.deltaTime, DamageType.Starvation);
            }

            // Take damage when dehydrated
            if (IsDehydrated)
            {
                TakeDamage(dehydrationDamageRate * Time.deltaTime, DamageType.Dehydration);
            }
        }

        #endregion

        #region Consumables

        public void Eat(float amount)
        {
            currentHunger = Mathf.Min(maxHunger, currentHunger + amount);
            OnHungerChanged?.Invoke(currentHunger, maxHunger);
        }

        public void Drink(float amount)
        {
            currentThirst = Mathf.Min(maxThirst, currentThirst + amount);
            OnThirstChanged?.Invoke(currentThirst, maxThirst);
        }

        public bool UseStamina(float amount)
        {
            if (currentStamina < amount) return false;

            currentStamina -= amount;
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            return true;
        }

        #endregion
    }
}
