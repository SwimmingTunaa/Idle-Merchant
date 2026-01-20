using UnityEngine;

/// <summary>
/// INTEGRATION: Add to EntityBase or create IdentityComponent.
/// 
/// Option A: Add directly to EntityBase.cs
/// Option B: Create separate IdentityComponent (shown below)
/// </summary>
public class IdentityComponent : MonoBehaviour
{
    [SerializeField] private Identity identity;
    
    public Identity Identity { get; private set; }
    
    /// <summary>
    /// Apply identity from hiring candidate.
    /// Called once during entity spawn.
    /// </summary>
    public void ApplyIdentity(Identity newIdentity)
    {
        if (newIdentity == null)
        {
            return;
        }
        
        Identity = newIdentity;
        gameObject.name = Identity.DisplayName;
    }
    
    /// <summary>
    /// Get identity for save.
    /// </summary>
    public Identity GetIdentityForSave()
    {
        return Identity;
    }
    
    /// <summary>
    /// Load identity from save.
    /// </summary>
    public void LoadIdentityFromSave(Identity savedIdentity)
    {
        Identity = savedIdentity;
        if (Identity != null)
        {
            gameObject.name = Identity.DisplayName;
        }
    }
}

/*
// OR: Add to existing EntityBase.cs

public class EntityBase : MonoBehaviour
{
    [Header("Identity")]
    public Identity Identity { get; private set; }
    
    public void ApplyIdentity(Identity identity)
    {
        if (identity == null)
        {
            return;
        }
        
        Identity = identity;
        gameObject.name = Identity.DisplayName;
    }
    
    // In your existing ApplyTraitsFromCandidate method:
    public void ApplyTraitsFromCandidate(HiringCandidate candidate)
    {
        // Apply identity first
        ApplyIdentity(candidate.identity);
        
        // Then apply traits (existing code)
        if (traitComponent != null)
        {
            foreach (var trait in candidate.traits)
            {
                traitComponent.AddTrait(trait);
            }
            
            if (Stats != null)
            {
                traitComponent.ApplyTraitsToStats(Stats);
            }
        }
    }
}
*/
