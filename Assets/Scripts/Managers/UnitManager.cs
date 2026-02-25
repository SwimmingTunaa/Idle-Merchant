using System.Collections.Generic;
using UnityEngine;

// Base manager for all unit types (Adventurers, Porters, etc.).
// Handles hiring, spawning, tracking, and capacity management per layer.
// Uses a flat maxUnits cap for total layer capacity, with optional per-type limits via unitLimits.
public abstract class UnitManager<T> : MonoBehaviour, IUnitManager where T : EntityBase
{
    [Header("Layer Configuration")]
    [Tooltip("Which dungeon layer this manager controls (1-10)")]
    [SerializeField] private int _layerIndex = 1;
    
    public int LayerIndex 
    { 
        get => _layerIndex; 
        set => _layerIndex = value; 
    }
    
    [Tooltip("Area where units patrol/operate")]
    public BoxCollider2D operationArea;

    [Header("Capacity")]
    [Tooltip("Maximum total units allowed on this layer (all types combined)")]
    [SerializeField] private int _maxUnits = 10;

    public int MaxUnits
    {
        get => _maxUnits;
        set => _maxUnits = value;
    }
    
    [Header("Unit Type Limits (Optional)")]
    [Tooltip("Optional per-type caps. Types not listed are only limited by maxUnits.")]
    public List<UnitTypeLimit> unitLimits = new List<UnitTypeLimit>();
    
    List<UnitTypeLimit> IUnitManager.UnitLimits => unitLimits;

    [Header("Hireable Unit Types")]
    [Tooltip("Which unit defs appear as candidates in the hiring panel for this layer")]
    [SerializeField] private List<EntityDef> hireableDefs = new List<EntityDef>();

    List<EntityDef> IUnitManager.HireableDefs => hireableDefs;

    [Header("Debug")]
    [SerializeField] protected bool showDebugLogs = false;

    // Track spawned units by their definition
    protected Dictionary<EntityDef, List<T>> spawnedByType = new Dictionary<EntityDef, List<T>>();
    protected Vector3 spawnPoint;

    // Periodic cleanup instead of every frame
    private float cleanupTimer = 0f;
    private const float CLEANUP_INTERVAL = 2f;

    protected virtual void Awake()
    {
        spawnPoint = operationArea != null ? Spawner.GetRandomPointAboveSurface(operationArea) : transform.position;
    }

    protected virtual void Start()
    {
        ValidateSetup();
    }

    protected virtual void Update()
    {
        cleanupTimer += Time.deltaTime;
        if (cleanupTimer >= CLEANUP_INTERVAL)
        {
            cleanupTimer = 0f;
            CleanupNullReferences();
        }
    }

    // ===== VALIDATION =====

    protected virtual void ValidateSetup()
    {
        if (operationArea == null)
            Debug.LogError($"[{GetType().Name} Layer {LayerIndex}] operationArea is not assigned!");

        if (_maxUnits <= 0)
            Debug.LogWarning($"[{GetType().Name} Layer {LayerIndex}] maxUnits is {_maxUnits} — no units can be hired!");

        if (hireableDefs.Count == 0)
            Debug.LogWarning($"[{GetType().Name} Layer {LayerIndex}] No hireable defs defined — no candidates will appear in hiring panel.");
    }

    // ===== HIRING SYSTEM =====

    // Check if a unit can be hired on this layer.
    // Checks: gold, total capacity, optional per-type limit.
    public virtual bool CanHire(EntityDef def)
    {
        if (def == null)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[{GetType().Name}] CanHire called with null def");
            return false;
        }

        if (!Inventory.Instance.CanAfford(def.hireCost))
        {
            if (showDebugLogs)
                Debug.LogWarning($"[{GetType().Name}] Not enough gold to hire {def.displayName}. Need {def.hireCost}, have {Inventory.Instance.Gold}");
            return false;
        }

