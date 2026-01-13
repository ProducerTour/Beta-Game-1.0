using UnityEngine;
using CreatorWorld.Player.Animation;
using CreatorWorld.Player.Movement;
using CreatorWorld.Interfaces;
using CreatorWorld.Core;

namespace CreatorWorld.Player
{
    /// <summary>
    /// Handles player animation using Unity's Mecanim system.
    /// Uses a 2D Freeform Directional blend tree for smooth 8-way locomotion.
    ///
    /// Industry Standard Approach:
    /// - Parameters are cached as hashes for performance
    /// - Uses blend trees for smooth locomotion blending
    /// - Separate update for parameters vs state transitions
    ///
    /// Movement Mode Handling:
    /// - Non-aiming: Character rotates to face movement. MoveX=0, MoveZ=speed (no strafing)
    /// - Aiming: Character faces camera. MoveX/MoveZ from velocity (true strafing)
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimation : MonoBehaviour
    {
        [Header("Blend Settings")]
        [Tooltip("Damping time for animator parameters. Use 0.1-0.15 for responsive feel. Single smoothing only - no manual Lerp.")]
        [SerializeField] private float parameterDampTime = 0.1f;

        [Header("Layer Blending")]
        [Tooltip("How fast the upper body layer blends in/out during vault")]
        [SerializeField] private float layerBlendSpeed = 8f;

        // Layer weight tracking
        private float targetUpperBodyWeight = 0f;
        private float currentUpperBodyWeight = 0f;

        // Cached components
        private Animator animator;
        private PlayerController playerController;
        private ICameraService cameraService;
        private IInputService inputService;
        private SlideHandler slideHandler;
        private VaultHandler vaultHandler;

        // Weapon state
        private bool hasRifle = false;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            playerController = GetComponent<PlayerController>();
            slideHandler = GetComponent<SlideHandler>();
            if (slideHandler == null) slideHandler = GetComponentInParent<SlideHandler>();
            vaultHandler = GetComponent<VaultHandler>();
            if (vaultHandler == null) vaultHandler = GetComponentInParent<VaultHandler>();

            // Disable root motion - movement is handled by CharacterController, not animations
            if (animator != null)
            {
                animator.applyRootMotion = false;
            }
        }

        private void Start()
        {
            cameraService = ServiceLocator.Get<ICameraService>();
            inputService = ServiceLocator.Get<IInputService>();
        }

        private void Update()
        {
            if (animator == null || playerController == null) return;
            if (animator.runtimeAnimatorController == null) return;

            // Try to get camera service if not yet available
            if (cameraService == null)
            {
                cameraService = ServiceLocator.Get<ICameraService>();
            }

            UpdateLocomotionParameters();
            UpdateGroundedState();
            UpdateAirState();
            UpdateSlideState();
            UpdateVaultState();
            UpdateWeaponState();
        }

        /// <summary>
        /// Updates the IsSliding parameter based on SlideHandler state.
        /// </summary>
        private void UpdateSlideState()
        {
            bool isSliding = slideHandler != null && slideHandler.IsSliding;
            animator.SetBool(AnimatorHashes.IsSliding, isSliding);
        }

        /// <summary>
        /// Updates the IsVaulting parameter based on VaultHandler state.
        /// Also handles upper body layer blending to allow full-body vault animation.
        ///
        /// AAA TECHNIQUE: Layer Weight Blending
        /// - When vaulting: Upper body layer weight → 0 (full body vault plays)
        /// - When not vaulting + has weapon: Upper body layer weight → 1 (weapon animations play)
        /// - Smooth blend prevents jarring transitions
        /// </summary>
        private void UpdateVaultState()
        {
            bool isVaulting = vaultHandler != null && vaultHandler.IsVaulting;
            animator.SetBool(AnimatorHashes.IsVaulting, isVaulting);

            // Determine target upper body layer weight
            // When vaulting: 0 (let base layer vault animation control full body)
            // When has weapon and not vaulting: 1 (weapon layer active)
            // When no weapon: 0 (no upper body override needed)
            if (isVaulting)
            {
                targetUpperBodyWeight = 0f;
            }
            else
            {
                targetUpperBodyWeight = hasRifle ? 1f : 0f;
            }

            // Smoothly blend the layer weight
            currentUpperBodyWeight = Mathf.MoveTowards(
                currentUpperBodyWeight,
                targetUpperBodyWeight,
                layerBlendSpeed * Time.deltaTime
            );

            // Apply to animator
            animator.SetLayerWeight(AnimatorHashes.UpperBodyLayer, currentUpperBodyWeight);
        }

