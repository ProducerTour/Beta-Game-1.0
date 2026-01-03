using UnityEngine;
using UnityEditor;
using CreatorWorld.Items;
using System.IO;

namespace CreatorWorld.Editor
{
    /// <summary>
    /// Editor utility to create and populate the Item Database.
    /// </summary>
    public class ItemDatabaseSetup
    {
        private const string ITEMS_PATH = "Assets/_Project/ScriptableObjects/Items";
        private const string DATABASE_PATH = "Assets/_Project/ScriptableObjects/ItemDatabase.asset";

        [MenuItem("Tools/Creator World/Create Item Database", priority = 20)]
        public static void CreateItemDatabase()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Error", "Stop Play mode first.", "OK");
                return;
            }

            Debug.Log("========== CREATING ITEM DATABASE ==========");

            // Ensure folders exist
            EnsureFolders();

            // Create starter items
            CreateStarterWeapons();
            CreateStarterAmmo();
            CreateStarterConsumables();
            CreateStarterMaterials();

            // Create or update database
            CreateDatabase();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("========== ITEM DATABASE COMPLETE ==========");
            EditorUtility.DisplayDialog("Item Database Created",
                "Created Item Database with starter items:\n\n" +
                "Weapons: AK-47, Pistol\n" +
                "Ammo: 7.62mm, 9mm\n" +
                "Consumables: Bandage, Food, Water\n" +
                "Materials: Wood, Stone, Metal\n\n" +
                "Find items in: Assets/_Project/ScriptableObjects/Items/",
                "OK");
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/ScriptableObjects"))
                AssetDatabase.CreateFolder("Assets/_Project", "ScriptableObjects");

            if (!AssetDatabase.IsValidFolder(ITEMS_PATH))
                AssetDatabase.CreateFolder("Assets/_Project/ScriptableObjects", "Items");

