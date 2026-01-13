using UnityEngine;

namespace CreatorWorld.Player.Animation
{
    /// <summary>
    /// Centralized animator parameter and state hashes.
    /// Using hashes instead of strings improves performance (Unity best practice).
    /// </summary>
    public static class AnimatorHashes
    {
        // ===== PARAMETERS =====

        // Locomotion blend tree parameters
        public static readonly int Speed = Animator.StringToHash("Speed");
        public static readonly int MoveX = Animator.StringToHash("MoveX");
        public static readonly int MoveZ = Animator.StringToHash("MoveZ");

        // State booleans
        public static readonly int IsGrounded = Animator.StringToHash("IsGrounded");
        public static readonly int IsCrouching = Animator.StringToHash("IsCrouching");
        public static readonly int IsSprinting = Animator.StringToHash("IsSprinting");
        public static readonly int IsWalking = Animator.StringToHash("IsWalking");
        public static readonly int IsSliding = Animator.StringToHash("IsSliding");
        public static readonly int IsVaulting = Animator.StringToHash("IsVaulting");
        public static readonly int HasRifle = Animator.StringToHash("HasRifle");
        public static readonly int IsAiming = Animator.StringToHash("IsAiming");

        // Air state booleans (for adaptive jump animations)
        public static readonly int IsJumping = Animator.StringToHash("IsJumping");
        public static readonly int IsFalling = Animator.StringToHash("IsFalling");

        // Triggers (one-shot animations)
        public static readonly int VaultTrigger = Animator.StringToHash("Vault");
        public static readonly int JumpTrigger = Animator.StringToHash("Jump");
        public static readonly int LandTrigger = Animator.StringToHash("Land");

        // Vertical velocity for jump phases
        public static readonly int VelocityY = Animator.StringToHash("VelocityY");
        public static readonly int NormalizedVelocityY = Animator.StringToHash("NormalizedVelocityY");

        // ===== LAYER INDICES =====
        public const int BaseLayer = 0;
        public const int UpperBodyLayer = 1;
    }
}
