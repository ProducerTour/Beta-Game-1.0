using System;
using UnityEngine;

namespace CreatorWorld.Combat
{
    /// <summary>
    /// Manages weapon slots and inventory. No input handling.
    /// </summary>
    public class WeaponInventory : MonoBehaviour
    {
        [Header("Weapon Slots")]
        [SerializeField] private WeaponBase primaryWeapon;
        [SerializeField] private WeaponBase secondaryWeapon;

        [Header("References")]
        [SerializeField] private Transform weaponHolder;

        // State
        private int currentSlot = -1; // -1 = holstered
        private WeaponBase currentWeapon;

        // Properties
        public WeaponBase CurrentWeapon => currentWeapon;
        public WeaponBase PrimaryWeapon => primaryWeapon;
        public WeaponBase SecondaryWeapon => secondaryWeapon;
        public int CurrentSlot => currentSlot;
        public bool IsHolstered => currentSlot < 0;
        public bool HasPrimary => primaryWeapon != null;
        public bool HasSecondary => secondaryWeapon != null;

        // Events
        public event Action<WeaponBase, int> OnWeaponChanged; // weapon, slot (-1 = holstered)
        public event Action<WeaponBase> OnWeaponDropped;
        public event Action<WeaponBase, int> OnWeaponPickedUp; // weapon, slot

        private void Awake()
        {
            // Hide weapons immediately on load to prevent visible-during-spawn issue
            HideAllWeapons();
        }

        private void Start()
        {
            // Start holstered (unarmed) - player must press 1/2/Tab to equip
            HideAllWeapons();
            currentSlot = -1;
            currentWeapon = null;
            OnWeaponChanged?.Invoke(null, -1);
        }

        /// <summary>
        /// Equip weapon in specified slot.
        /// </summary>
        public bool EquipSlot(int slot)
        {
            if (slot == currentSlot && currentWeapon != null) return false;

            WeaponBase targetWeapon = GetWeaponInSlot(slot);
            if (targetWeapon == null) return false;

            // Hide ALL weapons first to ensure clean state
            HideAllWeapons();

            // Show and setup new weapon
            currentWeapon = targetWeapon;
            currentSlot = slot;
            currentWeapon.gameObject.SetActive(true);

            // Parent to holder
            if (weaponHolder != null)
            {
                currentWeapon.transform.SetParent(weaponHolder);

                // Apply weapon alignment if available
                var alignment = currentWeapon.GetComponent<WeaponAlignment>();
                if (alignment != null)
                {
                    alignment.ApplyGripOffset();
                }
                else
                {
                    currentWeapon.transform.localPosition = Vector3.zero;
                    currentWeapon.transform.localRotation = Quaternion.identity;
                }
            }

            OnWeaponChanged?.Invoke(currentWeapon, currentSlot);
            return true;
        }

        /// <summary>
        /// Cycle to next available weapon.
        /// </summary>
        public void CycleWeapon()
        {
            if (IsHolstered)
            {
                // Re-equip last weapon
                if (primaryWeapon != null)
                    EquipSlot(0);
                else if (secondaryWeapon != null)
                    EquipSlot(1);
                return;
            }

            int nextSlot = (currentSlot + 1) % 2;
            if (GetWeaponInSlot(nextSlot) != null)
            {
                EquipSlot(nextSlot);
            }
        }

        /// <summary>
        /// Holster current weapon.
        /// </summary>
        public void Holster()
        {
            if (currentWeapon != null)
            {
                currentWeapon.CancelReload();
                currentWeapon.gameObject.SetActive(false);
            }

            currentWeapon = null;
            currentSlot = -1;

            OnWeaponChanged?.Invoke(null, -1);
        }

        /// <summary>
        /// Drop the current weapon.
        /// </summary>
        public void DropCurrentWeapon()
        {
            if (currentWeapon == null) return;

            // Unparent and add physics
            WeaponBase dropped = currentWeapon;
            dropped.transform.SetParent(null);

            var rb = dropped.gameObject.AddComponent<Rigidbody>();
            rb.AddForce(transform.forward * 3f + Vector3.up * 2f, ForceMode.Impulse);

            // Clear slot
            if (currentSlot == 0)
                primaryWeapon = null;
            else if (currentSlot == 1)
                secondaryWeapon = null;

            currentWeapon = null;
            currentSlot = -1;

            OnWeaponDropped?.Invoke(dropped);
            OnWeaponChanged?.Invoke(null, -1);

            // Try to equip another weapon
            CycleWeapon();
        }

        /// <summary>
        /// Set weapon in a specific slot.
        /// </summary>
        public void SetWeapon(int slot, WeaponBase weapon)
        {
            if (slot == 0)
                primaryWeapon = weapon;
            else if (slot == 1)
                secondaryWeapon = weapon;

            if (weapon != null)
            {
                OnWeaponPickedUp?.Invoke(weapon, slot);
            }

            // If this is the current slot, re-equip
            if (slot == currentSlot)
            {
                EquipSlot(slot);
            }
        }

        /// <summary>
        /// Get weapon in specified slot.
        /// </summary>
        public WeaponBase GetWeaponInSlot(int slot)
        {
            return slot switch
            {
                0 => primaryWeapon,
                1 => secondaryWeapon,
                _ => null
            };
        }

        private void HideAllWeapons()
        {
            if (primaryWeapon != null) primaryWeapon.gameObject.SetActive(false);
            if (secondaryWeapon != null) secondaryWeapon.gameObject.SetActive(false);
        }

        /// <summary>
        /// Check if any weapon has ammo for the given type.
        /// </summary>
        public bool HasAmmoForType(CreatorWorld.Interfaces.WeaponType type)
        {
            if (primaryWeapon != null && primaryWeapon.Type == type && primaryWeapon.ReserveAmmo > 0)
                return true;
            if (secondaryWeapon != null && secondaryWeapon.Type == type && secondaryWeapon.ReserveAmmo > 0)
                return true;
            return false;
        }

        /// <summary>
        /// Add ammo to weapons of specified type.
        /// </summary>
        public void AddAmmo(CreatorWorld.Interfaces.WeaponType type, int amount)
        {
            if (primaryWeapon != null && primaryWeapon.Type == type)
                primaryWeapon.AddAmmo(amount);
            if (secondaryWeapon != null && secondaryWeapon.Type == type)
                secondaryWeapon.AddAmmo(amount);
        }
    }
}
