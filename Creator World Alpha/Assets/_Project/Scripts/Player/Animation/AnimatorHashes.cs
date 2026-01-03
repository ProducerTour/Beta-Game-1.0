using UnityEngine;

namespace CreatorWorld.Player.Animation
{
    /// <summary>
    /// Centralized animator parameter hash definitions.
    /// All animator parameters should be defined here for consistency and performance.
    /// </summary>
    public static class AnimatorHashes
    {
        // Locomotion
        public static readonly int Speed = Animator.StringToHash("Speed");
        public static readonly int MoveX = Animator.StringToHash("MoveX");
        public static readonly int MoveZ = Animator.StringToHash("MoveZ");

        // Ground State
        public static readonly int IsGrounded = Animator.StringToHash("IsGrounded");
        public static readonly int IsCrouching = Animator.StringToHash("IsCrouching");
        public static readonly int IsSprinting = Animator.StringToHash("IsSprinting");
        public static readonly int IsStrafing = Animator.StringToHash("IsStrafing");

        // Air State
        public static readonly int IsJumping = Animator.StringToHash("IsJumping");
        public static readonly int IsFalling = Animator.StringToHash("IsFalling");
        public static readonly int VelocityY = Animator.StringToHash("VelocityY");

        // Combat
        public static readonly int IsAiming = Animator.StringToHash("IsAiming");
        public static readonly int WeaponType = Animator.StringToHash("WeaponType");

        // Triggers
        public static readonly int Jump = Animator.StringToHash("Jump");
        public static readonly int Land = Animator.StringToHash("Land");
        public static readonly int Fire = Animator.StringToHash("Fire");
        public static readonly int Reload = Animator.StringToHash("Reload");
        public static readonly int Death = Animator.StringToHash("Death");
        public static readonly int Hit = Animator.StringToHash("Hit");

        // State Names (for checking current state)
        public static readonly int IdleState = Animator.StringToHash("Idle");
        public static readonly int WalkState = Animator.StringToHash("Walk");
        public static readonly int RunState = Animator.StringToHash("Run");
        public static readonly int SprintState = Animator.StringToHash("Sprint");
        public static readonly int JumpState = Animator.StringToHash("Jump");
        public static readonly int FallState = Animator.StringToHash("Fall");
        public static readonly int CrouchIdleState = Animator.StringToHash("CrouchIdle");
        public static readonly int CrouchWalkState = Animator.StringToHash("CrouchWalk");
        public static readonly int DeathState = Animator.StringToHash("Death");

        // Layer indices
        public const int BaseLayer = 0;
        public const int UpperBodyLayer = 1;
        public const int WeaponLayer = 2;
    }
}
