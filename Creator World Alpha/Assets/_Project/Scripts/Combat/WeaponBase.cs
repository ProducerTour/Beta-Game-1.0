using System;
using UnityEngine;
using CreatorWorld.Player;
using CreatorWorld.Interfaces;
using CreatorWorld.Enemy;

namespace CreatorWorld.Combat
{
    /// <summary>
    /// Base class for all weapons.
    /// Handles firing, reloading, ammo, and damage.
    /// </summary>
    public abstract class WeaponBase : MonoBehaviour
    {
        [Header("Weapon Info")]
        [SerializeField] protected string weaponName = "Weapon";
        [SerializeField] protected Interfaces.WeaponType weaponType = Interfaces.WeaponType.Rifle;
        [SerializeField] protected Sprite weaponIcon;

        [Header("Damage")]
        [SerializeField] protected float baseDamage = 25f;
        [SerializeField] protected float headshotMultiplier = 2f;
        [SerializeField] protected float range = 100f;

        [Header("Fire Rate")]
        [SerializeField] protected float fireRate = 10f; // rounds per second
        [SerializeField] protected bool isAutomatic = true;

        [Header("Ammo")]
        [SerializeField] protected int magazineSize = 30;
        [SerializeField] protected int currentAmmo = 30;
        [SerializeField] protected int reserveAmmo = 90;
        [SerializeField] protected float reloadTime = 2f;

        [Header("Recoil")]
        [SerializeField] protected float recoilVertical = 1f;
        [SerializeField] protected float recoilHorizontal = 0.5f;
        [SerializeField] protected float recoilRecovery = 5f;

        [Header("Spread")]
        [SerializeField] protected float baseSpread = 0.5f; // degrees
        [SerializeField] protected float adsSpread = 0.1f;
        [SerializeField] protected float moveSpread = 1f;

        [Header("Audio")]
        [Tooltip("Main gunshot sound(s). Multiple clips will be randomized.")]
        [SerializeField] protected AudioClip[] fireSounds;
        [Tooltip("Fire tail/echo sound for realism. Plays shortly after main shot.")]
        [SerializeField] protected AudioClip fireTailSound;
        [Tooltip("Delay before tail sound plays (seconds)")]
        [SerializeField] protected float fireTailDelay = 0.05f;
        [Tooltip("Volume multiplier for tail sound")]
        [SerializeField, Range(0f, 1f)] protected float fireTailVolume = 0.6f;
        [SerializeField] protected AudioClip reloadSound;
        [SerializeField] protected AudioClip emptySound;

        [Header("Audio (Legacy - use fireSounds array instead)")]
        [SerializeField] protected AudioClip fireSound; // Kept for backwards compatibility

        [Header("Effects")]
        [SerializeField] protected Transform muzzlePoint;
        [SerializeField] protected ParticleSystem muzzleFlash;

        [Header("Bullet Tracer")]
        [Tooltip("Tracer prefab with BulletTracer component and TrailRenderer")]
        [SerializeField] protected GameObject bulletTracerPrefab;
        [Tooltip("Legacy line renderer trail (used if no tracer prefab assigned)")]
        [SerializeField] protected GameObject bulletTrailPrefab;

        // State
        protected bool isReloading;
        protected float lastFireTime;
        protected float reloadEndTime;
        protected Vector2 currentRecoil;

        // References
        protected PlayerCamera playerCamera;
        protected PlayerAnimation playerAnimation;
        protected AudioSource audioSource;

        // Events
        public delegate void AmmoChanged(int current, int magazine, int reserve);
        public event AmmoChanged OnAmmoChanged;

        /// <summary>
        /// Fired when a target is hit. Used by hit feedback systems.
        /// Parameters: hitPoint, isHeadshot, isKill
        /// AAA Pattern: Event-driven feedback decouples weapons from UI/audio.
        /// </summary>
        public event Action<Vector3, bool, bool> OnTargetHit;

        /// <summary>
        /// Static event fired when ANY weapon fires. Used by enemy AI for gunshot detection.
        /// Parameters: shooterPosition, loudness (0-1 range, affects detection radius)
        /// </summary>
        public static event Action<Vector3, float> OnGunfired;

        // Properties
        public string WeaponName => weaponName;
        public Interfaces.WeaponType Type => weaponType;
        public Sprite Icon => weaponIcon;
        public int CurrentAmmo => currentAmmo;
        public int MagazineSize => magazineSize;
        public int ReserveAmmo => reserveAmmo;
        public bool IsReloading => isReloading;
        public bool IsAutomatic => isAutomatic;
        public float Damage => baseDamage;
        public float Range => range;
        public bool CanFire => !isReloading && currentAmmo > 0 && Time.time >= lastFireTime + (1f / fireRate);

        protected virtual void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        protected virtual void Start()
        {
            playerCamera = FindFirstObjectByType<PlayerCamera>();
            playerAnimation = GetComponentInParent<PlayerAnimation>();
        }

