using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace CreatorWorld.Items
{
    /// <summary>
    /// Central registry for all item definitions.
    /// Use this to look up items by ID, get random loot, etc.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "Creator World/Items/Item Database")]
    public class ItemDatabase : ScriptableObject
    {
        [Header("All Items")]
        [Tooltip("All item definitions in the game")]
        public List<ItemData> allItems = new List<ItemData>();

        // Cached lookups (built at runtime)
        private Dictionary<string, ItemData> itemsById;
        private Dictionary<ItemCategory, List<ItemData>> itemsByCategory;
        private bool isInitialized;

        /// <summary>
        /// Initialize lookup dictionaries.
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;

            itemsById = new Dictionary<string, ItemData>();
            itemsByCategory = new Dictionary<ItemCategory, List<ItemData>>();

            // Initialize category lists
            foreach (ItemCategory cat in System.Enum.GetValues(typeof(ItemCategory)))
            {
                itemsByCategory[cat] = new List<ItemData>();
            }

            // Build lookups
            foreach (var item in allItems)
            {
                if (item == null) continue;

                // By ID
                if (!string.IsNullOrEmpty(item.itemId))
                {
                    if (!itemsById.ContainsKey(item.itemId))
                    {
                        itemsById[item.itemId] = item;
                    }
                    else
                    {
                        Debug.LogWarning($"Duplicate item ID: {item.itemId}");
                    }
                }

                // By category
                itemsByCategory[item.category].Add(item);
            }

            isInitialized = true;
            Debug.Log($"ItemDatabase initialized: {allItems.Count} items");
        }

        /// <summary>
        /// Get item by unique ID.
        /// </summary>
        public ItemData GetItemById(string itemId)
        {
            if (!isInitialized) Initialize();

            if (itemsById.TryGetValue(itemId, out var item))
            {
                return item;
            }

            Debug.LogWarning($"Item not found: {itemId}");
            return null;
        }

        /// <summary>
        /// Get all items in a category.
        /// </summary>
        public List<ItemData> GetItemsByCategory(ItemCategory category)
        {
            if (!isInitialized) Initialize();

            return itemsByCategory[category];
        }

        /// <summary>
        /// Get all weapons.
        /// </summary>
        public List<WeaponData> GetAllWeapons()
        {
            if (!isInitialized) Initialize();

            return itemsByCategory[ItemCategory.Weapon]
                .OfType<WeaponData>()
                .ToList();
        }

        /// <summary>
        /// Get all consumables.
        /// </summary>
        public List<ConsumableData> GetAllConsumables()
        {
            if (!isInitialized) Initialize();

            return itemsByCategory[ItemCategory.Consumable]
                .OfType<ConsumableData>()
                .ToList();
        }

        /// <summary>
        /// Get all materials.
        /// </summary>
        public List<MaterialData> GetAllMaterials()
        {
            if (!isInitialized) Initialize();

            return itemsByCategory[ItemCategory.Material]
                .OfType<MaterialData>()
                .ToList();
        }

        /// <summary>
        /// Get random item by rarity (for loot tables).
        /// </summary>
        public ItemData GetRandomItemByRarity(ItemRarity rarity)
        {
            if (!isInitialized) Initialize();

            var matching = allItems.Where(i => i != null && i.rarity == rarity).ToList();
            if (matching.Count == 0) return null;

            return matching[Random.Range(0, matching.Count)];
        }

        /// <summary>
        /// Get random item from category.
        /// </summary>
        public ItemData GetRandomFromCategory(ItemCategory category)
        {
            if (!isInitialized) Initialize();

            var items = itemsByCategory[category];
            if (items.Count == 0) return null;

            return items[Random.Range(0, items.Count)];
        }

        /// <summary>
        /// Get weighted random item (higher rarity = lower chance).
        /// </summary>
        public ItemData GetWeightedRandomItem()
        {
            if (!isInitialized) Initialize();
            if (allItems.Count == 0) return null;

            // Rarity weights (Common = high chance, Legendary = low chance)
            float[] weights = { 50f, 25f, 15f, 8f, 2f }; // Common to Legendary
            float totalWeight = 0f;

            foreach (var item in allItems)
            {
                if (item == null) continue;
                totalWeight += weights[(int)item.rarity];
            }

            float random = Random.Range(0f, totalWeight);
            float current = 0f;

            foreach (var item in allItems)
            {
                if (item == null) continue;
                current += weights[(int)item.rarity];
                if (random <= current)
                {
                    return item;
                }
            }

            return allItems[0];
        }

        /// <summary>
        /// Validate database in editor.
        /// </summary>
        private void OnValidate()
        {
            // Remove null entries
            allItems.RemoveAll(i => i == null);

            // Check for duplicate IDs
            var ids = new HashSet<string>();
            foreach (var item in allItems)
            {
                if (item == null) continue;
                if (!string.IsNullOrEmpty(item.itemId) && !ids.Add(item.itemId))
                {
                    Debug.LogWarning($"Duplicate item ID in database: {item.itemId}");
                }
            }

            // Reset initialization flag to rebuild caches
            isInitialized = false;
        }
    }
}
