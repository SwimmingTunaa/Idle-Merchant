using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Populates a hiring-board poster (Poster.uxml) from HiringCandidate data:
/// header strip (rank gem, first name, class, cost), portrait + epithet banner, headline stats,
/// trait chips and the trait-multiplier stat bars. Hire button state is HireController's job.
/// </summary>
public static class CandidateUIMapper
{
    // Bar rows (stat, icon class suffix). Adventurer: Damage / Attack speed / Reach / Speed; porter: Speed / Carry / Pickup.
    private static readonly (StatType stat, string icon)[] AdventurerBars =
        { (StatType.AttackDamage, "damage"), (StatType.AttackSpeed, "aspd"), (StatType.AttackRange, "reach"), (StatType.MoveSpeed, "speed") };
    private static readonly (StatType stat, string icon)[] PorterBars =
        { (StatType.MoveSpeed, "speed"), (StatType.CarryCapacity, "carry"), (StatType.PickupTime, "pickup") };

    public static void PopulateUI(VisualElement root, HiringCandidate candidate)
    {
        var def = candidate.entityDef;
        if (root == null || def == null)
        {
            Debug.LogError("[CandidateUIMapper] Null poster root or entityDef");
            return;
        }

        var advDef = def as AdventurerDef;
        var identity = candidate.identity;
        int rank = Mathf.Max(candidate.startRank, def.rank);

        // Strip
        root.Q<Label>("first-name").text = identity != null ? identity.firstName : def.displayName;
        root.Q<Label>("class-name").text = def.displayName;
        root.Q<Label>("cost").text = candidate.hireCost.ToString("N0");
        var classIcon = root.Q("class-icon");
        classIcon.style.display = advDef != null && advDef.classIcon != null ? DisplayStyle.Flex : DisplayStyle.None;
        if (advDef != null && advDef.classIcon != null)
            classIcon.style.backgroundImage = new StyleBackground(advDef.classIcon);

        // Rank gem + portrait frame: tiers above Wood take the tier tint so a Gold find pops
        root.Q<Label>("rank-level").text = rank.ToString();
        if (rank >= 2)
        {
            Color tint = TierTint(rank);
            root.Q("rank-gem").style.unityBackgroundImageTintColor = tint;
            var frame = root.Q("portrait");
            frame.style.borderTopColor = tint;
            frame.style.borderRightColor = tint;
            frame.style.borderBottomColor = tint;
            frame.style.borderLeftColor = tint;
        }

        // Epithet banner only when the candidate has one
        var epithet = identity?.epithet;
        root.Q("epithet").style.display = string.IsNullOrEmpty(epithet) ? DisplayStyle.None : DisplayStyle.Flex;
        root.Q<Label>("epithet-label").text = epithet;

        var sprite = candidate.GetShopSprite();
        if (sprite != null)
            root.Q("unit-icon").style.backgroundImage = new StyleBackground(sprite);
        else
            Debug.LogWarning($"[CandidateUIMapper] Failed to generate shop sprite for {candidate.DisplayName}");

        root.Q<Label>("flavour").text = !string.IsNullOrEmpty(identity?.description) ? identity.description : def.description;

        // Headline numbers (class base × trait multipliers). AttackSpeed is the attack *interval* (Stats.AttackInterval):
        // a Mult scales the seconds between hits, so it divides damage per second.
        var baseStats = BaseStats.FromEntityDef(def);
        if (advDef != null)
        {
            float dps = baseStats.attackDamage * TraitMult(candidate, StatType.AttackDamage, baseStats)
                        / (baseStats.attackSpeed * TraitMult(candidate, StatType.AttackSpeed, baseStats));
            SetBig(root, "Damage", Mathf.RoundToInt(dps).ToString("N0"), "Health", Mathf.RoundToInt(advDef.MaxHealth).ToString("N0"), true);
        }
        else
        {
            float carry = baseStats.carryCapacity * TraitMult(candidate, StatType.CarryCapacity, baseStats);
            float speed = baseStats.moveSpeed * TraitMult(candidate, StatType.MoveSpeed, baseStats);
            SetBig(root, "Carry", Mathf.RoundToInt(carry).ToString("N0"), "Speed", speed.ToString("0.0"), false);
        }

        PopulateTraits(root, candidate);
        PopulateBars(root, candidate, advDef != null ? AdventurerBars : PorterBars, baseStats);
    }

