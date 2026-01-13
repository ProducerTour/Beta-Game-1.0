using UnityEngine;
using CreatorWorld.Interfaces;

namespace CreatorWorld.Enemy
{
    /// <summary>
    /// Attach to colliders on enemy body parts for precise hit detection.
    /// AAA Pattern: Each collider reports its type and damage multiplier.
    /// This allows headshots, limb shots, etc. without expensive tag lookups.
    ///
    /// Usage:
    /// 1. Create child GameObjects with Colliders on the enemy
    /// 2. Add EnemyHitbox component to each collider
    /// 3. Set the HitboxType (Head, Body, Limb)
    /// 4. The parent EnemyHealth component will receive damage notifications
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class EnemyHitbox : MonoBehaviour
    {
        [Header("Hitbox Settings")]
        [Tooltip("Type of body part this hitbox represents")]
        [SerializeField] private HitboxType hitboxType = HitboxType.Body;

        [Tooltip("Optional override for damage multiplier. Leave at 0 to use EnemyConfig defaults.")]
        [SerializeField] private float customMultiplier = 0f;

        // Cached reference to parent health component
        private EnemyHealth enemyHealth;

        /// <summary>
        /// The type of hitbox (Head, Body, Limb).
        /// </summary>
        public HitboxType Type => hitboxType;

        /// <summary>
        /// Damage multiplier for this hitbox.
        /// Returns custom multiplier if set, otherwise gets from EnemyConfig.
        /// </summary>
        public float DamageMultiplier
        {
            get
            {
                if (customMultiplier > 0f)
                    return customMultiplier;

                if (enemyHealth != null && enemyHealth.Config != null)
                    return enemyHealth.Config.GetDamageMultiplier(hitboxType);

                // Fallback defaults if no config
                return hitboxType switch
                {
                    HitboxType.Head => 2.5f,
                    HitboxType.Limb => 0.75f,
                    _ => 1f
                };
            }
        }

        /// <summary>
        /// Whether this is a headshot hitbox.
        /// </summary>
        public bool IsHeadshot => hitboxType == HitboxType.Head;

        /// <summary>
        /// Reference to the parent EnemyHealth component.
        /// </summary>
        public EnemyHealth Health => enemyHealth;

        private void Awake()
        {
            // Find EnemyHealth in parent hierarchy
            enemyHealth = GetComponentInParent<EnemyHealth>();

            if (enemyHealth == null)
            {
                Debug.LogWarning($"[EnemyHitbox] No EnemyHealth found in parent of {gameObject.name}. " +
                    "Damage will not be applied correctly.");
            }

            // Ensure collider is set up correctly
            var collider = GetComponent<Collider>();
            if (collider != null && collider.isTrigger)
            {
                Debug.LogWarning($"[EnemyHitbox] Collider on {gameObject.name} is a trigger. " +
                    "Hitboxes should use non-trigger colliders for raycast detection.");
            }
        }

        /// <summary>
        /// Apply damage to this hitbox, routing to the parent EnemyHealth.
        /// Called by weapons when they hit this collider.
        /// </summary>
        /// <param name="baseDamage">Base damage before multiplier</param>
        /// <param name="damageType">Type of damage being dealt</param>
        /// <param name="hitPoint">World position where the hit occurred</param>
        /// <param name="hitDirection">Direction the damage came from</param>
        /// <returns>True if damage was applied, false if no health component found</returns>
        public bool ApplyDamage(float baseDamage, DamageType damageType, Vector3 hitPoint, Vector3 hitDirection)
        {
            if (enemyHealth == null)
                return false;

            float finalDamage = baseDamage * DamageMultiplier;
            enemyHealth.TakeDamageFromHitbox(finalDamage, damageType, hitPoint, hitDirection, hitboxType);
            return true;
        }

        private void OnValidate()
        {
            // Visualize hitbox type in editor with colored gizmos
            if (customMultiplier < 0f)
                customMultiplier = 0f;
        }

        private void OnDrawGizmosSelected()
        {
            // Draw colored wireframe to show hitbox type
            Gizmos.color = hitboxType switch
            {
                HitboxType.Head => Color.red,
                HitboxType.Limb => Color.yellow,
                _ => Color.green
            };

            var collider = GetComponent<Collider>();
            if (collider is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (collider is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
            }
            else if (collider is CapsuleCollider capsule)
            {
                Gizmos.DrawWireSphere(transform.position + capsule.center, capsule.radius);
            }
        }
    }
}
