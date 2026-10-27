using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Auto-crafting, Against-the-Storm style, driven by station slots: a recipe runs iff it is pinned in a slot of its
/// station. Each slot runs one job at a time (ingredients consumed at start); the same recipe may fill several slots and
/// then crafts in parallel. A slot starts a job when its station is owned, the output's stock + jobs in flight stays
/// below the recipe's cap, and every ingredient stays at or above its item floor (Inventory.GetFloor). Slot order is the
/// priority (stations top to bottom, slot 1 first). Slots, caps and running jobs are saved (SaveManager crafting chunk).
/// </summary>
public class CraftingManager : MonoBehaviour
{
    public static CraftingManager Instance { get; private set; }

    public const int DefaultCap = 10, MaxCap = 999;

    // Indexed by CraftingStation. Forging starts with 2 slots (it's the first station bought).
    private static readonly UpgradeType[] StationUpgrade = { UpgradeType.ForgingStation, UpgradeType.Smithy, UpgradeType.ArcaneWorkshop };
    private static readonly int[] BaseSlots = { 2, 1, 1 };
    private const int SlotBaseCost = 2500;
    private const float SlotCostGrowth = 1.6f;

    /// <summary>What a slot is doing right now (display). Pure.</summary>
    public enum SlotState { Empty, NoStation, Crafting, AtCap, MissingMaterials, Ready }

    private List<RecipeDef> craftingRecipes = new();

    private readonly Dictionary<RecipeDef, int> caps = new();
    // Per station, ordered; count = slots owned; null = empty slot
    private readonly List<RecipeDef>[] slots = new List<RecipeDef>[BaseSlots.Length];
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
        public CraftingStation station;
        public int slot;
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

