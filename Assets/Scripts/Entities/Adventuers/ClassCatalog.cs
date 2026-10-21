using System.Collections.Generic;
using UnityEngine;

/// <summary>Adventurer class line. Novice is the classless Wood tier; the first promotion picks one of the others.</summary>
public enum ClassLine { Novice, Warrior, Guardian, Rogue, Mage }   // serialized ints — append only

/// <summary>
/// Every hireable adventurer def (Novice + line × tier) plus the hire-board odds and premium pricing.
/// Lives at Resources/ClassCatalog so the hire board, promotions and save restore share one list
/// (replaces per-manager hireableDefs for adventurers). See CLASSES_DESIGN.md.
/// </summary>
[CreateAssetMenu(menuName = "Data/Class Catalog")]
public class ClassCatalog : ScriptableObject
{
    [Tooltip("Novice + one AdventurerDef per line × tier. Def asset names are save keys — never rename.")]
    public List<AdventurerDef> defs = new();

    [Header("Hire Board")]
    [Tooltip("Weight per tier (index 0 = Wood/Novice … 4 = Gold), normalised over the unlocked tiers.")]
    public float[] tierWeights = { 50f, 25f, 13f, 8f, 4f };

    [Tooltip("Weight multiplier for a newly unlocked layer's tier.")]
    public float unlockBoost = 2f;

    [Tooltip("How long (game seconds) the unlock boost lasts.")]
    public float unlockBoostSeconds = 600f;

    [Tooltip("Class hires cost (Novice price + the promotions they skip) × this.")]
    public float hirePremium = 1.4f;

    private static ClassCatalog instance;
    public static ClassCatalog Instance => instance != null ? instance : instance = Resources.Load<ClassCatalog>("ClassCatalog");

    public AdventurerDef Novice => Get(ClassLine.Novice, 1);

    /// <summary>The def for a line at a tier (= rank), or null.</summary>
    public AdventurerDef Get(ClassLine line, int tier)
    {
        foreach (var d in defs)
            if (d != null && d.classLine == line && d.rank == tier) return d;
        return null;
    }

    /// <summary>Base hire price before the ×1.25-per-owned scaling: Novice cost, or (Novice cost + skipped promotions) × premium.</summary>
    public int HireCost(AdventurerDef d)
    {
        if (d.rank <= 1) return d.hireCost;
        int cost = d.hireCost;
        for (int r = 1; r < d.rank; r++) cost += d.PromoteCost(r);
        return Mathf.RoundToInt(cost * hirePremium);
    }
}
