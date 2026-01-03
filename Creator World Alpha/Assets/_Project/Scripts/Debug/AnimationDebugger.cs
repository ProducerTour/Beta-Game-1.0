using UnityEngine;
using CreatorWorld.Player;
using CreatorWorld.Player.Movement;

namespace CreatorWorld.Debugging
{
    /// <summary>
    /// Debug component to visualize animation-related values in real-time.
    /// Add to player and check Console for output.
    /// </summary>
    public class AnimationDebugger : MonoBehaviour
    {
        [Header("Debug Settings")]
        [SerializeField] private bool logToConsole = false;
        [SerializeField] private bool showOnScreen = false; // DevPanel handles this
        [SerializeField] private float logInterval = 0.5f;

        private Animator animator;
        private PlayerController playerController;
        private MovementHandler movementHandler;

        private float lastLogTime;
        private string debugText = "";

        // Cached animator parameter hashes
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int WeaponTypeHash = Animator.StringToHash("WeaponType");
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");

        private void Start()
        {
            animator = GetComponent<Animator>();
            playerController = GetComponent<PlayerController>();
            movementHandler = GetComponent<MovementHandler>();

            if (animator == null)
                UnityEngine.Debug.LogError("[AnimDebug] No Animator found!");
            if (playerController == null)
                UnityEngine.Debug.LogError("[AnimDebug] No PlayerController found!");
            if (movementHandler == null)
                UnityEngine.Debug.LogError("[AnimDebug] No MovementHandler found!");
        }

        private void Update()
        {
            if (animator == null) return;

            // Get current values
            float animatorSpeed = animator.GetFloat(SpeedHash);
            int weaponType = animator.GetInteger(WeaponTypeHash);
            bool isGrounded = animator.GetBool(IsGroundedHash);
            bool isCrouching = animator.GetBool(IsCrouchingHash);

            float normalizedSpeed = playerController != null ? playerController.NormalizedSpeed : -1;
            float currentSpeed = movementHandler != null ? movementHandler.CurrentSpeed : -1;
            bool isSprinting = playerController != null ? playerController.IsSprinting : false;

            // Get current state info
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            string stateName = GetStateName(stateInfo);

            // Build debug text
            debugText = $"=== ANIMATION DEBUG ===\n" +
                       $"Animator Speed: {animatorSpeed:F3}\n" +
                       $"NormalizedSpeed: {normalizedSpeed:F3}\n" +
                       $"CurrentSpeed: {currentSpeed:F2} m/s\n" +
                       $"WeaponType: {weaponType} ({GetWeaponName(weaponType)})\n" +
                       $"Grounded: {isGrounded}\n" +
                       $"Crouching (Anim): {isCrouching}\n" +
                       $"Sprinting: {isSprinting}\n" +
                       $"CURRENT STATE: {stateName}\n" +
                       $"State Time: {stateInfo.normalizedTime:F2}\n" +
                       $"======================";

            // Log periodically
            if (logToConsole && Time.time - lastLogTime > logInterval)
            {
                lastLogTime = Time.time;
                UnityEngine.Debug.Log(debugText);
            }
        }

        private string GetWeaponName(int type)
        {
            return type switch
            {
                0 => "None/Unarmed",
                1 => "Rifle",
                2 => "Pistol",
                _ => "Unknown"
            };
        }

        private string GetStateName(AnimatorStateInfo info)
        {
            // Check known state hashes
            if (info.IsName("Locomotion")) return "Locomotion";
            if (info.IsName("Crouch")) return "Crouch";
            if (info.IsName("Jump")) return "Jump";
            if (info.IsName("Rifle.Locomotion")) return "Rifle.Locomotion";
            if (info.IsName("Pistol.Locomotion")) return "Pistol.Locomotion";
            if (info.IsName("Unarmed.Locomotion")) return "Unarmed.Locomotion";
            return $"Hash: {info.shortNameHash}";
        }

        private void OnGUI()
        {
            if (!showOnScreen) return;

            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.fontSize = 14;
            style.alignment = TextAnchor.UpperLeft;
            style.normal.textColor = Color.white;

            GUI.Box(new Rect(10, 10, 300, 220), debugText, style);
        }
    }
}
