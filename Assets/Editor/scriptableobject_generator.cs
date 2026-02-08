#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Generates all game ScriptableObjects from the data tables.
/// Creates mobs, items, recipes with proper dependencies and folder structure.
/// Menu: Tools > Game Data Generator > Generate All Data
/// </summary>
public class GameDataGenerator : EditorWindow
{
    private static string basePath = "Assets/Data";
    private bool overwriteExisting = false;
    private Vector2 scrollPos;
    
    [MenuItem("Tools/Game Data Generator/Open Generator Window")]
    public static void ShowWindow()
    {
        var window = GetWindow<GameDataGenerator>("Game Data Generator");
        window.minSize = new Vector2(500, 600);
    }
    
    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        
        GUILayout.Label("Game Data Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);
        
        EditorGUILayout.HelpBox(
            "This will generate all ScriptableObjects for:\n" +
            "• 13 MobDefs (with loot tables)\n" +
            "• 15 ItemDefs\n" +
            "• 15 RecipeDefs (with dependency chains)\n\n" +
            "Files are organized in Assets/Data/[Category]/",
            MessageType.Info
        );
        
        EditorGUILayout.Space(10);
        
        basePath = EditorGUILayout.TextField("Base Path:", basePath);
        overwriteExisting = EditorGUILayout.Toggle("Overwrite Existing:", overwriteExisting);
        
        EditorGUILayout.Space(20);
        
        // Individual generation buttons
        GUILayout.Label("Generate Individual Systems:", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Generate Items Only", GUILayout.Height(30)))
        {
            GenerateAllItems();
        }
        
        if (GUILayout.Button("Generate Mobs Only", GUILayout.Height(30)))
        {
            GenerateAllMobs();
        }
        
        if (GUILayout.Button("Generate Recipes Only", GUILayout.Height(30)))
        {
            GenerateAllRecipes();
        }
        
        EditorGUILayout.Space(20);
        
        // Generate everything button
        GUILayout.Label("Generate Complete Data:", EditorStyles.boldLabel);
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("GENERATE ALL DATA", GUILayout.Height(50)))
        {
            if (EditorUtility.DisplayDialog(
                "Generate All Game Data",
                "This will create 43 ScriptableObjects. Continue?",
                "Generate",
                "Cancel"))
            {
                GenerateAllData();
            }
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.Space(20);
        
        // Cleanup button
        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
        if (GUILayout.Button("Delete All Generated Data", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog(
                "Delete All Data",
                "This will DELETE all generated ScriptableObjects. This cannot be undone!",
                "Delete",
                "Cancel"))
            {
                DeleteAllData();
            }
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.EndScrollView();
    }
    
    // ===== MAIN GENERATION METHODS =====
    
    private void GenerateAllData()
    {
        Debug.Log("=== Starting Complete Game Data Generation ===");
        
        // Order matters: Items → Mobs → Recipes (recipes depend on items)
        GenerateAllItems();
        GenerateAllMobs();
        GenerateAllRecipes();
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log("=== Game Data Generation Complete! ===");
        EditorUtility.DisplayDialog("Success", "All game data generated successfully!", "OK");
    }
    
    // ===== ITEM GENERATION =====
    
    private void GenerateAllItems()
    {
        Debug.Log("Generating Items...");
        string itemPath = $"{basePath}/Items";
        EnsureFolderExists(itemPath);
        
        // Layer 1: Forest Materials
        CreateItem(itemPath, "Wood", ItemCategory.Common, 2, 
            "Sturdy branches that sprites tend with care. The foundation of any proper workshop.", 1);
        CreateItem(itemPath, "Slime Gel", ItemCategory.Common, 2, 
            "Viscous goo that's surprisingly useful. Sticks things together and makes great waterproofing!", 1);
        CreateItem(itemPath, "Sprite Dust", ItemCategory.Common, 5, 
            "Shimmering powder that sprites shed naturally. Enhances any craft with a touch of forest magic.", 1);
        
        // Layer 2: Stone & Minerals
        CreateItem(itemPath, "Stone", ItemCategory.Common, 1, 
            "Basic quarry stone. Heavy, reliable, and found everywhere in the caverns.", 2);
        CreateItem(itemPath, "Iron Ore", ItemCategory.Common, 5, 
            "Raw metal veins extracted from golem bodies. The backbone of any smithy's inventory.", 2);
        CreateItem(itemPath, "Crystal Shard", ItemCategory.Common, 10, 
            "Fragments of living crystal. They hum faintly with residual magic when held to light.", 2);
        
        // Layer 3: Beast Materials
        CreateItem(itemPath, "Wolf Pelt", ItemCategory.Crafted, 12, 
            "Thick fur from wild predators. Warm, durable, and smells faintly of pine needles.", 3);
        CreateItem(itemPath, "Bear Hide", ItemCategory.Crafted, 20, 
            "Legendary toughness. Hunters claim bear leather can stop a sword stroke—they're not exaggerating.", 3);
        CreateItem(itemPath, "Spider Silk", ItemCategory.Crafted, 15, 
            "Impossibly strong thread found in monster dens. Lighter than cotton, stronger than steel.", 3);
        
        // Layer 4: Undead Materials
        CreateItem(itemPath, "Bone Fragment", ItemCategory.Crafted, 8, 
            "Ancient bones that refuse to crumble. Necromancers used them for... something. You'd rather not know.", 4);
        CreateItem(itemPath, "Ectoplasm", ItemCategory.Crafted, 12, 
            "Spectral essence that ghosts leave behind. Cold to touch and faintly glowing. Perfect for spirit enchantments.", 4);
        CreateItem(itemPath, "Soul Shard", ItemCategory.Crafted, 30, 
            "Crystallized willpower of the departed. Handle with respect—these were once people.", 4);
        
        // Layer 5: Infernal Materials
        CreateItem(itemPath, "Demon Horn", ItemCategory.Luxury, 25, 
            "Twisted horns that radiate malevolent energy. Prized by dark enchanters and nobles alike.", 5);
        CreateItem(itemPath, "Orc Hide", ItemCategory.Luxury, 30, 
            "Battle-tested leather from warriors who never retreated. The best armor money can buy.", 5);
        CreateItem(itemPath, "Infernal Core", ItemCategory.Luxury, 80, 
            "Hearts of flame from demon lords. They burn eternally without fuel—priceless for master crafters.", 5);
        
        Debug.Log("Items generation complete: 15 items created");
    }
    
    private void CreateItem(string path, string itemName, ItemCategory category, int sellPrice, string description, int layer)
    {
        string fileName = $"Item_{itemName.Replace(" ", "")}";
        string fullPath = $"{path}/{fileName}.asset";
        
        if (!overwriteExisting && File.Exists(fullPath))
        {
            Debug.Log($"Skipping existing item: {itemName}");
            return;
        }
        
        ItemDef item = ScriptableObject.CreateInstance<ItemDef>();
        item.id = fileName;
        item.displayName = itemName;
        item.itemCategory = category;
        item.sellPrice = sellPrice;
        item.baseValue = sellPrice;
        item.description = description;
        item.chance = 1f;
        
        AssetDatabase.CreateAsset(item, fullPath);
        Debug.Log($"Created item: {itemName} (Layer {layer})");
    }
    
    // ===== MOB GENERATION =====
    
    private void GenerateAllMobs()
    {
        Debug.Log("Generating Mobs...");
        string mobPath = $"{basePath}/Mobs";
        EnsureFolderExists(mobPath);
        
        // Layer 1: Forest Glade
        CreateMob(mobPath, "Forest Sprite", 1, 10, false, CombatBehaviorType.Passive, 
            new LootEntry[] {
                new LootEntry("Wood", 80),
                new LootEntry("Sprite Dust", 20)
            });
            
        CreateMob(mobPath, "Blue Slime", 1, 12, true, CombatBehaviorType.Territorial,
            new LootEntry[] {
                new LootEntry("Slime Gel", 85),
                new LootEntry("Sprite Dust", 15)
            }, territorialRadius: 2f);
            
        CreateMob(mobPath, "Deer", 1, 8, false, CombatBehaviorType.Passive,
            new LootEntry[] {
                new LootEntry("Wood", 70),
                new LootEntry("Slime Gel", 30)
            }, spawnWeight: 1.0f);
        
        // Layer 2: Stone Caverns
        CreateMob(mobPath, "Stone Golem", 2, 35, true, CombatBehaviorType.Aggressive,
            new LootEntry[] {
                new LootEntry("Stone", 75),
                new LootEntry("Iron Ore", 25)
            });
            
        CreateMob(mobPath, "Red Slime", 2, 25, true, CombatBehaviorType.Aggressive,
            new LootEntry[] {
                new LootEntry("Stone", 80),
                new LootEntry("Slime Gel", 20)
            }, spawnWeight: 1.8f);
            
        CreateMob(mobPath, "Crystal Golem", 2, 50, true, CombatBehaviorType.Aggressive,
            new LootEntry[] {
                new LootEntry("Iron Ore", 50),
                new LootEntry("Crystal Shard", 50)
            }, spawnWeight: 1.0f);
        
        // Layer 3: Wild Thicket
        CreateMob(mobPath, "Gray Wolf", 3, 60, true, CombatBehaviorType.Aggressive,
            new LootEntry[] {
                new LootEntry("Wolf Pelt", 80),
                new LootEntry("Bear Hide", 20)
            }, scanRange: 12f, spawnWeight: 2.2f);
            
        CreateMob(mobPath, "Brown Bear", 3, 120, true, CombatBehaviorType.Aggressive,
            new LootEntry[] {
                new LootEntry("Bear Hide", 70),
                new LootEntry("Spider Silk", 30)
            }, attackDamage: 15f, spawnWeight: 1.2f);
        
        // Layer 4: Cursed Crypts
        CreateMob(mobPath, "Skeleton", 4, 100, true, CombatBehaviorType.Aggressive,
            new LootEntry[] {
                new LootEntry("Bone Fragment", 80),
                new LootEntry("Soul Shard", 20)
            }, attackInterval: 1.2f);
            
        CreateMob(mobPath, "Ghost", 4, 80, true, CombatBehaviorType.Aggressive,
            new LootEntry[] {
                new LootEntry("Ectoplasm", 85),
                new LootEntry("Soul Shard", 15)
            }, moveSpeed: 2.5f, spawnWeight: 1.5f);
            
        CreateMob(mobPath, "Necromancer", 4, 200, true, CombatBehaviorType.Aggressive,
            new LootEntry[] {
                new LootEntry("Soul Shard", 60),
                new LootEntry("Ectoplasm", 40)
            }, scanRange: 15f, spawnWeight: 0.8f);
        
        // Layer 5: Infernal Depths
        CreateMob(mobPath, "Imp", 5, 150, true, CombatBehaviorType.Aggressive,
            new LootEntry[] {
                new LootEntry("Demon Horn", 80),
                new LootEntry("Orc Hide", 20)
            }, moveSpeed: 2f);
            
        CreateMob(mobPath, "Orc Grunt", 5, 250, true, CombatBehaviorType.Aggressive,
            new LootEntry[] {
                new LootEntry("Orc Hide", 75),
                new LootEntry("Demon Horn", 25)
            }, attackDamage: 20f, spawnWeight: 1.8f);
            
        CreateMob(mobPath, "Demon Lord", 5, 500, true, CombatBehaviorType.Aggressive,
            new LootEntry[] {
                new LootEntry("Infernal Core", 60),
                new LootEntry("Demon Horn", 30),
                new LootEntry("Orc Hide", 10)
            }, attackDamage: 30f, spawnWeight: 0.5f);
        
        Debug.Log("Mob generation complete: 13 mobs created");
    }
    
    private struct LootEntry
    {
        public string itemName;
        public float chance;
        
        public LootEntry(string name, float chance)
        {
            this.itemName = name;
            this.chance = chance;
        }
    }
    
    private void CreateMob(string path, string mobName, int layer, float baseHP, bool canAttack, 
        CombatBehaviorType behaviorType, LootEntry[] lootTable, 
        float moveSpeed = 1.5f, float attackDamage = 5f, float attackInterval = 1.5f, 
        float attackRange = 1.5f, float scanRange = 10f, float territorialRadius = 0f,
        float spawnWeight = 2.0f)
    {
        string fileName = $"Mob_{mobName.Replace(" ", "")}";
        string fullPath = $"{path}/{fileName}.asset";
        
        if (!overwriteExisting && File.Exists(fullPath))
        {
            Debug.Log($"Skipping existing mob: {mobName}");
            return;
        }
        
        MobDef mob = ScriptableObject.CreateInstance<MobDef>();
        mob.id = fileName;
        mob.displayName = mobName;
        mob.assignedLayer = layer;
        mob.baseHealth = baseHP;
        mob.moveSpeed = moveSpeed;
        mob.attackDamage = attackDamage;
        mob.attackInterval = attackInterval;
        mob.attackRange = attackRange;
        mob.scanRange = scanRange;
        mob.spawnWeight = spawnWeight;
        mob.description = GetMobDescription(mobName);
        mob.lootDropAmount = new Vector2Int(1, 2); // Drop 1-2 items
        
        // Combat config
        mob.combatConfig = new CombatConfig
        {
            canAttack = canAttack,
            behaviorType = behaviorType,
            hostileTo = canAttack ? HostilityTargets.Adventurers : HostilityTargets.None,
            territorialRadius = territorialRadius
        };
        
        // Loot table - MobDef uses List<ItemDef> with ItemDef.chance field
        mob.loot = new List<ItemDef>();
        foreach (var entry in lootTable)
        {
            ItemDef item = FindItem(entry.itemName);
            if (item != null)
            {
                // Set the chance on the ItemDef itself
                item.chance = entry.chance / 100f; // Convert percentage to 0-1
                mob.loot.Add(item);
                EditorUtility.SetDirty(item); // Mark item as modified
            }
            else
            {
                Debug.LogWarning($"Item not found for loot table: {entry.itemName}");
            }
        }
        
        AssetDatabase.CreateAsset(mob, fullPath);
        EditorUtility.SetDirty(mob);
        Debug.Log($"Created mob: {mobName} (Layer {layer}, HP: {baseHP}, {mob.loot.Count} loot items)");
    }
    
    // ===== RECIPE GENERATION =====
    
    private void GenerateAllRecipes()
    {
        Debug.Log("Generating Recipes...");
        string recipePath = $"{basePath}/Recipes";
        EnsureFolderExists(recipePath);
        
        // IMPORTANT: Create recipes in dependency order (base recipes first, then chains)
        
        // TIER 1: Tutorial Recipes
        CreateRecipe(recipePath, "Wooden Club", "Wooden Club", 10, 5f,
            new RecipeIngredient[] {
                Ingredient("Wood", 2),
                Ingredient("Slime Gel", 1)
            });
            
        CreateRecipe(recipePath, "Leather Armor", "Leather Armor", 20, 8f,
            new RecipeIngredient[] {
                Ingredient("Wood", 3),
                Ingredient("Sprite Dust", 1)
            });
        
        // TIER 2: Early Game Recipes
        CreateRecipe(recipePath, "Stone Sword", "Stone Sword", 30, 10f,
            new RecipeIngredient[] {
                FromRecipe("Wooden Club"), // Recipe chain!
                Ingredient("Stone", 2),
                Ingredient("Slime Gel", 1)
            });
            
        CreateRecipe(recipePath, "Iron Sword", "Iron Sword", 60, 15f,
            new RecipeIngredient[] {
                FromRecipe("Stone Sword"), // Recipe chain!
                Ingredient("Iron Ore", 2),
                Ingredient("Wood", 1)
            });
            
        CreateRecipe(recipePath, "Crystal Focus", "Crystal Focus", 80, 18f,
            new RecipeIngredient[] {
                Ingredient("Crystal Shard", 1),
                Ingredient("Iron Ore", 1),
                Ingredient("Sprite Dust", 1)
            });
        
        // TIER 3: Mid Game Recipes
        CreateRecipe(recipePath, "Wolf Armor", "Wolf Armor", 120, 25f,
            new RecipeIngredient[] {
                FromRecipe("Leather Armor"), // Recipe chain!
                Ingredient("Wolf Pelt", 2),
                Ingredient("Iron Ore", 1)
            });
            
        CreateRecipe(recipePath, "Bear Cloak", "Bear Cloak", 200, 30f,
            new RecipeIngredient[] {
                FromRecipe("Wolf Armor"), // Recipe chain!
                Ingredient("Bear Hide", 1),
                Ingredient("Spider Silk", 1)
            });
            
        CreateRecipe(recipePath, "Enchanted Ring", "Enchanted Ring", 150, 28f,
            new RecipeIngredient[] {
                FromRecipe("Crystal Focus"), // Recipe chain!
                Ingredient("Spider Silk", 1),
                Ingredient("Wolf Pelt", 1)
            });
        
        // TIER 4: Late Game Recipes
        CreateRecipe(recipePath, "Bone Blade", "Bone Blade", 300, 35f,
            new RecipeIngredient[] {
                FromRecipe("Iron Sword"), // Recipe chain!
                Ingredient("Bone Fragment", 1),
                Ingredient("Soul Shard", 1)
            });
            
        CreateRecipe(recipePath, "Spectral Robe", "Spectral Robe", 350, 40f,
            new RecipeIngredient[] {
                FromRecipe("Bear Cloak"), // Recipe chain!
                Ingredient("Ectoplasm", 2),
                Ingredient("Soul Shard", 1)
            });
            
        CreateRecipe(recipePath, "Soul Gem", "Soul Gem", 400, 38f,
            new RecipeIngredient[] {
                FromRecipe("Enchanted Ring"), // Recipe chain!
                Ingredient("Soul Shard", 2),
                Ingredient("Ectoplasm", 1)
            });
        
        // TIER 5: Endgame Recipes
        CreateRecipe(recipePath, "Demon Warblade", "Demon Warblade", 600, 50f,
            new RecipeIngredient[] {
                FromRecipe("Bone Blade"), // Recipe chain!
                Ingredient("Demon Horn", 1),
                Ingredient("Infernal Core", 1)
            });
            
        CreateRecipe(recipePath, "Abyssal Armor", "Abyssal Armor", 800, 55f,
            new RecipeIngredient[] {
                FromRecipe("Spectral Robe"), // Recipe chain!
                Ingredient("Orc Hide", 2),
                Ingredient("Infernal Core", 1)
            });
            
        CreateRecipe(recipePath, "Infernal Amulet", "Infernal Amulet", 700, 48f,
            new RecipeIngredient[] {
                FromRecipe("Soul Gem"), // Recipe chain!
                Ingredient("Demon Horn", 1),
                Ingredient("Orc Hide", 1)
            });
        
        Debug.Log("Recipe generation complete: 15 recipes created");
    }
    
    private struct RecipeIngredient
    {
        public string itemName;
        public string recipeName;
        public int quantity;
        public bool isRecipe;
    }
    
    private RecipeIngredient Ingredient(string itemName, int qty)
    {
        return new RecipeIngredient { itemName = itemName, quantity = qty, isRecipe = false };
    }
    
    private RecipeIngredient FromRecipe(string recipeName)
    {
        return new RecipeIngredient { recipeName = recipeName, quantity = 1, isRecipe = true };
    }
    
    private void CreateRecipe(string path, string recipeName, string outputItemName, 
        int sellPrice, float craftTime, RecipeIngredient[] ingredients)
    {
        string fileName = $"Recipe_{recipeName.Replace(" ", "")}";
        string fullPath = $"{path}/{fileName}.asset";
        
        if (!overwriteExisting && File.Exists(fullPath))
        {
            Debug.Log($"Skipping existing recipe: {recipeName}");
            return;
        }
        
        RecipeDef recipe = ScriptableObject.CreateInstance<RecipeDef>();
        AssetDatabase.CreateAsset(recipe, fullPath);
        
        // Use SerializedObject to set private fields
        SerializedObject so = new SerializedObject(recipe);
        
        // Set output item
        ItemDef outputItem = FindOrCreateCraftedItem(outputItemName, sellPrice);
        SerializedProperty outputProp = so.FindProperty("output");
        outputProp.objectReferenceValue = outputItem;
        
        // Set output quantity
        SerializedProperty outputQtyProp = so.FindProperty("outputQty");
        outputQtyProp.intValue = 1;
        
        // Set craft time
        SerializedProperty craftSecondsProp = so.FindProperty("craftSeconds");
        craftSecondsProp.floatValue = craftTime;
        
        // Set ingredients
        SerializedProperty ingredientsProp = so.FindProperty("ingredients");
        ingredientsProp.ClearArray();
        
        foreach (var ing in ingredients)
        {
            ItemDef item = null;
            
            if (ing.isRecipe)
            {
                // This ingredient is a crafted item from another recipe
                item = FindOrCreateCraftedItem(ing.recipeName, 0);
            }
            else
            {
                // Regular item ingredient
                item = FindItem(ing.itemName);
            }
            
            if (item != null)
            {
                ingredientsProp.InsertArrayElementAtIndex(ingredientsProp.arraySize);
                SerializedProperty element = ingredientsProp.GetArrayElementAtIndex(ingredientsProp.arraySize - 1);
                
                SerializedProperty itemProp = element.FindPropertyRelative("item");
                SerializedProperty qtyProp = element.FindPropertyRelative("qty");
                
                itemProp.objectReferenceValue = item;
                qtyProp.intValue = ing.quantity;
            }
            else
            {
                Debug.LogError($"Item not found for recipe {recipeName}: {(ing.isRecipe ? ing.recipeName : ing.itemName)}");
            }
        }
        
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(recipe);
        
        Debug.Log($"Created recipe: {recipeName} → {outputItemName}");
    }
    
    // ===== HELPER METHODS =====
    
    private string GetMobDescription(string mobName)
    {
        return mobName switch
        {
            "Forest Sprite" => "Gentle forest guardians who shed magical dust as they tend to the trees. Too shy to fight back.",
            "Blue Slime" => "Gelatinous blobs that wobble indignantly when you get too close. Surprisingly territorial about their puddles.",
            "Deer" => "Peaceful woodland dwellers. They drop antlers naturally—no harm done! (They're just clumsy.)",
            "Stone Golem" => "Ancient constructs awakened by your presence. They crumble reluctantly, leaving valuable minerals behind.",
            "Red Slime" => "Molten cousins of the blue variety. Angrier, hotter, and somehow managed to absorb rocks.",
            "Crystal Golem" => "Rare guardians protecting crystalline veins. Their bodies refract light beautifully before shattering into treasure.",
            "Gray Wolf" => "Pack hunters with keen senses. They spot intruders from afar and coordinate their attacks with eerie precision.",
            "Brown Bear" => "Territorial titans who don't take kindly to dungeon delvers. Somehow their dens are always full of spider silk.",
            "Skeleton" => "Restless bones animated by lingering magic. They're not evil—just very committed to their guard duty.",
            "Ghost" => "Wispy spirits who phase through walls. Their ectoplasm is surprisingly useful for enchanting.",
            "Necromancer" => "Former scholars who took 'eternal study' too literally. They're protective of their soul gem collections.",
            "Imp" => "Mischievous fiends who dart around cackling. Their horns are prized for their dark enchantment properties.",
            "Orc Grunt" => "Brutish warriors wearing surprisingly well-crafted leather. They hit hard and ask questions never.",
            "Demon Lord" => "Ancient powers from beyond the veil. Their infernal cores burn with otherworldly flame—legendary crafting material.",
            _ => $"Mysterious creature"
        };
    }
    
    private string GetCraftedItemDescription(string itemName)
    {
        return itemName switch
        {
            "Wooden Club" => "A solid stick with slime-wrapped grip. Simple, effective, and surprisingly popular with farmers.",
            "Leather Armor" => "Wooden plates laced with sprite magic for flexibility. It's not much, but it keeps the rain off.",
            "Stone Sword" => "Sharpened stone blade mounted on a club handle. Crude but deadly—and way better than just a stick.",
            "Iron Sword" => "A proper blade at last! Forged from golem ore, it holds an edge that stone never could.",
            "Crystal Focus" => "A gemstone set in an iron frame, blessed with sprite magic. Mages clutch these like their lives depend on it.",
            "Wolf Armor" => "Reinforced leather with wolf fur trim and iron buckles. Rangers swear it lets them move like their namesake.",
            "Bear Cloak" => "Heavy bear leather lined with spider silk for impossible flexibility. Warriors call it 'the unkillable coat.'",
            "Enchanted Ring" => "Crystal focus reshaped into a ring, wrapped in enchanted silk. The gem pulses faintly with stored mana.",
            "Bone Blade" => "Iron blade grafted with spectral bones. It cuts flesh and spirit alike—unnerving but undeniably effective.",
            "Spectral Robe" => "Bear hide infused with ghost essence. Wearers report feeling lighter, colder... and occasionally invisible.",
            "Soul Gem" => "A crystal ring supercharged with captured souls. Necromancers pay fortunes for these. You try not to think about it.",
            "Demon Warblade" => "A bone blade reforged in demon flame. The edge burns with hellfire—no sheath can contain it.",
            "Abyssal Armor" => "Orc leather bound with spectral wards and demon fire. Nothing alive or dead can pierce it—or so the legends claim.",
            "Infernal Amulet" => "A soul gem wrapped in demon horn and orc leather. It whispers promises of power—best not to listen too closely.",
            _ => "Masterfully crafted item"
        };
    }
    
    private void EnsureFolderExists(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parentPath = Path.GetDirectoryName(path);
            string folderName = Path.GetFileName(path);
            
            if (!string.IsNullOrEmpty(parentPath))
            {
                EnsureFolderExists(parentPath);
            }
            
            AssetDatabase.CreateFolder(parentPath, folderName);
        }
    }
    
    private ItemDef FindItem(string itemName)
    {
        string searchName = itemName.Replace(" ", "");
        string[] guids = AssetDatabase.FindAssets($"Item_{searchName} t:ItemDef");
        
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<ItemDef>(path);
        }
        
        return null;
    }
    
