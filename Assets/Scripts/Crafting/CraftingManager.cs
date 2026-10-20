using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Auto-crafting, Against-the-Storm style. An enabled recipe starts a job (ingredients consumed at start) when:
/// its station is owned and has a free slot, its output stock is below the recipe's cap, and every ingredient
/// stays at or above its item floor (Inventory.GetFloor). One job per recipe; higher priority (1–4) starts first,
/// ties in chain/tier order. Settings, slots and running jobs are saved (SaveManager crafting chunk).
/// </summary>
public class CraftingManager : MonoBehaviour
{
    public static CraftingManager Instance { get; private set; }

    public const int DefaultCap = 10, MaxCap = 999;
    public const int MinPriority = 1, MaxPriority = 4, DefaultPriority = 2;

    // Indexed by CraftingStation. Forging starts with 2 slots (it's the first station bought).
    private static readonly UpgradeType[] StationUpgrade = { UpgradeType.ForgingStation, UpgradeType.Smithy, UpgradeType.ArcaneWorkshop };
    private static readonly int[] BaseSlots = { 2, 1, 1 };
    private const int SlotBaseCost = 2500;
    private const float SlotCostGrowth = 1.6f;

    private List<RecipeDef> craftingRecipes = new();

    private readonly HashSet<RecipeDef> enabledRecipes = new();
    private readonly Dictionary<RecipeDef, int> caps = new();
    private readonly Dictionary<RecipeDef, int> priorities = new();
    private readonly int[] stationSlots = (int[])BaseSlots.Clone();
    private List<CraftingJob> activeCrafts = new();
    private Inventory inventory;

    [Header("Safety Polling")]
    [SerializeField] private float safetyPollInterval = 2f;
    private float safetyPollTimer = 0f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    public struct CraftingJob
    {
        public RecipeDef recipe;
        public float timeRemaining;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        craftingRecipes = new List<RecipeDef>(Resources.LoadAll<RecipeDef>("Recipes"));
        // Chain then tier (craft time rises with tier): the start loop's tie order and the UI's "Unlock Order"
        craftingRecipes.Sort((a, b) => a.Station != b.Station ? a.Station.CompareTo(b.Station) : a.CraftSeconds.CompareTo(b.CraftSeconds));
        if (showDebugLogs) Debug.Log($"[CraftingManager] Loaded {craftingRecipes.Count} recipes from Resources/Recipes");
    }

    void Start()
    {
        inventory = Inventory.Instance;
        
        // Subscribe to events for event-driven auto-craft
        GameSignals.OnLootCollected += OnInventoryChanged;
        GameSignals.OnProductCrafted += OnInventoryChanged;
        GameSignals.OnCraftingConfigChanged += TryStartEnabledRecipes;
    }

    void OnDestroy()
    {
        GameSignals.OnLootCollected -= OnInventoryChanged;
        GameSignals.OnProductCrafted -= OnInventoryChanged;
        GameSignals.OnCraftingConfigChanged -= TryStartEnabledRecipes;
    }

    void Update()
    {
        TickCraftingTimers();
        SafetyPollAutoCraft();
    }

    // ===== CRAFTING TIMER SYSTEM =====

    private void TickCraftingTimers()
    {
        // Tick down all active crafts
        for (int i = 0; i < activeCrafts.Count; i++)
        {
            CraftingJob job = activeCrafts[i];
            job.timeRemaining -= Time.deltaTime;
            activeCrafts[i] = job;
        }

        // Check for completed crafts (iterate backwards to safely remove)
        for (int i = activeCrafts.Count - 1; i >= 0; i--)
        {
            if (activeCrafts[i].timeRemaining <= 0f)
            {
                // Remove first so CompleteCraft's restart check doesn't see this recipe as still crafting
                RecipeDef done = activeCrafts[i].recipe;
                activeCrafts.RemoveAt(i);
                CompleteCraft(done);
            }
        }
    }

    private void SafetyPollAutoCraft()
    {
        safetyPollTimer -= Time.deltaTime;
        if (safetyPollTimer <= 0f)
        {
            safetyPollTimer = safetyPollInterval;
            TryStartEnabledRecipes();
        }
    }