        protected virtual void Update()
        {
            // Update reload timer
            if (isReloading && Time.time >= reloadEndTime)
            {
                FinishReload();
            }

            // Recover recoil
            currentRecoil = Vector2.Lerp(currentRecoil, Vector2.zero, Time.deltaTime * recoilRecovery);
        }

        #region Firing

        public virtual bool TryFire()
        {
            if (!CanFire)
            {
                if (currentAmmo <= 0 && !isReloading)
                {
                    PlaySound(emptySound);
                }
                return false;
            }

            Fire();
            return true;
        }

        protected virtual void Fire()
        {
            currentAmmo--;
            lastFireTime = Time.time;

            // Calculate spread
            float spread = GetCurrentSpread();
            Vector3 direction = GetFireDirection(spread);

            // AAA Pattern: Find where crosshair points first, then aim from muzzle toward that point
            Vector3 aimPoint = GetFireOrigin() + direction * range; // Default: max range

            // First raycast from camera to find aim point (where crosshair points)
            if (Physics.Raycast(GetFireOrigin(), direction, out RaycastHit hit, range))
            {
                aimPoint = hit.point;
                OnHit(hit);
            }

            // Effects
            PlayMuzzleFlash();
            PlayFireSound();
            ApplyRecoil();
            SpawnBulletTrail(aimPoint);

            // Animation
            playerAnimation?.TriggerFire();

            // Notify ammo change
            OnAmmoChanged?.Invoke(currentAmmo, magazineSize, reserveAmmo);

            // Alert nearby enemies (gunshot detection)
            // Loudness of 1.0 = full alert radius, adjust per weapon type if needed
            OnGunfired?.Invoke(GetFireOrigin(), 1.0f);

            // Auto-reload when empty
            if (currentAmmo <= 0 && reserveAmmo > 0)
            {
                StartReload();
            }
        }

        protected virtual void OnHit(RaycastHit hit)
        {
            float damage = baseDamage;
            bool isHeadshot = false;
            bool isKill = false;
            bool hitValidTarget = false; // Track if we hit something damageable
            Vector3 hitDirection = (hit.point - GetFireOrigin()).normalized;

            // AAA Pattern: Check for EnemyHitbox first (provides damage multipliers)
            var hitbox = hit.collider.GetComponent<EnemyHitbox>();
            if (hitbox != null)
            {
                // Use hitbox damage multiplier and headshot detection
                isHeadshot = hitbox.IsHeadshot;
                damage *= hitbox.DamageMultiplier;

                // Apply damage through hitbox (routes to EnemyHealth)
                if (hitbox.Health != null)
                {
                    hitValidTarget = true;
                    bool wasAlive = !hitbox.Health.IsDead;
                    hitbox.ApplyDamage(baseDamage, DamageType.Bullet, hit.point, hitDirection);
                    isKill = wasAlive && hitbox.Health.IsDead;
                }

                if (isHeadshot)
                {
                    Debug.Log($"[Weapon] HEADSHOT! {damage:F1} damage to {hit.collider.name}");
                }
            }
            else
            {
                // Fallback: Check for IDamageable (works for any damageable entity)
                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    hitValidTarget = true;

                    // Legacy headshot check via tag (for PlayerHealth or other entities)
                    if (hit.collider.gameObject.tag == "Head")
                    {
                        damage *= headshotMultiplier;
                        isHeadshot = true;
                        Debug.Log($"[Weapon] HEADSHOT! {damage:F1} damage");
                    }

                    bool wasAlive = !damageable.IsDead;
                    damageable.TakeDamage(damage, DamageType.Bullet);
                    isKill = wasAlive && damageable.IsDead;
                }
            }

            // Only fire hit event if we hit a valid target (enemy, player, etc.)
            if (hitValidTarget)
            {
                OnTargetHit?.Invoke(hit.point, isHeadshot, isKill);
            }

            // Spawn hit effect
            // TODO: Spawn decal, particle effect at hit.point

            Debug.Log($"[Weapon] Hit {hit.collider.name} for {damage:F1} damage");
        }

        #endregion

        #region Reloading

        public virtual void StartReload()
        {
            if (isReloading) return;
            if (currentAmmo >= magazineSize) return;
            if (reserveAmmo <= 0) return;

            isReloading = true;
            reloadEndTime = Time.time + reloadTime;

            PlaySound(reloadSound);
            playerAnimation?.TriggerReload();

            Debug.Log($"[Weapon] Reloading {weaponName}...");
        }

        protected virtual void FinishReload()
        {
            int ammoNeeded = magazineSize - currentAmmo;
            int ammoToAdd = Mathf.Min(ammoNeeded, reserveAmmo);

            currentAmmo += ammoToAdd;
            reserveAmmo -= ammoToAdd;
            isReloading = false;

            OnAmmoChanged?.Invoke(currentAmmo, magazineSize, reserveAmmo);
            Debug.Log($"[Weapon] Reload complete. {currentAmmo}/{magazineSize} ({reserveAmmo} reserve)");
        }

