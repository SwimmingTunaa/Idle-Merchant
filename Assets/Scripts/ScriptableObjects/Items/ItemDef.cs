using UnityEngine;

public enum ItemCategory { Common, Crafted, Luxury }

/// <summary>
/// Standalone item definition for loot, crafting materials, and shop goods.
/// Does NOT inherit from EntityDef (items aren't entities).
/// </summary>
[CreateAssetMenu(menuName = "Data/Item (New)", fileName = "Item_")]
public class ItemDef : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique identifier for this item")]
    public string id;
    
    [Tooltip("Display name shown in UI")]
    public string displayName;
    
    [Tooltip("Item description for tooltips")]
    [TextArea(2, 4)]
    public string description;

    [Header("Category")]
    [Tooltip("Item category determines which inventory it goes into")]
    public ItemCategory itemCategory = ItemCategory.Common;

    [Header("Visuals")]
    [Tooltip("Sprite shown when loot is on the ground")]
    public Sprite spriteDrop;
    
    [Tooltip("Icon shown in inventory/shop UI")]
    public Sprite icon;

    [Header("Prefab")]
    [Tooltip("GameObject with Loot component (container + collider, NO visuals)")]
    public GameObject prefab;

    [Header("Drop Configuration")]
    [Range(0f, 1f)]
    [Tooltip("Chance this item drops from a mob's loot table (0 = never, 1 = always)")]
    public float chance = 1f;

    [Header("Economy")]
    [Tooltip("Base sell price at shop counter")]
    public int sellPrice = 1;
    
    [Tooltip("Base value used for calculations (may differ from sell price)")]
    public int baseValue = 1;

    void OnValidate()
    {
        if (string.IsNullOrEmpty(id))
        {
            id = name;
        }
        
        if (string.IsNullOrEmpty(displayName))
        {
            displayName = name;
        }

        ValidatePrefab();
    }

    private void ValidatePrefab()
    {
        if (prefab == null) return;

        Loot lootComponent = prefab.GetComponent<Loot>();
        if (lootComponent == null)
        {
            Debug.LogWarning($"[ItemDef: {name}] prefab missing Loot component!");
        }

        Collider2D collider = prefab.GetComponent<Collider2D>();
        if (collider == null)
        {
            Debug.LogWarning($"[ItemDef: {name}] prefab missing Collider2D!");
        }
    }
}