    // ===== EVENT-DRIVEN AUTO-CRAFT =====

    private void OnInventoryChanged(ResourceStack stack)
    {
        TryStartEnabledRecipes();
    }

    // Priority 4 → 1; within a priority, craftingRecipes order (chain, tier) — stable and alloc-free.
    private void TryStartEnabledRecipes()
    {
        if (inventory == null) return;
        for (int p = MaxPriority; p >= MinPriority; p--)
            foreach (var recipe in craftingRecipes)
                if (GetPriority(recipe) == p && enabledRecipes.Contains(recipe) && GetBlockReason(recipe) == null)
                    StartCraft(recipe);
    }

    /// <summary>Why this recipe can't start a job right now (ignores enabled); null when it can. Pure.</summary>
    public string GetBlockReason(RecipeDef recipe)
    {
        if (IsCrafting(recipe)) return "Crafting";
        if (!IsStationOwned(recipe.Station)) return "No station";
        if (GetRunningCount(recipe.Station) >= GetStationSlots(recipe.Station)) return "Station busy";
        if (inventory.Get(recipe.Output.itemCategory, recipe.Output) >= GetCap(recipe)) return "At cap";
        if (!CanCraft(recipe)) return "Missing materials";
        return null;
    }

    // ===== CRAFTING LOGIC =====

    public bool CanCraft(RecipeDef recipe)
    {
        if (showDebugLogs) Debug.Log($"[CanCraft] Checking recipe: {recipe.Output.displayName}");

        foreach (var ingredient in recipe.Ingredients)
        {
            int availableQty = inventory.Get(ingredient.Item.itemCategory, ingredient.Item);
            int floorQty = inventory.GetFloor(ingredient.Item);
            int neededQty = ingredient.Qty;
            
            if (showDebugLogs) Debug.Log($"  - {ingredient.Item.displayName}: Have {availableQty}, Need {neededQty}, Floor {floorQty}, Available: {availableQty - floorQty}");
            
            if (availableQty - floorQty < ingredient.Qty)
            {      
                if (showDebugLogs) Debug.Log($"  - NOT ENOUGH!");
                return false;
            }
        }
    
        if (showDebugLogs) Debug.Log($"  - CAN CRAFT!");
        return true;
    }

    public void StartCraft(RecipeDef recipe)
    {
        if (!CanCraft(recipe))
        {
            if (showDebugLogs)
                Debug.LogWarning($"[CraftingManager] Cannot start crafting {recipe.Output.displayName}: insufficient ingredients.");
            return;
        }

        // Remove ingredients from inventory
        foreach (var ingredient in recipe.Ingredients)
        {
            if (!inventory.TryRemove(inventory.GetInventoryType(ingredient.Item.itemCategory), ingredient.Item, ingredient.Qty))
            {
                Debug.LogError($"[CraftingManager] Failed to remove {ingredient.Qty}x {ingredient.Item.displayName} from inventory despite CanCraft check.");
                return;
            }
        }

        // Add to active crafts
        activeCrafts.Add(new CraftingJob
        {
            recipe = recipe,
            timeRemaining = recipe.CraftSeconds
        });

        if (showDebugLogs)
            Debug.Log($"[CraftingManager] Started crafting {recipe.Output.displayName} ({recipe.CraftSeconds}s)");
        GameSignals.RaiseCraftStarted(recipe);
    }

    public bool IsCrafting(RecipeDef recipe)
    {
        foreach (var job in activeCrafts)
        {
            if (job.recipe == recipe)
                return true;
        }
        return false;
    }

    public void CompleteCraft(RecipeDef recipe)
    {
        // Add crafted item to inventory
        inventory.Add(
            inventory.GetInventoryType(recipe.Output.itemCategory),
            new ResourceStack(recipe.Output, recipe.OutputQty, 0)
        );

        // Notify game signals
        GameSignals.RaiseProductCrafted(new ResourceStack(recipe.Output, recipe.OutputQty, 0));

        if (showDebugLogs)
            Debug.Log($"[CraftingManager] Crafted {recipe.OutputQty}x {recipe.Output.displayName}");

        // Try to start next craft immediately (event-driven)
        TryStartEnabledRecipes();
    }

