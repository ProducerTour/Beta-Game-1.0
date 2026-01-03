using UnityEngine;
using CreatorWorld.Core;
using CreatorWorld.Interfaces;

namespace CreatorWorld.Player.Survival
{
    /// <summary>
    /// Coordinates all survival subsystems (health, hunger, thirst, stamina).
    /// Slim coordinator that delegates to focused components.
    /// </summary>
    [RequireComponent(typeof(HealthSystem))]
    [RequireComponent(typeof(HungerSystem))]
    [RequireComponent(typeof(ThirstSystem))]
    [RequireComponent(typeof(StaminaSystem))]
    public class SurvivalManager : MonoBehaviour, IDamageable
    {
        // Subsystems
        private HealthSystem health;
        private HungerSystem hunger;
        private ThirstSystem thirst;
        private StaminaSystem stamina;

        // References
        private PlayerController playerController;
        private PlayerAnimation playerAnimation;

        // IDamageable implementation
        public float CurrentHealth => health?.CurrentHealth ?? 0;
        public float MaxHealth => health?.MaxHealth ?? 100;
        public bool IsDead => health?.IsDead ?? false;

        // Aggregate properties
        public float HealthPercent => health?.HealthPercent ?? 0;
        public float HungerPercent => hunger?.HungerPercent ?? 0;
        public float ThirstPercent => thirst?.ThirstPercent ?? 0;
        public float StaminaPercent => stamina?.StaminaPercent ?? 0;
        public bool IsStarving => hunger?.IsStarving ?? false;
        public bool IsDehydrated => thirst?.IsDehydrated ?? false;
        public bool IsExhausted => stamina?.IsExhausted ?? false;

        // Forward events from subsystems
        public event System.Action<float, float> OnHealthChanged;
        public event System.Action<float, float> OnHungerChanged;
        public event System.Action<float, float> OnThirstChanged;
        public event System.Action<float, float> OnStaminaChanged;
        public event System.Action OnDeath;

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
            hunger = GetComponent<HungerSystem>();
            thirst = GetComponent<ThirstSystem>();
            stamina = GetComponent<StaminaSystem>();

            playerController = GetComponent<PlayerController>();
            playerAnimation = GetComponent<PlayerAnimation>();

            // Subscribe to subsystem events
            if (health != null)
            {
                health.OnHealthChanged += (c, m) => OnHealthChanged?.Invoke(c, m);
                health.OnDeath += HandleDeath;
            }
            if (hunger != null)
            {
                hunger.OnHungerChanged += (c, m) => OnHungerChanged?.Invoke(c, m);
            }
            if (thirst != null)
            {
                thirst.OnThirstChanged += (c, m) => OnThirstChanged?.Invoke(c, m);
            }
            if (stamina != null)
            {
                stamina.OnStaminaChanged += (c, m) => OnStaminaChanged?.Invoke(c, m);
            }
        }

        private void Update()
        {
            if (IsDead) return;

            float dt = Time.deltaTime;

            // Process decay
            hunger?.ProcessDecay(dt);
            thirst?.ProcessDecay(dt);

            // Sprint stamina
            bool isSprinting = playerController != null && playerController.IsSprinting;
            stamina?.ProcessSprintDrain(isSprinting, dt);

            // Update regen conditions
            bool canRegen = hunger != null && hunger.IsWellFed &&
                           thirst != null && thirst.IsHydrated;
            health?.SetCanRegenerate(canRegen);
            stamina?.SetCanRegenerate(!IsStarving && !IsDehydrated);

            // Health regen
            if (canRegen)
            {
                health?.ProcessRegen(dt);
            }

            // Survival damage
            ApplySurvivalDamage(dt);
        }

        private void ApplySurvivalDamage(float dt)
        {
            float starvationDmg = hunger?.GetStarvationDamage() ?? 0;
            float dehydrationDmg = thirst?.GetDehydrationDamage() ?? 0;

            if (starvationDmg > 0)
            {
                health?.TakeDamage(starvationDmg * dt, DamageType.Starvation);
            }
            if (dehydrationDmg > 0)
            {
                health?.TakeDamage(dehydrationDmg * dt, DamageType.Dehydration);
            }
        }

        private void HandleDeath()
        {
            playerAnimation?.TriggerDeath();

            var gameState = ServiceLocator.Get<IGameStateService>();
            gameState?.SetGameState(Interfaces.GameState.Dead);

            OnDeath?.Invoke();
        }

        // IDamageable
        public void TakeDamage(float amount, DamageType type = DamageType.Generic)
        {
            health?.TakeDamage(amount, type);
        }

        public void Heal(float amount)
        {
            health?.Heal(amount);
        }

        // Consumables
        public void Eat(float amount) => hunger?.Eat(amount);
        public void Drink(float amount) => thirst?.Drink(amount);
        public bool UseStamina(float amount) => stamina?.TryUseStamina(amount) ?? false;

        // Respawn
        public void Respawn(Vector3 position)
        {
            health?.Reset();
            hunger?.Reset();
            thirst?.Reset();
            stamina?.Reset();

            transform.position = position;

            var gameState = ServiceLocator.Get<IGameStateService>();
            gameState?.SetGameState(Interfaces.GameState.Playing);
        }
    }
}