        /// <summary>
        /// Updates weapon state based on input.
        /// Press 1 to equip rifle, 3 or H to holster.
        /// </summary>
        private void UpdateWeaponState()
        {
            if (inputService == null)
            {
                inputService = ServiceLocator.Get<IInputService>();
                if (inputService == null) return;
            }

            // Equip rifle with key 1
            if (inputService.WeaponSwitch1Pressed)
            {
                hasRifle = true;
            }

            // Holster with key 3 or H
            if (inputService.HolsterPressed)
            {
                hasRifle = false;
            }

            // Update animator parameters
            animator.SetBool(AnimatorHashes.HasRifle, hasRifle);

            // Update aiming state (right-click held while armed)
            bool isAiming = hasRifle && inputService.AimHeld;
            animator.SetBool(AnimatorHashes.IsAiming, isAiming);
        }

        /// <summary>
        /// Updates blend tree parameters for smooth locomotion.
        /// MoveX/MoveZ control direction relative to character facing.
        ///
        /// IMPORTANT: Uses ONLY animator's built-in damping (SetFloat with dampTime).
        /// Do NOT add manual Lerp smoothing - double smoothing causes oscillation/lag.
        /// </summary>
        private void UpdateLocomotionParameters()
        {
            // Get target values directly - no manual smoothing
            float targetSpeed = playerController.NormalizedSpeed;
            Vector2 targetDirection = GetLocalMovementDirection();

            // Apply to animator with SINGLE level of damping
            // The animator's SetFloat(value, dampTime, deltaTime) handles all smoothing
            animator.SetFloat(AnimatorHashes.Speed, targetSpeed, parameterDampTime, Time.deltaTime);
            animator.SetFloat(AnimatorHashes.MoveX, targetDirection.x, parameterDampTime, Time.deltaTime);
            animator.SetFloat(AnimatorHashes.MoveZ, targetDirection.y, parameterDampTime, Time.deltaTime);

            // Debug: Log values when aiming
            bool isAiming = animator.GetBool(AnimatorHashes.IsAiming);
            if (isAiming && playerController.IsMoving)
            {
                Debug.Log($"[Aim Locomotion] MoveX: {targetDirection.x:F2}, MoveZ: {targetDirection.y:F2}, Speed: {targetSpeed:F2}, State: {animator.GetCurrentAnimatorStateInfo(0).shortNameHash}");
            }

            // Vertical velocity for jump animations (no damping needed - instant response)
            animator.SetFloat(AnimatorHashes.VelocityY, playerController.Velocity.y);
        }

        /// <summary>
        /// Calculates blend tree direction based on movement mode.
        ///
        /// Non-aiming: Character rotates to face movement direction.
        ///   - No strafing occurs, so MoveX = 0
        ///   - MoveZ = normalized speed (always positive forward)
        ///   - This prevents pingponging during rotation
        ///
        /// Aiming: Character faces camera direction (true strafe mode).
        ///   - MoveX/MoveZ derived from velocity relative to character facing
        ///   - Allows proper strafe animation selection
        /// </summary>
        private Vector2 GetLocalMovementDirection()
        {
            // Check if aiming - use strafe animations when aiming, forward when not
            bool isAiming = cameraService != null && cameraService.IsAiming;

            if (isAiming)
            {
                // Aiming: Character faces camera, use velocity for strafe animations
                return GetVelocityBasedDirection();
            }
            else
            {
                // Not aiming: Character turns to face movement, just use forward animation
                return GetForwardMovementDirection();
            }
        }

        /// <summary>
        /// For non-aiming: Character always turns to face movement direction.
        /// Only uses forward animation - MoveX stays 0.
        /// </summary>
        private Vector2 GetForwardMovementDirection()
        {
            float speed = playerController.NormalizedSpeed;

            // Not moving
            if (speed < 0.01f)
            {
                return Vector2.zero;
            }

            // Always use forward animation - character turns to face direction
            return new Vector2(0f, speed);
        }

        /// <summary>
        /// For aiming: Use velocity to determine strafe direction.
        /// Character faces camera, so lateral velocity = strafing.
        /// </summary>
        private Vector2 GetVelocityBasedDirection()
        {
            // Get the actual velocity in world space (horizontal only)
            Vector3 worldVelocity = playerController.Velocity;
            worldVelocity.y = 0;

            // If not moving, return zero
            if (worldVelocity.sqrMagnitude < 0.01f)
            {
                return Vector2.zero;
            }

            // Transform world velocity to local space relative to character's facing
            Vector3 localVelocity = transform.InverseTransformDirection(worldVelocity);

            // Normalize and return as Vector2 (x = strafe, z = forward/back)
            Vector2 localDir = new Vector2(localVelocity.x, localVelocity.z);

            // Normalize but preserve small movements
            if (localDir.magnitude > 1f)
            {
                localDir.Normalize();
            }

            return localDir;
        }

