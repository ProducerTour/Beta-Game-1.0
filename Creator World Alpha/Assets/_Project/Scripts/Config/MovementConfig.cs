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

        [Tooltip("Walking speed when aiming down sights in m/s")]
        public float AimWalkSpeed = 1.5f;

        [Tooltip("Walking speed when hip-firing (shooting without ADS) in m/s")]
        public float HipFireWalkSpeed = 2f;

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
        public float JumpForce = 10f;

        [Tooltip("Gravity strength (negative value). AAA games typically use -20 to -40.")]
        public float Gravity = -35f;

        [Tooltip("Extra gravity applied when falling for snappier landings (1.5-2.5)")]
        public float FallMultiplier = 2.2f;

        [Tooltip("Extra gravity when releasing jump early for variable height (1.5-2.5)")]
        public float LowJumpMultiplier = 2.5f;

        [Tooltip("Velocity threshold for apex hang (reduces gravity near peak). Set 0 to disable.")]
        public float ApexHangThreshold = 2f;

        [Tooltip("Gravity multiplier at apex (0.3-0.7 for floaty peak, 1.0 = no effect)")]
        [Range(0.1f, 1f)]
        public float ApexHangMultiplier = 0.4f;

        [Tooltip("Time after leaving ground when jump is still allowed")]
        public float CoyoteTime = 0.15f;

        [Tooltip("Time before landing when jump input is buffered")]
        public float JumpBufferTime = 0.15f;

        [Tooltip("Terminal falling velocity")]
        public float TerminalVelocity = 25f;

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

        [Header("Sliding")]
        [Tooltip("Height during slide")]
        public float SlideHeight = 0.8f;

        [Tooltip("Initial slide speed (boosted from current speed)")]
        public float SlideSpeedBoost = 1.5f;

        [Tooltip("How quickly slide speed decays")]
        public float SlideDeceleration = 3f;

        [Tooltip("Duration of slide in seconds")]
        public float SlideDuration = 0.8f;

        [Tooltip("Minimum speed required to initiate slide")]
        public float MinSlideSpeed = 3f;

        [Tooltip("Time window to detect double-tap (seconds)")]
        public float DoubleTapWindow = 0.3f;

        [Tooltip("Cooldown between slides (seconds)")]
        public float SlideCooldown = 0.5f;

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