        // Check total layer capacity
        if (GetTotalCount() >= _maxUnits)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[{GetType().Name}] Layer {LayerIndex} full ({GetTotalCount()}/{_maxUnits})");
            return false;
        }

        // Check optional per-type limit (only if def exists in unitLimits)
        int typeLimit = GetUnitLimit(def);
        if (typeLimit >= 0 && GetUnitCount(def) >= typeLimit)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[{GetType().Name}] {def.displayName} at type limit ({GetUnitCount(def)}/{typeLimit})");
            return false;
        }

        return true;
    }

    // Hire and spawn a unit. Deducts gold and tracks the unit.
    public virtual bool HireUnit(EntityDef def)
    {
        if (!CanHire(def))
            return false;

        if (!Inventory.Instance.TrySpendGold(def.hireCost))
        {
            Debug.LogError($"[{GetType().Name}] Failed to deduct gold for {def.displayName} - this shouldn't happen after CanHire()!");
            return false;
        }

        T unit = SpawnUnit(def);

        if (unit == null)
        {
            Debug.LogError($"[{GetType().Name}] Failed to spawn {def.displayName}!");
            Inventory.Instance.AddGold(def.hireCost);
            return false;
        }

        if (!spawnedByType.ContainsKey(def))
            spawnedByType[def] = new List<T>();
        spawnedByType[def].Add(unit);

        if (showDebugLogs)
            Debug.Log($"[{GetType().Name}] Hired {def.displayName} on layer {LayerIndex}. Total: {GetTotalCount()}/{_maxUnits}");

        return true;
    }
    
    // Check if a candidate can be hired (uses candidate's modified cost).
    public virtual bool CanHire(HiringCandidate candidate)
    {
        if (candidate.entityDef == null)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[{GetType().Name}] CanHire called with null entityDef");
            return false;
        }

        if (!Inventory.Instance.CanAfford(candidate.hireCost))
        {
            if (showDebugLogs)
                Debug.LogWarning($"[{GetType().Name}] Not enough gold. Need {candidate.hireCost}, have {Inventory.Instance.Gold}");
            return false;
        }

        if (GetTotalCount() >= _maxUnits)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[{GetType().Name}] Layer {LayerIndex} full ({GetTotalCount()}/{_maxUnits})");
            return false;
        }

        int typeLimit = GetUnitLimit(candidate.entityDef);
        if (typeLimit >= 0 && GetUnitCount(candidate.entityDef) >= typeLimit)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[{GetType().Name}] {candidate.entityDef.displayName} at type limit ({GetUnitCount(candidate.entityDef)}/{typeLimit})");
            return false;
        }

        return true;
    }

    // Hire from a HiringCandidate with identity and traits.
    // Uses candidate's modified hire cost. Rank is decoupled from deployment layer.
    public virtual bool HireUnit(HiringCandidate candidate)
    {
        if (!CanHire(candidate))
            return false;

        if (!Inventory.Instance.TrySpendGold(candidate.hireCost))
        {
            Debug.LogError($"[{GetType().Name}] Failed to deduct gold for {candidate.DisplayName}!");
            return false;
        }

        T unit = SpawnUnitWithCandidate(candidate);

        if (unit == null)
        {
            Debug.LogError($"[{GetType().Name}] Failed to spawn {candidate.DisplayName}!");
            Inventory.Instance.AddGold(candidate.hireCost);
            return false;
        }

        if (!spawnedByType.ContainsKey(candidate.entityDef))
            spawnedByType[candidate.entityDef] = new List<T>();
        spawnedByType[candidate.entityDef].Add(unit);

        if (showDebugLogs)
            Debug.Log($"[{GetType().Name}] Hired {candidate.DisplayName} (Rank {candidate.entityDef.rank}) on layer {LayerIndex}. Total: {GetTotalCount()}/{_maxUnits}");

        return true;
    }

#if UNITY_EDITOR
    // Debug-only hire: bypasses gold cost and capacity checks.
    // Generates a random HiringCandidate from the given def and calls SpawnUnitWithCandidate directly.
    public bool DebugHireUnit(EntityDef def, float traitChance = 0.7f)
    {
        if (def == null) return false;

        var candidates = HiringCandidateGenerator.GenerateCandidates(def, 1, 0, traitChance);
        if (candidates == null || candidates.Length == 0)
        {
            Debug.LogError($"[{GetType().Name}] DebugHireUnit: failed to generate candidate for {def.displayName}");
            return false;
        }

        var candidate = candidates[0];
        T unit = SpawnUnitWithCandidate(candidate);

        if (unit == null)
        {
            Debug.LogError($"[{GetType().Name}] DebugHireUnit: SpawnUnitWithCandidate failed for {def.displayName}");
            return false;
        }

        if (!spawnedByType.ContainsKey(def))
            spawnedByType[def] = new List<T>();
        spawnedByType[def].Add(unit);

        Debug.Log($"[{GetType().Name}] DebugHireUnit: Spawned {candidate.DisplayName} on layer {LayerIndex}");
        return true;
    }