    private static void SetBig(VisualElement root, string label1, string value1, string label2, string value2, bool perSecond)
    {
        root.Q<Label>("big1-label").text = label1;
        root.Q<Label>("big1-value").text = value1;
        root.Q<Label>("big1-unit").style.display = perSecond ? DisplayStyle.Flex : DisplayStyle.None;
        root.Q<Label>("big2-label").text = label2;
        root.Q<Label>("big2-value").text = value2;
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
    /// Raw product of the candidate's trait-tier modifiers on one stat vs the class baseline (1 = unchanged).
    /// Mult multiplies; Add contributes (base + value) / base.
    /// </summary>
    public static float TraitMult(HiringCandidate c, StatType stat, BaseStats baseStats)
    {
        float m = 1f;
        if (c.traits == null) return m;

        float baseValue = baseStats.GetStat(stat);
        foreach (var t in c.traits)
        {
            var traitDef = TraitDatabase.GetTrait(t.traitId);
            if (traitDef == null || t.tier < 1 || t.tier > traitDef.tiers.Length) continue;
            var mods = traitDef.tiers[t.tier - 1].modifiers;
            if (mods == null) continue;

            foreach (var mod in mods)
                if (mod.stat == stat)
                    m *= mod.operation == ModifierOp.Mult ? mod.value
                        : baseValue > 0f ? (baseValue + mod.value) / baseValue : 1f;
        }
        return m;
    }

    /// <summary>Bar value for a stat: the trait multiplier, inverted for time stats (attack interval, pickup time)
    /// so a longer bar always means better.</summary>
    public static float BarMult(HiringCandidate c, StatType stat, BaseStats baseStats)
    {
        float m = TraitMult(c, stat, baseStats);
        return stat == StatType.AttackSpeed || stat == StatType.PickupTime ? 1f / m : m;
    }

    private static void PopulateTraits(VisualElement root, HiringCandidate candidate)
    {
        var container = root.Q("trait-container");
        container.Clear();

        if (candidate.traits != null)
            foreach (var t in candidate.traits)
            {
                var traitDef = TraitDatabase.GetTrait(t.traitId);
                if (traitDef == null) continue;
                var chip = new Label(traitDef.displayName);
                chip.AddToClassList("poster-trait");
                chip.EnableInClassList("poster-trait--cursed", candidate.IsCursed);
                container.Add(chip);
            }

        container.style.display = container.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // 10 cells over 0.5→1.5 in 0.1 steps: a cell is filled iff its upper edge ≤ m; tick at 1.0.
    private static void PopulateBars(VisualElement root, HiringCandidate candidate, (StatType stat, string icon)[] rows, BaseStats baseStats)
    {
        var bars = root.Q("bars");
        bars.Clear();

        foreach (var (stat, icon) in rows)
        {
            float m = Mathf.Clamp(Mathf.Round(BarMult(candidate, stat, baseStats) * 10f) / 10f, 0.5f, 1.5f); // whole cells

            var row = new VisualElement();
            row.AddToClassList("bar-row");
            var iconEl = new VisualElement { tooltip = TraitUIHelper.GetStatDisplayName(stat) };
            iconEl.AddToClassList("bar-icon");
            iconEl.AddToClassList("stat-" + icon);
            row.Add(iconEl);

            var bar = new VisualElement();
            bar.AddToClassList("bar");
            for (int k = 0; k < 10; k++)
            {
                var cell = new VisualElement();
                cell.AddToClassList("bar-cell");
                if (CellFilled(m, k)) cell.AddToClassList("bar-cell--on");
                bar.Add(cell);
            }
            var tick = new VisualElement();
            tick.AddToClassList("bar-tick");
            bar.Add(tick);

            row.Add(bar);
            bars.Add(row);
        }
    }

    /// <summary>Bar cell k (covering [0.5 + k/10, 0.6 + k/10]) is filled iff its upper edge ≤ m.</summary>
    public static bool CellFilled(float m, int k) => 0.6f + k * 0.1f <= m + 0.001f;
}
