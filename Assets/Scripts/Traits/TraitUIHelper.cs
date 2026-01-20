using UnityEngine;
using System.Text;

/// <summary>
/// Helper utilities for displaying trait information in UI.
/// </summary>
public static class TraitUIHelper
{
    /// <summary>
    /// Format tier as Roman numeral (I, II, III).
    /// </summary>
    public static string RomanNumeral(int tier)
    {
        return tier switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            _ => tier.ToString()
        };
    }
    
    /// <summary>
    /// Format stat modifier for display.
    /// </summary>
    public static string FormatModifier(TraitStatModifier mod)
    {
        if (mod.operation == ModifierOp.Add)
        {
            return mod.value >= 0 ? $"+{mod.value}" : $"{mod.value}";
        }
        else // Mult
        {
            float percent = (mod.value - 1f) * 100f;
            return percent >= 0 ? $"+{percent:F0}%" : $"{percent:F0}%";
        }
    }
    
    /// <summary>
    /// Get display name for stat type.
    /// </summary>
    public static string GetStatDisplayName(StatType stat)
    {
        return stat switch
        {
            StatType.MoveSpeed => "Move Speed",
            StatType.AttackDamage => "Attack Damage",
            StatType.AttackSpeed => "Attack Speed",
            StatType.AttackRange => "Attack Range",
            StatType.CarryCapacity => "Carry Capacity",
            StatType.PickupTime => "Pickup Time",
            StatType.DepositTime => "Deposit Time",
            _ => stat.ToString()
        };
    }
    
    /// <summary>
    /// Format all stat modifiers from a tier as multi-line string.
    /// </summary>
    public static string FormatStatDeltas(TraitStatModifier[] modifiers)
    {
        if (modifiers == null || modifiers.Length == 0)
            return "";
        
        var sb = new StringBuilder();
        foreach (var mod in modifiers)
        {
            string statName = GetStatDisplayName(mod.stat);
            string delta = FormatModifier(mod);
            sb.AppendLine($"{delta} {statName}");
        }
        return sb.ToString().TrimEnd();
    }
    
    /// <summary>
    /// Get full trait display string with tier.
    /// Example: "Leeroy Certified II"
    /// </summary>
    public static string GetTraitDisplayString(TraitInstance trait)
    {
        var traitDef = TraitDatabase.GetTrait(trait.traitId);
        if (traitDef == null)
            return $"Unknown ({trait.traitId})";
        
        return $"{traitDef.displayName} {RomanNumeral(trait.tier)}";
    }
    
    /// <summary>
    /// Get trait description with stats for tooltip.
    /// </summary>
    public static string GetTraitTooltip(TraitInstance trait)
    {
        var traitDef = TraitDatabase.GetTrait(trait.traitId);
        if (traitDef == null)
            return "Unknown trait";
        
        var sb = new StringBuilder();
        sb.AppendLine($"<b>{traitDef.displayName} {RomanNumeral(trait.tier)}</b>");
        
        if (!string.IsNullOrEmpty(traitDef.description))
        {
            sb.AppendLine(traitDef.description);
        }
        
        // Add stat modifiers
        if (trait.tier > 0 && trait.tier <= traitDef.tiers.Length)
        {
            var tierData = traitDef.tiers[trait.tier - 1];
            if (tierData.modifiers != null && tierData.modifiers.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine(FormatStatDeltas(tierData.modifiers));
            }
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Get color for trait compatibility.
    /// </summary>
    public static Color GetCompatibilityColor(TraitCompatibility compatibility)
    {
        return compatibility switch
        {
            TraitCompatibility.Compatible => Color.green,
            TraitCompatibility.Conflict => Color.yellow,
            TraitCompatibility.Cursed => Color.red,
            _ => Color.white
        };
    }
}