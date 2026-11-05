using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Populates a hiring-board poster (Poster.uxml) from HiringCandidate data:
/// header strip (rank gem, first name, class, cost), portrait + epithet banner, headline stats,
/// trait chips and the trait-multiplier stat bars. Hire button state is HireController's job.
/// </summary>
public static class CandidateUIMapper
{
    // Adventurer bar rows (icon class suffix, tooltip): absolute values, see AdvValue / AdvRange.
    private static readonly (string icon, string tip)[] AdventurerBars =
        { ("damage", "Damage per second"), ("health", "Health"), ("reach", "Reach"), ("speed", "Move speed") };
    // Porter rows (stat, icon class suffix): trait multiplier vs the def's baseline.
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

        // Rank badge per tier; the portrait frame takes the tier tint above Wood so a Gold find pops
        root.Q<Label>("rank-level").text = rank.ToString();
        var badge = ClassCatalog.Instance != null ? ClassCatalog.Instance.RankBadge(rank) : null;
        if (badge != null) root.Q("rank-gem").style.backgroundImage = new StyleBackground(badge);
        if (rank >= 2)
        {
            Color tint = TierTint(rank);
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
        PopulateBars(root.Q("bars"), candidate, advDef != null, baseStats);
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

        // Hover tip with the trait's bonuses: one label, last child of the body so it draws over the bars
        var body = root.Q("poster-body");
        var tip = body.Q<Label>("trait-tip");
        if (tip == null)
        {
            tip = new Label { name = "trait-tip", pickingMode = PickingMode.Ignore };
            tip.AddToClassList("poster-trait-tip");
            body.Add(tip);
        }
        tip.style.display = DisplayStyle.None;

        if (candidate.traits != null)
            foreach (var t in candidate.traits)
            {
                var traitDef = TraitDatabase.GetTrait(t.traitId);
                if (traitDef == null) continue;
                var chip = new Label(traitDef.displayName);
                chip.AddToClassList("poster-trait");
                chip.EnableInClassList("poster-trait--cursed", candidate.IsCursed);
                string bonus = BonusText(traitDef, t.tier);
                if (bonus.Length > 0)
                {
                    chip.RegisterCallback<PointerEnterEvent>(_ =>
                    {
                        var r = body.WorldToLocal(chip.worldBound);
                        tip.text = bonus;
                        tip.style.left = r.xMin;
                        tip.style.top = r.yMax + 6f;
                        tip.style.display = DisplayStyle.Flex;
                        tip.BringToFront();
                    });
                    chip.RegisterCallback<PointerLeaveEvent>(_ => tip.style.display = DisplayStyle.None);
                }
                container.Add(chip);
            }

        container.style.display = container.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>One line per stat modifier, green when it helps, red when it hurts ("+30% Move Speed").</summary>
    private static string BonusText(TraitDef def, int tier)
    {
        if (tier < 1 || tier > def.tiers.Length || def.tiers[tier - 1].modifiers == null) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var mod in def.tiers[tier - 1].modifiers)
        {
            string delta = TraitUIHelper.FormatModifier(mod);
            bool lowerIsBetter = mod.stat == StatType.PickupTime || mod.stat == StatType.DepositTime;
            bool good = delta.StartsWith("+") != lowerIsBetter;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(good ? "<color=#3F7A2A>" : "<color=#A8322A>").Append(delta).Append(' ')
              .Append(TraitUIHelper.GetStatDisplayName(mod.stat)).Append("</color>");
        }
        return sb.ToString();
    }