    // ===== CONFIGURATION METHODS =====

    // Every setter raises OnCraftingConfigChanged, which this manager also listens to → immediate start check.
    public void EnableRecipe(RecipeDef recipe)
    {
        if (recipe != null && enabledRecipes.Add(recipe)) GameSignals.RaiseCraftingConfigChanged();
    }

    public void DisableRecipe(RecipeDef recipe)
    {
        if (recipe != null && enabledRecipes.Remove(recipe)) GameSignals.RaiseCraftingConfigChanged();
    }

    /// <summary>"Produce until stock ≥ cap": no new job while the output's stock is at or above this.</summary>
    public int GetCap(RecipeDef recipe) => caps.TryGetValue(recipe, out int c) ? c : DefaultCap;

    public void SetCap(RecipeDef recipe, int cap)
    {
        cap = Mathf.Clamp(cap, 1, MaxCap);
        if (recipe == null || GetCap(recipe) == cap) return;
        caps[recipe] = cap;
        GameSignals.RaiseCraftingConfigChanged();
    }

    /// <summary>1–4; higher starts first when recipes compete for materials or station slots.</summary>
    public int GetPriority(RecipeDef recipe) => priorities.TryGetValue(recipe, out int p) ? p : DefaultPriority;

    public void SetPriority(RecipeDef recipe, int priority)
    {
        priority = Mathf.Clamp(priority, MinPriority, MaxPriority);
        if (recipe == null || GetPriority(recipe) == priority) return;
        priorities[recipe] = priority;
        GameSignals.RaiseCraftingConfigChanged();
    }

    // ===== STATIONS =====

    public static UpgradeType StationUpgradeType(CraftingStation station) => StationUpgrade[(int)station];

    public bool IsStationOwned(CraftingStation station) =>
        ProgressionManager.Instance != null && ProgressionManager.Instance.HasUpgrade(StationUpgrade[(int)station]);

    public int GetStationSlots(CraftingStation station) => stationSlots[(int)station];

    public int GetRunningCount(CraftingStation station)
    {
        int n = 0;
        foreach (var job in activeCrafts)
            if (job.recipe.Station == station) n++;
        return n;
    }

    /// <summary>Next slot: 2500 × 1.6^(slots bought so far).</summary>
    public int GetSlotCost(CraftingStation station) =>
        Mathf.RoundToInt(SlotBaseCost * Mathf.Pow(SlotCostGrowth, stationSlots[(int)station] - BaseSlots[(int)station]));

    /// <summary>Gold sink: +1 concurrent job at an owned station. Raises OnCraftingConfigChanged.</summary>
    public bool BuyStationSlot(CraftingStation station)
    {
        if (!IsStationOwned(station) || !Inventory.Instance.TrySpendGold(GetSlotCost(station))) return false;
        stationSlots[(int)station]++;
        if (showDebugLogs) Debug.Log($"[CraftingManager] {station} now has {stationSlots[(int)station]} slots");
        GameSignals.RaiseCraftingConfigChanged();
        return true;
    }

    // ===== SAVE =====

    public CraftingSave ToSave()
    {
        var s = new CraftingSave();
        foreach (var r in craftingRecipes)
            s.recipes[r.name] = new RecipeSave { enabled = enabledRecipes.Contains(r), cap = GetCap(r), priority = GetPriority(r) };
        for (int i = 0; i < stationSlots.Length; i++) s.stationSlots[(CraftingStation)i] = stationSlots[i];
        foreach (var job in activeCrafts) s.jobs.Add(new CraftJobSave { recipe = job.recipe.name, remaining = job.timeRemaining });
        return s;
    }

