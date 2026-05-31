using UnityEngine;

/// <summary>
/// Static lookup for adventurer rank tiers and their display.
/// Ranks ascend Wood &lt; Bronze &lt; Iron &lt; Silver &lt; Gold. Each rank holds N levels
/// shown as roman numerals (I..V) on the slot badge, e.g. "Wood I" … "Gold V".
/// </summary>
public static class AdventurerRank
{
    /// <summary>Rank tier names, lowest to highest (1-based via <see cref="Name"/>).</summary>
    public static readonly string[] Names = { "Wood", "Bronze", "Iron", "Silver", "Gold" };

    private static readonly string[] Romans = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };

    /// <summary>Tier name for a 1-based rank index (clamped to the table).</summary>
    public static string Name(int rank)
    {
        if (Names.Length == 0) return rank.ToString();
        return Names[Mathf.Clamp(rank - 1, 0, Names.Length - 1)];
    }

    /// <summary>Roman numeral for a 1-based level (falls back to the number past the table).</summary>
    public static string Roman(int level)
    {
        if (level >= 1 && level <= Romans.Length) return Romans[level - 1];
        return level.ToString();
    }

    /// <summary>"Wood V" — rank name plus roman level.</summary>
    public static string Display(int rank, int level) => $"{Name(rank)} {Roman(level)}";
}
