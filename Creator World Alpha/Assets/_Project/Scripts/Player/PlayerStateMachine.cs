using System;
using UnityEngine;
using CreatorWorld.Player.Movement;

namespace CreatorWorld.Player
{
    /// <summary>
    /// Formal state machine for player locomotion states.
    /// Replaces scattered boolean flags with explicit state transitions.
    /// </summary>
    public class PlayerStateMachine : MonoBehaviour
    {
        // Dependencies
        private GroundChecker groundChecker;
        private MovementHandler movementHandler;
        private JumpController jumpController;
        private CrouchHandler crouchHandler;

        // State
        private PlayerState currentState = PlayerState.Idle;
        private PlayerState previousState = PlayerState.Idle;

        // Properties
        public PlayerState CurrentState => currentState;
        public PlayerState PreviousState => previousState;

        // Events
        public event Action<PlayerState, PlayerState> OnStateChanged;

        private void Awake()
        {
            groundChecker = GetComponent<GroundChecker>();
            movementHandler = GetComponent<MovementHandler>();
            jumpController = GetComponent<JumpController>();
            crouchHandler = GetComponent<CrouchHandler>();

            // Also check parent for components
            if (groundChecker == null) groundChecker = GetComponentInParent<GroundChecker>();
            if (movementHandler == null) movementHandler = GetComponentInParent<MovementHandler>();
            if (jumpController == null) jumpController = GetComponentInParent<JumpController>();
            if (crouchHandler == null) crouchHandler = GetComponentInParent<CrouchHandler>();
        }

        /// <summary>
        /// Update the state machine. Call from PlayerController.
        /// </summary>
        public void UpdateState()
        {
            PlayerState newState = DetermineState();

            if (newState != currentState)
            {
                TransitionTo(newState);
            }
        }

        private PlayerState DetermineState()
        {
            // Priority-based state determination

            // Air states (highest priority)
            if (!groundChecker.IsGrounded)
            {
                if (jumpController.IsJumping)
                    return PlayerState.Jumping;
                if (jumpController.IsFalling)
                    return PlayerState.Falling;
            }

            // Just landed
            if (groundChecker.JustLanded)
            {
                return PlayerState.Landing;
            }

            // Ground states
            bool isCrouching = crouchHandler != null && crouchHandler.IsCrouching;
            bool isMoving = movementHandler != null && movementHandler.IsMoving;
            bool isSprinting = movementHandler != null && movementHandler.IsSprinting;

            if (isCrouching)
            {
                return isMoving ? PlayerState.CrouchWalking : PlayerState.Crouching;
            }

            if (isSprinting)
            {
                return PlayerState.Sprinting;
            }

            if (isMoving)
            {
                // Determine walk vs run based on speed
                float normalizedSpeed = movementHandler.NormalizedSpeed;
                if (normalizedSpeed > 0.7f)
                    return PlayerState.Running;
                return PlayerState.Walking;
            }

            return PlayerState.Idle;
        }

        private void TransitionTo(PlayerState newState)
        {
            // Exit current state
            OnExitState(currentState);

            previousState = currentState;
            currentState = newState;

            // Enter new state
            OnEnterState(newState);

            // Notify listeners
            OnStateChanged?.Invoke(previousState, currentState);
        }

        private void OnEnterState(PlayerState state)
        {
            // State-specific enter logic can be added here
            switch (state)
            {
                case PlayerState.Landing:
                    // Brief landing state, will transition on next frame
                    break;
            }
        }

        private void OnExitState(PlayerState state)
        {
            // State-specific exit logic can be added here
        }

        /// <summary>
        /// Force transition to a specific state (e.g., for death).
        /// </summary>
        public void ForceState(PlayerState state)
        {
            TransitionTo(state);
        }

        /// <summary>
        /// Check if currently in any of the given states.
        /// </summary>
        public bool IsInState(params PlayerState[] states)
        {
            foreach (var state in states)
            {
                if (currentState == state) return true;
            }
            return false;
        }

        /// <summary>
        /// Check if player is in any grounded state.
        /// </summary>
        public bool IsGroundedState()
        {
            return IsInState(
                PlayerState.Idle,
                PlayerState.Walking,
                PlayerState.Running,
                PlayerState.Sprinting,
                PlayerState.Crouching,
                PlayerState.CrouchWalking,
                PlayerState.Landing
            );
        }

        /// <summary>
        /// Check if player is in any airborne state.
        /// </summary>
        public bool IsAirborneState()
        {
            return IsInState(PlayerState.Jumping, PlayerState.Falling);
        }
    }

    /// <summary>
    /// All possible player locomotion states.
    /// </summary>
    public enum PlayerState
    {
        Idle,
        Walking,
        Running,
        Sprinting,
        Crouching,
        CrouchWalking,
        Jumping,
        Falling,
        Landing,
        Dead
    }
}
