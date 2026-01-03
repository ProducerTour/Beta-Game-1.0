using UnityEngine;
using CreatorWorld.Player;
using CreatorWorld.Interfaces;

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
        [SerializeField] protected AudioClip fireSound;
        [SerializeField] protected AudioClip reloadSound;
        [SerializeField] protected AudioClip emptySound;

        [Header("Effects")]
        [SerializeField] protected Transform muzzlePoint;
        [SerializeField] protected ParticleSystem muzzleFlash;
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

            // Raycast for hit detection
            if (Physics.Raycast(GetFireOrigin(), direction, out RaycastHit hit, range))
            {
                OnHit(hit);
            }

            // Effects
            PlayMuzzleFlash();
            PlaySound(fireSound);
            ApplyRecoil();
            SpawnBulletTrail(hit.point != Vector3.zero ? hit.point : GetFireOrigin() + direction * range);

            // Animation
            playerAnimation?.TriggerFire();

            // Notify
            OnAmmoChanged?.Invoke(currentAmmo, magazineSize, reserveAmmo);

            // Auto-reload when empty
            if (currentAmmo <= 0 && reserveAmmo > 0)
            {
                StartReload();
            }
        }

        protected virtual void OnHit(RaycastHit hit)
        {
            float damage = baseDamage;

            // Check for headshot (safely check tag without throwing if not defined)
            try
            {
                if (hit.collider.CompareTag("Head"))
                {
                    damage *= headshotMultiplier;
                    Debug.Log($"[Weapon] HEADSHOT! {damage} damage");
                }
            }
            catch { /* Head tag not defined - ignore */ }

            // Apply damage to target
            var health = hit.collider.GetComponentInParent<PlayerHealth>();
            if (health != null)
            {
                health.TakeDamage(damage, DamageType.Bullet);
            }

            // Spawn hit effect
            // TODO: Spawn decal, particle effect at hit.point

            Debug.Log($"[Weapon] Hit {hit.collider.name} for {damage} damage");
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
                    Random.Range(-spreadRad, spreadRad),
                    Random.Range(-spreadRad, spreadRad),
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
            currentRecoil.x += Random.Range(-recoilHorizontal, recoilHorizontal);
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

        protected void SpawnBulletTrail(Vector3 endPoint)
        {
            if (bulletTrailPrefab == null || muzzlePoint == null) return;

            // TODO: Use object pool for bullet trails
            GameObject trail = Instantiate(bulletTrailPrefab, muzzlePoint.position, Quaternion.identity);
            var line = trail.GetComponent<LineRenderer>();
            if (line != null)
            {
                line.SetPosition(0, muzzlePoint.position);
                line.SetPosition(1, endPoint);
            }
            Destroy(trail, 0.1f);
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