        for (int i = 0; i < slots.Length; i++) slots[i] = new List<RecipeDef>(new RecipeDef[BaseSlots[i]]);
        craftingRecipes = new List<RecipeDef>(Resources.LoadAll<RecipeDef>("Recipes"));
        // Chain then tier (craft time rises with tier): the UI's "Unlock Order" and the legacy-save migration order
        craftingRecipes.Sort((a, b) => a.Station != b.Station ? a.Station.CompareTo(b.Station) : a.CraftSeconds.CompareTo(b.CraftSeconds));
        if (showDebugLogs) Debug.Log($"[CraftingManager] Loaded {craftingRecipes.Count} recipes from Resources/Recipes");
    }

    void Start()
    {
        inventory = Inventory.Instance;
        
        // Subscribe to events for event-driven auto-craft
        GameSignals.OnLootCollected += OnInventoryChanged;
        GameSignals.OnProductCrafted += OnInventoryChanged;
        GameSignals.OnCraftingConfigChanged += TryStartSlots;
    }

    void OnDestroy()
    {
        GameSignals.OnLootCollected -= OnInventoryChanged;
        GameSignals.OnProductCrafted -= OnInventoryChanged;
        GameSignals.OnCraftingConfigChanged -= TryStartSlots;
    }

    void Update()
    {
        TickCraftingTimers();
        SafetyPollAutoCraft();
    }

    // ===== CRAFTING TIMER SYSTEM =====

    private void TickCraftingTimers()
    {
        for (int i = 0; i < activeCrafts.Count; i++)
        {
            CraftingJob job = activeCrafts[i];
            job.timeRemaining -= Time.deltaTime;
            activeCrafts[i] = job;
        }

        // Iterate backwards to safely remove
        for (int i = activeCrafts.Count - 1; i >= 0; i--)
        {
            if (activeCrafts[i].timeRemaining <= 0f)
            {
                // Remove first so CompleteCraft's restart check sees the slot as free
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
            TryStartSlots();
        }
    }

    // ===== EVENT-DRIVEN AUTO-CRAFT =====

    private void OnInventoryChanged(ResourceStack stack)
    {
        TryStartSlots();
    }

    // Stations in order, slots first to last — slot order is the priority. Alloc-free.
    private void TryStartSlots()
    {
        if (inventory == null) return;
        for (int st = 0; st < slots.Length; st++)
            for (int i = 0; i < slots[st].Count; i++)
            {
                var r = slots[st][i];
                if (r != null && JobIndex((CraftingStation)st, i) < 0 && GetBlockReason(r) == null)
                    StartCraft((CraftingStation)st, i);
            }
    }

    /// <summary>Why a free slot holding this recipe can't start a job right now; null when it can. Pure.
    /// "At cap" counts stock + jobs already in flight, so stacked slots don't overshoot the cap.</summary>
    public string GetBlockReason(RecipeDef recipe)
    {
        if (!IsStationOwned(recipe.Station)) return "No station";
        if (inventory.Get(recipe.Output.itemCategory, recipe.Output) + InFlight(recipe) * recipe.OutputQty >= GetCap(recipe)) return "At cap";
        if (!CanCraft(recipe)) return "Missing materials";
        return null;
    }

    // ===== CRAFTING LOGIC =====

    public bool CanCraft(RecipeDef recipe)
    {
        foreach (var ingredient in recipe.Ingredients)
        {
            int usable = inventory.Get(ingredient.Item.itemCategory, ingredient.Item) - inventory.GetFloor(ingredient.Item);
            if (usable < ingredient.Qty) return false;
        }
        return true;
    }

    private void StartCraft(CraftingStation station, int slot)
    {
        var recipe = slots[(int)station][slot];
        if (!CanCraft(recipe)) return;

        foreach (var ingredient in recipe.Ingredients)
        {
            if (!inventory.TryRemove(inventory.GetInventoryType(ingredient.Item.itemCategory), ingredient.Item, ingredient.Qty))
            {
                Debug.LogError($"[CraftingManager] Failed to remove {ingredient.Qty}x {ingredient.Item.displayName} from inventory despite CanCraft check.");
                return;
            }
        }

        activeCrafts.Add(new CraftingJob { recipe = recipe, station = station, slot = slot, timeRemaining = recipe.CraftSeconds });

        if (showDebugLogs)
            Debug.Log($"[CraftingManager] Started {recipe.Output.displayName} in {station} slot {slot + 1} ({recipe.CraftSeconds}s)");
        GameSignals.RaiseCraftStarted(recipe);
    }

    public bool IsCrafting(RecipeDef recipe) => InFlight(recipe) > 0;

    private int InFlight(RecipeDef recipe)
    {
        int n = 0;
        foreach (var job in activeCrafts)
            if (job.recipe == recipe) n++;
        return n;
    }

    private int JobIndex(CraftingStation station, int slot)
    {
        for (int i = 0; i < activeCrafts.Count; i++)
            if (activeCrafts[i].station == station && activeCrafts[i].slot == slot) return i;
        return -1;
    }

    private void CompleteCraft(RecipeDef recipe)
    {
        inventory.Add(inventory.GetInventoryType(recipe.Output.itemCategory), new ResourceStack(recipe.Output, recipe.OutputQty, 0));
        GameSignals.RaiseProductCrafted(new ResourceStack(recipe.Output, recipe.OutputQty, 0));

        if (showDebugLogs)
            Debug.Log($"[CraftingManager] Crafted {recipe.OutputQty}x {recipe.Output.displayName}");

        TryStartSlots();
    }

    // A slot whose recipe changes drops its running job and gets the ingredients back (nothing is ever lost).
    private void CancelJob(CraftingStation station, int slot)
    {
        int j = JobIndex(station, slot);
        if (j < 0) return;
        Refund(activeCrafts[j].recipe);
        activeCrafts.RemoveAt(j);
    }

    private void Refund(RecipeDef recipe)
    {
        foreach (var ing in recipe.Ingredients)
            if (ing.Item != null)
                inventory.Add(inventory.GetInventoryType(ing.Item.itemCategory), new ResourceStack(ing.Item, ing.Qty, 0));
    }

    // ===== SLOTS =====

    /// <summary>The station's slots in order (count = slots owned; null = empty).</summary>
    public IReadOnlyList<RecipeDef> GetSlots(CraftingStation station) => slots[(int)station];

    /// <summary>Any slot at any station holds a recipe. Pure, alloc-free.</summary>
    public bool AnyRecipePinned
    {
        get
        {
            foreach (var list in slots)
                foreach (var r in list)
                    if (r != null) return true;
            return false;
        }
    }

    public int GetStationSlots(CraftingStation station) => slots[(int)station].Count;

    public bool HasFreeSlot(CraftingStation station) => slots[(int)station].Contains(null);

    /// <summary>Put a recipe (or null = clear) in a slot. A running job in that slot is cancelled and refunded.
    /// Raises OnCraftingConfigChanged.</summary>
    public void AssignSlot(CraftingStation station, int index, RecipeDef recipe)
    {
        var list = slots[(int)station];
        if (index < 0 || index >= list.Count || list[index] == recipe || (recipe != null && recipe.Station != station)) return;
        CancelJob(station, index);
        list[index] = recipe;
        if (showDebugLogs) Debug.Log($"[CraftingManager] {station} slot {index + 1} = {(recipe != null ? recipe.name : "empty")}");
        GameSignals.RaiseCraftingConfigChanged();
    }

    /// <summary>Book Craft stamp: first empty slot of the recipe's station. False when the station is full.</summary>
    public bool PinToFirstFreeSlot(RecipeDef recipe)
    {
        if (recipe == null) return false;
        int i = slots[(int)recipe.Station].IndexOf(null);
        if (i < 0) return false;
        AssignSlot(recipe.Station, i, recipe);
        return true;
    }

    /// <summary>Empty every slot holding this recipe (book dither-off). Raises OnCraftingConfigChanged once.</summary>
    public void ClearRecipeSlots(RecipeDef recipe)
    {
        if (recipe == null) return;
        var list = slots[(int)recipe.Station];
        bool any = false;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != recipe) continue;
            CancelJob(recipe.Station, i);
            list[i] = null;
            any = true;
        }
        if (any) GameSignals.RaiseCraftingConfigChanged();
    }

    /// <summary>Pins the recipe unless it already has a slot (progression unlocks, debugger). False if the station is full.</summary>
    public bool EnableRecipe(RecipeDef recipe) => recipe != null && (IsRecipeEnabled(recipe) || PinToFirstFreeSlot(recipe));

    /// <summary>Pinned in at least one slot.</summary>
    public bool IsRecipeEnabled(RecipeDef recipe) => recipe != null && slots[(int)recipe.Station].Contains(recipe);

    /// <summary>0..1 progress of the job running in this slot; 0 when idle.</summary>
    public float GetSlotProgress(CraftingStation station, int index)
    {
        int j = JobIndex(station, index);
        return j < 0 ? 0f : 1f - activeCrafts[j].timeRemaining / activeCrafts[j].recipe.CraftSeconds;
    }

    public SlotState GetSlotState(CraftingStation station, int index)
    {
        if (!IsStationOwned(station)) return SlotState.NoStation;
        if (JobIndex(station, index) >= 0) return SlotState.Crafting;
        var r = slots[(int)station][index];
        if (r == null) return SlotState.Empty;
        return GetBlockReason(r) switch
        {
            "At cap" => SlotState.AtCap,
            "Missing materials" => SlotState.MissingMaterials,
            _ => SlotState.Ready,
        };
    }

    // ===== CONFIGURATION METHODS =====

    /// <summary>"Produce until stock ≥ cap": no new job while the output's stock (+ jobs in flight) is at or above this.</summary>
    public int GetCap(RecipeDef recipe) => caps.TryGetValue(recipe, out int c) ? c : DefaultCap;

    public void SetCap(RecipeDef recipe, int cap)
    {
        cap = Mathf.Clamp(cap, 1, MaxCap);
        if (recipe == null || GetCap(recipe) == cap) return;
        caps[recipe] = cap;
        GameSignals.RaiseCraftingConfigChanged();
    }

    // ===== STATIONS =====

    public static UpgradeType StationUpgradeType(CraftingStation station) => StationUpgrade[(int)station];

    public bool IsStationOwned(CraftingStation station) =>
        ProgressionManager.Instance != null && ProgressionManager.Instance.HasUpgrade(StationUpgrade[(int)station]);

    public int GetRunningCount(CraftingStation station)
    {
        int n = 0;
        foreach (var job in activeCrafts)
            if (job.station == station) n++;
        return n;
    }

    /// <summary>Next slot: 2500 × 1.6^(slots bought so far).</summary>
    public int GetSlotCost(CraftingStation station) =>
        Mathf.RoundToInt(SlotBaseCost * Mathf.Pow(SlotCostGrowth, slots[(int)station].Count - BaseSlots[(int)station]));

    /// <summary>Gold sink: appends an empty slot at an owned station. Raises OnCraftingConfigChanged.</summary>
    public bool BuyStationSlot(CraftingStation station)
    {
        if (!IsStationOwned(station) || !Inventory.Instance.TrySpendGold(GetSlotCost(station))) return false;
        slots[(int)station].Add(null);
        if (showDebugLogs) Debug.Log($"[CraftingManager] {station} now has {slots[(int)station].Count} slots");
        GameSignals.RaiseCraftingConfigChanged();
        return true;
    }

    // ===== SAVE =====

    public CraftingSave ToSave()
    {
        var s = new CraftingSave { slots = new Dictionary<CraftingStation, List<string>>() };
        foreach (var r in craftingRecipes)
            s.recipes[r.name] = new RecipeSave { cap = GetCap(r) };
        for (int i = 0; i < slots.Length; i++)
            s.slots[(CraftingStation)i] = slots[i].Select(r => r != null ? r.name : null).ToList();
        foreach (var job in activeCrafts)
            s.jobs.Add(new CraftJobSave { station = job.station, slot = job.slot, recipe = job.recipe.name, remaining = job.timeRemaining });
        return s;
    }

    /// <summary>Load path (SaveManager.ApplyLate, after stock + floors). Running jobs resume in their slot with their
    /// remaining time (ingredients were consumed before the save). Pre-3b saves (no <c>slots</c>): enabled recipes are
    /// placed into free slots of their station (ones with a running job first; priority dropped), and a job that finds
    /// no slot holding its recipe is refunded.</summary>
    public void LoadFrom(CraftingSave s)
    {
        var byName = craftingRecipes.ToDictionary(r => r.name);
        caps.Clear();
        activeCrafts.Clear();
        foreach (var kv in s.recipes)
            if (byName.TryGetValue(kv.Key, out var r) && kv.Value != null) caps[r] = Mathf.Clamp(kv.Value.cap, 1, MaxCap);

        for (int i = 0; i < slots.Length; i++)
        {
            var st = (CraftingStation)i;
            List<string> saved = null;
            s.slots?.TryGetValue(st, out saved);
            int count = saved != null ? saved.Count
                      : s.stationSlots != null && s.stationSlots.TryGetValue(st, out int n) ? n : 0;
            slots[i].Clear();
            for (int k = 0; k < Mathf.Max(BaseSlots[i], count); k++)
            {
                string name = saved != null && k < saved.Count ? saved[k] : null;
                RecipeDef r = null;
                if (name != null && (!byName.TryGetValue(name, out r) || r.Station != st))
                {
                    Debug.LogError($"[CraftingManager] Unknown recipe '{name}' in {st} slot {k + 1} (asset renamed?) — slot left empty");
                    r = null;
                }
                slots[i].Add(r);
            }
        }

        if (s.slots == null)   // legacy: enabled flags → slots
        {
            var withJob = new HashSet<string>(s.jobs.Where(j => j != null).Select(j => j.recipe));
            foreach (var r in craftingRecipes.Where(r => s.recipes.TryGetValue(r.name, out var rs) && rs != null && rs.enabled)
                                             .OrderBy(r => withJob.Contains(r.name) ? 0 : 1))
            {
                int free = slots[(int)r.Station].IndexOf(null);
                if (free >= 0) slots[(int)r.Station][free] = r;
            }
        }

        foreach (var j in s.jobs)
        {
            if (j == null || !byName.TryGetValue(j.recipe ?? "", out var r))
            {
                Debug.LogError($"[CraftingManager] Unknown recipe '{j?.recipe}' in saved job (asset renamed?) — job and its ingredients lost");
                continue;
            }
            var list = slots[(int)r.Station];
            int slot = j.slot >= 0 && j.slot < list.Count && list[j.slot] == r && JobIndex(r.Station, j.slot) < 0 ? j.slot : -1;
            for (int k = 0; slot < 0 && k < list.Count; k++)
                if (list[k] == r && JobIndex(r.Station, k) < 0) slot = k;
            if (slot < 0) { Refund(r); continue; }
            activeCrafts.Add(new CraftingJob { recipe = r, station = r.Station, slot = slot, timeRemaining = Mathf.Clamp(j.remaining, 0f, r.CraftSeconds) });
        }
        GameSignals.RaiseCraftingConfigChanged();
    }

    public int GetActiveCraftCount() => activeCrafts.Count;

    public IReadOnlyList<CraftingJob> ActiveJobs => activeCrafts;

    public List<RecipeDef> GetAllRecipes() => new List<RecipeDef>(craftingRecipes);

    /// <summary>Furthest-along job of this recipe (it may run in several slots); 0 when none.</summary>
    public float GetCraftingProgress(RecipeDef recipe)
    {
        float best = 0f;
        foreach (var job in activeCrafts)
            if (job.recipe == recipe) best = Mathf.Max(best, 1f - job.timeRemaining / recipe.CraftSeconds);
        return best;
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

    [ContextMenu("Debug/Print Active Crafts")]
    public void DebugPrintActiveCrafts()
    {
        Debug.Log($"[CraftingManager] Active crafts: {activeCrafts.Count}");
        foreach (var job in activeCrafts)
        {
            Debug.Log($"  - {job.station} slot {job.slot + 1}: {job.recipe.Output.displayName}, {job.timeRemaining:F1}s remaining");
        }
    }
}