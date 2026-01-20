using UnityEngine;
using System;

/// <summary>
/// Runtime trait instance stored on entities.
/// Only stores ID + tier, pulls data from TraitDef on demand.
/// </summary>
[System.Serializable]
public struct TraitInstance
{
    public string traitId;
    public int tier; // 1, 2, or 3
}

/// <summary>
/// Stat modifier operation type.
/// </summary>
public enum ModifierOp
{
    Add,  // Flat addition (e.g., +10)
    Mult  // Multiplier (e.g., 1.3 = +30%, 0.7 = -30%)
}

/// <summary>
/// Individual stat modifier for traits.
/// Separate from StatModifier base class - this is data-only.
/// </summary>
[System.Serializable]
public struct TraitStatModifier
{
    public StatType stat;
    public ModifierOp operation;
    public float value;
}

/// <summary>
/// Tier configuration - modifiers for a specific tier.
/// </summary>
[System.Serializable]
public struct TraitTier
{
    public TraitStatModifier[] modifiers;
}

/// <summary>
/// Trait role - determines which entities can have this trait.
/// </summary>
public enum TraitRole
{
    Adventurer,
    Porter
}

/// <summary>
/// Trait compatibility result.
/// </summary>
public enum TraitCompatibility
{
    Compatible,  // Green - no issues
    Conflict,    // Yellow - anti-frustration warning, blocks generation
    Cursed       // Red - extremely risky, allowed but flagged
}