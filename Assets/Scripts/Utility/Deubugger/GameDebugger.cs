#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;

/// <summary>
/// Comprehensive debug helper EditorWindow for testing game systems.
/// Access via Window > Game Debug Helper
/// UPDATED: Full Guild Progression System support
/// </summary>
public class GameDebugger : EditorWindow
{
    private enum DebugTab { Economy, Inventory, Spawning, Crafting, Progression, TimeControl, Settings, Save }
    private DebugTab currentTab = DebugTab.Economy;

    // Economy
    private int goldAmount = 1000;
    private int dbgDmgLevel, dbgRadiusLevel, dbgAOELevel, dbgAutoClickLevel, dbgAutoCollectLevel;

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
    private ItemDef floorItem;
    private int floorAmount = 20;
    private int chainMaterialQty = 50;

    // Time Control
    private float timeScale = 1f;

    // Settings
    private GameDebugSettings settings;
    private Vector2 settingsScrollPos;
    
    // Main window scroll
    private Vector2 mainScrollPos;

    [MenuItem("Window/Game Debug Helper")]
    public static void ShowWindow()
    {
        var window = GetWindow<GameDebugger>("Game Debug Helper");
        window.minSize = new Vector2(400, 600);
    }

    void OnEnable()
    {
        LoadSettings();
    }

    // Live job timers / stock on the Crafting tab
    void OnInspectorUpdate()
    {
        if (Application.isPlaying && currentTab == DebugTab.Crafting) Repaint();
    }