    /// <summary>Load path (SaveManager.ApplyLate, after stock + floors). Running jobs resume with their remaining
    /// time — their ingredients were consumed before the save, so nothing is refunded or lost.</summary>
    public void LoadFrom(CraftingSave s)
    {
        var byName = craftingRecipes.ToDictionary(r => r.name);
        enabledRecipes.Clear();
        caps.Clear();
        priorities.Clear();
        activeCrafts.Clear();
        foreach (var kv in s.recipes)
        {
            if (!byName.TryGetValue(kv.Key, out var r) || kv.Value == null) continue;
            if (kv.Value.enabled) enabledRecipes.Add(r);
            caps[r] = Mathf.Clamp(kv.Value.cap, 1, MaxCap);
            priorities[r] = Mathf.Clamp(kv.Value.priority, MinPriority, MaxPriority);
        }
        for (int i = 0; i < stationSlots.Length; i++)
            stationSlots[i] = s.stationSlots.TryGetValue((CraftingStation)i, out int n) ? Mathf.Max(BaseSlots[i], n) : BaseSlots[i];
        foreach (var j in s.jobs)
        {
            if (j != null && byName.TryGetValue(j.recipe ?? "", out var r))
                activeCrafts.Add(new CraftingJob { recipe = r, timeRemaining = Mathf.Clamp(j.remaining, 0f, r.CraftSeconds) });
            else
                Debug.LogError($"[CraftingManager] Unknown recipe '{j?.recipe}' in saved job (asset renamed?) — job and its ingredients lost");
        }
        GameSignals.RaiseCraftingConfigChanged();
    }

    public bool IsRecipeEnabled(RecipeDef recipe)
    {
        return enabledRecipes.Contains(recipe);
    }

    public int GetActiveCraftCount()
    {
        return activeCrafts.Count;
    }

    public IReadOnlyList<CraftingJob> ActiveJobs => activeCrafts;

    public List<RecipeDef> GetEnabledRecipes()
    {
        return new List<RecipeDef>(enabledRecipes);
    }

    public List<RecipeDef> GetAllRecipes()
    {
        return new List<RecipeDef>(craftingRecipes);
    }

    public float GetCraftingProgress(RecipeDef recipe)
    {
        foreach (var job in activeCrafts)
        {
            if (job.recipe == recipe)
                return 1f - (job.timeRemaining / recipe.CraftSeconds);
        }
        return 0f;
    }

    // ===== DEBUG METHODS =====

    [ContextMenu("Debug/Print All Recipes")]
    public void DebugPrintAllRecipes()
    {
        Debug.Log($"[CraftingManager] Total recipes: {craftingRecipes.Count}");
        foreach (var recipe in craftingRecipes)
        {
            Debug.Log($"  - {recipe.Output.displayName} ({recipe.CraftSeconds}s)");
        }
    }

    [ContextMenu("Debug/Check Can Craft All Recipes")]
    public void DebugCheckCanCraft()
    {
        Debug.Log($"[CraftingManager] Checking all recipes:");
        foreach (var recipe in craftingRecipes)
        {
            bool canCraft = CanCraft(recipe);
            Debug.Log($"  - {recipe.Output.displayName}: {(canCraft ? "✓ Can craft" : "✗ Cannot craft")}");
        }
    }

    [ContextMenu("Debug/Set All Material Floors to 10")]
    public void DebugSetMaterialFloor()
    {
        foreach (var recipe in craftingRecipes)
            foreach (var ingredient in recipe.Ingredients)
                Inventory.Instance.SetFloor(ingredient.Item, 10);
        Debug.Log("[CraftingManager] Set every ingredient's floor to 10");
    }

    [ContextMenu("Debug/Enable All Recipes")]
    public void DebugEnableAllRecipes()
    {
        foreach (var recipe in craftingRecipes)
        {
            EnableRecipe(recipe);
        }
        Debug.Log($"[CraftingManager] Enabled all {craftingRecipes.Count} recipes");
    }

    [ContextMenu("Debug/Force Craft First Recipe")]
    public void DebugForceCraft()
    {
        if (craftingRecipes.Count > 0)
        {
            StartCraft(craftingRecipes[0]);
            Debug.Log($"[CraftingManager] Force started craft: {craftingRecipes[0].Output.displayName}");
        }
        else
        {
            Debug.LogWarning("[CraftingManager] No recipes available to craft");
        }
    }

    [ContextMenu("Debug/Print Active Crafts")]
    public void DebugPrintActiveCrafts()
    {
        Debug.Log($"[CraftingManager] Active crafts: {activeCrafts.Count}");
        foreach (var job in activeCrafts)
        {
            Debug.Log($"  - {job.recipe.Output.displayName}: {job.timeRemaining:F1}s remaining");
        }
    }
}