using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Optimized LootManager using spatial grid for O(1) queries.
/// OLD: Linear scan through all loot per porter (O(n))
/// NEW: Spatial grid query only nearby loot (O(1))
/// 
/// With 50 loot items:
/// - OLD: 50 distance checks per porter = 250 checks for 5 porters
/// - NEW: ~8 distance checks per porter = 40 checks for 5 porters (6x faster)
/// </summary>
public class LootManager : MonoBehaviour
{
    public static LootManager Instance { get; private set; }

    [Header("Spatial Grid Settings")]
    [Tooltip("Cell size for spatial grid. Larger = fewer cells, coarser queries")]
    [SerializeField] private float gridCellSize = 5f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    [SerializeField] private bool showDebugGizmos = false;

    // Spatial grids per layer for fast queries
    private Dictionary<int, SpatialGrid<Loot>> spatialGridsByLayer = new Dictionary<int, SpatialGrid<Loot>>();
    
    // All active loot (for cleanup/queries that need full list)
    private Dictionary<int, List<Loot>> lootByLayer = new Dictionary<int, List<Loot>>();
    
    // Reservation tracking
    private Dictionary<Loot, PorterAgent> reservations = new Dictionary<Loot, PorterAgent>();

    // Cleanup timer (runs every 2 seconds instead of every frame)
    private float cleanupTimer = 0f;
    private const float CLEANUP_INTERVAL = 2f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Update()
    {
        // Periodic cleanup (not every frame)
        cleanupTimer += Time.deltaTime;
        if (cleanupTimer >= CLEANUP_INTERVAL)
        {
            cleanupTimer = 0f;
            CleanupNullReferences();
        }
    }

    // ===== LOOT REGISTRATION =====

    public void RegisterLoot(Loot loot, int layer)
    {
        if (loot == null) return;

        // Ensure spatial grid exists for this layer
        if (!spatialGridsByLayer.ContainsKey(layer))
        {
            spatialGridsByLayer[layer] = new SpatialGrid<Loot>(gridCellSize);
        }

        if (!lootByLayer.ContainsKey(layer))
        {
            lootByLayer[layer] = new List<Loot>();
        }

        // Register in spatial grid
        spatialGridsByLayer[layer].Register(loot);
        
        // Add to list
        if (!lootByLayer[layer].Contains(loot))
        {
            lootByLayer[layer].Add(loot);
            
            if (showDebugLogs)
                Debug.Log($"[LootManager] Registered {loot.name} on layer {layer}. Total: {lootByLayer[layer].Count}");
        }
    }

    public void UnregisterLoot(Loot loot, int layer)
    {
        if (loot == null) return;

        // Remove from spatial grid
        if (spatialGridsByLayer.ContainsKey(layer))
        {
            spatialGridsByLayer[layer].Unregister(loot);
        }

        // Remove from list
        if (lootByLayer.ContainsKey(layer))
        {
            lootByLayer[layer].Remove(loot);
            
            if (showDebugLogs)
                Debug.Log($"[LootManager] Unregistered {loot.name} from layer {layer}. Remaining: {lootByLayer[layer].Count}");
        }

        // Release reservation
        if (reservations.ContainsKey(loot))
        {
            var porter = reservations[loot];
            reservations.Remove(loot);
            
            if (showDebugLogs)
                Debug.Log($"[LootManager] Released reservation on {loot.name}");
        }
    }

    // ===== LOOT ACQUISITION (Spatial Grid Optimized) =====

    /// <summary>
    /// Find nearest unreserved loot using spatial grid.
    /// OLD: O(n) scan through all loot
    /// NEW: O(1) query nearby cells only
    /// </summary>
    public Loot RequestLoot(PorterAgent requester, Vector3 position, float scanRange, int layer)
    {
        if (requester == null) return null;

        // Check if spatial grid exists for this layer
        if (!spatialGridsByLayer.ContainsKey(layer))
            return null;

        // Query nearby loot using spatial grid
        var nearbyLoot = spatialGridsByLayer[layer].QueryRadius(position, scanRange);

        if (nearbyLoot.Count == 0)
            return null;

        Loot closestLoot = null;
        float closestSqrDistance = float.MaxValue;
        float scanRangeSqr = scanRange * scanRange;

        // Check nearby loot only (much smaller set than full layer)
        foreach (var loot in nearbyLoot)
        {
            // Skip invalid loot
            if (loot == null || loot.gameObject == null) continue;

            // If already reserved by this porter, return it
            if (IsReservedBy(loot, requester))
            {
                if (showDebugLogs)
                    Debug.Log($"[LootManager] {requester.name} maintaining existing reservation");
                return loot;
            }

            // Skip reserved loot
            if (IsReserved(loot)) continue;

            // Find closest (use sqrMagnitude to avoid sqrt)
            float sqrDistance = (position - loot.transform.position).sqrMagnitude;
            
            if (sqrDistance <= scanRangeSqr && sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closestLoot = loot;
            }
        }

        // Reserve if found
        if (closestLoot != null)
        {
            ReserveLoot(closestLoot, requester);
            
            if (showDebugLogs)
                Debug.Log($"[LootManager] {requester.name} reserved {closestLoot.name}");
        }

        return closestLoot;
    }

