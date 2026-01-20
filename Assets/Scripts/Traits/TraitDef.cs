using UnityEngine;
using System.Collections.Generic;
/// <summary>
/// ScriptableObject definition for a single trait.
/// Designer-friendly - create in Unity inspector, no code changes needed.
/// </summary>
[CreateAssetMenu(fileName = "NewTrait", menuName = "Traits/Trait Definition")]
public class TraitDef : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique identifier (e.g., 'leeroy_certified', 'built_like_a_mule')")]
    public string traitId;
    
    [Tooltip("Display name shown to player")]
    public string displayName;
    
    [TextArea(2, 4)]
    public string description;
    
    [Header("Identity Text")]
    [Tooltip("Epithets for this trait (e.g., 'the Unstoppable')")]
    public List<string> epithets = new List<string>();

    [Tooltip("One-line descriptions for Tier I")]
    public List<string> descriptionsTier1 = new List<string>();

    [Header("Hiring")]
    public float hireCostMultiplier = 1.0f;

    [Header("Role")]
    public TraitRole role;
    
    [Header("Tier Limits")]
    [Tooltip("Maximum tier this trait can reach (1-3)")]
    [Range(1, 3)]
    public int maxTier = 2;
    
    [Header("Tier Modifiers")]
    [Tooltip("Array of tier configurations. Index 0 = Tier I, 1 = Tier II, 2 = Tier III")]
    public TraitTier[] tiers;
    
    [Header("Soft Exclusivity (Anti-Frustration)")]
    [Tooltip("Traits that conflict with this one (warns player, blocks normal generation)")]
    public TraitDef[] conflictsWith;
    
    void OnValidate()
    {
        // Validate tier array length matches maxTier
        if (tiers != null && tiers.Length != maxTier)
        {
            Debug.LogWarning($"[{name}] Tier array length ({tiers.Length}) doesn't match maxTier ({maxTier})");
        }
        
        // Validate traitId has no spaces
        if (!string.IsNullOrEmpty(traitId) && traitId.Contains(" "))
        {
            Debug.LogWarning($"[{name}] traitId should not contain spaces - use underscores");
        }
    }
}
