using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages trait assignment and distribution across entities.
/// Handles random trait selection with conflict checking.
/// </summary>
public class TraitManager : MonoBehaviour
{
    public static TraitManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public bool AssignRandomAdventurerTraitTier1(EntityBase entity)
    {
        if (entity == null) return false;

        var traitComponent = entity.traitComponent;
        if (traitComponent == null) return false;

        var allTraits = TraitDatabase.GetAllTraits();
        if (allTraits == null || allTraits.Count == 0) return false;

        var existing = traitComponent.GetTraits();

        var valid = new List<TraitDef>();
        foreach (var traitDef in allTraits)
        {
            if (traitDef.role != TraitRole.Adventurer) continue;
            if (!TraitCompatibilityChecker.CanAddTrait(traitDef, existing)) continue;
            valid.Add(traitDef);
        }

        if (valid.Count == 0) return false;

        // ✅ FIX: use `valid`, not `validTraits`
        var selected = valid[Random.Range(0, valid.Count)];

        var instance = new TraitInstance
        {
            traitId = selected.traitId,
            tier = 1
        };

        // ✅ ACTUALLY APPLY THE TRAIT
        traitComponent.AddTrait(instance);
        traitComponent.ApplyTraitsToStats(entity.Stats);
        return true;
    }


    /// <summary>
    /// Assign a random trait to an entity.
    /// Respects conflict rules and max trait limits.
    /// </summary>
    public bool AssignRandomTraitToEntity(EntityBase entity)
    {
        if (entity == null)
        {
            Debug.LogError("[TraitManager] Entity is null");
            return false;
        }

        var traitComponent = entity.GetComponent<TraitComponent>();
        if (traitComponent == null)
        {
            Debug.LogError($"[TraitManager] {entity.gameObject.name} has no TraitComponent");
            return false;
        }

        // Get all available traits
        var allTraits = TraitDatabase.GetAllTraits();
        if (allTraits == null || allTraits.Count == 0)
        {
            Debug.LogWarning("[TraitManager] No traits available in database");
            return false;
        }

        // Filter valid traits (not conflicting with existing ones)
        var validTraits = new List<TraitDef>();
        var existingTraits = traitComponent.GetTraits();

        foreach (var traitDef in allTraits)
        {
            if (TraitCompatibilityChecker.CanAddTrait(traitDef, existingTraits))
            {
                validTraits.Add(traitDef);
            }
        }

        if (validTraits.Count == 0)
        {
            Debug.LogWarning($"[TraitManager] No valid traits available for {entity.gameObject.name}");
            return false;
        }

        // Pick random trait and random tier
        TraitDef selectedTrait = validTraits[Random.Range(0, validTraits.Count)];
        int randomTier = Random.Range(1, selectedTrait.maxTier + 1);

        var traitInstance = new TraitInstance
        {
            traitId = selectedTrait.traitId,
            tier = randomTier
        };

        return traitComponent.AddTrait(traitInstance);
    }

    /// <summary>
    /// Assign multiple random traits to an entity.
    /// </summary>
    public int AssignRandomTraitsToEntity(EntityBase entity, int count = 1)
    {
        int assigned = 0;
        for (int i = 0; i < count; i++)
        {
            if (AssignRandomTraitToEntity(entity))
            {
                assigned++;
            }
            else
            {
                break; // Stop if we can't add more
            }
        }
        return assigned;
    }
}