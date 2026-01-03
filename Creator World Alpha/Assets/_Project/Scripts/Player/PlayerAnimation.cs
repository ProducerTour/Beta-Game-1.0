using UnityEngine;
using CreatorWorld.Core;
using CreatorWorld.Interfaces;
using CreatorWorld.Player.Animation;
using CreatorWorld.Combat;

namespace CreatorWorld.Player
{
    /// <summary>
    /// Handles player animation state machine.
    /// Uses centralized AnimatorHashes for parameter names.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimation : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float animationDampTime = 0.1f;
        [SerializeField] private float locomotionBlendSpeed = 10f;

        // Components
        private Animator animator;
        private PlayerController playerController;
        private ICameraService cameraService;

        // State
        private Vector2 smoothMoveInput;
        private float smoothSpeed;
        private Interfaces.WeaponType currentWeaponType = Interfaces.WeaponType.None;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            playerController = GetComponent<PlayerController>();

            // Subscribe to weapon change events
            var inventory = GetComponent<WeaponInventory>();
            if (inventory != null)
            {
                inventory.OnWeaponChanged += OnWeaponChanged;
            }
        }

        private void OnDestroy()
        {
            var inventory = GetComponent<WeaponInventory>();
            if (inventory != null)
            {
                inventory.OnWeaponChanged -= OnWeaponChanged;
            }
        }

        private void OnWeaponChanged(WeaponBase weapon, int slot)
        {
            if (weapon != null)
                SetWeaponType(weapon.Type);
            else
                SetWeaponType(Interfaces.WeaponType.None);
        }

        private void Start()
        {
            cameraService = ServiceLocator.Get<ICameraService>();
        }

        private void Update()
        {
            if (playerController == null || animator == null) return;
            if (animator.runtimeAnimatorController == null) return;

            UpdateLocomotion();
            UpdateState();
        }

        private void UpdateLocomotion()
        {
            Vector2 targetInput = playerController.MoveInput;
            smoothMoveInput = Vector2.Lerp(smoothMoveInput, targetInput, Time.deltaTime * locomotionBlendSpeed);
            smoothSpeed = Mathf.Lerp(smoothSpeed, playerController.NormalizedSpeed, Time.deltaTime * locomotionBlendSpeed);

            animator.SetFloat(AnimatorHashes.MoveX, smoothMoveInput.x, animationDampTime, Time.deltaTime);
            animator.SetFloat(AnimatorHashes.MoveZ, smoothMoveInput.y, animationDampTime, Time.deltaTime);
            animator.SetFloat(AnimatorHashes.Speed, smoothSpeed, animationDampTime, Time.deltaTime);
        }

        private void UpdateState()
        {
            // Boolean states
            animator.SetBool(AnimatorHashes.IsGrounded, playerController.IsGrounded);
            animator.SetBool(AnimatorHashes.IsCrouching, playerController.IsCrouching);
            animator.SetBool(AnimatorHashes.IsSprinting, playerController.IsSprinting);
            animator.SetBool(AnimatorHashes.IsStrafing, playerController.IsStrafing);

            // Air states
            animator.SetBool(AnimatorHashes.IsJumping, playerController.IsJumping);
            animator.SetBool(AnimatorHashes.IsFalling, playerController.IsFalling);
            animator.SetFloat(AnimatorHashes.VelocityY, playerController.Velocity.y);

            // Aiming
            if (cameraService != null)
            {
                animator.SetBool(AnimatorHashes.IsAiming, cameraService.IsAiming);
            }

            // Weapon
            animator.SetInteger(AnimatorHashes.WeaponType, (int)currentWeaponType);
        }

        #region Animation Triggers

        public void TriggerJump() => animator?.SetTrigger(AnimatorHashes.Jump);
        public void TriggerFire() => animator?.SetTrigger(AnimatorHashes.Fire);
        public void TriggerReload() => animator?.SetTrigger(AnimatorHashes.Reload);
        public void TriggerDeath() => animator?.SetTrigger(AnimatorHashes.Death);
        public void OnLanded() => animator?.SetTrigger(AnimatorHashes.Land);

        #endregion

        #region Weapon Management

        public void SetWeaponType(Interfaces.WeaponType type)
        {
            currentWeaponType = type;
            animator?.SetInteger(AnimatorHashes.WeaponType, (int)type);
        }

        #endregion

        #region Animation Events

        public void OnFootstep()
        {
            // AudioManager.Instance?.PlayFootstep(transform.position);
        }

        public void OnFireAnimationEvent() { }
        public void OnReloadComplete() { }

        #endregion
    }
}