        public virtual void CancelReload()
        {
            isReloading = false;
        }

        /// <summary>
        /// Called by animation event when reload animation completes.
        /// This allows animation-driven reload timing instead of fixed duration.
        /// </summary>
        public virtual void OnReloadAnimationComplete()
        {
            if (!isReloading) return;
            FinishReload();
        }

        #endregion

        #region Helpers

        protected Vector3 GetFireOrigin()
        {
            if (playerCamera != null)
            {
                return playerCamera.transform.position;
            }
            return muzzlePoint != null ? muzzlePoint.position : transform.position;
        }

        protected Vector3 GetFireDirection(float spreadDegrees)
        {
            Vector3 baseDirection = playerCamera != null ? playerCamera.GetAimDirection() : transform.forward;

            if (spreadDegrees > 0)
            {
                float spreadRad = spreadDegrees * Mathf.Deg2Rad;
                Vector3 spread = new Vector3(
                    UnityEngine.Random.Range(-spreadRad, spreadRad),
                    UnityEngine.Random.Range(-spreadRad, spreadRad),
                    0
                );
                baseDirection = Quaternion.Euler(spread) * baseDirection;
            }

            return baseDirection;
        }

        protected virtual float GetCurrentSpread()
        {
            float spread = baseSpread;

            // Reduce spread when aiming
            if (playerCamera != null && playerCamera.IsAiming)
            {
                spread = adsSpread;
            }

            // Increase spread when moving
            var controller = GetComponentInParent<PlayerController>();
            if (controller != null && controller.IsMoving)
            {
                spread += moveSpread * controller.NormalizedSpeed;
            }

            return spread;
        }

        protected void ApplyRecoil()
        {
            currentRecoil.x += UnityEngine.Random.Range(-recoilHorizontal, recoilHorizontal);
            currentRecoil.y += recoilVertical;

            // TODO: Apply recoil to camera
        }

        protected void PlayMuzzleFlash()
        {
            if (muzzleFlash != null)
            {
                muzzleFlash.Play();
            }
        }

        protected void PlaySound(AudioClip clip)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        /// <summary>
        /// Play weapon fire sound with optional tail/echo.
        /// AAA Pattern: Layered audio - main shot + tail for realistic gunshot sound.
        /// </summary>
        protected void PlayFireSound()
        {
            if (audioSource == null) return;

            // Pick main fire sound (from array or legacy single clip)
            AudioClip mainClip = null;
            if (fireSounds != null && fireSounds.Length > 0)
            {
                mainClip = fireSounds[UnityEngine.Random.Range(0, fireSounds.Length)];
            }
            else if (fireSound != null)
            {
                mainClip = fireSound; // Fallback to legacy
            }

            // Play main shot
            if (mainClip != null)
            {
                audioSource.PlayOneShot(mainClip);
            }

            // Play tail sound with delay for realism
            if (fireTailSound != null)
            {
                StartCoroutine(PlayFireTailDelayed());
            }
        }

        private System.Collections.IEnumerator PlayFireTailDelayed()
        {
            yield return new WaitForSeconds(fireTailDelay);

            if (audioSource != null && fireTailSound != null)
            {
                audioSource.PlayOneShot(fireTailSound, fireTailVolume);
            }
        }

        protected void SpawnBulletTrail(Vector3 endPoint)
        {
            if (muzzlePoint == null)
            {
                Debug.LogWarning($"[{weaponName}] Cannot spawn bullet trail - muzzlePoint is null!");
                return;
            }

            // Use new tracer system if available
            if (bulletTracerPrefab != null)
            {
                BulletTracer.Spawn(bulletTracerPrefab, muzzlePoint.position, endPoint);
                return;
            }
            else
            {
                Debug.Log($"[{weaponName}] No bulletTracerPrefab assigned");
            }

            // Fallback to legacy LineRenderer trail
            if (bulletTrailPrefab != null)
            {
                GameObject trail = Instantiate(bulletTrailPrefab, muzzlePoint.position, Quaternion.identity);
                var line = trail.GetComponent<LineRenderer>();
                if (line != null)
                {
                    line.SetPosition(0, muzzlePoint.position);
                    line.SetPosition(1, endPoint);
                }
                Destroy(trail, 0.1f);
            }
        }

        #endregion

        #region Ammo Management

        public void AddAmmo(int amount)
        {
            reserveAmmo += amount;
            OnAmmoChanged?.Invoke(currentAmmo, magazineSize, reserveAmmo);
        }

        public void SetAmmo(int magazine, int reserve)
        {
            currentAmmo = Mathf.Clamp(magazine, 0, magazineSize);
            reserveAmmo = reserve;
            OnAmmoChanged?.Invoke(currentAmmo, magazineSize, reserveAmmo);
        }

        #endregion
    }
}
