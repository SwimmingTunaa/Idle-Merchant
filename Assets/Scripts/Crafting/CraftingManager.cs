using System.Collections.Generic;
using UnityEngine;

public class CraftingManager : MonoBehaviour
{
    public static CraftingManager Instance { get; private set; }

    [Header("Crafting Config")]
    [SerializeField] private List<RecipeDef> craftingRecipes = new();

    private Dictionary<ItemDef, int> materialReserves = new();
    private List<RecipeDef> enabledRecipes = new();
    private List<CraftingJob> activeCrafts = new();
    private Inventory inventory;

    [Header("Safety Polling")]
    [SerializeField] private float safetyPollInterval = 2f;
    private float safetyPollTimer = 0f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    private struct CraftingJob
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
        }
    }

    void Start()
    {
        inventory = Inventory.Instance;
        
        // Subscribe to events for event-driven auto-craft
        GameSignals.OnLootCollected += OnInventoryChanged;
        GameSignals.OnProductCrafted += OnInventoryChanged;
    }

    void OnDestroy()
    {
        GameSignals.OnLootCollected -= OnInventoryChanged;
        GameSignals.OnProductCrafted -= OnInventoryChanged;
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
                CompleteCraft(activeCrafts[i].recipe);
                activeCrafts.RemoveAt(i);
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

    private void TryStartEnabledRecipes()
    {
        foreach (var recipe in enabledRecipes)
        {
            if (CanCraft(recipe) && !IsCrafting(recipe))
            {
                StartCraft(recipe);
            }
        }
    }

    // ===== CRAFTING LOGIC =====

    public bool CanCraft(RecipeDef recipe)
    {
        foreach (var ingredient in recipe.Ingredients)
        {
            int availableQty = inventory.Get(ingredient.Item.itemCategory, ingredient.Item);
            int reserveQty = materialReserves.ContainsKey(ingredient.Item) ? materialReserves[ingredient.Item] : 0;
            
            if (availableQty - reserveQty < ingredient.Qty)
            {
                return false;
            }
        }
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

    public void EnableRecipe(RecipeDef recipe)
    {
        if (!enabledRecipes.Contains(recipe))
        {
            enabledRecipes.Add(recipe);
            TryStartEnabledRecipes(); // Immediate check
        }
    }

    public void DisableRecipe(RecipeDef recipe)
    {
        if (enabledRecipes.Contains(recipe))
        {
            enabledRecipes.Remove(recipe);
        }
    }

    public void SetReserve(ItemDef material, int qty)
    {
        materialReserves[material] = qty;
    }

    // ===== QUERIES =====

    public int GetReserve(ItemDef material)
    {
        return materialReserves.ContainsKey(material) ? materialReserves[material] : 0;
    }

    public bool IsRecipeEnabled(RecipeDef recipe)
    {
        return enabledRecipes.Contains(recipe);
    }

    public int GetActiveCraftCount()
    {
        return activeCrafts.Count;
    }

    public List<RecipeDef> GetEnabledRecipes()
    {
        return new List<RecipeDef>(enabledRecipes);
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

    [ContextMenu("Debug/Set All Material Reserves to 10")]
    public void DebugSetMaterialReserve()
    {
        foreach (var recipe in craftingRecipes)
        {
            foreach (var ingredient in recipe.Ingredients)
            {
                SetReserve(ingredient.Item, 10);
                Debug.Log($"[CraftingManager] Set reserve of {ingredient.Item.displayName} to 10");
            }
        }
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