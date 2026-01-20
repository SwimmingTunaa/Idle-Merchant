using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Singleton database for fast trait lookups.
/// Caches all TraitDefs on initialization.
/// </summary>
public static class TraitDatabase
{
    private static Dictionary<string, TraitDef> traitCache;
    private static Dictionary<TraitRole, List<TraitDef>> traitsByRole;
    private static bool isInitialized = false;
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        Initialize();
    }
    
    public static void Initialize()
    {
        if (isInitialized)
        {
            Debug.LogWarning("[TraitDatabase] Already initialized, skipping");
            return;
        }
        
        var allTraits = Resources.LoadAll<TraitDef>("Traits");
        
        traitCache = new Dictionary<string, TraitDef>();
        traitsByRole = new Dictionary<TraitRole, List<TraitDef>>
        {
            { TraitRole.Adventurer, new List<TraitDef>() },
            { TraitRole.Porter, new List<TraitDef>() }
        };
        
        foreach (var trait in allTraits)
        {
            if (string.IsNullOrEmpty(trait.traitId))
            {
                Debug.LogError($"[TraitDatabase] TraitDef '{trait.name}' has no traitId!");
                continue;
            }
            
            if (traitCache.ContainsKey(trait.traitId))
            {
                Debug.LogError($"[TraitDatabase] Duplicate traitId: {trait.traitId}");
                continue;
            }
            
            traitCache[trait.traitId] = trait;
            traitsByRole[trait.role].Add(trait);
        }
        
        isInitialized = true;

    }
    
    public static TraitDef GetTrait(string traitId)
    {
        if (!isInitialized) Initialize();
        
        if (string.IsNullOrEmpty(traitId))
        {
            Debug.LogWarning("[TraitDatabase] Null or empty traitId");
            return null;
        }
        
        if (traitCache.TryGetValue(traitId, out var trait))
        {
            return trait;
        }
        
        Debug.LogWarning($"[TraitDatabase] Trait not found: {traitId}");
        return null;
    }
    
    public static List<TraitDef> GetTraitsForRole(TraitRole role)
    {
        if (!isInitialized) Initialize();
        
        return new List<TraitDef>(traitsByRole[role]);
    }
    
    public static List<TraitDef> GetAllTraits()
    {
        if (!isInitialized) Initialize();
        
        return traitCache.Values.ToList();
    }
    
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Traits/Reload Trait Database")]
    public static void ReloadDatabase()
    {
        isInitialized = false;
        traitCache?.Clear();
        traitsByRole?.Clear();
        Initialize();
    }
    
    [UnityEditor.MenuItem("Tools/Traits/Print Trait Database")]
    public static void PrintDatabase()
    {
        if (!isInitialized) Initialize();
        
        Debug.Log("=== TRAIT DATABASE ===");
        Debug.Log($"Total Traits: {traitCache.Count}");
        
        foreach (var role in System.Enum.GetValues(typeof(TraitRole)))
        {
            var traits = traitsByRole[(TraitRole)role];
            Debug.Log($"\n{role} Traits ({traits.Count}):");
            foreach (var trait in traits)
            {
                Debug.Log($"  - {trait.traitId} ({trait.displayName}) [Max Tier {trait.maxTier}]");
            }
        }
    }
#endif
}
