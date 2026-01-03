using System;
using UnityEngine;

namespace CreatorWorld.Combat
{
    /// <summary>
    /// State machine for weapon actions (firing, reloading, switching).
    /// </summary>
    public class WeaponStateMachine : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float switchDuration = 0.5f;

        // State
        private WeaponState currentState = WeaponState.Idle;
        private float stateTimer;
        private WeaponBase currentWeapon;

        // Properties
        public WeaponState CurrentState => currentState;
        public bool IsIdle => currentState == WeaponState.Idle;
        public bool IsFiring => currentState == WeaponState.Firing;
        public bool IsReloading => currentState == WeaponState.Reloading;
        public bool IsSwitching => currentState == WeaponState.Switching;

        // Events
        public event Action<WeaponState> OnStateChanged;

        /// <summary>
        /// Set the current weapon reference.
        /// </summary>
        public void SetWeapon(WeaponBase weapon)
        {
            currentWeapon = weapon;
        }

        /// <summary>
        /// Start weapon switch animation.
        /// </summary>
        public void StartSwitch()
        {
            if (currentState == WeaponState.Switching) return;

            SetState(WeaponState.Switching);
            stateTimer = switchDuration;
        }

        /// <summary>
        /// Start reload.
        /// </summary>
        public void StartReload()
        {
            if (currentState != WeaponState.Idle) return;
            if (currentWeapon == null) return;

            currentWeapon.StartReload();
            SetState(WeaponState.Reloading);
        }

        /// <summary>
        /// Try to fire the weapon.
        /// </summary>
        public bool TryFire()
        {
            if (currentState != WeaponState.Idle) return false;
            if (currentWeapon == null) return false;

            if (currentWeapon.TryFire())
            {
                SetState(WeaponState.Firing);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Update the state machine.
        /// </summary>
        public void UpdateState()
        {
            switch (currentState)
            {
                case WeaponState.Switching:
                    UpdateSwitching();
                    break;

                case WeaponState.Reloading:
                    UpdateReloading();
                    break;

                case WeaponState.Firing:
                    UpdateFiring();
                    break;
            }
        }

        private void UpdateSwitching()
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0)
            {
                SetState(WeaponState.Idle);
            }
        }

        private void UpdateReloading()
        {
            if (currentWeapon == null || !currentWeapon.IsReloading)
            {
                SetState(WeaponState.Idle);
            }
        }

        private void UpdateFiring()
        {
            // Firing is instant, return to idle immediately
            // (or wait for fire rate cooldown if implementing here)
            SetState(WeaponState.Idle);
        }

        private void SetState(WeaponState newState)
        {
            if (newState == currentState) return;

            currentState = newState;
            OnStateChanged?.Invoke(currentState);
        }

        /// <summary>
        /// Cancel current action and return to idle.
        /// </summary>
        public void Cancel()
        {
            if (currentWeapon != null)
            {
                currentWeapon.CancelReload();
            }
            SetState(WeaponState.Idle);
        }

        /// <summary>
        /// Force a specific state.
        /// </summary>
        public void ForceState(WeaponState state)
        {
            SetState(state);
        }
    }

    /// <summary>
    /// Weapon action states.
    /// </summary>
    public enum WeaponState
    {
        Idle,
        Firing,
        Reloading,
        Switching,
        Holstered
    }
}
