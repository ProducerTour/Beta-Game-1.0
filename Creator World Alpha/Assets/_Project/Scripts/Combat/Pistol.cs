using UnityEngine;
using CreatorWorld.Player;
using CreatorWorld.Interfaces;

namespace CreatorWorld.Combat
{
    /// <summary>
    /// Semi-automatic pistol - single shot, high accuracy, moderate damage.
    /// Secondary/backup weapon.
    /// </summary>
    public class Pistol : WeaponBase
    {
        [Header("Pistol Settings")]
        [SerializeField] private float quickDrawTime = 0.3f;
        [SerializeField] private bool allowFanFire = true;
        [SerializeField] private float fanFireSpreadMultiplier = 2f;

        [Header("ADS Settings")]
        [SerializeField] private float adsZoomFOV = 55f;
        [SerializeField] private float adsTransitionSpeed = 15f;

        // Fan fire state (rapid clicking)
        private float lastClickTime;
        private int rapidClicks;
        private const float RAPID_CLICK_WINDOW = 0.3f;

        protected override void Awake()
        {
            base.Awake();

            // Default pistol stats
            weaponName = "Pistol";
            weaponType = Interfaces.WeaponType.Pistol;
            baseDamage = 35f;
            headshotMultiplier = 3f; // Higher headshot bonus for precision
            range = 50f;

            fireRate = 5f; // 300 RPM max
            isAutomatic = false; // Semi-auto

            magazineSize = 12;
            currentAmmo = 12;
            reserveAmmo = 48;
            reloadTime = 1.5f; // Faster reload

            recoilVertical = 2f; // Higher per-shot recoil
            recoilHorizontal = 0.8f;
            recoilRecovery = 8f; // Faster recovery

            baseSpread = 0.3f; // More accurate base
            adsSpread = 0.05f; // Very accurate when aimed
            moveSpread = 0.8f;
        }

        protected override void Update()
        {
            base.Update();

            // Reset rapid click counter after window expires
            if (Time.time - lastClickTime > RAPID_CLICK_WINDOW)
            {
                rapidClicks = 0;
            }
        }

        public override bool TryFire()
        {
            // Track rapid clicks for fan fire mechanic
            if (allowFanFire)
            {
                float timeSinceLastClick = Time.time - lastClickTime;
                if (timeSinceLastClick < RAPID_CLICK_WINDOW)
                {
                    rapidClicks++;
                }
                else
                {
                    rapidClicks = 1;
                }
                lastClickTime = Time.time;
            }

            return base.TryFire();
        }

        protected override void Fire()
        {
            base.Fire();

            // Pistol-specific effects
        }

        protected override float GetCurrentSpread()
        {
            float spread = base.GetCurrentSpread();

            // Fan fire increases spread significantly
            if (allowFanFire && rapidClicks > 2)
            {
                spread *= fanFireSpreadMultiplier * (1 + (rapidClicks - 2) * 0.5f);
            }

            return spread;
        }

        protected override void OnHit(RaycastHit hit)
        {
            base.OnHit(hit);

            // Pistol-specific hit effects
            // Could add special execution damage at close range
            float distance = Vector3.Distance(GetFireOrigin(), hit.point);
            if (distance < 5f)
            {
                // Close range bonus damage
                // TODO: Apply bonus damage
            }
        }

        /// <summary>
        /// Quick draw - faster weapon switch.
        /// </summary>
        public float GetQuickDrawTime() => quickDrawTime;

        /// <summary>
        /// Get ADS zoom parameters for camera.
        /// </summary>
        public float GetADSZoomFOV() => adsZoomFOV;
        public float GetADSTransitionSpeed() => adsTransitionSpeed;
    }
}
