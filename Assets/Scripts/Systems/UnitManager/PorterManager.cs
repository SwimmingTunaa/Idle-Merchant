using UnityEngine;

/// <summary>
/// Manages porters for a single dungeon layer.
/// Inherits from UnitManager to get spawning, tracking, and limit logic.
/// Only implements porter-specific spawn initialization and transport setup.
/// </summary>
public class PorterManager : UnitManager<PorterAgent>
{
    [Header("Porter-Specific Config")]
    [Tooltip("Transport point porters use to reach shop (Ladder/Elevator/Teleporter)")]
    public TransportPoint transport;
    
    [Tooltip("Where porters deposit loot at shop")]
    public Transform depositPoint;

    // ===== VALIDATION =====

    protected override void ValidateSetup()
    {
        base.ValidateSetup();

        if (transport == null)
        {
            Debug.LogError($"[PorterManager Layer {layerIndex}] transport is not assigned!");
        }
        
        if (depositPoint == null)
        {
            Debug.LogError($"[PorterManager Layer {layerIndex}] depositPoint is not assigned!");
        }
    }

    // ===== SPAWNING (Porter-Specific) =====

    /// <summary>
    /// Spawn and initialize a porter.
    /// Called by base class HireUnit().
    /// </summary>
    protected override PorterAgent SpawnUnit(EntityDef def)
    {
        if (operationArea == null)
        {
            Debug.LogError("[PorterManager] Cannot spawn - operationArea not set!");
            return null;
        }

        if (transport == null || depositPoint == null)
        {
            Debug.LogError("[PorterManager] Cannot spawn - transport or depositPoint not set!");
            return null;
        }

        PorterDef porterDef = def as PorterDef;
        if (porterDef == null)
        {
            Debug.LogError($"[PorterManager] {def.displayName} is not a PorterDef!");
            return null;
        }

        // Spawn using object pool
        PorterAgent porter = ObjectPoolManager.Instance.SpawnObject<PorterAgent>(
            porterDef,
            Spawner.GetRandomPointAboveSurface(operationArea),
            Quaternion.identity
        );

        if (porter == null)
        {
            Debug.LogError($"[PorterManager] ObjectPoolManager failed to spawn {def.displayName}!");
            return null;
        }

        // Initialize porter (spawner is null for hired porters)
        porter.Init(porterDef, layerIndex, spawner: null, operationArea);
        
        // Set transport and deposit point
        porter.SetTransport(transport, depositPoint.position);

        return porter;
    }

    // ===== TRANSPORT UPGRADE =====

    /// <summary>
    /// Upgrade the transport for this layer (Ladder → Elevator → Teleporter).
    /// Assumes gold has already been deducted by the UI.
    /// </summary>
    public bool UpgradeTransport()
    {
        if (transport == null)
        {
            Debug.LogWarning($"[PorterManager Layer {layerIndex}] No transport assigned!");
            return false;
        }
        
        bool success = transport.TryUpgrade();
        
        if (success && showDebugLogs)
        {
            Debug.Log($"[PorterManager Layer {layerIndex}] Upgraded transport to {transport.transportType}");
        }
        
        return success;
    }

    // ===== LEGACY WRAPPERS (for backward compatibility) =====

    /// <summary>
    /// Wrapper for HireUnit() to maintain old API.
    /// Call HireUnit() directly in new code.
    /// </summary>
    public bool HirePorter(PorterDef def)
    {
        return HireUnit(def);
    }

    /// <summary>
    /// Wrapper for RemoveUnit() to maintain old API.
    /// Call RemoveUnit() directly in new code.
    /// </summary>
    public void RemovePorter(PorterAgent agent)
    {
        RemoveUnit(agent);
    }

    /// <summary>
    /// Get all active porters (wrapper for GetAllUnits).
    /// </summary>
    public System.Collections.Generic.List<PorterAgent> GetActivePorters()
    {
        return GetAllUnits();
    }

    /// <summary>
    /// Get available slots (legacy - kept for compatibility).
    /// </summary>
    public int GetAvailableSlots()
    {
        return GetTotalCapacity() - GetTotalCount();
    }

    // ===== DEBUG =====

    [ContextMenu("Debug: Upgrade Transport")]
    private void DebugUpgradeTransport()
    {
        if (UpgradeTransport())
        {
            Debug.Log($"[PorterManager Layer {layerIndex}] Transport upgraded to {transport.transportType}");
        }
    }
}