            // Subfolders
            string[] subfolders = { "Weapons", "Ammo", "Consumables", "Materials" };
            foreach (var folder in subfolders)
            {
                string path = $"{ITEMS_PATH}/{folder}";
                if (!AssetDatabase.IsValidFolder(path))
                    AssetDatabase.CreateFolder(ITEMS_PATH, folder);
            }
        }

        static void CreateStarterWeapons()
        {
            // AK-47
            CreateWeapon("AK-47", new WeaponConfig
            {
                weaponType = Interfaces.WeaponType.Rifle,
                fireMode = FireMode.Automatic,
                damage = 27f,
                rpm = 600,
                magazineSize = 30,
                reloadTime = 2.5f,
                range = 150f,
                baseSpread = 1.2f,
                adsSpread = 0.2f,
                recoilVertical = 1.8f,
                recoilHorizontal = 0.6f,
                rarity = ItemRarity.Common,
                description = "Reliable assault rifle. High damage, moderate accuracy."
            });

            // Pistol
            CreateWeapon("Pistol", new WeaponConfig
            {
                weaponType = Interfaces.WeaponType.Pistol,
                fireMode = FireMode.SemiAuto,
                damage = 35f,
                rpm = 300,
                magazineSize = 12,
                reloadTime = 1.5f,
                range = 50f,
                baseSpread = 0.8f,
                adsSpread = 0.15f,
                recoilVertical = 2f,
                recoilHorizontal = 0.3f,
                rarity = ItemRarity.Common,
                description = "Standard sidearm. Fast reload, good accuracy."
            });
        }

        struct WeaponConfig
        {
            public Interfaces.WeaponType weaponType;
            public FireMode fireMode;
            public float damage;
            public int rpm;
            public int magazineSize;
            public float reloadTime;
            public float range;
            public float baseSpread;
            public float adsSpread;
            public float recoilVertical;
            public float recoilHorizontal;
            public ItemRarity rarity;
            public string description;
        }

        static void CreateWeapon(string name, WeaponConfig config)
        {
            string path = $"{ITEMS_PATH}/Weapons/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<WeaponData>(path) != null) return;

            var weapon = ScriptableObject.CreateInstance<WeaponData>();
            weapon.itemId = "weapon_" + name.ToLower().Replace(" ", "_").Replace("-", "");
            weapon.displayName = name;
            weapon.description = config.description;
            weapon.category = ItemCategory.Weapon;
            weapon.rarity = config.rarity;
            weapon.isStackable = false;
            weapon.maxStackSize = 1;

            weapon.weaponType = config.weaponType;
            weapon.fireMode = config.fireMode;
            weapon.damage = config.damage;
            weapon.rpm = config.rpm;
            weapon.magazineSize = config.magazineSize;
            weapon.reloadTime = config.reloadTime;
            weapon.range = config.range;
            weapon.baseSpread = config.baseSpread;
            weapon.adsSpread = config.adsSpread;
            weapon.recoilVertical = config.recoilVertical;
            weapon.recoilHorizontal = config.recoilHorizontal;

            // Try to load icon
            string iconPath = $"Assets/Art/UI/Icons/Weapons/{name.ToLower().Replace("-", "").Replace(" ", "")}.png";
            weapon.icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);

            // Try to load prefab
            string prefabPath = $"Assets/_Project/Prefabs/Weapons/{name.Replace("-", "").Replace(" ", "")}.prefab";
            weapon.heldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            AssetDatabase.CreateAsset(weapon, path);
            Debug.Log($"  Created weapon: {name}");
        }

        static void CreateStarterAmmo()
        {
            // 7.62mm Rifle Ammo
            CreateAmmo("7.62mm Rounds", AmmoCaliber.Rifle_762, 60, "Standard rifle ammunition.");

            // 9mm Pistol Ammo
            CreateAmmo("9mm Rounds", AmmoCaliber.Pistol_9mm, 120, "Standard pistol ammunition.");
        }

        static void CreateAmmo(string name, AmmoCaliber caliber, int stackSize, string description)
        {
            string path = $"{ITEMS_PATH}/Ammo/{name.Replace(" ", "_")}.asset";
            if (AssetDatabase.LoadAssetAtPath<AmmoData>(path) != null) return;

            var ammo = ScriptableObject.CreateInstance<AmmoData>();
            ammo.itemId = "ammo_" + caliber.ToString().ToLower();
            ammo.displayName = name;
            ammo.description = description;
            ammo.category = ItemCategory.Ammo;
            ammo.rarity = ItemRarity.Common;
            ammo.isStackable = true;
            ammo.maxStackSize = stackSize;
            ammo.caliber = caliber;

            AssetDatabase.CreateAsset(ammo, path);
            Debug.Log($"  Created ammo: {name}");
        }

        static void CreateStarterConsumables()
        {
            // Bandage
            CreateConsumable("Bandage", ConsumableType.Medical, new ConsumableStats
            {
                healthRestore = 15f,
                consumeTime = 3f,
                description = "Basic medical supplies. Restores a small amount of health."
            });

            // Medkit
            CreateConsumable("Medkit", ConsumableType.Medical, new ConsumableStats
            {
                healthRestore = 50f,
                consumeTime = 5f,
                rarity = ItemRarity.Uncommon,
                description = "Professional medical kit. Restores significant health."
            });

            // Canned Food
            CreateConsumable("Canned Food", ConsumableType.Food, new ConsumableStats
            {
                hungerRestore = 30f,
                consumeTime = 2f,
                description = "Preserved food. Satisfies hunger."
            });

            // Water Bottle
            CreateConsumable("Water Bottle", ConsumableType.Water, new ConsumableStats
            {
                thirstRestore = 40f,
                consumeTime = 1f,
                description = "Clean drinking water. Quenches thirst."
            });

            // Energy Drink
            CreateConsumable("Energy Drink", ConsumableType.Stimulant, new ConsumableStats
            {
                staminaRestore = 50f,
                thirstRestore = 10f,
                consumeTime = 1f,
                rarity = ItemRarity.Uncommon,
                description = "Caffeinated beverage. Restores stamina quickly."
            });
        }

        struct ConsumableStats
        {
            public float healthRestore;
            public float hungerRestore;
            public float thirstRestore;
            public float staminaRestore;
            public float consumeTime;
            public ItemRarity rarity;
            public string description;
        }

        static void CreateConsumable(string name, ConsumableType type, ConsumableStats stats)
        {
            string path = $"{ITEMS_PATH}/Consumables/{name.Replace(" ", "_")}.asset";
            if (AssetDatabase.LoadAssetAtPath<ConsumableData>(path) != null) return;

            var item = ScriptableObject.CreateInstance<ConsumableData>();
            item.itemId = "consumable_" + name.ToLower().Replace(" ", "_");
            item.displayName = name;
            item.description = stats.description;
            item.category = ItemCategory.Consumable;
            item.rarity = stats.rarity;
            item.isStackable = true;
            item.maxStackSize = 10;

            item.consumableType = type;
            item.healthRestore = stats.healthRestore;
            item.hungerRestore = stats.hungerRestore;
            item.thirstRestore = stats.thirstRestore;
            item.staminaRestore = stats.staminaRestore;
            item.consumeTime = stats.consumeTime;

            AssetDatabase.CreateAsset(item, path);
            Debug.Log($"  Created consumable: {name}");
        }

        static void CreateStarterMaterials()
        {
            // Wood
            CreateMaterial("Wood", MaterialType.Wood, new MaterialStats
            {
                description = "Basic building material. Can be used as fuel.",
                isFuel = true,
                burnTime = 30f
            });

            // Stone
            CreateMaterial("Stone", MaterialType.Stone, new MaterialStats
            {
                description = "Sturdy building material. Resistant to damage."
            });

            // Metal Fragments
            CreateMaterial("Metal Fragments", MaterialType.Metal, new MaterialStats
            {
                rarity = ItemRarity.Uncommon,
                description = "Processed metal. Used for advanced crafting."
            });

            // Scrap
            CreateMaterial("Scrap", MaterialType.Scrap, new MaterialStats
            {
                description = "Miscellaneous salvage. Can be recycled."
            });

            // Cloth
            CreateMaterial("Cloth", MaterialType.Cloth, new MaterialStats
            {
                description = "Fabric scraps. Used for clothing and bandages."
            });
        }

        struct MaterialStats
        {
            public string description;
            public ItemRarity rarity;
            public bool isFuel;
            public float burnTime;
        }

        static void CreateMaterial(string name, MaterialType type, MaterialStats stats)
        {
            string path = $"{ITEMS_PATH}/Materials/{name.Replace(" ", "_")}.asset";
            if (AssetDatabase.LoadAssetAtPath<MaterialData>(path) != null) return;

            var item = ScriptableObject.CreateInstance<MaterialData>();
            item.itemId = "mat_" + name.ToLower().Replace(" ", "_");
            item.displayName = name;
            item.description = stats.description;
            item.category = ItemCategory.Material;
            item.rarity = stats.rarity;
            item.isStackable = true;
            item.maxStackSize = 1000;

            item.materialType = type;
            item.isFuel = stats.isFuel;
            item.burnTime = stats.burnTime;

            AssetDatabase.CreateAsset(item, path);
            Debug.Log($"  Created material: {name}");
        }

        static void CreateDatabase()
        {
            // Load or create database
            var database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DATABASE_PATH);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<ItemDatabase>();
                AssetDatabase.CreateAsset(database, DATABASE_PATH);
                Debug.Log("Created new ItemDatabase");
            }

            // Clear and repopulate
            database.allItems.Clear();

            // Find all items
            string[] guids = AssetDatabase.FindAssets("t:ItemData", new[] { ITEMS_PATH });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item != null)
                {
                    database.allItems.Add(item);
                }
            }

            EditorUtility.SetDirty(database);
            Debug.Log($"Database populated with {database.allItems.Count} items");
        }

        [MenuItem("Tools/Creator World/Refresh Item Database")]
        public static void RefreshDatabase()
        {
            CreateDatabase();
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Database Refreshed",
                $"Item Database updated.",
                "OK");
        }
    }
}
