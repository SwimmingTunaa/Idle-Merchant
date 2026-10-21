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

        // Rank badge — class tier (Recruitment Favor Novices: their start rank)
        int rank = Mathf.Max(candidate.startRank, candidate.entityDef.rank);
        var rankLabel = candidateRoot.Q<Label>("rank-level");
        if (rankLabel != null)
            rankLabel.text = rank.ToString();

        // Class icon + name; tiers above Novice tint the portrait frame + badge so a Gold find pops
        var advDef = candidate.entityDef as AdventurerDef;
        var classRow = candidateRoot.Q<VisualElement>("class-row");
        if (classRow != null)
            classRow.style.display = advDef != null ? DisplayStyle.Flex : DisplayStyle.None;
        if (advDef != null)
        {
            var classIcon = candidateRoot.Q<VisualElement>("class-icon");
            if (classIcon != null)
                classIcon.style.backgroundImage = advDef.classIcon != null ? new StyleBackground(advDef.classIcon) : null;
            var className = candidateRoot.Q<Label>("class-name");
            if (className != null)
                className.text = advDef.displayName;
        }

        if (advDef != null && advDef.rank > 1)
        {
            Color tint = TierTint(advDef.rank);
            var frame = candidateRoot.Q<VisualElement>("sprite-container");
            if (frame != null)
            {
                frame.style.borderTopColor = tint;
                frame.style.borderRightColor = tint;
                frame.style.borderBottomColor = tint;
                frame.style.borderLeftColor = tint;
            }
            var badge = candidateRoot.Q<VisualElement>("rank-container");
            if (badge != null)
                badge.style.unityBackgroundImageTintColor = tint;
        }
                
        // Traits
        PopulateTraits(candidateRoot, candidate);
        
        // Stat Modifiers
        PopulateStatModifiers(candidateRoot, candidate);

        // Newspaper name (stable — assigned once when candidate is generated)
        var newspaperLabel = candidateRoot.Q<Label>("newspaper-name");
        if (newspaperLabel != null)
            newspaperLabel.text = candidate.newspaperName;
    }
    
    /// <summary>Frame tint per tier (2 = Bronze … 5 = Gold).</summary>
    public static Color TierTint(int tier) => tier switch
    {
        2 => new Color32(184, 115, 51, 255),   // bronze
        3 => new Color32(140, 146, 158, 255),  // iron
        4 => new Color32(200, 212, 228, 255),  // silver
        5 => new Color32(240, 190, 40, 255),   // gold
        _ => new Color32(195, 137, 93, 255),   // default card brown
    };

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