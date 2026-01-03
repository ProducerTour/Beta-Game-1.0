using UnityEngine;
using CreatorWorld.Core;
using CreatorWorld.Interfaces;
using CreatorWorld.Player;

namespace CreatorWorld.Combat
{
    /// <summary>
    /// Manages weapon combat. Delegates inventory to WeaponInventory.
    /// Uses InputService instead of direct input reading.
    /// </summary>
    public class WeaponManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WeaponInventory inventory;
        [SerializeField] private WeaponStateMachine stateMachine;

        // Services
        private IInputService input;
        private PlayerAnimation playerAnimation;

        // State
        private bool wasFirePressed;

        // Properties
        public WeaponBase CurrentWeapon => inventory?.CurrentWeapon;
        public bool IsAiming => input?.AimHeld ?? false;
        public bool IsSwitching => stateMachine?.IsSwitching ?? false;
        public bool IsReloading => stateMachine?.IsReloading ?? false;

        private void Awake()
        {
            if (inventory == null) inventory = GetComponent<WeaponInventory>();
            if (stateMachine == null) stateMachine = GetComponent<WeaponStateMachine>();
            playerAnimation = GetComponent<PlayerAnimation>();

            // Subscribe to inventory events
            if (inventory != null)
            {
                inventory.OnWeaponChanged += OnWeaponChanged;
            }
        }

        private void OnDestroy()
        {
            if (inventory != null)
            {
                inventory.OnWeaponChanged -= OnWeaponChanged;
            }
        }

        private void Start()
        {
            input = ServiceLocator.Get<IInputService>();
        }

        private void Update()
        {
            if (input == null)
            {
                input = ServiceLocator.Get<IInputService>();
                if (input == null) return;
            }

            stateMachine?.UpdateState();
            HandleInput();
        }

        private void HandleInput()
        {
            // Weapon switching - always allow even when holstered
            if (input.WeaponSwitch1Pressed)
            {
                Debug.Log($">>> WEAPON 1 - HasPrimary: {inventory?.HasPrimary}, Primary: {inventory?.PrimaryWeapon} <<<");
                inventory?.EquipSlot(0);
                stateMachine?.StartSwitch();
            }
            else if (input.WeaponSwitch2Pressed)
            {
                Debug.Log($">>> WEAPON 2 - HasSecondary: {inventory?.HasSecondary}, Secondary: {inventory?.SecondaryWeapon} <<<");
                inventory?.EquipSlot(1);
                stateMachine?.StartSwitch();
            }
            else if (input.WeaponCyclePressed)
            {
                Debug.Log(">>> WEAPON CYCLE <<<");
                inventory?.CycleWeapon();
                stateMachine?.StartSwitch();
            }
            else if (input.HolsterPressed)
            {
                Debug.Log(">>> HOLSTER <<<");
                if (inventory != null)
                {
                    if (inventory.IsHolstered)
                        inventory.CycleWeapon();
                    else
                        inventory.Holster();
                }
                stateMachine?.StartSwitch();
            }

            // Need a weapon for the rest
            var weapon = inventory?.CurrentWeapon;
            if (weapon == null) return;

            // Can't do actions while switching
            if (stateMachine != null && !stateMachine.IsIdle) return;

            // Reload
            if (input.ReloadPressed)
            {
                stateMachine?.StartReload();
                playerAnimation?.TriggerReload();
            }

            // Fire mode toggle
            if (input.FireModePressed && weapon is Rifle rifle)
            {
                rifle.ToggleFireMode();
            }

            // Firing
            HandleFiring(weapon);
        }

        private void HandleFiring(WeaponBase weapon)
        {
            if (weapon.IsAutomatic)
            {
                // Auto weapons fire while held
                if (input.FireHeld)
                {
                    if (weapon.TryFire())
                    {
                        playerAnimation?.TriggerFire();
                    }
                }
            }
            else
            {
                // Semi-auto fires on press only
                if (input.FirePressed && !wasFirePressed)
                {
                    if (weapon.TryFire())
                    {
                        playerAnimation?.TriggerFire();
                    }
                }
            }

            wasFirePressed = input.FirePressed;
        }

        private void OnWeaponChanged(WeaponBase weapon, int slot)
        {
            stateMachine?.SetWeapon(weapon);

            if (weapon != null)
            {
                playerAnimation?.SetWeaponType(weapon.Type);
            }
            else
            {
                playerAnimation?.SetWeaponType(Interfaces.WeaponType.None);
            }
        }

        // Public API for external systems
        public void DropCurrentWeapon() => inventory?.DropCurrentWeapon();
        public void Holster() => inventory?.Holster();
        public bool HasAmmoForType(Interfaces.WeaponType type) => inventory?.HasAmmoForType(type) ?? false;
        public void AddAmmo(Interfaces.WeaponType type, int amount) => inventory?.AddAmmo(type, amount);
    }
}
