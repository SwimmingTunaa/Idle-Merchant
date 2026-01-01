using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic base class for managing units on a layer.
/// Handles spawning, tracking, and per-type limits.
/// Derived classes (AdventurerManager, PorterManager) only need to implement type-specific initialization.
/// </summary>
/// <typeparam name="T">Type of unit this manager handles (must inherit from EntityBase)</typeparam>
public abstract class UnitManager<T> : MonoBehaviour, IUnitManager where T : EntityBase
{
    [Header("Layer Configuration")]
    [Tooltip("Which dungeon layer this manager controls (1-10)")]
    [SerializeField] private int _layerIndex = 1;
    
    // Property to implement IUnitManager interface
    public int layerIndex 
    { 
        get => _layerIndex; 
        set => _layerIndex = value; 
    }
    
    [Tooltip("Area where units patrol/operate")]
    public BoxCollider2D operationArea;
    
    [Header("Unit Type Limits")]
    [Tooltip("Define max count for each unit type on this layer")]
    public List<UnitTypeLimit> unitLimits = new List<UnitTypeLimit>();
    
    [Header("Debug")]
    [SerializeField] protected bool showDebugLogs = true;

    // Track spawned units by their definition
    protected Dictionary<EntityDef, List<T>> spawnedByType = new Dictionary<EntityDef, List<T>>();
    protected Vector3 spawnPoint;

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
        // Clean up null references periodically
        CleanupNullReferences();
    }

    // ===== VALIDATION =====

    protected virtual void ValidateSetup()
    {
        if (operationArea == null)
        {
            Debug.LogError($"[{GetType().Name} Layer {layerIndex}] operationArea is not assigned!");
        }
        
        if (unitLimits.Count == 0)
        {
            Debug.LogWarning($"[{GetType().Name} Layer {layerIndex}] No unit limits defined!");
        }

        // Validate all unit defs are assigned to this layer
        foreach (var limit in unitLimits)
        {
            if (limit.unitDef != null && limit.unitDef.assignedLayer != layerIndex)
            {
                Debug.LogWarning($"[{GetType().Name} Layer {layerIndex}] {limit.unitDef.displayName} is assigned to layer {limit.unitDef.assignedLayer}, not {layerIndex}!");
            }
        }
    }

    // ===== HIRING SYSTEM =====

    /// <summary>
    /// Check if a specific unit type can be hired on this layer.
    /// Returns false if: wrong layer, no gold, or at type limit.
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
        if (def.assignedLayer != layerIndex)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[{GetType().Name}] {def.displayName} is for layer {def.assignedLayer}, this is layer {layerIndex}");
            return false;
        }

        // Check gold
        if (Inventory.Instance.Gold < def.hireCost)
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
    /// </summary>
    public virtual bool HireUnit(EntityDef def)
    {
        if (!CanHire(def))
            return false;

        // Deduct gold
        Inventory.Instance.AddGold(-def.hireCost);

        // Spawn the unit
        T unit = SpawnUnit(def);

        if (unit == null)
        {
            Debug.LogError($"[{GetType().Name}] Failed to spawn {def.displayName}!");
            // Refund gold
            Inventory.Instance.AddGold(def.hireCost);
            return false;
        }

        // Track the spawned unit
        if (!spawnedByType.ContainsKey(def))
        {
            spawnedByType[def] = new List<T>();
        }
        spawnedByType[def].Add(unit);

        if (showDebugLogs)
            Debug.Log($"[{GetType().Name}] Hired {def.displayName} on layer {layerIndex}. Count: {GetUnitCount(def)}/{GetUnitLimit(def)}");

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
                    Debug.Log($"[{GetType().Name}] Removed {kvp.Key.displayName} from layer {layerIndex}");
                
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

    // ===== QUERIES =====

    /// <summary>
    /// Get current count of a specific unit type
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
    /// Get max allowed count for a specific unit type
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
    /// Check if a specific unit type is at its limit
    /// </summary>
    public bool IsTypeFull(EntityDef def)
    {
        return GetUnitCount(def) >= GetUnitLimit(def);
    }

    /// <summary>
    /// Check if all unit slots are filled
    /// </summary>
    public bool IsFull()
    {
        return GetTotalCount() >= GetTotalCapacity();
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
        Debug.Log($"=== {GetType().Name} Layer {layerIndex} Unit Counts ===");
        
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
        Debug.Log($"[{GetType().Name}] Removed all units from layer {layerIndex}");
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
                $"{GetType().Name}\nLayer {layerIndex}\n{GetTotalCount()}/{GetTotalCapacity()}"
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