    private ItemDef FindOrCreateCraftedItem(string itemName, int sellPrice)
    {
        // First try to find existing
        ItemDef existing = FindItem(itemName);
        if (existing != null)
            return existing;
        
        // If it doesn't exist and we're creating it for a recipe output, create it
        if (sellPrice > 0)
        {
            string itemPath = $"{basePath}/Items";
            EnsureFolderExists(itemPath);
            
            string fileName = $"Item_{itemName.Replace(" ", "")}";
            string fullPath = $"{itemPath}/{fileName}.asset";
            
            ItemDef item = ScriptableObject.CreateInstance<ItemDef>();
            item.id = fileName;
            item.displayName = itemName;
            item.itemCategory = DetermineCategory(sellPrice);
            item.sellPrice = sellPrice;
            item.baseValue = sellPrice;
            item.description = GetCraftedItemDescription(itemName);
            item.chance = 1f;
            
            AssetDatabase.CreateAsset(item, fullPath);
            Debug.Log($"Auto-created crafted item: {itemName}");
            return item;
        }
        
        return null;
    }
    
    private ItemCategory DetermineCategory(int sellPrice)
    {
        if (sellPrice >= 300) return ItemCategory.Luxury;
        if (sellPrice >= 100) return ItemCategory.Crafted;
        return ItemCategory.Common;
    }
    
    private void DeleteAllData()
    {
        string[] folders = new string[] 
        { 
            $"{basePath}/Items",
            $"{basePath}/Mobs", 
            $"{basePath}/Recipes"
        };
        
        int deletedCount = 0;
        foreach (string folder in folders)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                string[] assets = AssetDatabase.FindAssets("", new[] { folder });
                foreach (string guid in assets)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    AssetDatabase.DeleteAsset(path);
                    deletedCount++;
                }
            }
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log($"Deleted {deletedCount} assets");
        EditorUtility.DisplayDialog("Complete", $"Deleted {deletedCount} assets", "OK");
    }
}
#endif