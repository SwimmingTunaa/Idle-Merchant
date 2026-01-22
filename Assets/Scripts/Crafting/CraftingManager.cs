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
        
    }

    public bool CanCraft(RecipeDef recipe)
    {
        // Check if player has required ingredients available in inventory considering reserves
        foreach (var ingredient in recipe.Inputs)
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
            foreach (var ingredient in recipe.Inputs)
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
            if (CanCraft(recipe))
            {
                Debug.Log($"Can craft {recipe.Output.name}");
                // Remove ingredients
                foreach (var ingredient in recipe.Inputs)
                {
                    if(inventory.TryRemove(inventory.GetInventoryType(ingredient.Item.itemCategory), ingredient.Item, ingredient.Qty))
                    {
                        inventory.Add(inventory.GetInventoryType(recipe.Output.itemCategory), new ResourceStack(ingredient.Item , -ingredient.Qty, 0));
                        Debug.Log($"Crafted {recipe.Output.name} and removed {ingredient.Qty}x {ingredient.Item.displayName} from inventory");
                        Debug.Log($"Current {ingredient.Item.displayName} qty: {inventory.Get(ingredient.Item.itemCategory, ingredient.Item)}");
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to remove {ingredient.Qty}x {ingredient.Item.displayName}");
                    }
                }
            }
            else
            {
                Debug.Log($"Cannot craft {recipe.Output.name}");
            }
        }
    }
}