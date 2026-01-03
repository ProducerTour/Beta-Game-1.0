using UnityEngine;
using CreatorWorld.Interfaces;

namespace CreatorWorld.Items
{
    /// <summary>
    /// Weapon firing modes.
    /// </summary>
    public enum FireMode
    {
        SemiAuto,
        Automatic,
        Burst,
        BoltAction
    }

    /// <summary>
    /// ScriptableObject defining weapon stats.
    /// Used to configure weapon prefabs and spawn weapons from data.
    /// </summary>
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "Creator World/Items/Weapon")]
    public class WeaponData : ItemData
    {
        [Header("Weapon Type")]
        public WeaponType weaponType = WeaponType.Rifle;
        public FireMode fireMode = FireMode.Automatic;

        [Header("Damage")]
        [Range(1, 500)]
        public float damage = 25f;

        [Range(1, 5)]
        public float headshotMultiplier = 2f;

        [Range(1, 500)]
        public float range = 100f;

        [Header("Fire Rate")]
        [Tooltip("Rounds per minute")]
        [Range(30, 1200)]
        public int rpm = 600;

        [Tooltip("For burst mode - shots per burst")]
        [Range(1, 10)]
        public int burstCount = 3;

        [Header("Magazine")]
        [Range(1, 200)]
        public int magazineSize = 30;

        [Range(0.5f, 5f)]
        public float reloadTime = 2f;

        [Header("Ammo")]
        [Tooltip("What ammo type this weapon uses")]
        public AmmoData ammoType;

        [Header("Accuracy")]
        [Tooltip("Base spread in degrees")]
        [Range(0, 10)]
        public float baseSpread = 1f;

        [Tooltip("Spread when aiming down sights")]
        [Range(0, 5)]
        public float adsSpread = 0.2f;

        [Tooltip("Additional spread when moving")]
        [Range(0, 5)]
        public float moveSpread = 1.5f;

        [Header("Recoil")]
        [Range(0, 10)]
        public float recoilVertical = 1.5f;

        [Range(0, 5)]
        public float recoilHorizontal = 0.5f;

        [Range(1, 20)]
        public float recoilRecovery = 5f;

        [Header("ADS (Aim Down Sights)")]
        [Range(20, 70)]
        public float adsFOV = 45f;

        [Range(1, 20)]
        public float adsSpeed = 10f;

        [Header("Audio")]
        public AudioClip fireSound;
        public AudioClip reloadSound;
        public AudioClip emptySound;
        public AudioClip equipSound;

        [Header("Visual Effects")]
        public GameObject muzzleFlashPrefab;
        public GameObject bulletTrailPrefab;
        public GameObject impactEffectPrefab;

        /// <summary>
        /// Calculate fire rate in shots per second.
        /// </summary>
        public float FireRate => rpm / 60f;

        /// <summary>
        /// Calculate time between shots.
        /// </summary>
        public float TimeBetweenShots => 60f / rpm;

        protected override void OnValidate()
        {
            base.OnValidate();

            // Weapons are not stackable
            isStackable = false;
            maxStackSize = 1;
            category = ItemCategory.Weapon;

            // Auto-generate ID
            if (string.IsNullOrEmpty(itemId) && !string.IsNullOrEmpty(displayName))
            {
                itemId = "weapon_" + displayName.ToLower().Replace(" ", "_").Replace("-", "");
            }
        }
    }
}
