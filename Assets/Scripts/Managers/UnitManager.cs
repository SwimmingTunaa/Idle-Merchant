using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// OPTIMIZED: Periodic cleanup + uses enhanced Inventory API.
/// 
/// Changes from original:
/// 1. Cleanup runs every 2 seconds instead of every frame (99% reduction)
/// 2. Uses Inventory.CanAfford() for clearer validation
/// 3. Uses Inventory.TrySpendGold() for atomic gold deduction (safer)
/// 
/// Performance gain: 99% reduction in cleanup overhead
/// Code quality: More readable and less error-prone
/// </summary>
/// <typeparam name="T">Type of unit this manager handles (must inherit from EntityBase)</typeparam>
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
    
    [Header("Unit Type Limits")]
    [Tooltip("Define max count for each unit type on this layer")]
    public List<UnitTypeLimit> unitLimits = new List<UnitTypeLimit>();
    
    List<UnitTypeLimit> IUnitManager.UnitLimits => unitLimits;
    
    [Header("Debug")]
    [SerializeField] protected bool showDebugLogs = true;

    // Track spawned units by their definition
    protected Dictionary<EntityDef, List<T>> spawnedByType = new Dictionary<EntityDef, List<T>>();
    protected Vector3 spawnPoint;

    // OPTIMIZATION: Periodic cleanup instead of every frame
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
        // OPTIMIZED: Cleanup only every 2 seconds instead of every frame
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
        {
            Debug.LogError($"[{GetType().Name} Layer {LayerIndex}] operationArea is not assigned!");
        }
        
        if (unitLimits.Count == 0)
        {
            Debug.LogWarning($"[{GetType().Name} Layer {LayerIndex}] No unit limits defined!");
        }

        foreach (var limit in unitLimits)
        {
            if (limit.unitDef != null && limit.unitDef.assignedLayer != LayerIndex)
            {
                Debug.LogWarning($"[{GetType().Name} Layer {LayerIndex}] {limit.unitDef.displayName} is assigned to layer {limit.unitDef.assignedLayer}, not {LayerIndex}!");
            }
        }
    }

    // ===== HIRING SYSTEM =====

    /// <summary>
    /// Check if a specific unit type can be hired on this layer.
    /// Returns false if: wrong layer, no gold, or at type limit.
    /// Implements IUnitManager.CanHire
    /// </summary>
    public virtual bool CanHire(EntityDef def)
    {
        if (def == null)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[{GetType().Name}] CanHire called with null def");
            return false;
        }

        // Check layer assignment
        if (def.assignedLayer != LayerIndex)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[{GetType().Name}] {def.displayName} is for layer {def.assignedLayer}, this is layer {LayerIndex}");
            return false;
        }

        // IMPROVED: Use CanAfford() helper for cleaner code
        if (!Inventory.Instance.CanAfford(def.hireCost))
        {
            if (showDebugLogs)
                Debug.LogWarning($"[{GetType().Name}] Not enough gold to hire {def.displayName}. Need {def.hireCost}, have {Inventory.Instance.Gold}");
            return false;
        }

        // Check type limit
        int currentCount = GetUnitCount(def);
        int maxCount = GetUnitLimit(def);

        if (currentCount >= maxCount)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[{GetType().Name}] {def.displayName} at limit ({currentCount}/{maxCount})");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Hire and spawn a unit of the specified type.
    /// Deducts gold and adds to tracked units.
    /// Implements IUnitManager.HireUnit
    /// </summary>
    public virtual bool HireUnit(EntityDef def)
    {
        if (!CanHire(def))
            return false;

        // IMPROVED: Use TrySpendGold() for atomic deduction with validation
        // This is safer than AddGold(-cost) because it validates affordability
        if (!Inventory.Instance.TrySpendGold(def.hireCost))
        {
            Debug.LogError($"[{GetType().Name}] Failed to deduct gold for {def.displayName} - this shouldn't happen after CanHire()!");
            return false;
        }

        // Spawn the unit
        T unit = SpawnUnit(def);

        if (unit == null)
        {
            Debug.LogError($"[{GetType().Name}] Failed to spawn {def.displayName}!");
            Inventory.Instance.AddGold(def.hireCost); // Refund
            return false;
        }

        // Track the spawned unit
        if (!spawnedByType.ContainsKey(def))
        {
            spawnedByType[def] = new List<T>();
        }
        spawnedByType[def].Add(unit);

        if (showDebugLogs)
            Debug.Log($"[{GetType().Name}] Hired {def.displayName} on layer {LayerIndex}. Count: {GetUnitCount(def)}/{GetUnitLimit(def)}");

        return true;
    }
    
    /// <summary>
    /// Hire and spawn a unit from a HiringCandidate (with identity and traits).
    /// Uses candidate's modified hire cost and applies identity/traits to spawned unit.
    /// </summary>
    public virtual bool HireUnit(HiringCandidate candidate)
    {
        if (candidate.entityDef == null)
        {
            Debug.LogError($"[{GetType().Name}] Candidate has null entityDef");
            return false;
        }
        
        // Validate candidate belongs to this layer
        if (candidate.entityDef.assignedLayer != LayerIndex)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[{GetType().Name}] Candidate is for layer {candidate.entityDef.assignedLayer}, this is layer {LayerIndex}");
            return false;
        }
        
        // IMPROVED: Use CanAfford() helper
        if (!Inventory.Instance.CanAfford(candidate.hireCost))
        {
            if (showDebugLogs)
                Debug.LogWarning($"[{GetType().Name}] Not enough gold. Need {candidate.hireCost}, have {Inventory.Instance.Gold}");
            return false;
        }
        
        // Check capacity
        int currentCount = GetUnitCount(candidate.entityDef);
        int maxCount = GetUnitLimit(candidate.entityDef);
        
        if (currentCount >= maxCount)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[{GetType().Name}] {candidate.entityDef.displayName} at limit ({currentCount}/{maxCount})");
            return false;
        }
        
        // IMPROVED: Use TrySpendGold() for candidate's modified cost
        if (!Inventory.Instance.TrySpendGold(candidate.hireCost))
        {
            Debug.LogError($"[{GetType().Name}] Failed to deduct gold for {candidate.DisplayName}!");
            return false;
        }
        
        // Spawn unit with candidate data
        T unit = SpawnUnitWithCandidate(candidate);
        
        if (unit == null)
        {
            Debug.LogError($"[{GetType().Name}] Failed to spawn {candidate.DisplayName}!");
            Inventory.Instance.AddGold(candidate.hireCost); // Refund
            return false;
        }
        
        // Track the spawned unit
        if (!spawnedByType.ContainsKey(candidate.entityDef))
        {
            spawnedByType[candidate.entityDef] = new List<T>();
        }
        spawnedByType[candidate.entityDef].Add(unit);
        
        if (showDebugLogs)
            Debug.Log($"[{GetType().Name}] Hired {candidate.DisplayName} with {candidate.traits?.Length ?? 0} traits on layer {LayerIndex}");
        
        return true;
    }

    /// <summary>
    /// Remove a specific unit (for testing/debug).
    /// In production, units are usually permanent.
    /// </summary>
    public virtual void RemoveUnit(T unit)
    {
        if (unit == null) return;

        // Find which type list contains this unit
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

    // ===== SPAWNING (Abstract - derived classes implement) =====

    /// <summary>
    /// Spawn and initialize a unit of the specified type.
    /// Derived classes implement type-specific initialization (AdventurerAgent.Init vs PorterAgent.Init).
    /// </summary>
    protected abstract T SpawnUnit(EntityDef def);
    
    /// <summary>
    /// Spawn and initialize a unit from a HiringCandidate.
    /// Applies identity and traits from candidate to the spawned unit.
    /// Derived classes implement type-specific initialization + candidate data application.
    /// </summary>
    protected abstract T SpawnUnitWithCandidate(HiringCandidate candidate);

    // ===== QUERIES (Implements IUnitManager interface) =====

    /// <summary>
    /// Get current count of a specific unit type.
    /// Implements IUnitManager.GetUnitCount
    /// </summary>
    public int GetUnitCount(EntityDef def)
    {
        if (def == null || !spawnedByType.ContainsKey(def))
            return 0;

        // Clean nulls before counting
        spawnedByType[def].RemoveAll(u => u == null);
        return spawnedByType[def].Count;
    }

    /// <summary>
    /// Get max allowed count for a specific unit type.
    /// Implements IUnitManager.GetUnitLimit
    /// </summary>
    public int GetUnitLimit(EntityDef def)
    {
        if (def == null) return 0;

        foreach (var limit in unitLimits)
        {
            if (limit.unitDef == def)
                return limit.maxCount;
        }

        // Not found in limits - assume 0 (can't hire)
        return 0;
    }

    /// <summary>
    /// Check if a specific unit type is at its limit.
    /// Implements IUnitManager.IsTypeFull
    /// </summary>
    public bool IsTypeFull(EntityDef def)
    {
        return GetUnitCount(def) >= GetUnitLimit(def);
    }

    /// <summary>
    /// Get all spawned units of a specific type
    /// </summary>
    public List<T> GetUnitsOfType(EntityDef def)
    {
        if (def == null || !spawnedByType.ContainsKey(def))
            return new List<T>();

        // Clean nulls and return copy
        spawnedByType[def].RemoveAll(u => u == null);
        return new List<T>(spawnedByType[def]);
    }

    /// <summary>
    /// Get all active units across all types
    /// </summary>
    public List<T> GetAllUnits()
    {
        var result = new List<T>();
        
        foreach (var list in spawnedByType.Values)
        {
            list.RemoveAll(u => u == null);
            result.AddRange(list);
        }

        return result;
    }

    /// <summary>
    /// Get total count of all units on this layer
    /// </summary>
    public int GetTotalCount()
    {
        int total = 0;
        foreach (var list in spawnedByType.Values)
        {
            list.RemoveAll(u => u == null);
            total += list.Count;
        }
        return total;
    }

    /// <summary>
    /// Get total maximum capacity across all unit types
    /// </summary>
    public int GetTotalCapacity()
    {
        int total = 0;
        foreach (var limit in unitLimits)
        {
            total += limit.maxCount;
        }
        return total;
    }

    /// <summary>
    /// Check if all unit slots are filled
    /// </summary>
    public bool IsFull()
    {
        return GetTotalCount() >= GetTotalCapacity();
    }

    // ===== CLEANUP =====

    /// <summary>
    /// OPTIMIZED: Only runs every 2 seconds instead of every frame.
    /// Removes null references from spawned units tracking.
    /// </summary>
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
        Debug.Log($"=== {GetType().Name} Layer {LayerIndex} Unit Counts ===");
        
        foreach (var limit in unitLimits)
        {
            if (limit.unitDef == null) continue;
            
            int current = GetUnitCount(limit.unitDef);
            int max = limit.maxCount;
            string status = current >= max ? "FULL" : "AVAILABLE";
            
            Debug.Log($"{limit.unitDef.displayName}: {current}/{max} {status}");
        }

        Debug.Log($"Total: {GetTotalCount()}/{GetTotalCapacity()}");
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
            // Draw operation area
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
            Gizmos.DrawCube(operationArea.bounds.center, operationArea.bounds.size);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(operationArea.bounds.center, operationArea.bounds.size);

            // Draw label
            UnityEditor.Handles.Label(
                operationArea.bounds.center + Vector3.up * (operationArea.bounds.extents.y + 0.5f),
                $"{GetType().Name}\nLayer {LayerIndex}\n{GetTotalCount()}/{GetTotalCapacity()}"
            );
        }
    }
#endif
}

/// <summary>
/// Defines the maximum count allowed for a specific unit type on a layer.
/// Used by all managers (AdventurerManager, PorterManager, etc.).
/// </summary>
[System.Serializable]
public class UnitTypeLimit
{
    [Tooltip("The unit type (Miner, Soldier, Porter, etc.)")]
    public EntityDef unitDef;

    [Tooltip("Maximum number of this unit type allowed on this layer")]
    public int maxCount = 10;
}