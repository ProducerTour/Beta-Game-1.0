using UnityEngine;

namespace CreatorWorld.Config
{
    /// <summary>
    /// ScriptableObject containing all movement-related constants.
    /// Create via Assets > Create > Config > Movement Config
    /// </summary>
    [CreateAssetMenu(fileName = "MovementConfig", menuName = "Config/Movement Config")]
    public class MovementConfig : ScriptableObject
    {
        [Header("Movement Speeds")]
        [Tooltip("Walking speed in m/s")]
        public float WalkSpeed = 2.5f;

        [Tooltip("Running speed in m/s (holding shift without forward input)")]
        public float RunSpeed = 4f;

        [Tooltip("Sprinting speed in m/s (shift + forward)")]
        public float SprintSpeed = 5f;

        [Tooltip("Crouching speed in m/s")]
        public float CrouchSpeed = 1.5f;

        [Tooltip("Strafing speed in m/s")]
        public float StrafeSpeed = 2.5f;

        [Header("Acceleration")]
        [Tooltip("How quickly player reaches target speed")]
        public float Acceleration = 40f;

        [Tooltip("How quickly player stops")]
        public float Deceleration = 50f;

        [Tooltip("Movement control multiplier while airborne (0-1)")]
        [Range(0f, 1f)]
        public float AirControlMultiplier = 0.5f;

        [Header("Jumping")]
        [Tooltip("Jump force/height (velocity in m/s)")]
        public float JumpForce = 7f;

        [Tooltip("Gravity strength (negative value)")]
        public float Gravity = -20f;

        [Tooltip("Extra gravity applied when falling for snappier feel")]
        public float FallMultiplier = 1.5f;

        [Tooltip("Extra gravity when releasing jump early for variable height")]
        public float LowJumpMultiplier = 1.2f;

        [Tooltip("Time after leaving ground when jump is still allowed")]
        public float CoyoteTime = 0.12f;

        [Tooltip("Time before landing when jump input is buffered")]
        public float JumpBufferTime = 0.15f;

        [Tooltip("Terminal falling velocity")]
        public float TerminalVelocity = 20f;

        [Header("Ground Detection")]
        [Tooltip("Distance to check for ground below player")]
        public float GroundCheckDistance = 0.15f;

        [Tooltip("Layers considered as ground")]
        public LayerMask GroundMask = ~0; // Default to everything

        [Header("Velocity Limits")]
        [Tooltip("Maximum horizontal velocity")]
        public float MaxHorizontalVelocity = 15f;

        [Header("Slopes")]
        [Tooltip("Maximum walkable slope angle in degrees")]
        public float MaxSlopeAngle = 45f;

        [Tooltip("Speed of sliding down steep slopes")]
        public float SlopeSlideSpeed = 5f;

        [Tooltip("Whether to slide down slopes steeper than max angle")]
        public bool SlideDownSlopes = true;

        [Header("Crouching")]
        [Tooltip("Normal standing height")]
        public float StandingHeight = 2f;

        [Tooltip("Crouching height")]
        public float CrouchHeight = 1.2f;

        [Tooltip("How fast to transition between heights")]
        public float CrouchTransitionSpeed = 8f;

        [Header("Rotation")]
        [Tooltip("How fast player rotates to face movement direction (rad/s)")]
        public float RotationSpeed = 22f;

        [Header("Input Thresholds")]
        [Tooltip("Minimum input magnitude to register as moving")]
        public float MoveDeadzone = 0.1f;

        [Tooltip("Minimum forward input to allow sprinting")]
        public float SprintForwardThreshold = 0.5f;

        [Tooltip("Minimum sideways input to consider strafing")]
        public float StrafeThreshold = 0.3f;
    }
}
