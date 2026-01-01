using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Optimized ProgressionManager that uses registration instead of scene scanning.
/// Spawners register themselves on Awake - no expensive FindObjectsByType calls.
/// </summary>
public class ProgressionManager : MonoBehaviour
{
    public static ProgressionManager Instance { get; private set; }

    [Header("Progression")]
    [Tooltip("Maximum dungeon layer currently unlocked (1-10)")]
    public int maxUnlockedLayer = 1;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // Cache for performance
    private Dictionary<ItemCategory, List<ItemDef>> availableItemsCache = new Dictionary<ItemCategory, List<ItemDef>>();
    private int cachedForLayer = -1;

    // Spawner registration system (replaces FindObjectsByType)
    private static List<Spawner> registeredSpawners = new List<Spawner>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        RebuildAvailableItemsCache();
    }

    // ===== SPAWNER REGISTRATION (replaces scene scanning) =====

    /// <summary>
    /// Called by Spawner.Awake() to register itself.
    /// Replaces expensive FindObjectsByType scene scan.
    /// </summary>
    public static void RegisterSpawner(Spawner spawner)
    {
        if (spawner != null && !registeredSpawners.Contains(spawner))
        {
            registeredSpawners.Add(spawner);
        }
    }

    /// <summary>
    /// Called by Spawner.OnDestroy() to unregister.
    /// </summary>
    public static void UnregisterSpawner(Spawner spawner)
    {
        registeredSpawners.Remove(spawner);
    }

    // ===== PUBLIC API =====

    public List<ItemDef> GetAvailableItems(ItemCategory category, float maxBudget)
    {
        if (cachedForLayer != maxUnlockedLayer)
        {
            RebuildAvailableItemsCache();
        }

        if (!availableItemsCache.ContainsKey(category))
        {
            if (showDebugLogs)
                Debug.LogWarning($"[ProgressionManager] No items found for category: {category}");
            return new List<ItemDef>();
        }

        var affordable = availableItemsCache[category]
            .Where(item => item.sellPrice <= maxBudget)
            .ToList();

        if (showDebugLogs)
            Debug.Log($"[ProgressionManager] Found {affordable.Count} {category} items within budget {maxBudget}g");

        return affordable;
    }

    public ItemDef GetRandomAvailableItem(ItemCategory category, float maxBudget)
    {
        var available = GetAvailableItems(category, maxBudget);
        
        if (available.Count == 0)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[ProgressionManager] No available items for category {category} within budget {maxBudget}g");
            return null;
        }

        return available[Random.Range(0, available.Count)];
    }

    public void UnlockLayer(int layer)
    {
        if (layer <= maxUnlockedLayer)
        {
            Debug.LogWarning($"[ProgressionManager] Layer {layer} is already unlocked");
            return;
        }

        maxUnlockedLayer = layer;
        RebuildAvailableItemsCache();

        if (showDebugLogs)
            Debug.Log($"[ProgressionManager] Unlocked layer {layer}! Rebuilding available items cache.");
    }

    // ===== CACHE MANAGEMENT =====

    /// <summary>
    /// Optimized cache rebuild using registered spawners (no scene scan).
    /// OLD: FindObjectsByType (5-50ms spike)
    /// NEW: Iterate registered list (0.1-0.5ms)
    /// </summary>
    private void RebuildAvailableItemsCache()
    {
        availableItemsCache.Clear();

        if (registeredSpawners.Count == 0)
        {
            Debug.LogWarning("[ProgressionManager] No spawners registered! Make sure Spawner.Awake() calls RegisterSpawner()");
            return;
        }

        HashSet<ItemDef> collectedItems = new HashSet<ItemDef>();

        // Only iterate registered spawners (no scene scan)
        foreach (var spawner in registeredSpawners)
        {
            if (spawner == null) continue;

            // Only include spawners from unlocked layers
            if (spawner.layerIndex <= maxUnlockedLayer && spawner.layerIndex > 0)
            {
                foreach (var candidate in spawner.candidates)
                {
                    if (candidate == null) continue;

                    MobDef mobDef = candidate as MobDef;
                    if (mobDef != null)
                    {
                        foreach (var itemDef in mobDef.loot)
                        {
                            if (itemDef != null)
                            {
                                collectedItems.Add(itemDef);
                            }
                        }
                    }
                }
            }
        }

        // Organize by category
        foreach (var item in collectedItems)
        {
            if (!availableItemsCache.ContainsKey(item.itemCategory))
            {
                availableItemsCache[item.itemCategory] = new List<ItemDef>();
            }

            availableItemsCache[item.itemCategory].Add(item);
        }

        cachedForLayer = maxUnlockedLayer;

        if (showDebugLogs)
        {
            Debug.Log($"[ProgressionManager] Cache rebuilt for layers 1-{maxUnlockedLayer}:");
            Debug.Log($"  Found {registeredSpawners.Count} registered spawners");
            foreach (var kvp in availableItemsCache)
            {
                Debug.Log($"  {kvp.Key}: {kvp.Value.Count} items");
            }
        }
    }

    // ===== DEBUG =====

    [ContextMenu("Debug: Print Available Items")]
    private void DebugPrintAvailableItems()
    {
        Debug.Log($"=== Available Items (Layers 1-{maxUnlockedLayer}) ===");
        
        foreach (var kvp in availableItemsCache)
        {
            Debug.Log($"\n{kvp.Key} ({kvp.Value.Count} items):");
            foreach (var item in kvp.Value)
            {
                Debug.Log($"  - {item.displayName} ({item.sellPrice}g)");
            }
        }
    }

    [ContextMenu("Debug: Unlock Next Layer")]
    private void DebugUnlockNextLayer()
    {
        if (maxUnlockedLayer < 10)
        {
            UnlockLayer(maxUnlockedLayer + 1);
            Debug.Log($"[ProgressionManager] Unlocked layer {maxUnlockedLayer}");
        }
        else
        {
            Debug.Log("[ProgressionManager] All layers already unlocked!");
        }
    }

    [ContextMenu("Debug: Reset to Layer 1")]
    private void DebugResetProgression()
    {
        maxUnlockedLayer = 1;
        RebuildAvailableItemsCache();
        Debug.Log("[ProgressionManager] Reset to layer 1");
    }

    [ContextMenu("Debug: Print Registered Spawners")]
    private void DebugPrintSpawners()
    {
        Debug.Log($"=== Registered Spawners ({registeredSpawners.Count}) ===");
        foreach (var spawner in registeredSpawners)
        {
            if (spawner != null)
                Debug.Log($"  - Layer {spawner.layerIndex}: {spawner.name} ({spawner.candidates.Count} candidates)");
        }
    }
}