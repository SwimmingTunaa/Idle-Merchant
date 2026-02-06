using UnityEngine;
using System.Collections.Generic;
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
        // Don't call base - porters work on any layer, skip layer validation
        if (operationArea == null)
        {
            if (showDebugLogs)
                Debug.LogError($"[PorterManager Layer {LayerIndex}] operationArea is not assigned!");
        }

        if (transport == null)
        {
            if (showDebugLogs)
                Debug.LogError($"[PorterManager Layer {LayerIndex}] transport is not assigned!");
        }
        
        if (depositPoint == null)
        {
            if (showDebugLogs)
                Debug.LogError($"[PorterManager Layer {LayerIndex}] depositPoint is not assigned!");
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
        porter.Init(porterDef, LayerIndex, spawner: null, operationArea);
        
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
            Debug.LogWarning($"[PorterManager Layer {LayerIndex}] No transport assigned!");
            return false;
        }
        
        bool success = transport.TryUpgrade();
        
        if (success && showDebugLogs)
        {
            Debug.Log($"[PorterManager Layer {LayerIndex}] Upgraded transport to {transport.transportType}");
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
    /// Hire from HiringCandidate.
    /// Applies identity and traits from candidate data.
    /// </summary>
    

    /// <summary>
    /// Spawn unit without applying random traits.
    /// Used by HireUnit(HiringCandidate) to apply candidate's traits instead.
    /// </summary>
    private PorterAgent SpawnUnitWithoutTraits(EntityDef def)
    {
        if (operationArea == null)
        {
            Debug.LogError("[PorterManager] Cannot spawn - operationArea not set!");
            return null;
        }

        PorterDef porterDef = def as PorterDef;
        if (porterDef == null)
        {
            Debug.LogError($"[PorterManager] {def.displayName} is not a PorterDef!");
            return null;
        }

        // Spawn using object pool
        PorterAgent agent = ObjectPoolManager.Instance.SpawnObject<PorterAgent>(
            porterDef,
            Spawner.GetRandomPointAboveSurface(operationArea),
            Quaternion.identity
        );

        if (agent == null)
        {
            Debug.LogError($"[PorterManager] ObjectPoolManager failed to spawn {def.displayName}!");
            return null;
        }

        // Initialize porter (without adding random traits)
        agent.Init(porterDef, LayerIndex, spawner: null, operationArea);
        
        // DON'T call random trait assignment here
        // Candidate will provide traits
        
        return agent;
    }

    /// <summary>
    /// Spawn porter from HiringCandidate with identity and traits.
    /// </summary>
    protected override PorterAgent SpawnUnitWithCandidate(HiringCandidate candidate)
    {
        if (operationArea == null)
        {
            Debug.LogError("[PorterManager] Cannot spawn - operationArea not set!");
            return null;
        }

        PorterDef porterDef = candidate.entityDef as PorterDef;
        if (porterDef == null)
        {
            Debug.LogError($"[PorterManager] {candidate.entityDef.displayName} is not a PorterDef!");
            return null;
        }

        // Spawn using object pool
        PorterAgent agent = ObjectPoolManager.Instance.SpawnObject<PorterAgent>(
            porterDef,
            Spawner.GetRandomPointAboveSurface(operationArea),
            Quaternion.identity
        );

        if (agent == null)
        {
            Debug.LogError($"[PorterManager] ObjectPoolManager failed to spawn {porterDef.displayName}!");
            return null;
        }

        // Initialize porter
        agent.Init(porterDef, LayerIndex, spawner: null, operationArea);
        
        // Apply identity
        var identityComponent = agent.GetComponent<IdentityComponent>();
        if (identityComponent != null && candidate.identity != null)
        {
            identityComponent.ApplyIdentity(candidate.identity);
        }
        else if (candidate.identity != null)
        {
            // Fallback: rename GameObject if no IdentityComponent
            agent.gameObject.name = candidate.identity.DisplayName;
        }
        
        // Apply candidate's traits
        if (agent.traitComponent != null)
        {
            // Clear any default traits that might have been assigned
            agent.traitComponent.ClearTraits();
            
            // Add candidate's specific traits
            if (candidate.traits != null)
            {
                foreach (var trait in candidate.traits)
                {
                    agent.traitComponent.AddTrait(trait);
                }
                
                // Apply trait modifiers to stats
                agent.traitComponent.ApplyTraitsToStats(agent.Stats);
            }
        }
          
        // Set transport and deposit point
        agent.SetTransport(transport, depositPoint.position);
        return agent;
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
            Debug.Log($"[PorterManager Layer {LayerIndex}] Transport upgraded to {transport.transportType}");
        }
    }
}