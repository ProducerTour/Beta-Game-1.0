using UnityEngine;

namespace CreatorWorld.Items
{
    /// <summary>
    /// Item categories for inventory organization.
    /// </summary>
    public enum ItemCategory
    {
        Weapon,
        Ammo,
        Consumable,
        Material,
        Equipment,
        Tool,
        Buildable
    }

    /// <summary>
    /// Item rarity for loot tables and UI coloring.
    /// </summary>
    public enum ItemRarity
    {
        Common,      // White/Gray
        Uncommon,    // Green
        Rare,        // Blue
        Epic,        // Purple
        Legendary    // Orange/Gold
    }

    /// <summary>
    /// Base ScriptableObject for all items.
    /// Create specific items by inheriting from this class.
    /// </summary>
    [CreateAssetMenu(fileName = "NewItem", menuName = "Creator World/Items/Basic Item")]
    public class ItemData : ScriptableObject
    {
        [Header("Basic Info")]
        [Tooltip("Unique identifier for this item")]
        public string itemId;

        [Tooltip("Display name shown to player")]
        public string displayName;

        [TextArea(2, 4)]
        [Tooltip("Description shown in inventory")]
        public string description;

        [Tooltip("Icon shown in inventory/HUD")]
        public Sprite icon;

        [Header("Classification")]
        public ItemCategory category;
        public ItemRarity rarity = ItemRarity.Common;

        [Header("Stacking")]
        [Tooltip("Can multiple of this item stack in one slot?")]
        public bool isStackable = true;

        [Tooltip("Maximum stack size (1 for non-stackable)")]
        [Range(1, 999)]
        public int maxStackSize = 99;

        [Header("World Representation")]
        [Tooltip("Prefab used when item is dropped in world")]
        public GameObject worldPrefab;

        [Tooltip("Prefab used when item is held/equipped")]
        public GameObject heldPrefab;

        [Header("Economy")]
        [Tooltip("Base value for trading/selling")]
        public int baseValue = 1;

        [Tooltip("Weight in kg (affects carrying capacity)")]
        public float weight = 0.1f;

        /// <summary>
        /// Get the color associated with this item's rarity.
        /// </summary>
        public Color GetRarityColor()
        {
            return rarity switch
            {
                ItemRarity.Common => Color.white,
                ItemRarity.Uncommon => new Color(0.2f, 0.8f, 0.2f),    // Green
                ItemRarity.Rare => new Color(0.2f, 0.4f, 1f),          // Blue
                ItemRarity.Epic => new Color(0.6f, 0.2f, 0.8f),        // Purple
                ItemRarity.Legendary => new Color(1f, 0.6f, 0.1f),     // Orange
                _ => Color.white
            };
        }

        /// <summary>
        /// Called when item is used. Override in subclasses.
        /// </summary>
        public virtual bool Use(GameObject user)
        {
            Debug.Log($"Used item: {displayName}");
            return false; // Return true if consumed
        }

        /// <summary>
        /// Validate item data in editor.
        /// </summary>
        protected virtual void OnValidate()
        {
            // Auto-generate ID from name if empty
            if (string.IsNullOrEmpty(itemId))
            {
                itemId = name.ToLower().Replace(" ", "_");
            }

            // Non-stackable items have max stack of 1
            if (!isStackable)
            {
                maxStackSize = 1;
            }
        }
    }
}
