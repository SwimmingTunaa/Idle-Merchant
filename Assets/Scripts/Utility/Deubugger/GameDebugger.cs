#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;

/// <summary>
/// Comprehensive debug helper EditorWindow for testing game systems.
/// Access via Window > Game Debug Helper
/// </summary>
public class GameDebugger : EditorWindow
{
    private enum DebugTab { Economy, Inventory, Spawning, Crafting, Progression, TimeControl, Settings }
    private DebugTab currentTab = DebugTab.Economy;

    // Economy
    private int goldAmount = 100;

    // Inventory
    private ItemDef selectedItem;
    private int itemQuantity = 10;
    private Vector2 itemScrollPos;

    // Spawning
    private int selectedLayer = 1;
    private EntityDef selectedEntity;
    private MobDef selectedMob;
    private Vector2 entityScrollPos;
    private Vector2 mobScrollPos;

    // Crafting
    private RecipeDef selectedRecipe;
    private Vector2 recipeScrollPos;

    // Time Control
    private float timeScale = 1f;

    // Settings
    private GameDebugSettings settings;
    private Vector2 settingsScrollPos;

    [MenuItem("Window/Game Debug Helper")]
    public static void ShowWindow()
    {
        var window = GetWindow<GameDebugger>("Game Debug Helper");
        window.minSize = new Vector2(400, 500);
    }

    void OnEnable()
    {
        LoadSettings();
    }

