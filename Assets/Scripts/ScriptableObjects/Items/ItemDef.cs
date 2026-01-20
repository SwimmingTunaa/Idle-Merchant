using UnityEngine;

public enum ItemCategory { Common, Crafted, Luxury }

/// <summary>
/// Item definition for loot drops.
/// prefab = the Loot container (collider + Loot component, no visuals)
/// visualPrefab = the visual (sprite + optional VFX)
/// Container is pooled, visual is instantiated/destroyed.
/// </summary>
[CreateAssetMenu(menuName = "Data/Item")]
public class ItemDef : EntityDef
{
    [Header("Item Data")]
    public ItemCategory itemCategory;
    
    [Range(0f, 1f)] 
    [Tooltip("Chance this item drops from a mob's loot table")]
    public float chance;
    
    [Tooltip("How much this item sells for at the shop")]
    public float sellPrice = 1f;

    // ===== VALIDATION =====

    private void OnEnable()
    {
        if (sortingType != EntitySortingType.Loot)
        {
            sortingType = EntitySortingType.Loot;
        }
    }


#if UNITY_EDITOR
    void OnValidate()
    {
        ValidatePrefabs();
    }

    private void ValidatePrefabs()
    {
        // Validate loot container prefab
        if (prefab != null)
        {
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
#endif
}