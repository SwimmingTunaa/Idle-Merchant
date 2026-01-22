using System.Collections.Generic;
using UnityEngine;

public class CraftingManager : MonoBehaviour
{
    public static CraftingManager Instance { get; private set; }

    [Header("Crafting Config")]
    [SerializeField] private List<RecipeDef> craftingRecipes = new();
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
            if (Inventory.Instance.Get(ingredient.Item.itemCategory, ingredient.Item) < ingredient.Qty)
                return false;
        }
        return true;
    }
    
   [ContextMenu("Debug/Check Can Craft [RecipeName]")]
    public void DebugCheckCanCraft()
    {
       foreach (var recipe in craftingRecipes)
       {
           Debug.Log($"Can craft {recipe.Output.name}: {CanCraft(recipe)}");
       }
    }
}