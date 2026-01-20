using UnityEngine;

/// <summary>
/// Defines sorting order ranges for different entity types.
/// Prevents z-fighting by giving each type a unique range.
/// Designer can tweak ranges without touching code.
/// </summary>
[CreateAssetMenu(menuName = "Data/Entity Sorting Config", fileName = "EntitySortingConfig")]
public class EntitySortingConfig : ScriptableObject
{
    [System.Serializable]
    public class SortingRange
    {
        public EntitySortingType entityType;
        
        [Tooltip("Starting sorting order for this entity type")]
        public int minOrder;
        
        [Tooltip("Maximum sorting order (exclusive) for this entity type")]
        public int maxOrder;
        
        [Tooltip("Warn in console if spawn count approaches this limit")]
        public int warningThreshold = 900;
        
        public int RangeSize => maxOrder - minOrder;
    }
    
    [Header("Sorting Order Ranges")]
    [Tooltip("Define sorting order ranges for each entity type. Lower = behind, Higher = in front")]
    public SortingRange[] sortingRanges = new SortingRange[]
    {
        new SortingRange { entityType = EntitySortingType.Loot, minOrder = 2000, maxOrder = 3000 },
        new SortingRange { entityType = EntitySortingType.Mob, minOrder = 3000, maxOrder = 4000 },
        new SortingRange { entityType = EntitySortingType.Adventurer, minOrder = 4000, maxOrder = 5000 },
        new SortingRange { entityType = EntitySortingType.Porter, minOrder = 5000, maxOrder = 6000 },
        new SortingRange { entityType = EntitySortingType.Customer, minOrder = 6000, maxOrder = 7000 },
        new SortingRange { entityType = EntitySortingType.Effect, minOrder = 7000, maxOrder = 8000 }
    };
    
    /// <summary>
    /// Get sorting range for a specific entity type
    /// </summary>
    public SortingRange GetRange(EntitySortingType type)
    {
        foreach (var range in sortingRanges)
        {
            if (range.entityType == type)
                return range;
        }
        
        Debug.LogError($"[EntitySortingConfig] No range defined for type: {type}");
        return sortingRanges[0]; // Fallback to first range
    }
    
    /// <summary>
    /// Validate config (called in OnValidate)
    /// </summary>
    private void OnValidate()
    {
        // Check for overlapping ranges
        for (int i = 0; i < sortingRanges.Length; i++)
        {
            for (int j = i + 1; j < sortingRanges.Length; j++)
            {
                var rangeA = sortingRanges[i];
                var rangeB = sortingRanges[j];
                
                if (rangeA.minOrder < rangeB.maxOrder && rangeB.minOrder < rangeA.maxOrder)
                {
                    Debug.LogWarning($"[EntitySortingConfig] Overlapping ranges: {rangeA.entityType} and {rangeB.entityType}");
                }
            }
        }
        
        // Ensure ranges are large enough
        foreach (var range in sortingRanges)
        {
            if (range.RangeSize < 100)
            {
                Debug.LogWarning($"[EntitySortingConfig] Range for {range.entityType} is very small ({range.RangeSize}). Consider increasing.");
            }
        }
    }
}

/// <summary>
/// Entity type enum for sorting.
/// Add new types here as needed.
/// </summary>
public enum EntitySortingType
{
    Loot,
    Mob,
    Adventurer,
    Porter,
    Customer,
    Effect
}