        /// <summary>
        /// Updates grounded state for jump/fall transitions.
        /// </summary>
        private void UpdateGroundedState()
        {
            bool isGrounded = playerController.IsGrounded;
            bool isMoving = playerController.IsMoving;
            bool isSprinting = playerController.IsSprinting;

            animator.SetBool(AnimatorHashes.IsGrounded, isGrounded);
            animator.SetBool(AnimatorHashes.IsCrouching, playerController.IsCrouching);
            animator.SetBool(AnimatorHashes.IsSprinting, isSprinting);
            animator.SetBool(AnimatorHashes.IsWalking, isMoving && !isSprinting);
        }

        /// <summary>
        /// Updates air state parameters for adaptive jump animations.
        ///
        /// AAA TECHNIQUE: Velocity-Driven Animation
        /// - IsJumping/IsFalling booleans for state machine transitions
        /// - NormalizedVelocityY (-1 to +1) for blend trees and animation speed
        /// - Allows short jumps = faster animations, cliff falls = skip rise phase
        /// </summary>
        private void UpdateAirState()
        {
            bool isJumping = playerController.IsJumping;
            bool isFalling = playerController.IsFalling;
            float velocityY = playerController.Velocity.y;

            // Set state booleans for animator state machine
            animator.SetBool(AnimatorHashes.IsJumping, isJumping);
            animator.SetBool(AnimatorHashes.IsFalling, isFalling);

            // Normalized velocity: -1 (falling fast) to +1 (rising fast)
            // Useful for blend trees and animation speed multipliers
            // Dividing by 10 gives good range for typical jump velocities
            float normalizedVelY = Mathf.Clamp(velocityY / 10f, -1f, 1f);
            animator.SetFloat(AnimatorHashes.NormalizedVelocityY, normalizedVelY);
        }

        #region Animation Triggers

        /// <summary>
        /// Trigger jump animation via trigger parameter.
        /// </summary>
        public void TriggerJump()
        {
            Debug.Log($"[PlayerAnimation] TriggerJump called - HasRifle: {animator.GetBool(AnimatorHashes.HasRifle)}, IsGrounded: {animator.GetBool(AnimatorHashes.IsGrounded)}");
            animator.SetTrigger(AnimatorHashes.JumpTrigger);
        }

        /// <summary>
        /// Trigger vault animation.
        /// </summary>
        public void TriggerVault()
        {
            animator.SetTrigger(AnimatorHashes.VaultTrigger);
        }

        /// <summary>
        /// Trigger fire animation.
        /// </summary>
        public void TriggerFire()
        {
            // TODO: Add fire trigger when weapon animations are set up
        }

        /// <summary>
        /// Trigger reload animation.
        /// </summary>
        public void TriggerReload()
        {
            // TODO: Add reload trigger when weapon animations are set up
        }

        /// <summary>
        /// Trigger death animation.
        /// </summary>
        public void TriggerDeath()
        {
            // TODO: Add death trigger when death animation is set up
        }

        /// <summary>
        /// Set weapon type for animation selection.
        /// </summary>
        public void SetWeaponType(WeaponType type)
        {
            // TODO: Add weapon type parameter when weapon-specific animations are set up
        }

        /// <summary>
        /// Called when player lands from a jump.
        /// Triggers land animation for responsive feedback.
        /// </summary>
        public void OnLanded()
        {
            animator.SetTrigger(AnimatorHashes.LandTrigger);
        }

        /// <summary>
        /// Trigger slide effects.
        /// </summary>
        public void TriggerSlide()
        {
            // IsSliding bool handled by UpdateSlideState()
        }

        #endregion

        /// <summary>
        /// Debug info for testing
        /// </summary>
        public string GetDebugInfo()
        {
            if (animator == null || playerController == null) return "No animator/controller";

            float speed = animator.GetFloat(AnimatorHashes.Speed);
            float moveX = animator.GetFloat(AnimatorHashes.MoveX);
            float moveZ = animator.GetFloat(AnimatorHashes.MoveZ);

            return $"Speed: {speed:F2}, Move: ({moveX:F2}, {moveZ:F2}), Grounded: {playerController.IsGrounded}";
        }
    }
}
