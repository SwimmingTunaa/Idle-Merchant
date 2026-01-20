using UnityEngine;

/// <summary>
/// Generates immutable identities for adventurers.
/// Called once per candidate during hiring.
/// </summary>
public static class IdentityGenerator
{
    /// <summary>
    /// Generate a complete identity.
    /// </summary>
    /// <param name="pool">Name pool ScriptableObject</param>
    /// <param name="primaryTrait">Trait that determines epithet and description (can be null)</param>
    /// <param name="forcedSeed">Optional seed for deterministic generation</param>
    /// <param name="forcedGender">Optional gender override</param>
    public static Identity Generate(
        NamePoolDef pool, 
        TraitDef primaryTrait, 
        int? forcedSeed = null, 
        Gender? forcedGender = null)
    {
        // Determine gender
        Gender gender = forcedGender ?? (Random.value < 0.5f ? Gender.Male : Gender.Female);
        
        // Get first name
        string firstName = pool != null 
            ? pool.GetRandomName(gender) 
            : GetFallbackName(gender);
        
        // Get epithet and description
        string epithet;
        string description;
        
        if (primaryTrait != null)
        {
            // Has trait - earns an epithet
            epithet = primaryTrait.GetRandomEpithet();
            description = primaryTrait.GetRandomDescription(tier: 1);
        }
        else
        {
            // No trait - no epithet, just a generic description
            epithet = null;
            
            description = pool != null && pool.genericDescriptions != null && pool.genericDescriptions.Count > 0
                ? pool.genericDescriptions[Random.Range(0, pool.genericDescriptions.Count)]
                : "Ready for work. Probably.";
        }
        
        // Generate appearance seed
        int appearanceSeed = forcedSeed ?? Random.Range(int.MinValue, int.MaxValue);
        
        return new Identity(gender, firstName, epithet, description, appearanceSeed);
    }
    
    private static string GetFallbackName(Gender gender)
    {
        return gender == Gender.Male ? "Adventurer" : "Adventurer";
    }
}