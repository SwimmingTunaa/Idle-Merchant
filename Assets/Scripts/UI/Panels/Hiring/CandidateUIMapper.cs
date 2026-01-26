using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Static helper to populate Candidate.uxml elements from HiringCandidate data.
/// Handles name, traits, description, cost, and icon display.
/// UPDATED: Now uses runtime-generated sprites from HiringCandidate.
/// </summary>
public static class CandidateUIMapper
{
    /// <summary>
    /// Populate candidate card UXML with data from HiringCandidate.
    /// </summary>
    public static void PopulateUI(VisualElement candidateRoot, HiringCandidate candidate)
    {
        if (candidateRoot == null || candidate.entityDef == null)
        {
            Debug.LogError("[CandidateUIMapper] Null candidate root or entityDef");
            return;
        }
        
        // Identity
        var nameEpithetLabel = candidateRoot.Q<Label>("unit-name-epithet");
        var descriptionLabel = candidateRoot.Q<Label>("unit-description");
        
        if (candidate.identity != null)
        {
            if (nameEpithetLabel != null)
            {
                // Combine name and epithet into one label
                string fullName = candidate.identity.firstName;
                if (!string.IsNullOrEmpty(candidate.identity.epithet))
                {
                    fullName += " " + candidate.identity.epithet;
                }
                nameEpithetLabel.text = fullName;
            }
            
            if (descriptionLabel != null)
                descriptionLabel.text = candidate.identity.description ?? candidate.entityDef.description;
        }
        else
        {
            // Fallback if no identity
            if (nameEpithetLabel != null)
                nameEpithetLabel.text = candidate.entityDef.displayName;
            
            if (descriptionLabel != null)
                descriptionLabel.text = candidate.entityDef.description;
        }
        
        // Cost
        var costLabel = candidateRoot.Q<Label>("cost");
        if (costLabel != null)
            costLabel.text = candidate.hireCost.ToString();
        
        // Icon - Use generated sprite
        var iconElement = candidateRoot.Q<VisualElement>("unit-icon");
        if (iconElement != null)
        {
            Sprite shopSprite = candidate.GetShopSprite();
            if (shopSprite != null)
            {
                iconElement.style.backgroundImage = new StyleBackground(shopSprite);
            }
            else
            {
                Debug.LogWarning($"[CandidateUIMapper] Failed to generate shop sprite for {candidate.DisplayName}");
            }
        }
        
        // Traits
        PopulateTraits(candidateRoot, candidate);
        
        // Stat Modifiers
        PopulateStatModifiers(candidateRoot, candidate);
    }
    
    /// <summary>
    /// Populate trait badges.
    /// Shows cursed combo visual treatment if applicable.
    /// </summary>
    private static void PopulateTraits(VisualElement candidateRoot, HiringCandidate candidate)
    {
        var traitContainer = candidateRoot.Q<VisualElement>("trait-container");
        if (traitContainer == null)
            return;
        
        // Clear existing traits
        traitContainer.Clear();
        
        if (candidate.traits == null || candidate.traits.Length == 0)
        {
            // Hide container if no traits
            traitContainer.style.display = DisplayStyle.None;
            return;
        }
        
        traitContainer.style.display = DisplayStyle.Flex;
        
        // Add trait labels
        foreach (var traitInstance in candidate.traits)
        {
            var traitDef = TraitDatabase.GetTrait(traitInstance.traitId);
            if (traitDef == null)
                continue;
            
            var traitLabel = new Label(traitDef.displayName);
            traitLabel.AddToClassList("trait");
            
            // Add cursed visual treatment
            if (candidate.IsCursed)
                traitLabel.AddToClassList("trait-cursed");
            
            traitContainer.Add(traitLabel);
        }
    }
    
    /// <summary>
    /// Populate stat modifier labels from traits.
    /// </summary>
    private static void PopulateStatModifiers(VisualElement candidateRoot, HiringCandidate candidate)
    {
        var modsContainer = candidateRoot.Q<VisualElement>("mods-container");
        if (modsContainer == null)
            return;
        
        modsContainer.Clear();
        
        if (candidate.traits == null || candidate.traits.Length == 0)
        {
            // Hide container if no traits
            modsContainer.style.display = DisplayStyle.None;
            return;
        }
        
        modsContainer.style.display = DisplayStyle.Flex;
        
        foreach (var traitInstance in candidate.traits)
        {
            var traitDef = TraitDatabase.GetTrait(traitInstance.traitId);
            if (traitDef == null || traitInstance.tier < 1 || traitInstance.tier > traitDef.tiers.Length)
                continue;
            
            var tierData = traitDef.tiers[traitInstance.tier - 1];
            
            if (tierData.modifiers == null || tierData.modifiers.Length == 0)
                continue;
            
            foreach (var mod in tierData.modifiers)
            {
                string modText = FormatStatModifier(mod);
                var modLabel = new Label(modText);
                modLabel.AddToClassList("mod");
                modsContainer.Add(modLabel);
            }
        }
    }
    
    /// <summary>
    /// Format stat modifier for display.
    /// Examples: "+20% Attack", "+10 MaxHP", "-15% MoveSpeed"
    /// </summary>
    private static string FormatStatModifier(TraitStatModifier mod)
    {
        string sign = mod.value >= 0 ? "+" : "";
        string valueStr;
        
        if (mod.operation == ModifierOp.Mult)
        {
            // Multiplier: convert to percentage (1.2 -> +20%, 0.8 -> -20%)
            float percentChange = (mod.value - 1f) * 100f;
            sign = percentChange >= 0 ? "+" : "";
            valueStr = $"{sign}{percentChange:F0}%";
        }
        else
        {
            // Additive: show raw value
            valueStr = $"{sign}{mod.value:F0}";
        }
        
        return $"{valueStr} {mod.stat}";
    }
}