using UnityEngine;

namespace CreatorWorld.Items
{
    /// <summary>
    /// Types of consumable effects.
    /// </summary>
    public enum ConsumableType
    {
        Food,           // Restores hunger
        Water,          // Restores thirst
        Medical,        // Restores health
        Stimulant,      // Temporary buffs (stamina, speed)
        Radiation       // Anti-radiation (future feature)
    }

    /// <summary>
    /// ScriptableObject defining consumable items (food, water, meds).
    /// Integrates with SurvivalManager for stat restoration.
    /// </summary>
    [CreateAssetMenu(fileName = "NewConsumable", menuName = "Creator World/Items/Consumable")]
    public class ConsumableData : ItemData
    {
        [Header("Consumable Type")]
        public ConsumableType consumableType = ConsumableType.Food;

        [Header("Restoration Values")]
        [Tooltip("Health restored (0-100)")]
        [Range(0, 100)]
        public float healthRestore = 0f;

        [Tooltip("Hunger restored (0-100)")]
        [Range(0, 100)]
        public float hungerRestore = 0f;

        [Tooltip("Thirst restored (0-100)")]
        [Range(0, 100)]
        public float thirstRestore = 0f;

        [Tooltip("Stamina restored (0-100)")]
        [Range(0, 100)]
        public float staminaRestore = 0f;

        [Header("Consumption")]
        [Tooltip("Time to consume in seconds")]
        [Range(0.1f, 10f)]
        public float consumeTime = 1f;

        [Tooltip("Can be consumed while moving?")]
        public bool canUseWhileMoving = true;

        [Header("Side Effects")]
        [Tooltip("Health drain (negative values = poison)")]
        [Range(-50, 0)]
        public float healthDrain = 0f;

        [Tooltip("Radiation added (future feature)")]
        [Range(0, 50)]
        public float radiationAdded = 0f;

        [Header("Buffs (Duration in seconds, 0 = no buff)")]
        [Tooltip("Movement speed multiplier buff")]
        public float speedBuffMultiplier = 1f;
        public float speedBuffDuration = 0f;

        [Tooltip("Damage resistance buff")]
        [Range(0, 0.5f)]
        public float damageResistBuff = 0f;
        public float damageResistDuration = 0f;

        [Header("Audio")]
        public AudioClip consumeSound;

        public override bool Use(GameObject user)
        {
            // Find survival systems on user
            var health = user.GetComponent<CreatorWorld.Player.Survival.HealthSystem>();
            var hunger = user.GetComponent<CreatorWorld.Player.Survival.HungerSystem>();
            var thirst = user.GetComponent<CreatorWorld.Player.Survival.ThirstSystem>();
            var stamina = user.GetComponent<CreatorWorld.Player.Survival.StaminaSystem>();

            bool consumed = false;

            // Apply restoration
            if (healthRestore > 0 && health != null)
            {
                health.Heal(healthRestore);
                consumed = true;
            }

            if (hungerRestore > 0 && hunger != null)
            {
                hunger.Eat(hungerRestore);
                consumed = true;
            }

            if (thirstRestore > 0 && thirst != null)
            {
                thirst.Drink(thirstRestore);
                consumed = true;
            }

            if (staminaRestore > 0 && stamina != null)
            {
                // Add to current stamina (clamped by SetStamina)
                stamina.SetStamina(stamina.CurrentStamina + staminaRestore);
                consumed = true;
            }

            // Apply health drain (poison)
            if (healthDrain < 0 && health != null)
            {
                health.TakeDamage(-healthDrain, Interfaces.DamageType.Poison);
            }

            // TODO: Apply buffs via BuffSystem

            if (consumed)
            {
                Debug.Log($"Consumed {displayName}: +{healthRestore}HP, +{hungerRestore}Hunger, +{thirstRestore}Thirst");
            }

            return consumed;
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            isStackable = true;
            category = ItemCategory.Consumable;

            // Auto-set restoration based on type
            if (consumableType == ConsumableType.Food && hungerRestore == 0)
            {
                hungerRestore = 25f;
            }
            else if (consumableType == ConsumableType.Water && thirstRestore == 0)
            {
                thirstRestore = 25f;
            }
            else if (consumableType == ConsumableType.Medical && healthRestore == 0)
            {
                healthRestore = 25f;
            }
        }
    }
}
