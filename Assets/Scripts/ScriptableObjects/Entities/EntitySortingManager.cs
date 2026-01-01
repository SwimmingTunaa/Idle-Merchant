using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages entity sorting orders to prevent z-fighting.
/// Assigns unique, sequential sorting orders based on entity type and spawn order.
/// </summary>
public class EntitySortingManager : MonoBehaviour
{
    public static EntitySortingManager Instance { get; private set; }
    
    [Header("Configuration")]
    [Tooltip("Reference to the sorting config ScriptableObject")]
    public EntitySortingConfig config;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    [SerializeField] private bool showWarnings = true;
    
    // Track spawn count per entity type
    private Dictionary<EntitySortingType, int> spawnCounters = new Dictionary<EntitySortingType, int>();
    
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        InitializeCounters();
    }
    
    private void InitializeCounters()
    {
        spawnCounters.Clear();
        
        if (config == null)
        {
            Debug.LogError("[EntitySortingManager] No config assigned! Create one via Assets > Create > Data > Entity Sorting Config");
            return;
        }
        
        // Initialize all counters to 0
        foreach (EntitySortingType type in System.Enum.GetValues(typeof(EntitySortingType)))
        {
            spawnCounters[type] = 0;
        }
        
        if (showDebugLogs)
            Debug.Log("[EntitySortingManager] Initialized sorting counters");
    }
    
    /// <summary>
    /// Get next unique sorting order for an entity type.
    /// Call this when spawning entities.
    /// </summary>
    public int GetNextSortingOrder(EntitySortingType type)
    {
        if (config == null)
        {
            Debug.LogError("[EntitySortingManager] No config assigned!");
            return 0;
        }
        
        if (!spawnCounters.ContainsKey(type))
        {
            spawnCounters[type] = 0;
        }
        
        var range = config.GetRange(type);
        int counter = spawnCounters[type];
        int sortingOrder = range.minOrder + counter;
        
        // Increment counter for next spawn
        spawnCounters[type]++;
        
        // Overflow check
        if (sortingOrder >= range.maxOrder)
        {
            if (showWarnings)
            {
                Debug.LogWarning($"[EntitySortingManager] Sorting order overflow for {type}! " +
                    $"Order {sortingOrder} exceeds max {range.maxOrder}. " +
                    $"Consider increasing range size or resetting counters.");
            }
            
            // Wrap around to start of range (will cause z-fighting with early spawns)
            sortingOrder = range.minOrder + (counter % range.RangeSize);
        }
        
        // Warning threshold check
        if (showWarnings && counter >= range.warningThreshold && counter == range.warningThreshold)
        {
            Debug.LogWarning($"[EntitySortingManager] {type} spawn count ({counter}) approaching range limit ({range.maxOrder - range.minOrder})");
        }
        
        if (showDebugLogs)
            Debug.Log($"[EntitySortingManager] Assigned order {sortingOrder} to {type} (spawn #{counter})");
        
        return sortingOrder;
    }
    
    /// <summary>
    /// Reset all spawn counters (useful for scene reloads or gameplay resets)
    /// </summary>
    public void ResetCounters()
    {
        foreach (var key in new List<EntitySortingType>(spawnCounters.Keys))
        {
            spawnCounters[key] = 0;
        }
        
        if (showDebugLogs)
            Debug.Log("[EntitySortingManager] Reset all spawn counters");
    }
    
    /// <summary>
    /// Reset counter for specific entity type
    /// </summary>
    public void ResetCounter(EntitySortingType type)
    {
        if (spawnCounters.ContainsKey(type))
        {
            spawnCounters[type] = 0;
            
            if (showDebugLogs)
                Debug.Log($"[EntitySortingManager] Reset counter for {type}");
        }
    }
    
    /// <summary>
    /// Get current spawn count for an entity type
    /// </summary>
    public int GetSpawnCount(EntitySortingType type)
    {
        return spawnCounters.ContainsKey(type) ? spawnCounters[type] : 0;
    }
    
    // ===== DEBUG =====
    
    [ContextMenu("Debug: Print Spawn Counts")]
    private void DebugPrintCounts()
    {
        Debug.Log("=== Entity Sorting Manager: Spawn Counts ===");
        
        foreach (var kvp in spawnCounters)
        {
            var range = config.GetRange(kvp.Key);
            float usage = (float)kvp.Value / range.RangeSize * 100f;
            
            Debug.Log($"{kvp.Key}: {kvp.Value} spawns ({usage:F1}% of range {range.minOrder}-{range.maxOrder})");
        }
    }
    
    [ContextMenu("Debug: Reset All Counters")]
    private void DebugResetCounters()
    {
        ResetCounters();
        Debug.Log("[EntitySortingManager] Counters reset via context menu");
    }
    
    [ContextMenu("Debug: Simulate 100 Mob Spawns")]
    private void DebugSimulateSpawns()
    {
        for (int i = 0; i < 100; i++)
        {
            GetNextSortingOrder(EntitySortingType.Mob);
        }
        Debug.Log("[EntitySortingManager] Simulated 100 mob spawns");
        DebugPrintCounts();
    }
}