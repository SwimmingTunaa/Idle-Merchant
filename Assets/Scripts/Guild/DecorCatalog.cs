using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every guild-hall decor variant, at Resources/DecorCatalog so DecorSlot, the Decorate panel and save restore share one list.
/// </summary>
[CreateAssetMenu(menuName = "Data/Decor Catalog")]
public class DecorCatalog : ScriptableObject
{
    [Tooltip("All variants, in Decorate-panel order per slot group. Asset names are save keys — never rename.")]
    public List<DecorVariantDef> variants = new();

    private static DecorCatalog instance;
    public static DecorCatalog Instance => instance != null ? instance : instance = Resources.Load<DecorCatalog>("DecorCatalog");

    /// <summary>Variants of a slot group, catalog order. Allocates (panel open only).</summary>
    public List<DecorVariantDef> ForSlot(string slot) => variants.FindAll(v => v != null && v.slot == slot);

    public DecorVariantDef Find(string name) => variants.Find(v => v != null && v.name == name);
}