    // Also drives the roster detail page (a hired unit's traits wrapped in a candidate). Styles: Components/StatBars.uss
    // Adventurers: absolute power on one fixed log scale shared by every class × tier, so Wood vs Bronze compare
    // directly (each tier ≈ 2 cells). Porters: 10 cells over 0.5→1.5 of the trait multiplier.
    public static void PopulateBars(VisualElement bars, HiringCandidate candidate, bool adventurer, BaseStats baseStats)
    {
        bars.Clear();
        var advDef = candidate.entityDef as AdventurerDef;

        if (adventurer && advDef != null)
        {
            for (int r = 0; r < AdventurerBars.Length; r++)
            {
                var (lo, hi) = AdvRange(r);
                AddBarRow(bars, AdventurerBars[r].icon, AdventurerBars[r].tip,
                          LogCells(AdvValue(r, advDef, baseStats, candidate), lo, hi));
            }
            return;
        }

        foreach (var (stat, icon) in PorterBars)
        {
            float m = Mathf.Clamp(Mathf.Round(BarMult(candidate, stat, baseStats) * 10f) / 10f, 0.5f, 1.5f); // whole cells
            int filled = 0;
            for (int k = 0; k < 10; k++) if (CellFilled(m, k)) filled++;
            AddBarRow(bars, icon, TraitUIHelper.GetStatDisplayName(stat), filled);
        }
    }

    private static void AddBarRow(VisualElement bars, string icon, string tip, int filled)
    {
        var row = new VisualElement();
        row.AddToClassList("bar-row");
        var iconEl = new VisualElement { tooltip = tip };
        iconEl.AddToClassList("bar-icon");
        iconEl.AddToClassList("stat-" + icon);
        row.Add(iconEl);

        var bar = new VisualElement();
        bar.AddToClassList("bar");
        for (int k = 0; k < 10; k++)
        {
            var cell = new VisualElement();
            cell.AddToClassList("bar-cell");
            if (k < filled) cell.AddToClassList("bar-cell--on");
            bar.Add(cell);
        }
        row.Add(bar);
        bars.Add(row);
    }

    /// <summary>Adventurer bar row value: 0 damage/s, 1 health, 2 reach, 3 move speed (class base × trait multipliers).</summary>
    private static float AdvValue(int row, AdventurerDef def, BaseStats b, HiringCandidate c) => row switch
    {
        0 => b.attackDamage * TraitMult(c, StatType.AttackDamage, b) / (b.attackSpeed * TraitMult(c, StatType.AttackSpeed, b)),
        1 => def.MaxHealth,
        2 => b.attackRange * TraitMult(c, StatType.AttackRange, b),
        _ => b.moveSpeed * TraitMult(c, StatType.MoveSpeed, b),
    };

    /// <summary>1–10 cells: the low end of the range still shows one cell, the high end all ten, log in between.</summary>
    private static int LogCells(float v, float lo, float hi) =>
        hi <= lo ? 5 : 1 + Mathf.Clamp(Mathf.RoundToInt(9f * Mathf.Log(Mathf.Max(v, lo) / lo) / Mathf.Log(hi / lo)), 0, 9);

    // Fixed per-row range over every ClassCatalog def × (no trait | each adventurer trait at tier 1), built once.
    // ponytail: single-trait extremes; a cursed pair can exceed them and just clamps. Rebuild if trait tiers start rolling higher.
    private static (float lo, float hi)[] advRanges;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRanges() => advRanges = null;

    private static (float lo, float hi) AdvRange(int row)
    {
        if (advRanges == null)
        {
            advRanges = new (float, float)[AdventurerBars.Length];
            for (int r = 0; r < advRanges.Length; r++) advRanges[r] = (float.MaxValue, 0f);

            var traits = TraitDatabase.GetTraitsForRole(TraitRole.Adventurer);
            var catalog = ClassCatalog.Instance;
            if (catalog != null)
                foreach (var def in catalog.defs)
                {
                    if (def == null) continue;
                    var b = BaseStats.FromEntityDef(def);
                    for (int t = -1; t < traits.Count; t++)
                    {
                        var c = new HiringCandidate { entityDef = def };
                        if (t >= 0) c.traits = new[] { new TraitInstance { traitId = traits[t].traitId, tier = 1 } };
                        for (int r = 0; r < advRanges.Length; r++)
                        {
                            float v = AdvValue(r, def, b, c);
                            advRanges[r] = (Mathf.Min(advRanges[r].lo, v), Mathf.Max(advRanges[r].hi, v));
                        }
                    }
                }
        }
        return advRanges[row];
    }

    /// <summary>Bar cell k (covering [0.5 + k/10, 0.6 + k/10]) is filled iff its upper edge ≤ m.</summary>
    public static bool CellFilled(float m, int k) => 0.6f + k * 0.1f <= m + 0.001f;
}
