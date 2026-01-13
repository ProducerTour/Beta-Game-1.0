using UnityEngine;

namespace CreatorWorld.Config
{
    /// <summary>
    /// Configuration for enemy stats and behavior.
    /// AAA Pattern: Externalize all tunable values to ScriptableObjects.
    /// This allows designers to tweak values without code changes.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyConfig", menuName = "Config/Enemy Config")]
    public class EnemyConfig : ScriptableObject
    {
        [Header("Health")]
        [Tooltip("Maximum health points")]
        [SerializeField] private float maxHealth = 100f;

        [Tooltip("Health regeneration per second (0 to disable)")]
        [SerializeField] private float healthRegen = 0f;

        [Tooltip("Delay before health regen starts after taking damage")]
        [SerializeField] private float regenDelay = 5f;

        [Header("Damage Multipliers")]
        [Tooltip("Damage multiplier for body shots")]
        [SerializeField] private float bodyDamageMultiplier = 1f;

        [Tooltip("Damage multiplier for headshots")]
        [SerializeField] private float headDamageMultiplier = 2.5f;

        [Tooltip("Damage multiplier for limb shots")]
        [SerializeField] private float limbDamageMultiplier = 0.75f;

        [Header("Movement")]
        [Tooltip("Walking speed (patrol, casual movement)")]
        [SerializeField] private float walkSpeed = 2f;

        [Tooltip("Running speed (chase, combat)")]
        [SerializeField] private float runSpeed = 4.5f;

        [Tooltip("Rotation speed in degrees per second")]
        [SerializeField] private float rotationSpeed = 360f;

        [Header("Detection")]
        [Tooltip("Maximum distance to detect player visually")]
        [SerializeField] private float viewDistance = 20f;

        [Tooltip("Field of view angle (half-angle in degrees)")]
        [SerializeField] private float viewAngle = 60f;

        [Tooltip("Distance at which enemy hears normal sounds")]
        [SerializeField] private float hearingRange = 15f;

        [Tooltip("Distance at which enemy hears gunshots")]
        [SerializeField] private float gunshotAlertRange = 30f;

        [Header("Combat")]
        [Tooltip("Distance at which enemy will start attacking")]
        [SerializeField] private float attackRange = 15f;

        [Tooltip("Preferred distance to maintain during combat")]
        [SerializeField] private float preferredCombatRange = 10f;

        [Tooltip("Time between attacks in seconds")]
        [SerializeField] private float attackCooldown = 1.5f;

        [Header("Behavior")]
        [Tooltip("Time to wait at patrol points")]
        [SerializeField] private float patrolWaitTime = 2f;

        [Tooltip("Time to search for player after losing sight")]
        [SerializeField] private float searchDuration = 5f;

        [Tooltip("Time before returning to patrol after losing player")]
        [SerializeField] private float alertCooldown = 10f;

        // Properties
        public float MaxHealth => maxHealth;
        public float HealthRegen => healthRegen;
        public float RegenDelay => regenDelay;

        public float BodyDamageMultiplier => bodyDamageMultiplier;
        public float HeadDamageMultiplier => headDamageMultiplier;
        public float LimbDamageMultiplier => limbDamageMultiplier;

        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public float RotationSpeed => rotationSpeed;

        public float ViewDistance => viewDistance;
        public float ViewAngle => viewAngle;
        public float HearingRange => hearingRange;
        public float GunshotAlertRange => gunshotAlertRange;

        public float AttackRange => attackRange;
        public float PreferredCombatRange => preferredCombatRange;
        public float AttackCooldown => attackCooldown;

        public float PatrolWaitTime => patrolWaitTime;
        public float SearchDuration => searchDuration;
        public float AlertCooldown => alertCooldown;

        /// <summary>
        /// Get the damage multiplier for a specific hitbox type.
        /// </summary>
        public float GetDamageMultiplier(Interfaces.HitboxType hitboxType)
        {
            return hitboxType switch
            {
                Interfaces.HitboxType.Head => headDamageMultiplier,
                Interfaces.HitboxType.Limb => limbDamageMultiplier,
                _ => bodyDamageMultiplier
            };
        }
    }
}
