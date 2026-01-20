using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// EXTENSION to existing TraitDef.cs
/// Add these fields to your existing TraitDef ScriptableObject:
/// </summary>

/*
// Add to your existing TraitDef.cs:

[Header("Identity Text")]
[Tooltip("Epithets for this trait (e.g., 'the Unstoppable')")]
public List<string> epithets = new List<string>();

[Tooltip("One-line descriptions for Tier I")]
public List<string> descriptionsTier1 = new List<string>();

// Future Phase 2:
// public List<string> descriptionsTier2 = new List<string>();
// public List<string> descriptionsTier3 = new List<string>();

*/

/// <summary>
/// Helper extension methods for TraitDef identity text.
/// </summary>
public static class TraitDefIdentityExtensions
{
    public static string GetRandomEpithet(this TraitDef trait)
    {
        if (trait.epithets == null || trait.epithets.Count == 0)
        {
            return "the Adventurer";
        }
        
        return trait.epithets[Random.Range(0, trait.epithets.Count)];
    }
    
    public static string GetRandomDescription(this TraitDef trait, int tier = 1)
    {
        List<string> pool = tier switch
        {
            1 => trait.descriptionsTier1,
            // Phase 2: 2 => trait.descriptionsTier2,
            // Phase 2: 3 => trait.descriptionsTier3,
            _ => trait.descriptionsTier1
        };
        
        if (pool == null || pool.Count == 0)
        {
            return "Ready for work. Probably.";
        }
        
        return pool[Random.Range(0, pool.Count)];
    }
}
