using UnityEngine;

namespace CreatorWorld.Config
{
    /// <summary>
    /// ScriptableObject containing animator blend tree thresholds.
    /// These values must match your Animator Controller blend tree settings.
    /// Create via Assets > Create > Config > Blend Tree Config
    /// </summary>
    [CreateAssetMenu(fileName = "BlendTreeConfig", menuName = "Config/Blend Tree Config")]
    public class BlendTreeConfig : ScriptableObject
    {
        [Header("Locomotion Blend Thresholds")]
        [Tooltip("Animator Speed value for idle state. MUST match blend tree Idle position Y.")]
        public float IdleThreshold = 0f;

        [Tooltip("Animator Speed value for walk state. MUST match blend tree Walk Forward position Y (default: 0.5).")]
        public float WalkThreshold = 0.5f;

        [Tooltip("Animator Speed value for run state. MUST match blend tree Run Forward position Y (default: 1.0).")]
        public float RunThreshold = 1.0f;

        [Tooltip("Animator Speed value for sprint state. Uses same position as run (1.0) - blend tree has no separate sprint.")]
        public float SprintThreshold = 1.0f;

        [Header("Transition Damping")]
        [Tooltip("Smoothing time for locomotion transitions")]
        public float LocomotionDampTime = 0.1f;

        [Tooltip("Smoothing time when aiming (0 for instant)")]
        public float AimingDampTime = 0f;

        [Header("Thresholds")]
        [Tooltip("Minimum speed to register as moving")]
        public float MinMovementThreshold = 0.1f;

        /// <summary>
        /// Maps a physical speed to animator blend threshold.
        /// </summary>
        /// <param name="currentSpeed">Current movement speed in m/s</param>
        /// <param name="walkSpeed">Walk speed from MovementConfig</param>
        /// <param name="runSpeed">Run speed from MovementConfig</param>
        /// <param name="sprintSpeed">Sprint speed from MovementConfig</param>
        /// <returns>Normalized speed for animator (0 to 1)</returns>
        public float GetNormalizedSpeed(float currentSpeed, float walkSpeed, float runSpeed, float sprintSpeed)
        {
            if (currentSpeed < MinMovementThreshold) return IdleThreshold;

            if (currentSpeed <= walkSpeed)
            {
                // 0 to WalkSpeed maps to Idle to Walk threshold
                return Mathf.Lerp(IdleThreshold, WalkThreshold, currentSpeed / walkSpeed);
            }
            else if (currentSpeed <= runSpeed)
            {
                // WalkSpeed to RunSpeed maps to Walk to Run threshold
                float t = (currentSpeed - walkSpeed) / (runSpeed - walkSpeed);
                return Mathf.Lerp(WalkThreshold, RunThreshold, t);
            }
            else
            {
                // RunSpeed to SprintSpeed maps to Run to Sprint threshold
                float t = (currentSpeed - runSpeed) / (sprintSpeed - runSpeed);
                return Mathf.Lerp(RunThreshold, SprintThreshold, t);
            }
        }
    }
}
