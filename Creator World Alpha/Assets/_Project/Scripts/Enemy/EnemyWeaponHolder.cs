using UnityEngine;

namespace CreatorWorld.Enemy
{
    /// <summary>
    /// Holds and manages weapons for enemy NPCs.
    /// Spawns the assigned weapon prefab at the weapon hold point on start.
    /// </summary>
    public class EnemyWeaponHolder : MonoBehaviour
    {
        [Header("Weapon Configuration")]
        [Tooltip("Weapon prefab to spawn (e.g., AK47)")]
        [SerializeField] private GameObject weaponPrefab;

        [Tooltip("Transform where weapon should be held (typically right hand bone)")]
        [SerializeField] private Transform weaponHoldPoint;

        [Header("Weapon Transform Offset")]
        [SerializeField] private Vector3 positionOffset = Vector3.zero;
        [SerializeField] private Vector3 rotationOffset = Vector3.zero;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        // Runtime state
        private GameObject currentWeapon;

        public GameObject CurrentWeapon => currentWeapon;
        public bool HasWeapon => currentWeapon != null;

        private void Start()
        {
            FindWeaponHoldPoint();
            SpawnWeapon();
        }

        private void FindWeaponHoldPoint()
        {
            if (weaponHoldPoint != null) return;

            // Common bone name patterns for right hand across different rigs
            string[] possibleBoneNames = new[]
            {
                "mixamorig:RightHand",
                "RightHand",
                "Right_Hand",
                "hand_r",
                "hand.R",
                "Hand.R",
                "R_Hand",
                "Bip01_R_Hand",
                "Bip001_R_Hand",
                "R Hand",
                "RHand",
                "Grip",
                "WeaponGrip",
                "Weapon_Grip"
            };

            // Search for the weapon bone in the hierarchy
            Transform[] allChildren = GetComponentsInChildren<Transform>(true);

            // First try exact matches
            foreach (Transform child in allChildren)
            {
                string childNameLower = child.name.ToLower();
                foreach (string boneName in possibleBoneNames)
                {
                    if (child.name == boneName || child.name.Equals(boneName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        weaponHoldPoint = child;
                        if (showDebugInfo)
                            Debug.Log($"[EnemyWeaponHolder] Found weapon hold point: {child.name}");
                        return;
                    }
                }
            }

            // Then try partial matches
            foreach (Transform child in allChildren)
            {
                string childNameLower = child.name.ToLower();
                if (childNameLower.Contains("righthand") || childNameLower.Contains("right_hand") ||
                    childNameLower.Contains("hand_r") || childNameLower.Contains("r_hand") ||
                    childNameLower.Contains("grip"))
                {
                    weaponHoldPoint = child;
                    if (showDebugInfo)
                        Debug.Log($"[EnemyWeaponHolder] Found weapon hold point (partial match): {child.name}");
                    return;
                }
            }

            Debug.LogWarning($"[EnemyWeaponHolder] Could not find weapon hold point on {gameObject.name}. " +
                "Please assign manually or check bone names. Available bones:");

            // Log available bones to help debugging
            foreach (Transform child in allChildren)
            {
                if (child.name.ToLower().Contains("hand") || child.name.ToLower().Contains("arm") ||
                    child.name.ToLower().Contains("grip"))
                {
                    Debug.Log($"  - {child.name}");
                }
            }
        }

        /// <summary>
        /// Spawn the configured weapon at the hold point
        /// </summary>
        public void SpawnWeapon()
        {
            if (weaponPrefab == null)
            {
                Debug.LogWarning($"[EnemyWeaponHolder] No weapon prefab assigned to {gameObject.name}");
                return;
            }

            if (weaponHoldPoint == null)
            {
                Debug.LogWarning($"[EnemyWeaponHolder] No weapon hold point found on {gameObject.name}");
                return;
            }

            // Destroy existing weapon if any
            if (currentWeapon != null)
            {
                Destroy(currentWeapon);
            }

            // Spawn new weapon
            currentWeapon = Instantiate(weaponPrefab, weaponHoldPoint);
            currentWeapon.name = weaponPrefab.name;

            // Apply offsets
            currentWeapon.transform.localPosition = positionOffset;
            currentWeapon.transform.localRotation = Quaternion.Euler(rotationOffset);

            // Disable player-specific weapon components
            var weaponBase = currentWeapon.GetComponent<Combat.WeaponBase>();
            if (weaponBase != null)
            {
                // WeaponBase expects player input, so disable it for enemies
                // Enemies will have their own firing logic
                weaponBase.enabled = false;
            }

            if (showDebugInfo)
                Debug.Log($"[EnemyWeaponHolder] Spawned {weaponPrefab.name} on {gameObject.name}");
        }

        /// <summary>
        /// Remove the current weapon
        /// </summary>
        public void RemoveWeapon()
        {
            if (currentWeapon != null)
            {
                Destroy(currentWeapon);
                currentWeapon = null;
            }
        }

        /// <summary>
        /// Swap to a different weapon
        /// </summary>
        public void SwapWeapon(GameObject newWeaponPrefab)
        {
            weaponPrefab = newWeaponPrefab;
            SpawnWeapon();
        }

        /// <summary>
        /// Set the weapon prefab (for runtime configuration)
        /// </summary>
        public void SetWeaponPrefab(GameObject prefab)
        {
            weaponPrefab = prefab;
        }

        private void OnDrawGizmosSelected()
        {
            if (weaponHoldPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(weaponHoldPoint.position, 0.05f);
                Gizmos.DrawLine(weaponHoldPoint.position, weaponHoldPoint.position + weaponHoldPoint.forward * 0.2f);
            }
        }
    }
}