    void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to use debug functions", MessageType.Info);
            EditorGUILayout.Space(10);
        }

        // Tab selection (outside scroll)
        currentTab = (DebugTab)GUILayout.Toolbar((int)currentTab, System.Enum.GetNames(typeof(DebugTab)));
        EditorGUILayout.Space(10);

        // Main scroll view wraps all tab content
        mainScrollPos = EditorGUILayout.BeginScrollView(mainScrollPos);

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
            case DebugTab.Save:
                DrawSaveTab();
                break;
        }
        
        EditorGUILayout.EndScrollView();
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
        if (GUILayout.Button("Summon caravan now") && CaravanManager.Instance != null)
            Debug.Log(CaravanManager.Instance.Arrive() ? "[GameDebugger] Caravan summoned" : "[GameDebugger] No caravan: already here, or no raw lot of 20+ above its floor");
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

        EditorGUILayout.Space(10);
        GUILayout.Label("Clicker Upgrades", EditorStyles.boldLabel);

        var clicker = FindAnyObjectByType<ClickerManager>();
        if (clicker == null)
        {
            EditorGUILayout.HelpBox("No ClickerManager in scene.", MessageType.Info);
        }
        else
        {
            GUI.enabled = Application.isPlaying;

            DrawClickerLadder(clicker, "Click Damage", "clickDamageLevel", ref dbgDmgLevel,
                $"lvl {clicker.ClickDamageLevel} — {clicker.EffectiveClickDamage:0.#} dmg (next {clicker.ClickUpgradeCost}g)");
            DrawClickerLadder(clicker, "Pickup Radius", "pickupRadiusLevel", ref dbgRadiusLevel,
                $"lvl {clicker.PickupRadiusLevel}{(clicker.PickupRadiusMaxed ? " (max)" : "")} — {clicker.LootPickupRadius:0.0} wu (next {clicker.RadiusUpgradeCost}g)");
            DrawClickerLadder(clicker, "Damage AOE", "damageAOELevel", ref dbgAOELevel,
                $"lvl {clicker.AOELevel}{(clicker.AOEMaxed ? " (max)" : clicker.AOEUnlocked ? "" : " (locked)")} — {clicker.EffectiveDamageRadius:0.0} wu (next {clicker.AOEUpgradeCost}g)");
            DrawClickerLadder(clicker, "Auto Click", "autoClickLevel", ref dbgAutoClickLevel,
                $"lvl {clicker.AutoClickLevel}{(clicker.AutoClickMaxed ? " (max)" : clicker.AutoClickUnlocked ? "" : " (locked)")} — {(clicker.AutoClickLevel == 0 ? "off" : $"every {clicker.AutoClickInterval:0.0}s")} (next {clicker.AutoClickUpgradeCost}g)");
            DrawClickerLadder(clicker, "Auto Collect", "autoCollectLevel", ref dbgAutoCollectLevel,
                $"lvl {clicker.AutoCollectLevel}{(clicker.AutoCollectMaxed ? " (max)" : clicker.AutoCollectUnlocked ? "" : " (locked)")} — {(clicker.AutoCollectLevel == 0 ? "off" : $"every {clicker.AutoCollectInterval:0.0}s")} (next {clicker.AutoCollectUpgradeCost}g)");

            if (GUILayout.Button("Reset All Levels"))
            {
                BumpClickerLevel(clicker, "clickDamageLevel", 0, reset: true);
                BumpClickerLevel(clicker, "pickupRadiusLevel", 0, reset: true);
                BumpClickerLevel(clicker, "damageAOELevel", 0, reset: true);
                BumpClickerLevel(clicker, "autoClickLevel", 0, reset: true);
                BumpClickerLevel(clicker, "autoCollectLevel", 0, reset: true);
            }
            if (GUILayout.Button($"Reset inspect hint ({clicker.Inspects} inspects)")) clicker.Inspects = 0;
            GUI.enabled = true;
        }
    }

    // One ladder row: readout, +1, and a typed level with Set.
    void DrawClickerLadder(ClickerManager clicker, string label, string field, ref int typedLevel, string info)
    {
        EditorGUILayout.LabelField(label + ":", info);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+1", GUILayout.Width(36)))
            BumpClickerLevel(clicker, field, +1);
        typedLevel = EditorGUILayout.IntField(typedLevel, GUILayout.Width(50));
        if (GUILayout.Button("Set", GUILayout.Width(44)))
            BumpClickerLevel(clicker, field, typedLevel, set: true);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(2);
    }

    // Debug-only: bump or set the clicker's serialized upgrade levels without spending
    // gold, then raise the purchase signal so the Cursor page and board badge refresh.
    void BumpClickerLevel(ClickerManager clicker, string field, int value, bool reset = false, bool set = false)
    {
        var so = new SerializedObject(clicker);
        var prop = so.FindProperty(field);
        if (prop == null) { Debug.LogError($"[GameDebugger] Field '{field}' not found on ClickerManager"); return; }
        prop.intValue = reset ? 0 : set ? Mathf.Max(0, value) : Mathf.Max(0, prop.intValue + value);
        so.ApplyModifiedPropertiesWithoutUndo();
        GameSignals.RaiseClickUpgradePurchased(prop.intValue);
        Debug.Log($"[GameDebugger] {field} -> {prop.intValue}");
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
                    Debug.LogWarning($"[GameDebugger] Not enough {selectedItem.displayName} in inventory");
                }
            }
        }

        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
    }

    // ===== SPAWNING TAB =====

    void DrawSpawningTab()
    {
        GUILayout.Label("Entity Spawning", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        selectedLayer = EditorGUILayout.IntSlider("Layer:", selectedLayer, 1, 10);
        EditorGUILayout.Space(5);

        // Adventurer/Porter spawning
        GUILayout.Label("Hire Units", EditorStyles.boldLabel);
        selectedEntity = (EntityDef)EditorGUILayout.ObjectField("Unit:", selectedEntity, typeof(EntityDef), false);

        GUI.enabled = Application.isPlaying && selectedEntity != null;
        if (GUILayout.Button("Hire Unit", GUILayout.Height(30)))
        {
            if (selectedEntity is AdventurerDef)
            {
                var manager = FindAdventurerManager(selectedLayer);
                if (manager != null)
                {
                    manager.DebugHireUnit(selectedEntity);
                    Debug.Log($"[GameDebugger] Hired {selectedEntity.displayName} on layer {selectedLayer}");
                }
            }
            else if (selectedEntity is PorterDef)
            {
                var manager = FindPorterManager(selectedLayer);
                if (manager != null)
                {
                    manager.DebugHireUnit(selectedEntity);
                    Debug.Log($"[GameDebugger] Hired {selectedEntity.displayName} on layer {selectedLayer}");
                }
            }
            else
            {
                Debug.LogWarning("[GameDebugger] Selected entity is not AdventurerDef or PorterDef");
            }
        }

        EditorGUILayout.Space(5);
        GUILayout.Label("Remove Units (every depth)", EditorStyles.boldLabel);
        GUI.enabled = Application.isPlaying;
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Remove all adventurers", GUILayout.Height(26)))
        {
            int n = 0;
            foreach (var m in FindObjectsByType<AdventurerManager>()) n += m.RemoveAllUnits();
            Debug.Log($"[GameDebugger] Removed {n} adventurers");
        }
        if (GUILayout.Button("Remove all porters", GUILayout.Height(26)))
        {
            int n = 0;
            foreach (var m in FindObjectsByType<PorterManager>()) n += m.RemoveAllUnits();
            Debug.Log($"[GameDebugger] Removed {n} porters");
        }
        EditorGUILayout.EndHorizontal();
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
            var spawners = FindObjectsByType<Spawner>();
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
        var cm = CraftingManager.Instance;
        if (!Application.isPlaying || cm == null || Inventory.Instance == null)
        {
            EditorGUILayout.HelpBox("Enter play mode to use crafting controls", MessageType.Info);
            return;
        }
        var inv = Inventory.Instance;
        var recipes = cm.GetAllRecipes();
        var stationDefs = Resources.LoadAll<GuildUpgradeDef>("Guild Upgrades");

        // Stations: ownership, slots, buy, running jobs
        EditorGUILayout.Space(5);
        GUILayout.Label("Stations", EditorStyles.boldLabel);
        foreach (CraftingStation st in System.Enum.GetValues(typeof(CraftingStation)))
        {
            var def = stationDefs.FirstOrDefault(u => u.upgradeType == CraftingManager.StationUpgradeType(st));
            bool owned = cm.IsStationOwned(st);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{(def != null ? def.upgradeName : st.ToString())}", EditorStyles.boldLabel, GUILayout.Width(130));
            EditorGUILayout.LabelField(owned ? $"jobs {cm.GetRunningCount(st)}/{cm.GetStationSlots(st)}" : "not owned", GUILayout.Width(80));
            if (owned)
            {
                if (GUILayout.Button($"Buy slot ({cm.GetSlotCost(st)}g)", GUILayout.Width(130)) && !cm.BuyStationSlot(st))
                    Debug.LogWarning($"[GameDebugger] Can't afford {st} slot ({cm.GetSlotCost(st)}g)");
            }
            else if (def != null && GUILayout.Button($"Buy station ({def.goldCost}g, {def.starRequirement}★)", GUILayout.Width(170))
                     && !ProgressionManager.Instance.PurchaseUpgrade(def))
                Debug.LogWarning($"[GameDebugger] Can't buy {def.upgradeName} (gold/stars)");
            if (GUILayout.Button("Give mats", GUILayout.Width(80))) GiveChainMaterials(recipes, st, chainMaterialQty);
            EditorGUILayout.EndHorizontal();
            // Slots: pick a recipe per slot (index 0 = empty); the same recipe may fill several
            var stRecipes = recipes.Where(r => r.Station == st).ToList();
            var options = new[] { "(empty)" }.Concat(stRecipes.Select(r => r.Output.displayName)).ToArray();
            var slots = cm.GetSlots(st);
            for (int i = 0; i < slots.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"    slot {i + 1}", GUILayout.Width(60));
                int cur = slots[i] != null ? stRecipes.IndexOf(slots[i]) + 1 : 0;
                int pick = EditorGUILayout.Popup(cur, options, GUILayout.Width(160));
                if (pick != cur) cm.AssignSlot(st, i, pick == 0 ? null : stRecipes[pick - 1]);
                var state = cm.GetSlotState(st, i);
                GUILayout.Label(state == CraftingManager.SlotState.Crafting ? $"crafting {cm.GetSlotProgress(st, i):P0}" : state.ToString());
                EditorGUILayout.EndHorizontal();
            }
        }
        chainMaterialQty = EditorGUILayout.IntField("Give mats qty (raw inputs):", Mathf.Max(1, chainMaterialQty));

        // Recipes: pinned / cap / live status (on = pinned in a slot; ticking it pins to the first free slot)
        EditorGUILayout.Space(10);
        GUILayout.Label($"Recipes ({cm.GetActiveCraftCount()} jobs running)", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("slot  recipe", EditorStyles.miniLabel, GUILayout.Width(170));
        GUILayout.Label("stock / cap", EditorStyles.miniLabel, GUILayout.Width(95));
        GUILayout.Label("status", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
        foreach (var r in recipes)
        {
            EditorGUILayout.BeginHorizontal();
            bool on = cm.IsRecipeEnabled(r);
            bool newOn = EditorGUILayout.ToggleLeft($"{r.Output.displayName}", on, GUILayout.Width(170));
            if (newOn != on)
            {
                if (!newOn) cm.ClearRecipeSlots(r);
                else if (!cm.PinToFirstFreeSlot(r)) Debug.LogWarning($"[GameDebugger] {r.Station} has no free slot for {r.name}");
            }
            GUILayout.Label(inv.Get(r.Output.itemCategory, r.Output).ToString(), GUILayout.Width(35));
            int cap = EditorGUILayout.DelayedIntField(cm.GetCap(r), GUILayout.Width(55));
            if (cap != cm.GetCap(r)) cm.SetCap(r, cap);
            string reason = cm.GetBlockReason(r);
            GUILayout.Label(cm.IsCrafting(r) ? $"running {cm.GetCraftingProgress(r):P0}" : !on ? "not in a slot" : reason ?? "ready");
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button("Pin All Recipes (free slots)"))
            foreach (var r in recipes) cm.EnableRecipe(r);

        // Item floors ("keep at least N" — crafting and customer sales both stop here)
        EditorGUILayout.Space(10);
        GUILayout.Label("Item Floor (keep at least N)", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        floorItem = (ItemDef)EditorGUILayout.ObjectField(floorItem, typeof(ItemDef), false);
        floorAmount = Mathf.Max(0, EditorGUILayout.IntField(floorAmount, GUILayout.Width(60)));
        GUI.enabled = floorItem != null;
        if (GUILayout.Button("Set", GUILayout.Width(50))) inv.SetFloor(floorItem, floorAmount);
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
        if (floorItem != null)
            EditorGUILayout.LabelField($"{floorItem.displayName}:", $"stock {inv.Get(floorItem.itemCategory, floorItem)}, floor {inv.GetFloor(floorItem)}, " +
                $"for sale {(SalesManager.Instance != null ? SalesManager.Instance.GetAvailableForSale(floorItem) : 0)}");
    }

    // Raw inputs of a chain = its ingredients that no recipe produces.
    static void GiveChainMaterials(System.Collections.Generic.List<RecipeDef> recipes, CraftingStation station, int qty)
    {
        var inv = Inventory.Instance;
        var crafted = new System.Collections.Generic.HashSet<ItemDef>(recipes.Select(r => r.Output));
        var raw = recipes.Where(r => r.Station == station).SelectMany(r => r.Ingredients).Select(i => i.Item)
            .Where(i => i != null && !crafted.Contains(i)).Distinct().ToList();
        foreach (var item in raw) inv.Add(inv.GetInventoryType(item.itemCategory), new ResourceStack(item, qty, 0));
        Debug.Log($"[GameDebugger] Gave {qty}x of {string.Join(", ", raw.Select(i => i.displayName))}");
    }

    // ===== PROGRESSION TAB (UPDATED FOR GUILD SYSTEM) =====

    void DrawProgressionTab()
    {
        GUILayout.Label("Guild Progression System", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        if (Application.isPlaying && ProgressionManager.Instance != null)
        {
            var pm = ProgressionManager.Instance;
            
            // Current Status
            GUILayout.Label("Current Status", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Guild Stars:", $"{pm.GetCurrentStars()}★");
            EditorGUILayout.LabelField("Max Unlocked Layer:", pm.maxUnlockedLayer.ToString());
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10);
            
            // Milestone Progress
            GUILayout.Label("Milestone Progress", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            int nextStar = pm.GetCurrentStars() + 1;
            if (nextStar <= 5)
            {
                var milestones = pm.GetMilestonesForStar(nextStar);
                
                EditorGUILayout.LabelField($"Progress Toward {nextStar}★:", EditorStyles.boldLabel);
                EditorGUILayout.Space(3);
                
                if (milestones.Count > 0)
                {
                    foreach (var milestone in milestones)
                    {
                        int current = pm.GetMilestoneCurrentValue(milestone);
                        bool complete = pm.IsMilestoneComplete(milestone);
                        float progress = pm.GetMilestoneProgress(milestone);
                        
                        string status = pm.IsMilestoneClaimed(milestone) ? "✓✓" : pm.IsMilestoneReached(milestone) ? "$" : complete ? "✓" : "✗";
                        string progressText = $"{status} {milestone.description}: {current}/{milestone.targetValue} ({progress * 100:F0}%)";

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(progressText);
                        GUI.enabled = !pm.IsMilestoneReached(milestone);
                        if (GUILayout.Button("Reach", GUILayout.Width(55)))
                            pm.ForceReachMilestone(milestone);   // → claimable in the tracker
                        GUI.enabled = true;
                        EditorGUILayout.EndHorizontal();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox($"No milestones defined for {nextStar}★\nCreate milestone assets in Resources/Milestones/", MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.LabelField("Max stars reached (5★)!", EditorStyles.boldLabel);
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10);
            
            // Quick Actions
            GUILayout.Label("Grant Progress", EditorStyles.boldLabel);
            
            GUI.enabled = Application.isPlaying;
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("1000 Gold"))
            {
                pm.IncrementGoldEarned(1000);
                Debug.Log("[GameDebugger] Granted 1000 gold progress");
            }
            if (GUILayout.Button("100 Mobs"))
            {
                pm.IncrementMobsKilled(100);
                Debug.Log("[GameDebugger] Granted 100 mob kills");
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("50 Loot"))
            {
                pm.IncrementLootCollected(50);
                Debug.Log("[GameDebugger] Granted 50 loot collected");
            }
            if (GUILayout.Button("3 Hires"))
            {
                pm.IncrementUnitsHired(3);
                Debug.Log("[GameDebugger] Granted 3 units hired");
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("10 Crafts"))
            {
                pm.IncrementItemsCrafted(10);
                Debug.Log("[GameDebugger] Granted 10 items crafted");
            }
            if (GUILayout.Button("Complete 2★"))
            {
                // Use reflection to call private context menu method
                var method = typeof(ProgressionManager).GetMethod("Debug_Complete2StarMilestones", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null)
                    method.Invoke(pm, null);
                else
                    Debug.LogWarning("[GameDebugger] Debug_Complete2StarMilestones method not found");
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            // Star Awards
            GUILayout.Label("Force Award Stars", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("2★"))
            {
                var method = typeof(ProgressionManager).GetMethod("AwardStar", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null)
                    method.Invoke(pm, new object[] { 2 });
            }
            if (GUILayout.Button("3★"))
            {
                var method = typeof(ProgressionManager).GetMethod("AwardStar", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null)
                    method.Invoke(pm, new object[] { 3 });
            }
            if (GUILayout.Button("4★"))
            {
                var method = typeof(ProgressionManager).GetMethod("AwardStar", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null)
                    method.Invoke(pm, new object[] { 4 });
            }
            if (GUILayout.Button("5★"))
            {
                var method = typeof(ProgressionManager).GetMethod("AwardStar", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null)
                    method.Invoke(pm, new object[] { 5 });
            }
            EditorGUILayout.EndHorizontal();

            // Replays the visuals only (no award); sets the tracker's star card like a real fanfare
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Play star fanfare", GUILayout.Width(110));
            for (int s = 1; s <= 5; s++)
                if (GUILayout.Button($"{s}★")) StarFanfare.PlayDebug(s);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            // Layer Controls
            GUILayout.Label("Layer Controls", EditorStyles.boldLabel);
            int newLayer = EditorGUILayout.IntSlider("Unlock Layer:", selectedLayer, 1, 10);
            
            if (GUILayout.Button($"Unlock Layer {newLayer}", GUILayout.Height(30)))
            {
                pm.UnlockLayer(newLayer);
                Debug.Log($"[GameDebugger] Unlocked layer {newLayer}");
            }
            
            EditorGUILayout.Space(5);
            
            // Logging
            if (GUILayout.Button("Print Full Progress Report"))
            {
                var method = typeof(ProgressionManager).GetMethod("Debug_PrintMilestoneProgress", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null)
                    method.Invoke(pm, null);
            }
            
            EditorGUILayout.Space(10);
            
            // Reset
            if (GUILayout.Button("RESET PROGRESSION (1★, Layer 1)", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog(
                    "Reset Guild Progression",
                    "Reset to 1★ and Layer 1? This will clear all progress.",
                    "Yes",
                    "Cancel"))
                {
                    var method = typeof(ProgressionManager).GetMethod("Debug_ResetProgression", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (method != null)
                        method.Invoke(pm, null);
                }
            }
            
            GUI.enabled = true;
        }
        else
        {
            EditorGUILayout.HelpBox("Enter play mode to debug progression", MessageType.Info);
        }
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

        // Audio: master/sfx/music volumes (saved with the game) + audition buttons
        EditorGUILayout.Space(10);
        GUILayout.Label("Audio", EditorStyles.boldLabel);
        var sfx = SfxManager.Instance;
        if (!Application.isPlaying || sfx == null)
        {
            EditorGUILayout.HelpBox("Enter play mode to audition SFX", MessageType.Info);
            return;
        }
        sfx.MasterVolume = EditorGUILayout.Slider("Master Volume", sfx.MasterVolume, 0f, 1f);
        sfx.SfxVolume = EditorGUILayout.Slider("SFX Volume", sfx.SfxVolume, 0f, 1f);
        sfx.MusicVolume = EditorGUILayout.Slider("Music Volume", sfx.MusicVolume, 0f, 1f);
        var ids = (SfxId[])System.Enum.GetValues(typeof(SfxId));
        for (int i = 0; i < ids.Length; i += 5)
        {
            EditorGUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + 5, ids.Length); j++)
                if (GUILayout.Button($"{ids[j]} ({sfx.PlayCount(ids[j])})")) sfx.Play(ids[j]);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.LabelField("Last played:", sfx.LastPlayed?.ToString() ?? "-");
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

        if (settings == null)
        {
            EditorGUILayout.HelpBox("No GameDebugSettings asset found", MessageType.Warning);
            
            if (GUILayout.Button("Create Settings Asset"))
            {
                CreateSettingsAsset();
            }
            return;
        }

        settingsScrollPos = EditorGUILayout.BeginScrollView(settingsScrollPos);

        EditorGUILayout.LabelField("Settings Asset:", settings.name);
        EditorGUILayout.Space(5);

        EditorGUI.BeginChangeCheck();

        settings.applyOnPlaymodeStart = EditorGUILayout.Toggle("Apply on Play Mode Start", settings.applyOnPlaymodeStart);

        EditorGUILayout.Space(10);

        settings.setStartingGold = EditorGUILayout.Toggle("Set Starting Gold", settings.setStartingGold);
        if (settings.setStartingGold)
        {
            settings.startingGold = EditorGUILayout.IntField("  Amount:", settings.startingGold);
        }

        EditorGUILayout.Space(5);

        settings.addStartingItems = EditorGUILayout.Toggle("Add Starting Items", settings.addStartingItems);
        if (settings.addStartingItems)
        {
            // Show starting items array
            SerializedObject so = new SerializedObject(settings);
            SerializedProperty itemsProp = so.FindProperty("startingItems");
            EditorGUILayout.PropertyField(itemsProp, true);
            so.ApplyModifiedProperties();
        }

        EditorGUILayout.Space(5);

        settings.setStartingLayer = EditorGUILayout.Toggle("Set Starting Layer", settings.setStartingLayer);
        if (settings.setStartingLayer)
        {
            settings.startingLayer = EditorGUILayout.IntSlider("  Layer:", settings.startingLayer, 1, 10);
        }

        EditorGUILayout.Space(5);

        settings.setCustomTimeScale = EditorGUILayout.Toggle("Set Custom Time Scale", settings.setCustomTimeScale);
        if (settings.setCustomTimeScale)
        {
            settings.customTimeScale = EditorGUILayout.Slider("  Scale:", settings.customTimeScale, 0.1f, 10f);
        }

        EditorGUILayout.Space(5);

        settings.enableAllRecipes = EditorGUILayout.Toggle("Enable All Recipes", settings.enableAllRecipes);

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(settings);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(10);

        GUI.enabled = Application.isPlaying;
        if (GUILayout.Button("Apply Settings Now", GUILayout.Height(30)))
        {
            ApplySettings();
        }
        GUI.enabled = true;
    }

    // ===== SAVE TAB =====

    const string ReenterPlayKey = "GameDebugger.ReenterPlay";
    private float awayMinutes = 120f;

    void DrawSaveTab()
    {
        GUILayout.Label("Save System", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("File:", SaveManager.SavePath);
        EditorGUILayout.LabelField("Exists:", System.IO.File.Exists(SaveManager.SavePath)
            ? $"yes ({System.IO.File.GetLastWriteTime(SaveManager.SavePath):HH:mm:ss}), bak: {System.IO.File.Exists(SaveManager.SavePath + ".bak")}"
            : "no");
        EditorGUILayout.Space(5);

        GUI.enabled = Application.isPlaying && SaveManager.Instance != null;
        if (GUILayout.Button("Save Now", GUILayout.Height(30)))
            SaveManager.Instance.Save();

        // Load is a cold boot by design: restart play mode without the quit-save so the file on disk loads.
        if (GUILayout.Button("Load (restart play mode from file)", GUILayout.Height(30)))
        {
            SaveManager.Instance.SuspendSaving();
            RestartPlayMode();
        }
        GUI.enabled = true;

        // Offline earnings: live rolling rates, and a fake absence (rewrites lastPlayedUtc, then cold-boots from file)
        EditorGUILayout.Space(5);
        GUILayout.Label("Offline Earnings", EditorStyles.boldLabel);
        if (Application.isPlaying && SaveManager.Instance != null)
            EditorGUILayout.LabelField("Rates (passive):",
                $"{SaveManager.Instance.GoldPerMin:F1} gold/min, {SaveManager.Instance.LootPerMin:F2} loot/min");
        GUILayout.BeginHorizontal();
        awayMinutes = EditorGUILayout.FloatField("Away (min)", awayMinutes);
        if (GUILayout.Button("Simulate Away", GUILayout.Width(120)))
        {
            SaveManager.SimulateAway(awayMinutes);
            if (Application.isPlaying) RestartPlayMode();
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Wipe Save", GUILayout.Height(30)) &&
            EditorUtility.DisplayDialog("Wipe Save", "Delete save.json and save.json.bak? This cannot be undone.", "Wipe", "Cancel"))
        {
            SaveManager.WipeSaveFiles();
            if (Application.isPlaying) RestartPlayMode();
        }
    }

    static void RestartPlayMode()
    {
        SessionState.SetBool(ReenterPlayKey, true);
        EditorApplication.ExitPlaymode();
    }

    // ===== UTILITY METHODS =====

    void LoadSettings()
    {
        var guids = AssetDatabase.FindAssets("t:GameDebugSettings");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            settings = AssetDatabase.LoadAssetAtPath<GameDebugSettings>(path);
        }
    }

    void CreateSettingsAsset()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create GameDebugSettings",
            "GameDebugSettings",
            "asset",
            "Create a new GameDebugSettings asset");

        if (string.IsNullOrEmpty(path)) return;

        var newSettings = CreateInstance<GameDebugSettings>();
        AssetDatabase.CreateAsset(newSettings, path);
        AssetDatabase.SaveAssets();
        settings = newSettings;
        EditorGUIUtility.PingObject(settings);
        Debug.Log($"[GameDebugger] Created settings asset at {path}");
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

    AdventurerManager FindAdventurerManager(int layer)
    {
        var managers = FindObjectsByType<AdventurerManager>();
        return managers.FirstOrDefault(m => m.LayerIndex == layer);
    }

    PorterManager FindPorterManager(int layer)
    {
        var managers = FindObjectsByType<PorterManager>();
        return managers.FirstOrDefault(m => m.LayerIndex == layer);
    }

    Spawner FindSpawner(int layer, SpawnerType type)
    {
        var spawners = FindObjectsByType<Spawner>();
        return spawners.FirstOrDefault(s => s.layerIndex == layer && s.spawnerType == type);
    }

    [InitializeOnLoadMethod]
    static void InitializeOnLoad()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // A mid-play domain reload ("Recompile And Continue Playing") nulls every singleton and non-serialized
        // field (Stats' mediator, Health's flash timer) → NRE flood. Defer script reloads until play stops.
        if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.LockReloadAssemblies();
        else if (state == PlayModeStateChange.ExitingPlayMode) EditorApplication.UnlockReloadAssemblies();

        if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ReenterPlayKey, false))
        {
            SessionState.EraseBool(ReenterPlayKey);
            EditorApplication.EnterPlaymode();
        }

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            var guids = AssetDatabase.FindAssets("t:GameDebugSettings");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var settings = AssetDatabase.LoadAssetAtPath<GameDebugSettings>(path);
                
                if (settings != null && settings.applyOnPlaymodeStart)
                {
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
}
#endif