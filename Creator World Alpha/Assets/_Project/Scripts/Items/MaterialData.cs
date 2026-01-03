using UnityEngine;

namespace CreatorWorld.Items
{
    /// <summary>
    /// Categories of crafting/building materials.
    /// </summary>
    public enum MaterialType
    {
        Wood,
        Stone,
        Metal,
        Cloth,
        Leather,
        Scrap,
        Electronics,
        Chemical,
        Fuel
    }

    /// <summary>
    /// ScriptableObject defining crafting/building materials.
    /// Used in crafting recipes and building costs.
    /// </summary>
    [CreateAssetMenu(fileName = "NewMaterial", menuName = "Creator World/Items/Material")]
    public class MaterialData : ItemData
    {
        [Header("Material Properties")]
        public MaterialType materialType = MaterialType.Wood;

        [Tooltip("Quality tier (affects crafting results)")]
        [Range(1, 5)]
        public int tier = 1;

        [Header("Gathering")]
        [Tooltip("What tool is needed to gather this?")]
        public string requiredTool = ""; // Empty = hands

        [Tooltip("Time to gather one unit")]
        [Range(0.1f, 10f)]
        public float gatherTime = 1f;

        [Header("Crafting")]
        [Tooltip("Can be used as fuel?")]
        public bool isFuel = false;

        [Tooltip("Burn time in seconds (if fuel)")]
        public float burnTime = 0f;

        [Tooltip("Heat output (if fuel)")]
        public float heatOutput = 0f;

        protected override void OnValidate()
        {
            base.OnValidate();

            isStackable = true;
            category = ItemCategory.Material;

            // Set fuel properties for wood
            if (materialType == MaterialType.Wood && !isFuel)
            {
                isFuel = true;
                burnTime = 30f;
                heatOutput = 10f;
            }

            // Auto-generate ID
            if (string.IsNullOrEmpty(itemId))
            {
                itemId = "mat_" + materialType.ToString().ToLower();
                if (tier > 1)
                {
                    itemId += $"_t{tier}";
                }
            }
        }
    }
}
