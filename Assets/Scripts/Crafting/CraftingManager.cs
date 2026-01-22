using System.Collections.Generic;
using UnityEngine;

public class CraftingManager : MonoBehaviour
{
    public static CraftingManager Instance { get; private set; }

    [Header("Crafting Config")]
    [SerializeField] private List<RecipeDef> craftingRecipes = new();
    [SerializeField] private Dictionary<ItemDef, int> materialReserves = new ();
    private Dictionary<RecipeDef, float> craftingTimers = new ();

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

    void Update()
    {
        
    }

    bool CanCraft(RecipeDef recipe)
    {
        // Check if player has required ingredients
        foreach (var ingredient in recipe.Inputs)
        {
            if (Inventory.Instance.Get(ingredient.Item.itemCategory, ingredient.Item) < ingredient.Qty - materialReserves.GetValueOrDefault(ingredient.Item, 0))
                return false;
        }
        return true;
    }

    void SetReserve(ItemDef material, int qty)
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
            }
        }
    }
}