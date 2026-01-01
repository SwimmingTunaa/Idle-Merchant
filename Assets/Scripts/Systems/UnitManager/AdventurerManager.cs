using UnityEngine;

/// <summary>
/// Manages adventurers for a single dungeon layer.
/// Inherits from UnitManager to get spawning, tracking, and limit logic.
/// Only implements adventurer-specific spawn initialization.
/// </summary>
public class AdventurerManager : UnitManager<AdventurerAgent>
{
    // ===== SPAWNING (Adventurer-Specific) =====

    /// <summary>
    /// Spawn and initialize an adventurer.
    /// Called by base class HireUnit().
    /// </summary>
    protected override AdventurerAgent SpawnUnit(EntityDef def)
    {
        if (operationArea == null)
        {
            Debug.LogError("[AdventurerManager] Cannot spawn - operationArea not set!");
            return null;
        }

        AdventurerDef adventurerDef = def as AdventurerDef;
        if (adventurerDef == null)
        {
            Debug.LogError($"[AdventurerManager] {def.displayName} is not an AdventurerDef!");
            return null;
        }

        // Spawn using object pool
        AdventurerAgent agent = ObjectPoolManager.Instance.SpawnObject<AdventurerAgent>(
            adventurerDef,
            Spawner.GetRandomPointAboveSurface(operationArea),
            Quaternion.identity
        );

        if (agent == null)
        {
            Debug.LogError($"[AdventurerManager] ObjectPoolManager failed to spawn {def.displayName}!");
            return null;
        }

        // Initialize adventurer
        agent.Init(adventurerDef, layerIndex, spawner: null, operationArea);

        return agent;
    }

    // ===== LEGACY WRAPPERS (for backward compatibility) =====

    /// <summary>
    /// Wrapper for HireUnit() to maintain old API.
    /// Call HireUnit() directly in new code.
    /// </summary>
    public bool HireAdventurer(EntityDef def)
    {
        return HireUnit(def);
    }

    /// <summary>
    /// Wrapper for RemoveUnit() to maintain old API.
    /// Call RemoveUnit() directly in new code.
    /// </summary>
    public void RemoveAdventurer(AdventurerAgent agent)
    {
        RemoveUnit(agent);
    }

    /// <summary>
    /// Get all adventurers (wrapper for GetAllUnits).
    /// </summary>
    public System.Collections.Generic.List<AdventurerAgent> GetAllAdventurers()
    {
        return GetAllUnits();
    }
}