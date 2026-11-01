using UnityEngine;

/// <summary>
/// How an adventurer's rank fits the depth it works: gap = rank − depth. Below the depth it's dangerous
/// (less damage dealt, more taken) but it learns faster; above it learns less, and from
/// <see cref="slackFromRanksAbove"/> it also slacks between fights (fewer kills → less loot).
/// Tuned on Resources/RankFit; applied by AdventurerAgent.ApplyRankFit whenever rank or depth changes.
/// </summary>
[CreateAssetMenu(menuName = "Data/Rank Fit")]
public class RankFit : ScriptableObject
{
    [Header("Below the depth's rank (per rank short)")]
    [Range(0f, 0.3f)] public float damageLossPerRank = 0.15f;
    [Range(0f, 0.5f)] public float damageTakenPerRank = 0.20f;
    [Tooltip("The upside of working too deep: learning faster")]
    [Range(0f, 0.5f)] public float xpBonusPerRank = 0.25f;

    [Header("Above the depth's rank")]
    [Tooltip("XP multiplier by ranks above the depth: element 0 = one rank above; the last element covers anything higher")]
    public float[] xpMultiplierAbove = { 0.5f, 0f };
    [Tooltip("Ranks above the depth at which the unit starts slacking between fights")]
    [Min(1)] public int slackFromRanksAbove = 2;
    [Tooltip("Seconds a slacking unit ignores new targets after each kill (random in range)")]
    public Vector2 slackPause = new Vector2(1.5f, 3f);

    [Header("Faces (Tools/draw_fit_faces.py → Sprites/UI/Fit)")]
    [Tooltip("One per Mood, in order: outmatched, stretched, fit, easy, slacking")]
    public Sprite[] faces = new Sprite[5];

    [Header("World thought bubble (Sprites/UI/ThoughtBubble, the customers' bubble)")]
    [Tooltip("Pop-in frames, last = the full bubble the face sits in")]
    public Sprite[] bubbleFrames = new Sprite[3];
    [Tooltip("Mild moods (stretched, easy) show their bubble this long after the mood changes, and while the unit is hovered / selected; strong moods (outmatched, slacking) keep it up")]
    [Min(0f)] public float mildShowSeconds = 4f;

    public enum Mood { Outmatched, Stretched, Fit, Easy, Slacking }

    public static bool IsMild(Mood m) => m == Mood.Stretched || m == Mood.Easy;

    public static Mood MoodOf(Effect e) =>
        e.gap <= -2 ? Mood.Outmatched : e.gap == -1 ? Mood.Stretched : e.gap == 0 ? Mood.Fit : e.slacks ? Mood.Slacking : Mood.Easy;

    public Sprite Face(Mood m) => faces != null && (int)m < faces.Length ? faces[(int)m] : null;

    static RankFit instance;
    public static RankFit Instance => instance != null ? instance : instance = Resources.Load<RankFit>("RankFit");

    public struct Effect
    {
        public int gap;
        public float damage, damageTaken, xp;
        public bool slacks;
        public static readonly Effect Neutral = new Effect { damage = 1f, damageTaken = 1f, xp = 1f };
    }

    public Effect Evaluate(int gap)
    {
        var e = Effect.Neutral;
        e.gap = gap;
        if (gap < 0)
        {
            int n = -gap;
            e.damage = Mathf.Max(0.1f, 1f - damageLossPerRank * n);
            e.damageTaken = 1f + damageTakenPerRank * n;
            e.xp = 1f + xpBonusPerRank * n;
        }
        else if (gap > 0)
        {
            if (xpMultiplierAbove != null && xpMultiplierAbove.Length > 0)
                e.xp = Mathf.Max(0f, xpMultiplierAbove[Mathf.Min(gap, xpMultiplierAbove.Length) - 1]);
            e.slacks = gap >= slackFromRanksAbove;
        }
        return e;
    }

    /// <summary>One short Roster line for a fit ("" = good fit).</summary>
    public static string Describe(Effect e)
    {
        if (e.gap < 0)
            return $"Outmatched: −{(1f - e.damage) * 100f:0}% dmg, +{(e.damageTaken - 1f) * 100f:0}% hurt, +{(e.xp - 1f) * 100f:0}% XP";
        if (e.gap > 0)
            return "Too easy: " + (e.xp <= 0f ? "no XP" : $"{e.xp * 100f:0}% XP") + (e.slacks ? ", slacks" : "");
        return "";
    }
}
