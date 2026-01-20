using UnityEngine;
using System.Collections.Generic;
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
        agent.Init(adventurerDef, LayerIndex, spawner: null, operationArea);

        //Add random traits to adventurer
        //TraitManager.Instance.AssignRandomAdventurerTraitTier1(agent);

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
    /// Hire from HiringCandidate.
    /// Applies identity and traits from candidate data.
    /// </summary>
    

    protected override AdventurerAgent SpawnUnitWithCandidate(HiringCandidate candidate)
    {
        if (operationArea == null)
        {
            Debug.LogError("[AdventurerManager] Cannot spawn - operationArea not set!");
            return null;
        }

        AdventurerDef advDef = candidate.entityDef as AdventurerDef;
        if (advDef == null)
        {
            Debug.LogError($"[AdventurerManager] {candidate.entityDef.displayName} is not an AdventurerDef!");
            return null;
        }

        // Spawn using object pool
        AdventurerAgent agent = ObjectPoolManager.Instance.SpawnObject<AdventurerAgent>(
            advDef,
            Spawner.GetRandomPointAboveSurface(operationArea),
            Quaternion.identity
        );

        if (agent == null)
        {
            Debug.LogError($"[AdventurerManager] ObjectPoolManager failed to spawn {advDef.displayName}!");
            return null;
        }

       

        // Initialize adventurer
        agent.Init(advDef, LayerIndex, spawner: null, operationArea);

         //apply appearance from candidate
        if (agent.appearanceManager != null && advDef.useModularCharacter)
        {
            agent.appearanceManager.SetEntityDef(advDef);
            agent.appearanceManager.SetAppearanceIndices(candidate.appearanceIndices);
            agent.appearanceManager.ApplyAppearance();
        }
        
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

    
        return agent;
    }

    /// <summary>
    /// Spawn unit without applying random traits.
    /// Used by HireUnit(HiringCandidate) to apply candidate's traits instead.
    /// </summary>
    private AdventurerAgent SpawnUnitWithoutTraits(EntityDef def)
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

        // Initialize adventurer (without adding random traits)
        agent.Init(adventurerDef, LayerIndex, spawner: null, operationArea);
        
        // DON'T call TraitManager.AssignRandomAdventurerTraitTier1(agent);
        // Candidate will provide traits
        
        return agent;
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