    void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to use debug functions", MessageType.Info);
            EditorGUILayout.Space(10);
        }

        // Tab selection
        currentTab = (DebugTab)GUILayout.Toolbar((int)currentTab, System.Enum.GetNames(typeof(DebugTab)));
        EditorGUILayout.Space(10);

        // Tab content
        switch (currentTab)
        {
            case DebugTab.Economy:
                DrawEconomyTab();
                break;
            case DebugTab.Inventory:
                DrawInventoryTab();
                break;
            case DebugTab.Spawning:
                DrawSpawningTab();
                break;
            case DebugTab.Crafting:
                DrawCraftingTab();
                break;
            case DebugTab.Progression:
                DrawProgressionTab();
                break;
            case DebugTab.TimeControl:
                DrawTimeControlTab();
                break;
            case DebugTab.Settings:
                DrawSettingsTab();
                break;
        }
    }

    // ===== ECONOMY TAB =====

    void DrawEconomyTab()
    {
        GUILayout.Label("Gold Management", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        if (Application.isPlaying && Inventory.Instance != null)
        {
            EditorGUILayout.LabelField("Current Gold:", Inventory.Instance.Gold.ToString());
            EditorGUILayout.Space(5);
        }

        goldAmount = EditorGUILayout.IntField("Amount:", goldAmount);

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = Application.isPlaying;
        
        if (GUILayout.Button("Add Gold"))
        {
            if (Inventory.Instance != null)
            {
                Inventory.Instance.AddGold(goldAmount);
                Debug.Log($"[GameDebugger] Added {goldAmount} gold");
            }
        }

        if (GUILayout.Button("Spend Gold"))
        {
            if (Inventory.Instance != null)
            {
                if (Inventory.Instance.TrySpendGold(goldAmount))
                {
                    Debug.Log($"[GameDebugger] Spent {goldAmount} gold");
                }
                else
                {
                    Debug.LogWarning($"[GameDebugger] Cannot spend {goldAmount} gold - insufficient funds");
                }
            }
        }

        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        GUI.enabled = Application.isPlaying;
        if (GUILayout.Button("Set Gold to 0", GUILayout.Height(30)))
        {
            if (Inventory.Instance != null)
            {
                int current = Inventory.Instance.Gold;
                Inventory.Instance.TrySpendGold(current);
                Debug.Log($"[GameDebugger] Reset gold to 0");
            }
        }
        GUI.enabled = true;
    }

    // ===== INVENTORY TAB =====

    void DrawInventoryTab()
    {
        GUILayout.Label("Inventory Management", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        // Item selection
        selectedItem = (ItemDef)EditorGUILayout.ObjectField("Item:", selectedItem, typeof(ItemDef), false);
        itemQuantity = EditorGUILayout.IntField("Quantity:", Mathf.Max(1, itemQuantity));

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = Application.isPlaying && selectedItem != null;

        if (GUILayout.Button("Add Item"))
        {
            if (Inventory.Instance != null)
            {
                var inventory = Inventory.Instance.GetInventoryType(selectedItem.itemCategory);
                Inventory.Instance.Add(inventory, new ResourceStack(selectedItem, itemQuantity, 0));
                Debug.Log($"[GameDebugger] Added {itemQuantity}x {selectedItem.displayName}");
            }
        }

        if (GUILayout.Button("Remove Item"))
        {
            if (Inventory.Instance != null)
            {
                var inventory = Inventory.Instance.GetInventoryType(selectedItem.itemCategory);
                if (Inventory.Instance.TryRemove(inventory, selectedItem, itemQuantity))
                {
                    Debug.Log($"[GameDebugger] Removed {itemQuantity}x {selectedItem.displayName}");
                }
                else
                {
                    Debug.LogWarning($"[GameDebugger] Insufficient {selectedItem.displayName} in inventory");
                }
            }
        }

        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Quick add items
        GUILayout.Label("Quick Add (All Items in Project)", EditorStyles.boldLabel);
        
        itemScrollPos = EditorGUILayout.BeginScrollView(itemScrollPos, GUILayout.Height(250));
        
        var allItems = Resources.LoadAll<ItemDef>("")
            .OrderBy(i => i.itemCategory)
            .ThenBy(i => i.displayName)
            .ToArray();

        GUI.enabled = Application.isPlaying;
        foreach (var item in allItems)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{item.displayName} ({item.itemCategory})", GUILayout.Width(200));
            
            if (GUILayout.Button("+1", GUILayout.Width(50)))
            {
                AddItemQuick(item, 1);
            }
            if (GUILayout.Button("+10", GUILayout.Width(50)))
            {
                AddItemQuick(item, 10);
            }
            if (GUILayout.Button("+100", GUILayout.Width(50)))
            {
                AddItemQuick(item, 100);
            }
            
            EditorGUILayout.EndHorizontal();
        }
        GUI.enabled = true;

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(10);

        GUI.enabled = Application.isPlaying;
        if (GUILayout.Button("Clear All Inventory", GUILayout.Height(30)))
        {
            if (Inventory.Instance != null && EditorUtility.DisplayDialog(
                "Clear Inventory",
                "Are you sure you want to clear all items?",
                "Yes",
                "Cancel"))
            {
                Inventory.Instance.GetInventoryType(ItemCategory.Common).Clear();
                Inventory.Instance.GetInventoryType(ItemCategory.Crafted).Clear();
                Inventory.Instance.GetInventoryType(ItemCategory.Luxury).Clear();
                Debug.Log($"[GameDebugger] Cleared all inventory");
            }
        }
        GUI.enabled = true;
    }

    void AddItemQuick(ItemDef item, int qty)
    {
        if (Inventory.Instance != null)
        {
            var inventory = Inventory.Instance.GetInventoryType(item.itemCategory);
            Inventory.Instance.Add(inventory, new ResourceStack(item, qty, 0));
        }
    }

    // ===== SPAWNING TAB =====

    void DrawSpawningTab()
    {
        GUILayout.Label("Entity Spawning", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        selectedLayer = EditorGUILayout.IntSlider("Layer:", selectedLayer, 1, 10);

        EditorGUILayout.Space(10);

        // Adventurer/Porter spawning
        GUILayout.Label("Hire Units (Adventurers/Porters)", EditorStyles.boldLabel);
        selectedEntity = (EntityDef)EditorGUILayout.ObjectField("Entity:", selectedEntity, typeof(EntityDef), false);

        GUI.enabled = Application.isPlaying && selectedEntity != null;
        if (GUILayout.Button("Hire Unit (Free)", GUILayout.Height(30)))
        {
            if (selectedEntity is AdventurerDef)
            {
                var manager = FindAdventurerManager(selectedLayer);
                if (manager != null)
                {
                    manager.HireUnit(selectedEntity);
                    Debug.Log($"[GameDebugger] Hired {selectedEntity.displayName} on layer {selectedLayer}");
                }
            }
            else if (selectedEntity is PorterDef)
            {
                var manager = FindPorterManager(selectedLayer);
                if (manager != null)
                {
                    manager.HireUnit(selectedEntity);
                    Debug.Log($"[GameDebugger] Hired {selectedEntity.displayName} on layer {selectedLayer}");
                }
            }
            else
            {
                Debug.LogWarning("[GameDebugger] Selected entity is not AdventurerDef or PorterDef");
            }
        }
        GUI.enabled = true;

        EditorGUILayout.Space(10);

        // Mob spawning
        GUILayout.Label("Spawn Mobs (via Spawner)", EditorStyles.boldLabel);
        selectedMob = (MobDef)EditorGUILayout.ObjectField("Mob:", selectedMob, typeof(MobDef), false);

        GUI.enabled = Application.isPlaying && selectedMob != null;
        if (GUILayout.Button("Spawn Mob", GUILayout.Height(30)))
        {
            var spawner = FindSpawner(selectedLayer, SpawnerType.Mobs);
            if (spawner != null)
            {
                spawner.TrySpawn();
                Debug.Log($"[GameDebugger] Spawned mob on layer {selectedLayer}");
            }
            else
            {
                Debug.LogWarning($"[GameDebugger] No mob spawner found for layer {selectedLayer}");
            }
        }
        GUI.enabled = true;

        EditorGUILayout.Space(10);

        // Quick spawner access
        GUILayout.Label("Spawner Controls", EditorStyles.boldLabel);
        
        GUI.enabled = Application.isPlaying;
        if (GUILayout.Button("Trigger All Spawners Once"))
        {
            var spawners = FindObjectsByType<Spawner>(FindObjectsSortMode.None);
            foreach (var spawner in spawners)
            {
                spawner.TrySpawn();
            }
            Debug.Log($"[GameDebugger] Triggered {spawners.Length} spawners");
        }
        GUI.enabled = true;
    }

    // ===== CRAFTING TAB =====

    void DrawCraftingTab()
    {
        GUILayout.Label("Crafting System", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        if (Application.isPlaying && CraftingManager.Instance != null)
        {
            EditorGUILayout.LabelField("Active Crafts:", CraftingManager.Instance.GetActiveCraftCount().ToString());
            EditorGUILayout.LabelField("Enabled Recipes:", CraftingManager.Instance.GetEnabledRecipes().Count.ToString());
            EditorGUILayout.Space(5);
        }

        selectedRecipe = (RecipeDef)EditorGUILayout.ObjectField("Recipe:", selectedRecipe, typeof(RecipeDef), false);

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = Application.isPlaying && selectedRecipe != null && CraftingManager.Instance != null;

        if (GUILayout.Button("Enable Recipe"))
        {
            CraftingManager.Instance.EnableRecipe(selectedRecipe);
            Debug.Log($"[GameDebugger] Enabled recipe: {selectedRecipe.Output.displayName}");
        }

        if (GUILayout.Button("Disable Recipe"))
        {
            CraftingManager.Instance.DisableRecipe(selectedRecipe);
            Debug.Log($"[GameDebugger] Disabled recipe: {selectedRecipe.Output.displayName}");
        }

        EditorGUILayout.EndHorizontal();

        GUI.enabled = Application.isPlaying && selectedRecipe != null && CraftingManager.Instance != null;
        if (GUILayout.Button("Force Start Craft (Ignores Materials)"))
        {
            CraftingManager.Instance.StartCraft(selectedRecipe);
            Debug.Log($"[GameDebugger] Force started craft: {selectedRecipe.Output.displayName}");
        }
        GUI.enabled = true;

        EditorGUILayout.Space(10);

        // Recipe list
        GUILayout.Label("All Recipes", EditorStyles.boldLabel);
        
        recipeScrollPos = EditorGUILayout.BeginScrollView(recipeScrollPos, GUILayout.Height(250));

        if (Application.isPlaying && CraftingManager.Instance != null)
        {
            var recipes = CraftingManager.Instance.GetAllRecipes();
            
            foreach (var recipe in recipes)
            {
                EditorGUILayout.BeginHorizontal();
                
                bool isEnabled = CraftingManager.Instance.IsRecipeEnabled(recipe);
                bool canCraft = CraftingManager.Instance.CanCraft(recipe);
                
                string status = isEnabled ? "[ENABLED]" : "[DISABLED]";
                string craftStatus = canCraft ? "✓" : "✗";
                
                EditorGUILayout.LabelField($"{status} {recipe.Output.displayName} {craftStatus}", GUILayout.Width(250));
                
                if (GUILayout.Button(isEnabled ? "Disable" : "Enable", GUILayout.Width(80)))
                {
                    if (isEnabled)
                        CraftingManager.Instance.DisableRecipe(recipe);
                    else
                        CraftingManager.Instance.EnableRecipe(recipe);
                }
                
                EditorGUILayout.EndHorizontal();
            }
        }
        else
        {
            EditorGUILayout.LabelField("Enter play mode to see recipes");
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(10);

        GUI.enabled = Application.isPlaying && CraftingManager.Instance != null;
        if (GUILayout.Button("Enable All Recipes", GUILayout.Height(30)))
        {
            var recipes = CraftingManager.Instance.GetAllRecipes();
            foreach (var recipe in recipes)
            {
                CraftingManager.Instance.EnableRecipe(recipe);
            }
            Debug.Log($"[GameDebugger] Enabled all {recipes.Count} recipes");
        }
        GUI.enabled = true;
    }

    // ===== PROGRESSION TAB =====

    void DrawProgressionTab()
    {
        GUILayout.Label("Progression System", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        if (Application.isPlaying && ProgressionManager.Instance != null)
        {
            EditorGUILayout.LabelField("Max Unlocked Layer:", ProgressionManager.Instance.maxUnlockedLayer.ToString());
            EditorGUILayout.Space(5);
        }

        int newLayer = EditorGUILayout.IntSlider("Unlock Layer:", selectedLayer, 1, 10);

        GUI.enabled = Application.isPlaying && ProgressionManager.Instance != null;
        if (GUILayout.Button($"Unlock Layer {newLayer}", GUILayout.Height(30)))
        {
            ProgressionManager.Instance.UnlockLayer(newLayer);
            Debug.Log($"[GameDebugger] Unlocked layer {newLayer}");
        }

        if (GUILayout.Button("Reset to Layer 1", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog(
                "Reset Progression",
                "Reset to layer 1?",
                "Yes",
                "Cancel"))
            {
                ProgressionManager.Instance.maxUnlockedLayer = 1;
                Debug.Log($"[GameDebugger] Reset to layer 1");
            }
        }
        GUI.enabled = true;
    }

    // ===== TIME CONTROL TAB =====

    void DrawTimeControlTab()
    {
        GUILayout.Label("Time Control", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField("Current Time Scale:", Time.timeScale.ToString("F2"));

        timeScale = EditorGUILayout.Slider("Time Scale:", timeScale, 0.1f, 10f);

        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Apply"))
        {
            Time.timeScale = timeScale;
            Debug.Log($"[GameDebugger] Set time scale to {timeScale}");
        }

        if (GUILayout.Button("Reset (1x)"))
        {
            timeScale = 1f;
            Time.timeScale = 1f;
            Debug.Log($"[GameDebugger] Reset time scale to 1");
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Quick buttons
        GUILayout.Label("Quick Time Scale", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("0.5x")) SetTimeScale(0.5f);
        if (GUILayout.Button("1x")) SetTimeScale(1f);
        if (GUILayout.Button("2x")) SetTimeScale(2f);
        if (GUILayout.Button("5x")) SetTimeScale(5f);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Pause", GUILayout.Height(30)))
        {
            Time.timeScale = 0f;
            timeScale = 0f;
            Debug.Log($"[GameDebugger] Paused");
        }
    }

    void SetTimeScale(float scale)
    {
        timeScale = scale;
        Time.timeScale = scale;
        Debug.Log($"[GameDebugger] Set time scale to {scale}");
    }

    // ===== SETTINGS TAB =====

    void DrawSettingsTab()
    {
        GUILayout.Label("Persistent Debug Settings", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        settings = (GameDebugSettings)EditorGUILayout.ObjectField("Settings Asset:", settings, typeof(GameDebugSettings), false);

        if (settings == null)
        {
            EditorGUILayout.HelpBox("Create a GameDebugSettings asset via Create > Debug > Game Debug Settings", MessageType.Info);
            
            if (GUILayout.Button("Create New Settings Asset"))
            {
                CreateSettingsAsset();
            }
            return;
        }

        EditorGUILayout.Space(10);

        settingsScrollPos = EditorGUILayout.BeginScrollView(settingsScrollPos);

        // Draw settings
        SerializedObject so = new SerializedObject(settings);
        SerializedProperty prop = so.GetIterator();
        prop.NextVisible(true); // Skip script field

        while (prop.NextVisible(false))
        {
            EditorGUILayout.PropertyField(prop, true);
        }

        so.ApplyModifiedProperties();

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(10);

        GUI.enabled = Application.isPlaying && settings.applyOnPlaymodeStart;
        if (GUILayout.Button("Apply Settings Now", GUILayout.Height(40)))
        {
            ApplySettings();
        }
        GUI.enabled = true;

        if (!settings.applyOnPlaymodeStart)
        {
            EditorGUILayout.HelpBox("Enable 'Apply On Playmode Start' to automatically apply these settings when entering play mode", MessageType.Info);
        }
    }

    void CreateSettingsAsset()
    {
        settings = ScriptableObject.CreateInstance<GameDebugSettings>();
        
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Debug Settings",
            "GameDebugSettings",
            "asset",
            "Create new debug settings asset");

        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(settings);
            Debug.Log($"[GameDebugger] Created settings asset at {path}");
        }
    }

    // ===== UTILITY METHODS =====

    void LoadSettings()
    {
        // Try to find existing settings asset
        var guids = AssetDatabase.FindAssets("t:GameDebugSettings");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            settings = AssetDatabase.LoadAssetAtPath<GameDebugSettings>(path);
        }
    }

    void ApplySettings()
    {
        if (settings == null || !Application.isPlaying) return;

        if (settings.setStartingGold && Inventory.Instance != null)
        {
            int current = Inventory.Instance.Gold;
            Inventory.Instance.TrySpendGold(current);
            Inventory.Instance.AddGold(settings.startingGold);
            Debug.Log($"[GameDebugger] Set gold to {settings.startingGold}");
        }

        if (settings.addStartingItems && Inventory.Instance != null)
        {
            foreach (var pair in settings.startingItems)
            {
                if (pair.item != null)
                {
                    var inventory = Inventory.Instance.GetInventoryType(pair.item.itemCategory);
                    Inventory.Instance.Add(inventory, new ResourceStack(pair.item, pair.quantity, 0));
                }
            }
            Debug.Log($"[GameDebugger] Added {settings.startingItems.Length} starting items");
        }

        if (settings.setStartingLayer && ProgressionManager.Instance != null)
        {
            ProgressionManager.Instance.UnlockLayer(settings.startingLayer);
            Debug.Log($"[GameDebugger] Unlocked up to layer {settings.startingLayer}");
        }

        if (settings.setCustomTimeScale)
        {
            Time.timeScale = settings.customTimeScale;
            Debug.Log($"[GameDebugger] Set time scale to {settings.customTimeScale}");
        }

        if (settings.enableAllRecipes && CraftingManager.Instance != null)
        {
            var recipes = CraftingManager.Instance.GetAllRecipes();
            foreach (var recipe in recipes)
            {
                CraftingManager.Instance.EnableRecipe(recipe);
            }
            Debug.Log($"[GameDebugger] Enabled all recipes");
        }
    }

    AdventurerManager FindAdventurerManager(int layer)
    {
        var managers = FindObjectsByType<AdventurerManager>(FindObjectsSortMode.None);
        return managers.FirstOrDefault(m => m.LayerIndex == layer);
    }

    PorterManager FindPorterManager(int layer)
    {
        var managers = FindObjectsByType<PorterManager>(FindObjectsSortMode.None);
        return managers.FirstOrDefault(m => m.LayerIndex == layer);
    }

    Spawner FindSpawner(int layer, SpawnerType type)
    {
        var spawners = FindObjectsByType<Spawner>(FindObjectsSortMode.None);
        return spawners.FirstOrDefault(s => s.layerIndex == layer && s.spawnerType == type);
    }

    // Subscribe to play mode changes
    [InitializeOnLoadMethod]
    static void InitializeOnLoad()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            // Find settings and apply if enabled
            var guids = AssetDatabase.FindAssets("t:GameDebugSettings");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var settings = AssetDatabase.LoadAssetAtPath<GameDebugSettings>(path);
                
                if (settings != null && settings.applyOnPlaymodeStart)
                {
                    // Delay to ensure all systems are initialized
                    EditorApplication.delayCall += () =>
                    {
                        if (Application.isPlaying)
                        {
                            ApplySettingsStatic(settings);
                        }
                    };
                }
            }
        }
    }

    static void ApplySettingsStatic(GameDebugSettings settings)
    {
        if (settings == null) return;

        if (settings.setStartingGold && Inventory.Instance != null)
        {
            int current = Inventory.Instance.Gold;
            Inventory.Instance.TrySpendGold(current);
            Inventory.Instance.AddGold(settings.startingGold);
            Debug.Log($"[GameDebugger] Auto-applied starting gold: {settings.startingGold}");
        }

        if (settings.addStartingItems && Inventory.Instance != null)
        {
            foreach (var pair in settings.startingItems)
            {
                if (pair.item != null)
                {
                    var inventory = Inventory.Instance.GetInventoryType(pair.item.itemCategory);
                    Inventory.Instance.Add(inventory, new ResourceStack(pair.item, pair.quantity, 0));
                }
            }
            Debug.Log($"[GameDebugger] Auto-applied starting items");
        }

        if (settings.setStartingLayer && ProgressionManager.Instance != null)
        {
            ProgressionManager.Instance.UnlockLayer(settings.startingLayer);
            Debug.Log($"[GameDebugger] Auto-unlocked up to layer {settings.startingLayer}");
        }

        if (settings.setCustomTimeScale)
        {
            Time.timeScale = settings.customTimeScale;
            Debug.Log($"[GameDebugger] Auto-applied time scale: {settings.customTimeScale}");
        }

        if (settings.enableAllRecipes && CraftingManager.Instance != null)
        {
            var recipes = CraftingManager.Instance.GetAllRecipes();
            foreach (var recipe in recipes)
            {
                CraftingManager.Instance.EnableRecipe(recipe);
            }
            Debug.Log($"[GameDebugger] Auto-enabled all recipes");
        }
    }
}
#endif