    public void ReserveLoot(Loot loot, PorterAgent porter)
    {
        if (loot == null || porter == null) return;

        ReleaseLootByPorter(porter);

        reservations[loot] = porter;
        loot.SetReservedBy(porter);

        if (showDebugLogs)
            Debug.Log($"[LootManager] {porter.name} reserved {loot.name}");
    }

    public void ReleaseLoot(Loot loot)
    {
        if (loot == null) return;

        if (reservations.ContainsKey(loot))
        {
            var porter = reservations[loot];
            reservations.Remove(loot);
            loot.SetReservedBy(null);

            if (showDebugLogs)
                Debug.Log($"[LootManager] Released {loot.name}");
        }
    }

    public void ReleaseLootByPorter(PorterAgent porter)
    {
        if (porter == null) return;

        Loot lootToRelease = null;
        foreach (var kvp in reservations)
        {
            if (kvp.Value == porter)
            {
                lootToRelease = kvp.Key;
                break;
            }
        }

        if (lootToRelease != null)
        {
            ReleaseLoot(lootToRelease);
        }
    }

    // ===== QUERIES =====

    public bool IsReserved(Loot loot)
    {
        return loot != null && reservations.ContainsKey(loot);
    }

    public bool IsReservedBy(Loot loot, PorterAgent porter)
    {
        if (loot == null || porter == null) return false;
        return reservations.TryGetValue(loot, out var reserver) && reserver == porter;
    }

    public List<Loot> GetLootOnLayer(int layer)
    {
        if (lootByLayer.ContainsKey(layer))
            return new List<Loot>(lootByLayer[layer]);
        return new List<Loot>();
    }

    public int GetTotalLootCount()
    {
        int count = 0;
        foreach (var layer in lootByLayer.Values)
        {
            count += layer.Count;
        }
        return count;
    }

    public int GetUnreservedLootCount(int layer)
    {
        if (!lootByLayer.ContainsKey(layer)) return 0;
        
        int count = 0;
        foreach (var loot in lootByLayer[layer])
        {
            if (loot != null && !IsReserved(loot))
                count++;
        }
        return count;
    }

    // ===== CLEANUP =====

    private void CleanupNullReferences()
    {
        // Clean loot lists
        foreach (var layer in lootByLayer.Values)
        {
            layer.RemoveAll(l => l == null || l.gameObject == null);
        }

        // Clean spatial grids
        foreach (var grid in spatialGridsByLayer.Values)
        {
            grid.Cleanup();
        }

        // Clean reservations
        var keysToRemove = new List<Loot>();
        foreach (var kvp in reservations)
        {
            if (kvp.Key == null || kvp.Key.gameObject == null || kvp.Value == null)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            if (key != null) key.SetReservedBy(null);
            reservations.Remove(key);
        }
    }

    // ===== DEBUG =====

    [ContextMenu("Debug: Print All Loot")]
    private void DebugPrintAllLoot()
    {
        Debug.Log("=== LootManager: All Loot ===");
        foreach (var kvp in lootByLayer)
        {
            Debug.Log($"Layer {kvp.Key}: {kvp.Value.Count} loot items");
            foreach (var loot in kvp.Value)
            {
                if (loot == null) continue;
                
                string status = IsReserved(loot) 
                    ? $"RESERVED by {reservations[loot].name}" 
                    : "AVAILABLE";
                Debug.Log($"  - {loot.name}: {status}");
            }
        }
        Debug.Log($"Total reservations: {reservations.Count}");
    }

    [ContextMenu("Debug: Print Stats")]
    private void DebugPrintStats()
    {
        int totalLoot = GetTotalLootCount();
        int reservedLoot = reservations.Count;
        int unreservedLoot = totalLoot - reservedLoot;

        Debug.Log("=== LootManager Stats ===");
        Debug.Log($"Total Loot: {totalLoot}");
        Debug.Log($"Reserved: {reservedLoot}");
        Debug.Log($"Unreserved: {unreservedLoot}");
        
        foreach (var kvp in spatialGridsByLayer)
        {
            Debug.Log($"Layer {kvp.Key} grid: {kvp.Value.Count} entities");
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying) return;

        // Draw spatial grids
        foreach (var kvp in spatialGridsByLayer)
        {
            kvp.Value.DebugDraw(new Color(0f, 1f, 0f, 0.3f));
        }
    }
#endif
}