#endif

    // Remove a specific unit.
    public virtual void RemoveUnit(T unit)
    {
        if (unit == null) return;

        foreach (var kvp in spawnedByType)
        {
            if (kvp.Value.Contains(unit))
            {
                kvp.Value.Remove(unit);
                
                if (showDebugLogs)
                    Debug.Log($"[{GetType().Name}] Removed {kvp.Key.displayName} from layer {LayerIndex}");
                
                break;
            }
        }

        unit.Despawn();
    }

    // ===== SPAWNING (Abstract) =====

    protected abstract T SpawnUnit(EntityDef def);
    protected abstract T SpawnUnitWithCandidate(HiringCandidate candidate);

    // ===== QUERIES =====

    // Get current count of a specific unit type.
    public int GetUnitCount(EntityDef def)
    {
        if (def == null || !spawnedByType.ContainsKey(def))
            return 0;

        return spawnedByType[def].Count;
    }

    // Get per-type limit for a specific unit type.
    // Returns -1 if not found in unitLimits (uncapped for this type, only total cap applies).
    public int GetUnitLimit(EntityDef def)
    {
        if (def == null) return -1;

        foreach (var limit in unitLimits)
        {
            if (limit.unitDef == def)
                return limit.maxCount;
        }

        return -1;
    }

    // Check if a specific unit type is at its per-type limit.
    public bool IsTypeFull(EntityDef def)
    {
        int limit = GetUnitLimit(def);
        if (limit < 0) return false; // No per-type limit
        return GetUnitCount(def) >= limit;
    }

    // Get all spawned units of a specific type.
    public List<T> GetUnitsOfType(EntityDef def)
    {
        if (def == null || !spawnedByType.ContainsKey(def))
            return new List<T>();

        return new List<T>(spawnedByType[def]);
    }

    // Get all active units across all types.
    public List<T> GetAllUnits()
    {
        var result = new List<T>();
        foreach (var list in spawnedByType.Values)
            result.AddRange(list);
        return result;
    }

    // Get total count of all units on this layer.
    public int GetTotalCount()
    {
        int total = 0;
        foreach (var list in spawnedByType.Values)
            total += list.Count;
        return total;
    }

    // Get total maximum capacity for this layer.
    public int GetTotalCapacity()
    {
        return _maxUnits;
    }

    // Check if layer is at total capacity.
    public bool IsFull()
    {
        return GetTotalCount() >= _maxUnits;
    }

    // ===== CLEANUP =====

    protected virtual void CleanupNullReferences()
    {
        foreach (var list in spawnedByType.Values)
        {
            list.RemoveAll(u => u == null || u.gameObject == null);
        }
    }

    // ===== DEBUG =====

    [ContextMenu("Debug: Print Unit Counts")]
    protected virtual void DebugPrintUnitCounts()
    {
        Debug.Log($"=== {GetType().Name} Layer {LayerIndex} ===");
        Debug.Log($"Total: {GetTotalCount()}/{_maxUnits}");
        
        foreach (var kvp in spawnedByType)
        {
            if (kvp.Key == null) continue;
            kvp.Value.RemoveAll(u => u == null);
            
            int typeLimit = GetUnitLimit(kvp.Key);
            string limitStr = typeLimit >= 0 ? $"/{typeLimit}" : "";
            Debug.Log($"  {kvp.Key.displayName}: {kvp.Value.Count}{limitStr}");
        }
    }

    [ContextMenu("Debug: Remove All Units")]
    protected virtual void DebugRemoveAll()
    {
        var allUnits = GetAllUnits();
        foreach (var unit in allUnits)
        {
            RemoveUnit(unit);
        }
        Debug.Log($"[{GetType().Name}] Removed all units from layer {LayerIndex}");
    }

    [ContextMenu("Debug: Force Cleanup")]
    protected virtual void DebugForceCleanup()
    {
        CleanupNullReferences();
        Debug.Log($"[{GetType().Name}] Forced cleanup complete");
    }

#if UNITY_EDITOR
    protected virtual void OnDrawGizmos()
    {
        if (operationArea != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
            Gizmos.DrawCube(operationArea.bounds.center, operationArea.bounds.size);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(operationArea.bounds.center, operationArea.bounds.size);

            UnityEditor.Handles.Label(
                operationArea.bounds.center + Vector3.up * (operationArea.bounds.extents.y + 0.5f),
                $"{GetType().Name}\nLayer {LayerIndex}\n{GetTotalCount()}/{_maxUnits}"
            );
        }
    }
#endif
}

// Optional per-type cap. Types not listed in a manager's unitLimits are only limited by maxUnits.
[System.Serializable]
public class UnitTypeLimit
{
    [Tooltip("The unit type (Miner, Soldier, Porter, etc.)")]
    public EntityDef unitDef;

    [Tooltip("Maximum number of this unit type allowed on this layer")]
    public int maxCount = 10;
}