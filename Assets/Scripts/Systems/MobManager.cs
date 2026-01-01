using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Optimized MobManager using spatial grid for O(1) queries.
/// Handles multi-adventurer reservations with smart target distribution.
/// </summary>
public class MobManager : MonoBehaviour
{
    public static MobManager Instance { get; private set; }

    [Header("Reservation Settings")]
    [Tooltip("Default max adventurers per mob")]
    [SerializeField] private int defaultMaxAttackersPerMob = 3;

    [Header("Spatial Grid Settings")]
    [Tooltip("Cell size for spatial grid")]
    [SerializeField] private float gridCellSize = 5f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    [SerializeField] private bool showDebugGizmos = false;

    // Spatial grids per layer
    private Dictionary<int, SpatialGrid<MobAgent>> spatialGridsByLayer = new Dictionary<int, SpatialGrid<MobAgent>>();
    
    // All active mobs
    private Dictionary<int, List<MobAgent>> mobsByLayer = new Dictionary<int, List<MobAgent>>();
    
    // Multi-reservation tracking
    private Dictionary<MobAgent, List<AdventurerAgent>> reservations = new Dictionary<MobAgent, List<AdventurerAgent>>();

    // Cleanup timer
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
        cleanupTimer += Time.deltaTime;
        if (cleanupTimer >= CLEANUP_INTERVAL)
        {
            cleanupTimer = 0f;
            CleanupNullReferences();
        }
    }

    // ===== MOB REGISTRATION =====

    public void RegisterMob(MobAgent mob, int layer)
    {
        if (mob == null) return;

        if (!spatialGridsByLayer.ContainsKey(layer))
        {
            spatialGridsByLayer[layer] = new SpatialGrid<MobAgent>(gridCellSize);
        }

        if (!mobsByLayer.ContainsKey(layer))
        {
            mobsByLayer[layer] = new List<MobAgent>();
        }

        spatialGridsByLayer[layer].Register(mob);

        if (!mobsByLayer[layer].Contains(mob))
        {
            mobsByLayer[layer].Add(mob);
            
            if (showDebugLogs)
                Debug.Log($"[MobManager] Registered {mob.name} on layer {layer}. Total: {mobsByLayer[layer].Count}");
        }
    }

    public void UnregisterMob(MobAgent mob, int layer)
    {
        if (mob == null) return;

        if (spatialGridsByLayer.ContainsKey(layer))
        {
            spatialGridsByLayer[layer].Unregister(mob);
        }

        if (mobsByLayer.ContainsKey(layer))
        {
            mobsByLayer[layer].Remove(mob);
            
            if (showDebugLogs)
                Debug.Log($"[MobManager] Unregistered {mob.name} from layer {layer}. Remaining: {mobsByLayer[layer].Count}");
        }

        if (reservations.ContainsKey(mob))
        {
            int count = reservations[mob].Count;
            reservations.Remove(mob);
            
            if (showDebugLogs)
                Debug.Log($"[MobManager] Released {count} reservations on {mob.name}");
        }
    }

    // ===== TARGET ACQUISITION (Spatial Grid Optimized) =====

    /// <summary>
    /// Find best available mob using spatial grid.
    /// Prefers unreserved, fallback to shared targets.
    /// </summary>
    public MobAgent RequestTarget(AdventurerAgent requester, Vector3 position, float scanRange, int layer)
    {
        if (requester == null) return null;
        
        if (!spatialGridsByLayer.ContainsKey(layer))
            return null;

        // Query nearby mobs using spatial grid
        var nearbyMobs = spatialGridsByLayer[layer].QueryRadius(position, scanRange);

        if (nearbyMobs.Count == 0)
            return null;

        MobAgent closestUnreserved = null;
        MobAgent closestReserved = null;
        float closestUnreservedSqrDist = float.MaxValue;
        float closestReservedSqrDist = float.MaxValue;
        float scanRangeSqr = scanRange * scanRange;

        foreach (var mob in nearbyMobs)
        {
            if (mob == null || mob.gameObject == null) continue;

            // If already reserved by this adventurer, maintain it
            if (IsReservedBy(mob, requester))
            {
                if (showDebugLogs)
                    Debug.Log($"[MobManager] {requester.name} maintaining existing target");
                return mob;
            }

            float sqrDist = (position - mob.transform.position).sqrMagnitude;
            if (sqrDist > scanRangeSqr) continue;

            bool isReserved = IsReserved(mob);
            bool isAtCapacity = IsAtCapacity(mob);

            if (isAtCapacity) continue;

            if (!isReserved)
            {
                if (sqrDist < closestUnreservedSqrDist)
                {
                    closestUnreservedSqrDist = sqrDist;
                    closestUnreserved = mob;
                }
            }
            else
            {
                if (sqrDist < closestReservedSqrDist)
                {
                    closestReservedSqrDist = sqrDist;
                    closestReserved = mob;
                }
            }
        }

        // Prefer unreserved
        MobAgent target = closestUnreserved ?? closestReserved;

        if (target != null)
        {
            ReserveTarget(target, requester);
            
            if (showDebugLogs)
            {
                string status = closestUnreserved != null ? "unreserved" : "shared";
                Debug.Log($"[MobManager] {requester.name} acquired {status} target");
            }
        }

        return target;
    }

    public void ReserveTarget(MobAgent mob, AdventurerAgent adventurer)
    {
        if (mob == null || adventurer == null) return;

        ReleaseTargetByAdventurer(adventurer);

        if (!reservations.ContainsKey(mob))
        {
            reservations[mob] = new List<AdventurerAgent>();
        }

        if (!reservations[mob].Contains(adventurer))
        {
            reservations[mob].Add(adventurer);
            mob.AddReservation(adventurer);

            if (showDebugLogs)
                Debug.Log($"[MobManager] {adventurer.name} reserved {mob.name} (total: {reservations[mob].Count})");
        }
    }

    public void ReleaseTarget(MobAgent mob, AdventurerAgent adventurer)
    {
        if (mob == null || adventurer == null) return;

        if (reservations.ContainsKey(mob))
        {
            reservations[mob].Remove(adventurer);
            mob.RemoveReservation(adventurer);

            if (showDebugLogs)
                Debug.Log($"[MobManager] Released {mob.name} from {adventurer.name}");

            if (reservations[mob].Count == 0)
            {
                reservations.Remove(mob);
            }
        }
    }

    public void ReleaseTargetByAdventurer(AdventurerAgent adventurer)
    {
        if (adventurer == null) return;

        var mobsToUpdate = new List<MobAgent>();

        foreach (var kvp in reservations)
        {
            if (kvp.Value.Contains(adventurer))
            {
                mobsToUpdate.Add(kvp.Key);
            }
        }

        foreach (var mob in mobsToUpdate)
        {
            ReleaseTarget(mob, adventurer);
        }
    }

    // ===== QUERIES =====

    public bool IsReserved(MobAgent mob)
    {
        return mob != null && reservations.ContainsKey(mob) && reservations[mob].Count > 0;
    }

    public bool IsReservedBy(MobAgent mob, AdventurerAgent adventurer)
    {
        if (mob == null || adventurer == null) return false;
        return reservations.ContainsKey(mob) && reservations[mob].Contains(adventurer);
    }

    public bool IsAtCapacity(MobAgent mob)
    {
        if (mob == null || !reservations.ContainsKey(mob)) return false;
        
        int maxAttackers = mob.GetMaxAttackers();
        if (maxAttackers <= 0) maxAttackers = defaultMaxAttackersPerMob;
        
        return reservations[mob].Count >= maxAttackers;
    }

    public int GetReservationCount(MobAgent mob)
    {
        if (mob == null || !reservations.ContainsKey(mob)) return 0;
        return reservations[mob].Count;
    }

    public List<MobAgent> GetMobsOnLayer(int layer)
    {
        if (mobsByLayer.ContainsKey(layer))
            return new List<MobAgent>(mobsByLayer[layer]);
        return new List<MobAgent>();
    }

    public int GetTotalMobCount()
    {
        int count = 0;
        foreach (var layer in mobsByLayer.Values)
        {
            count += layer.Count;
        }
        return count;
    }

    // ===== CLEANUP =====

    private void CleanupNullReferences()
    {
        foreach (var layer in mobsByLayer.Values)
        {
            layer.RemoveAll(m => m == null || m.gameObject == null);
        }

        foreach (var grid in spatialGridsByLayer.Values)
        {
            grid.Cleanup();
        }

        var keysToRemove = new List<MobAgent>();
        foreach (var kvp in reservations)
        {
            kvp.Value.RemoveAll(a => a == null || a.gameObject == null);

            if (kvp.Key == null || kvp.Key.gameObject == null || kvp.Value.Count == 0)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            if (key != null) key.ClearReservations();
            reservations.Remove(key);
        }
    }

    // ===== DEBUG =====

    [ContextMenu("Debug: Print Stats")]
    private void DebugPrintStats()
    {
        int totalMobs = GetTotalMobCount();
        int reservedMobs = reservations.Count;
        int totalReservations = 0;
        
        foreach (var list in reservations.Values)
        {
            totalReservations += list.Count;
        }

        Debug.Log("=== MobManager Stats ===");
        Debug.Log($"Total Mobs: {totalMobs}");
        Debug.Log($"Reserved Mobs: {reservedMobs}");
        Debug.Log($"Total Reservations: {totalReservations}");
        Debug.Log($"Avg Attackers/Mob: {(reservedMobs > 0 ? (float)totalReservations / reservedMobs : 0):F2}");
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying) return;

        foreach (var kvp in spatialGridsByLayer)
        {
            kvp.Value.DebugDraw(new Color(1f, 0f, 0f, 0.3f));
        }
    }
#endif
}