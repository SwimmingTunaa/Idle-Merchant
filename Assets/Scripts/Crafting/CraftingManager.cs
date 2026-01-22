using System.Collections.Generic;
using UnityEngine;

public class CraftingManager : MonoBehaviour
{
    public static CraftingManager Instance { get; private set; }

    [Header("Crafting Config")]
    [SerializeField] private List<RecipeDef> craftingRecipes = new();
    [SerializeField] private Dictionary<ItemDef, int> materialReserves = new ();
    private Dictionary<RecipeDef, float> craftingTimers = new ();
    private Inventory inventory;

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
    }

    void Update()
    {
        foreach (var kvp in craftingTimers)
        {
            RecipeDef recipe = kvp.Key;
            craftingTimers[recipe] += Time.deltaTime;

            if (craftingTimers[recipe] >= recipe.CraftSeconds)
            {
                CompleteCraft(recipe);
            }
        }
    }

    public bool CanCraft(RecipeDef recipe)
    {
        // Check if player has required ingredients available in inventory considering reserves
        foreach (var ingredient in recipe.Ingredients)
        {
            int availableQty = inventory.Get(ingredient.Item.itemCategory, ingredient.Item);
            int reserveQty = materialReserves.ContainsKey(ingredient.Item) ? materialReserves[ingredient.Item] : 0;
            if (availableQty - reserveQty < ingredient.Qty)
            {      
                return false; // Not enough available quantity after considering reserves
            }
        }
        return true;
    }

    public void StartCraft(RecipeDef recipe)
    {
        if (!CanCraft(recipe))
        {
            Debug.LogWarning($"Cannot start crafting {recipe.Output.name}: insufficient ingredients.");
            //TODO: Feedback to player
            return;
        }

        //try remove ingredients from inventory
        foreach (var ingredient in recipe.Ingredients)
        {
            if(!inventory.TryRemove(inventory.GetInventoryType(ingredient.Item.itemCategory), ingredient.Item, ingredient.Qty))
            {
                Debug.LogError($"Failed to remove {ingredient.Qty}x {ingredient.Item.displayName} from inventory despite CanCraft check.");
                return;
            }
        }

        // add output to crafting timers
        if (!craftingTimers.ContainsKey(recipe))
        {
            craftingTimers[recipe] = 0f;
        }
    }

    public void CompleteCraft(RecipeDef recipe)
    {
        if (!craftingTimers.ContainsKey(recipe))
        {
            return;
        }

        // Add crafted item to inventory
        inventory.Add(inventory.GetInventoryType(recipe.Output.itemCategory), new ResourceStack(recipe.Output, recipe.OutputQty, 0));

        // Remove from crafting timers
        craftingTimers.Remove(recipe);

        // Notify game signals of crafted product
        GameSignals.RaiseProductCrafted(new ResourceStack(recipe.Output, recipe.OutputQty,0));

        Debug.Log($"Crafted {recipe.OutputQty}x {recipe.Output.displayName}.");
    }

    public void SetReserve(ItemDef material, int qty)
    {
        if (materialReserves.ContainsKey(material))
        {
            materialReserves[material] = qty;
        }
        else
        {
            materialReserves.Add(material, qty);
        }
    }
    
   [ContextMenu("Debug/Check Can Craft [RecipeName]")]
    public void DebugCheckCanCraft()
    {
       foreach (var recipe in craftingRecipes)
       {
           Debug.Log($"Can craft {recipe.Output.name}: {CanCraft(recipe)}");
       }
    }

    [ContextMenu("Debug/Set Material Reserve to 10")]
    public void DebugSetMaterialReserve()
    {
        foreach (var recipe in craftingRecipes)
        {
            foreach (var ingredient in recipe.Ingredients)
            {
                SetReserve(ingredient.Item, 10);
                Debug.Log($"Set reserve of {ingredient.Item.name} to 10");
            }
        }
    }

    [ContextMenu("Debug/Craft All Recipes and remove ingredients")]
    public void DebugCraftAllRecipes()
    {
         foreach (var recipe in craftingRecipes)
         {
              StartCraft(recipe);
              Debug.Log($"Started crafting {recipe.Output.name}");
         }
    }
}