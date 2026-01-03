using UnityEngine;
using CreatorWorld.Player;
using CreatorWorld.Interfaces;

namespace CreatorWorld.Combat
{
    /// <summary>
    /// Assault rifle weapon - automatic fire, medium damage, medium accuracy.
    /// Primary weapon for combat situations.
    /// </summary>
    public class Rifle : WeaponBase
    {
        [Header("Rifle Settings")]
        [SerializeField] private bool burstMode = false;
        [SerializeField] private int burstCount = 3;
        [SerializeField] private float burstDelay = 0.05f;

        [Header("ADS Settings")]
        [SerializeField] private float adsZoomFOV = 45f;
        [SerializeField] private float adsTransitionSpeed = 10f;

        // Burst fire state
        private int burstShotsFired;
        private float nextBurstShotTime;
        private bool isBursting;

        protected override void Awake()
        {
            base.Awake();

            // Default rifle stats
            weaponName = "Assault Rifle";
            weaponType = Interfaces.WeaponType.Rifle;
            baseDamage = 25f;
            headshotMultiplier = 2.5f;
            range = 150f;

            fireRate = 10f; // 600 RPM
            isAutomatic = true;

            magazineSize = 30;
            currentAmmo = 30;
            reserveAmmo = 120;
            reloadTime = 2.2f;

            recoilVertical = 1.2f;
            recoilHorizontal = 0.4f;
            recoilRecovery = 6f;

            baseSpread = 0.8f;
            adsSpread = 0.15f;
            moveSpread = 1.5f;
        }

        protected override void Update()
        {
            base.Update();

            // Handle burst fire
            if (isBursting && Time.time >= nextBurstShotTime)
            {
                ContinueBurst();
            }
        }

        public override bool TryFire()
        {
            if (burstMode && !isBursting)
            {
                return TryStartBurst();
            }

            return base.TryFire();
        }

        private bool TryStartBurst()
        {
            if (!CanFire) return false;

            isBursting = true;
            burstShotsFired = 0;
            ContinueBurst();
            return true;
        }

        private void ContinueBurst()
        {
            if (burstShotsFired >= burstCount || currentAmmo <= 0)
            {
                isBursting = false;
                return;
            }

            Fire();
            burstShotsFired++;
            nextBurstShotTime = Time.time + burstDelay;
        }

        protected override void Fire()
        {
            base.Fire();

            // Additional rifle-specific effects
            // Camera shake, etc.
        }

        protected override void OnHit(RaycastHit hit)
        {
            base.OnHit(hit);

            // Rifle-specific hit effects
            // Penetration check, etc.

            // Check for penetration on thin surfaces
            if (hit.collider.bounds.size.magnitude < 0.5f)
            {
                // TODO: Implement bullet penetration
            }
        }

        /// <summary>
        /// Toggle between auto and burst fire modes.
        /// </summary>
        public void ToggleFireMode()
        {
            burstMode = !burstMode;
            Debug.Log($"[Rifle] Fire mode: {(burstMode ? "BURST" : "AUTO")}");
        }

        /// <summary>
        /// Get ADS zoom parameters for camera.
        /// </summary>
        public float GetADSZoomFOV() => adsZoomFOV;
        public float GetADSTransitionSpeed() => adsTransitionSpeed;
    }
}
