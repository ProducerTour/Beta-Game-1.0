using UnityEngine;

namespace CreatorWorld.Items
{
    /// <summary>
    /// Ammo caliber types.
    /// </summary>
    public enum AmmoCaliber
    {
        Pistol_9mm,
        Rifle_556,
        Rifle_762,
        Shotgun_12Gauge,
        Sniper_50BMG
    }

    /// <summary>
    /// ScriptableObject defining ammo types.
    /// Weapons reference this to determine compatible ammo.
    /// </summary>
    [CreateAssetMenu(fileName = "NewAmmo", menuName = "Creator World/Items/Ammo")]
    public class AmmoData : ItemData
    {
        [Header("Ammo Properties")]
        public AmmoCaliber caliber = AmmoCaliber.Rifle_762;

        [Tooltip("Damage modifier applied to weapon base damage")]
        [Range(0.5f, 2f)]
        public float damageModifier = 1f;

        [Tooltip("Penetration modifier (affects armor)")]
        [Range(0f, 1f)]
        public float penetration = 0.3f;

        [Header("Special Properties")]
        [Tooltip("Is this tracer ammo? (visible bullet path)")]
        public bool isTracer = false;

        [Tooltip("Is this incendiary ammo? (fire damage)")]
        public bool isIncendiary = false;

        [Tooltip("Is this explosive ammo? (area damage)")]
        public bool isExplosive = false;

        protected override void OnValidate()
        {
            base.OnValidate();

            // Ammo is always stackable
            isStackable = true;
            category = ItemCategory.Ammo;

            // Set default stack sizes based on caliber
            if (maxStackSize == 99)
            {
                maxStackSize = caliber switch
                {
                    AmmoCaliber.Pistol_9mm => 120,
                    AmmoCaliber.Rifle_556 => 90,
                    AmmoCaliber.Rifle_762 => 60,
                    AmmoCaliber.Shotgun_12Gauge => 32,
                    AmmoCaliber.Sniper_50BMG => 20,
                    _ => 60
                };
            }

            // Auto-generate ID
            if (string.IsNullOrEmpty(itemId))
            {
                itemId = "ammo_" + caliber.ToString().ToLower();
            }
        }
    }
}
