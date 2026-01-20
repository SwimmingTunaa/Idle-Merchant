using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Checks trait compatibility for soft exclusivity and cursed combos.
/// </summary>
public static class TraitCompatibilityChecker
{
    /// <summary>
    /// Check compatibility between two traits.
    /// </summary>
    public static TraitCompatibility CheckCompatibility(
        TraitDef trait1, 
        TraitDef trait2,
        out CursedComboDef cursedCombo)
    {
        cursedCombo = null;
        
        if (trait1 == null || trait2 == null)
        {
            Debug.LogWarning("[TraitCompatibilityChecker] Null trait passed to compatibility check");
            return TraitCompatibility.Compatible;
        }
        
        // Check explicit conflicts list (soft warning)
        if (trait1.conflictsWith != null && System.Array.IndexOf(trait1.conflictsWith, trait2) >= 0)
        {
            return TraitCompatibility.Conflict;
        }
        
        if (trait2.conflictsWith != null && System.Array.IndexOf(trait2.conflictsWith, trait1) >= 0)
        {
            return TraitCompatibility.Conflict;
        }
        
        // Check if both heavily penalize same stat (anti-frustration)
        if (BothPenalizeSameStat(trait1, trait2, threshold: 0.3f))
        {
            return TraitCompatibility.Conflict;
        }
        
        // Check if cursed combo
        cursedCombo = CursedComboDatabase.GetCombo(trait1, trait2);
        if (cursedCombo != null)
        {
            return TraitCompatibility.Cursed;
        }
        
        return TraitCompatibility.Compatible;
    }
    
    /// <summary>
    /// Overload without cursedCombo out param.
    /// </summary>
    public static TraitCompatibility CheckCompatibility(TraitDef trait1, TraitDef trait2)
    {
        return CheckCompatibility(trait1, trait2, out _);
    }
    
    /// <summary>
    /// Check if both traits heavily penalize the same stat (anti-frustration rule).
    /// </summary>
    private static bool BothPenalizeSameStat(TraitDef trait1, TraitDef trait2, float threshold)
    {
        var trait1Penalties = GetStatPenalties(trait1);
        var trait2Penalties = GetStatPenalties(trait2);
        
        foreach (var kvp in trait1Penalties)
        {
            StatType stat = kvp.Key;
            float penalty1 = kvp.Value;
            
            if (trait2Penalties.TryGetValue(stat, out float penalty2))
            {
                // Both penalize same stat heavily
                if (penalty1 >= threshold && penalty2 >= threshold)
                {
                    return true;
                }
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Extract penalties from trait (multipliers < 1.0).
    /// </summary>
    private static Dictionary<StatType, float> GetStatPenalties(TraitDef trait)
    {
        var penalties = new Dictionary<StatType, float>();
        
        if (trait.tiers == null) return penalties;
        
        // Check all tiers for worst penalties
        foreach (var tier in trait.tiers)
        {
            if (tier.modifiers == null) continue;
            
            foreach (var mod in tier.modifiers)
            {
                if (mod.operation == ModifierOp.Mult && mod.value < 1.0f)
                {
                    float penalty = 1.0f - mod.value; // 0.7 → 0.3 penalty
                    
                    if (!penalties.ContainsKey(mod.stat) || penalties[mod.stat] < penalty)
                    {
                        penalties[mod.stat] = penalty;
                    }
                }
            }
        }
        
        return penalties;
    }
    
    /// <summary>
    /// Check if new trait can be added to existing traits.
    /// </summary>
    public static bool CanAddTrait(
        TraitDef newTrait, 
        List<TraitInstance> existingTraits,
        out CursedComboDef cursedCombo)
    {
        cursedCombo = null;
        
        foreach (var existing in existingTraits)
        {
            var existingDef = TraitDatabase.GetTrait(existing.traitId);
            if (existingDef == null) continue;
            
            var compatibility = CheckCompatibility(newTrait, existingDef, out var combo);
            
            // Conflicts block addition
            if (compatibility == TraitCompatibility.Conflict)
                return false;
            
            // Cursed combos are allowed but flagged
            if (compatibility == TraitCompatibility.Cursed)
                cursedCombo = combo;
        }
        
        return true;
    }
    
    /// <summary>
    /// Overload without cursedCombo out param.
    /// </summary>
    public static bool CanAddTrait(TraitDef newTrait, List<TraitInstance> existingTraits)
    {
        return CanAddTrait(newTrait, existingTraits, out _);
    }
}