using UnityEngine;
using CreatorWorld.Player.Animation;

namespace CreatorWorld.Enemy
{
    /// <summary>
    /// Simplified enemy animation controller for zombies.
    /// Drives locomotion via Speed parameter, handles Attack and Death triggers.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class EnemyAnimation : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;

        [Header("Animation Settings")]
        [Tooltip("Multiplier for calculated speed (adjust if animations look too fast/slow)")]
        [SerializeField] private float speedMultiplier = 1f;

        [Tooltip("How quickly the animation speed changes (higher = more responsive, lower = smoother)")]
        [SerializeField] private float speedDampTime = 0.15f;

        [Header("Enemy Type")]
        [Tooltip("Whether this enemy uses rifle animations (false for melee enemies like zombies)")]
        [SerializeField] private bool useRifleAnimations = false;

        [Header("Fallback Controller")]
        [Tooltip("Animator controller to use if none is assigned on the Animator")]
        [SerializeField] private RuntimeAnimatorController fallbackController;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        // State
        private Vector3 lastPosition;
        private bool isDead;

        // Cached hashes
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int DeathHash = Animator.StringToHash("Death");
        private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");

        public Animator Animator => animator;
        public bool IsDead => isDead;

        private void Awake()
        {
            animator = GetComponent<Animator>();

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (animator == null)
            {
                Debug.LogError($"[EnemyAnimation] No Animator found on {gameObject.name}!");
            }

            lastPosition = transform.position;
        }

        private void Start()
        {
            // Try fallback controller if needed
            if (animator != null && animator.runtimeAnimatorController == null)
            {
                if (fallbackController != null)
                {
                    animator.runtimeAnimatorController = fallbackController;
                    Debug.Log($"[EnemyAnimation] Using fallback controller on {gameObject.name}");
                }
            }

            if (animator == null || animator.runtimeAnimatorController == null)
            {
                Debug.LogError($"[EnemyAnimation] {gameObject.name} has no AnimatorController! Disabling.");
                enabled = false;
                return;
            }

            // Debug: Log animator info
            if (showDebugInfo)
                Debug.Log($"[EnemyAnimation] {gameObject.name} using controller: {animator.runtimeAnimatorController.name}, Avatar: {(animator.avatar != null ? animator.avatar.name : "NONE")}");

            // For rifle enemies using player-style animator
            if (useRifleAnimations)
            {
                animator.SetBool(AnimatorHashes.HasRifle, true);
                animator.SetBool(AnimatorHashes.IsGrounded, true);
            }
        }

        private void Update()
        {
            if (isDead) return;
            UpdateLocomotion();
        }

        private void UpdateLocomotion()
        {
            // Calculate speed from position delta
            Vector3 velocity = (transform.position - lastPosition) / Time.deltaTime;
            lastPosition = transform.position;

            // Get horizontal speed only
            Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
            float speed = horizontalVelocity.magnitude * speedMultiplier;

            // Set speed with damping to prevent flickering between animation states
            animator.SetFloat(SpeedHash, speed, speedDampTime, Time.deltaTime);

            if (showDebugInfo && Time.frameCount % 30 == 0)
            {
                Debug.Log($"[EnemyAnimation] {gameObject.name} Speed: {speed:F2}");
            }

            // Only for rifle enemies using player-style animator
            if (useRifleAnimations)
            {
                bool isWalking = speed > 0.1f && speed < 3f;
                bool isSprinting = speed >= 5f;
                animator.SetBool(AnimatorHashes.IsWalking, isWalking);
                animator.SetBool(AnimatorHashes.IsSprinting, isSprinting);
            }
        }

        /// <summary>
        /// Trigger death animation
        /// </summary>
        public void TriggerDeath(int deathType = 0)
        {
            if (isDead) return;

            isDead = true;
            animator.SetTrigger(DeathHash);
            animator.SetFloat(SpeedHash, 0f);
        }

        /// <summary>
        /// Trigger melee attack animation (for zombies)
        /// </summary>
        public void TriggerAttack()
        {
            if (isDead) return;
            animator.SetTrigger(AttackHash);
            animator.SetBool(IsAttackingHash, true);
        }

        /// <summary>
        /// Called when attack animation finishes (via animation event or timer)
        /// </summary>
        public void OnAttackFinished()
        {
            animator.SetBool(IsAttackingHash, false);
        }

        /// <summary>
        /// Check if currently in attack animation
        /// </summary>
        public bool IsAttacking()
        {
            return animator.GetBool(IsAttackingHash);
        }

        /// <summary>
        /// Reset animation state (for respawning)
        /// </summary>
        public void ResetAnimation()
        {
            isDead = false;
            animator.SetFloat(SpeedHash, 0f);
            animator.SetBool(IsAttackingHash, false);

            if (useRifleAnimations)
            {
                animator.SetBool(AnimatorHashes.HasRifle, true);
                animator.SetBool(AnimatorHashes.IsGrounded, true);
            }
        }

        /// <summary>
        /// Configure whether this enemy uses rifle animations
        /// </summary>
        public void SetUseRifleAnimations(bool useRifle)
        {
            useRifleAnimations = useRifle;
            if (animator != null && useRifle)
            {
                animator.SetBool(AnimatorHashes.HasRifle, true);
            }
        }

        /// <summary>
        /// Set whether the enemy is crouching (rifle enemies only)
        /// </summary>
        public void SetCrouching(bool crouching)
        {
            if (useRifleAnimations)
            {
                animator.SetBool(AnimatorHashes.IsCrouching, crouching);
            }
        }

        /// <summary>
        /// Set whether the enemy is aiming (rifle enemies only)
        /// </summary>
        public void SetAiming(bool aiming)
        {
            if (useRifleAnimations)
            {
                animator.SetBool(AnimatorHashes.IsAiming, aiming);
            }
        }
    }
}
