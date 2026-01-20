using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Component that holds trait instances on an entity.
/// Applies trait modifiers to Stats system.
/// </summary>
public class TraitComponent : MonoBehaviour
{
    [Header("Traits")]
    [SerializeField] private List<TraitInstance> traits = new List<TraitInstance>();
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    
    private List<PassiveStatBoostEffect> activeModifiers = new List<PassiveStatBoostEffect>();
    
    /// <summary>
    /// Add a trait to this entity.
    /// Max 2 traits per entity (Phase 1 rule).
    /// </summary>
    public bool AddTrait(TraitInstance trait)
    {
        if (traits.Count >= 2)
        {
            Debug.LogWarning($"[TraitComponent] Cannot add more than 2 traits to {gameObject.name}");
            return false;
        }
        
        // Check if trait exists
        var traitDef = TraitDatabase.GetTrait(trait.traitId);
        if (traitDef == null)
        {
            Debug.LogError($"[TraitComponent] Trait not found: {trait.traitId}");
            return false;
        }
        
        // Check compatibility with existing traits
        if (!TraitCompatibilityChecker.CanAddTrait(traitDef, traits))
        {
            Debug.LogWarning($"[TraitComponent] Trait {trait.traitId} conflicts with existing traits");
            return false;
        }
        
        traits.Add(trait);
        
        if (showDebugLogs)
        {
            Debug.Log($"[TraitComponent] Added trait {traitDef.displayName} (Tier {trait.tier}) to {gameObject.name}");
        }
        
        return true;
    }
    
    /// <summary>
    /// Apply all traits to the entity's Stats system.
    /// Called during entity initialization.
    /// </summary>
    public void ApplyTraitsToStats(Stats stats)
    {
        if (stats == null)
        {
            Debug.LogError($"[TraitComponent] Stats is null on {gameObject.name}");
            return;
        }
        
        // Clear old modifiers
        ClearModifiers(stats);
        
        // Apply each trait
        foreach (var trait in traits)
        {
            ApplyTrait(stats, trait);
        }
    }
    
    private void ApplyTrait(Stats stats, TraitInstance trait)
    {
        var traitDef = TraitDatabase.GetTrait(trait.traitId);
        if (traitDef == null)
        {
            Debug.LogError($"[TraitComponent] Trait not found: {trait.traitId}");
            return;
        }
        
        // Validate tier
        if (trait.tier < 1 || trait.tier > traitDef.tiers.Length)
        {
            Debug.LogError($"[TraitComponent] Invalid tier {trait.tier} for trait {trait.traitId}");
            return;
        }
        
        var tierData = traitDef.tiers[trait.tier - 1];
        
        if (tierData.modifiers == null || tierData.modifiers.Length == 0)
        {
            Debug.LogWarning($"[TraitComponent] No modifiers for {trait.traitId} Tier {trait.tier}");
            return;
        }
        
        // Get owner entity
        var owner = GetComponent<EntityBase>();
        if (owner == null)
        {
            Debug.LogError($"[TraitComponent] No EntityBase component found on {gameObject.name}");
            return;
        }
        
        // Create modifier for each stat
        foreach (var modifier in tierData.modifiers)
        {
            var effect = CreateModifierEffect(owner, modifier);
            stats.Mediator.AddModifier(effect);
            activeModifiers.Add(effect);
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"[TraitComponent] Applied {traitDef.displayName} Tier {trait.tier} " +
                     $"with {tierData.modifiers.Length} stat modifiers to {gameObject.name}");
        }
    }
    
    private PassiveStatBoostEffect CreateModifierEffect(EntityBase owner, TraitStatModifier modifier)
    {
        System.Func<float, float> operation = modifier.operation == ModifierOp.Add
            ? v => v + modifier.value
            : v => v * modifier.value;
        
        return new PassiveStatBoostEffect(
            owner,
            modifier.stat,
            operation,
            duration: -1f // Traits are permanent
        );
    }
    
    private void ClearModifiers(Stats stats)
    {
        foreach (var modifier in activeModifiers)
        {
            stats.Mediator.RemoveModifier(modifier.ID);
        }
        activeModifiers.Clear();
    }
    
    /// <summary>
    /// Get all traits on this entity.
    /// </summary>
    public List<TraitInstance> GetTraits()
    {
        return new List<TraitInstance>(traits);
    }
    
    /// <summary>
    /// Check if entity has a specific trait.
    /// </summary>
    public bool HasTrait(string traitId)
    {
        foreach (var trait in traits)
        {
            if (trait.traitId == traitId)
                return true;
        }
        return false;
    }
    
    /// <summary>
    /// Get trait tier if entity has this trait, otherwise 0.
    /// </summary>
    public int GetTraitTier(string traitId)
    {
        foreach (var trait in traits)
        {
            if (trait.traitId == traitId)
                return trait.tier;
        }
        return 0;
    }

    /// <summary>
    /// Clear all traits from this entity.
    /// Used when applying candidate traits to replace random traits.
    /// </summary>
    public void ClearTraits()
    {
        traits.Clear();
        
        if (showDebugLogs)
        {
            Debug.Log($"[TraitComponent] Cleared all traits from {gameObject.name}");
        }
    }
    
    // Save/Load support
    public TraitInstance[] GetTraitsForSave()
    {
        return traits.ToArray();
    }
    
    public void LoadTraitsFromSave(TraitInstance[] savedTraits)
    {
        traits.Clear();
        if (savedTraits != null)
        {
            traits.AddRange(savedTraits);
        }
    }
    
#if UNITY_EDITOR
    [ContextMenu("Print Traits")]
    private void PrintTraits()
    {
        Debug.Log($"=== Traits on {gameObject.name} ===");
        foreach (var trait in traits)
        {
            var def = TraitDatabase.GetTrait(trait.traitId);
            if (def != null)
            {
                Debug.Log($"  - {def.displayName} (Tier {trait.tier})");
            }
            else
            {
                Debug.Log($"  - MISSING: {trait.traitId} (Tier {trait.tier})");
            }
        }
    }
#endif
}