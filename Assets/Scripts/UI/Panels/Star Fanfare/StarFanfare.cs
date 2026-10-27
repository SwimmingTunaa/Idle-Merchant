using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Guild Star fanfare (StarFanfare.uxml, last child of main-root in MainUI.uxml) — a ~5 s ceremony on GameSignals.OnStarEarned:
/// vignette, ribbon slam + sparks, a big star that flies into the pennant's slot (pop + flag wiggle), unlock cards stamped
/// in, then a fold into the tracker's star card (MilestoneTracker.RevealStarRow). Nothing pauses; the host never picks,
/// only the ribbon and stamped cards do (click = skip to the fold). Spec: STAR_FANFARE_DESIGN.md, mock: scratchpad/starmock.
/// Every frame is computed from unscaled time in Update (the MilestoneTracker.Coin pattern), so time scale 3 looks identical.
/// </summary>
public class StarFanfare : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private bool showDebugLogs = false;

    // Timeline (seconds from fire), from the mock
    private const float ClaimDelay = 0.4f;     // a claim's coin shower lands first
    private const float VigIn = 0.35f;
    private const float SlamAt = 0.2f, SlamDur = 0.5f;
    private const float StarInAt = 0.45f, StarInDur = 0.45f;
    private const float FlyAt = 0.9f, FlyDur = 0.55f;
    private const float PopAt = 1.4f, PopDur = 0.55f;
    private const float CardAt = 1.3f, CardStep = 0.3f, CardDur = 0.32f;
    private const float HintAt = 2.6f, HintDur = 0.3f;
    private const float AutoFoldAt = 4.6f;
    private const float RibbonFoldDur = 0.5f, CardFoldDur = 0.35f, RevealAfter = 0.35f, VigOut = 0.35f, DoneAfter = 0.9f;
    private const float SparkDur = 0.9f;
    private const long FlagStepMs = 220;      // UIManager.WiggleOnce: 4 tilts ≈ 0.9 s

    private static readonly string[] Ordinals = { "First", "Second", "Third", "Fourth", "Fifth" };
    private static readonly string[] Ordinal = { "1st", "2nd", "3rd", "4th", "5th" };
    /// <summary>Label on the star count (1★ Founded … 5★ Legendary); reused later by the hall crest.</summary>
    public static readonly string[] RankWords = { "Founded", "Known", "Established", "Renowned", "Legendary" };
    private static readonly Color[] SparkColors =
    {
        new Color32(0xFF, 0xE0, 0x7A, 0xFF), new Color32(0xF0, 0xB4, 0x29, 0xFF),
        new Color32(0xFF, 0xF4, 0xC8, 0xFF), new Color32(0xFF, 0x8A, 0x3A, 0xFF),
    };

    /// <summary>True from fire until the fold hands over to the tracker (which hides its star card meanwhile).</summary>
    public static bool Playing { get; private set; }

    private static StarFanfare instance;

    private VisualElement host, vignette, bigStar, ribbon, cardRow;
    private Label sub, hint;
    private readonly List<VisualElement> cards = new();

    private int star;                  // playing star, 0 = idle
    private int pendingStar;           // queued by a claim, fires at pendingAt
    private float pendingAt, t0, prevT, foldAt;
    private bool revealed;

    private class Spark { public VisualElement el; public Vector2 from, to; public float t0; }
    private readonly List<Spark> sparks = new();

    // ═════════════════════════════════════════════
    // LIFECYCLE
    // ═════════════════════════════════════════════

    void Awake()
    {
        instance = this;
        if (uiDocument == null) uiDocument = GetComponentInParent<UIDocument>();
    }

    void OnEnable()
    {
        GameSignals.OnStarEarned += OnStarEarned;
    }

    void OnDisable()
    {
        GameSignals.OnStarEarned -= OnStarEarned;
        if (star != 0) Finish();
        Playing = false;
    }

    void Start()
    {
        var root = uiDocument != null ? uiDocument.rootVisualElement : null;
        host = root?.Q<VisualElement>("star-fanfare");
        if (host == null)
        {
            Debug.LogError("[StarFanfare] 'star-fanfare' not found — is StarFanfare instanced in MainUI.uxml?");
            return;
        }
        if (host.parent != null) host.parent.pickingMode = PickingMode.Ignore;   // full-screen TemplateContainer
        vignette = host.Q("sf-vignette");
        bigStar = host.Q("sf-bigstar");
        ribbon = host.Q("sf-ribbon");
        cardRow = host.Q("sf-cards");
        sub = host.Q<Label>("sf-sub");
        hint = host.Q<Label>("sf-hint");
        host.style.display = DisplayStyle.None;
        // Only the ribbon and stamped cards pick; their clicks bubble here
        host.RegisterCallback<ClickEvent>(_ => { if (star != 0 && foldAt == float.MaxValue) Fold(Time.unscaledTime - t0); });
    }

    // The claim already filled the pennant (UIManager); empty it until the star lands, then fire after the coins
    private void OnStarEarned(int s)
    {
        if (host == null || s < 1 || s > 5) return;
        SetSlot(s, false);
        Queue(s);
        pendingAt = Time.unscaledTime + ClaimDelay;
    }

    /// <summary>Game Debugger: replay any star's fanfare now (nothing is awarded; the tracker card is set like a real one).</summary>
    public static void PlayDebug(int s)
    {
        if (instance == null || instance.host == null || s < 1 || s > 5) return;
        instance.Queue(s);
        instance.pendingAt = Time.unscaledTime;
    }

    private void Queue(int s)
    {
        if (star != 0) Finish();
        pendingStar = s;
        Playing = true;
        if (ProgressionManager.Instance != null) ProgressionManager.Instance.unseenStar = s;   // quit mid-fanfare → card on load
    }

    // ═════════════════════════════════════════════
    // UNLOCK LIST (fire time, real data; the tracker's summary reads the same list)
    // ═════════════════════════════════════════════

    public struct Unlock { public string tag, title, brief, iconClass; public Sprite icon; }

    /// <summary>What a star opened, in card order; an entry is skipped when its source is empty. Phase 1: no GUILD HALL.</summary>
    public static List<Unlock> Unlocks(int s)
    {
        var list = new List<Unlock>();
        int depth = s + 1;   // AwardStar unlocks depth star + 1
        string depthName = RosterPanelController.GetLayerName(depth);
        list.Add(new Unlock { tag = "NEW DEPTH", title = $"{depthName} opened", brief = depthName, iconClass = "sf-ic--depth" });

        // Tier N classes join the hire board when depth N opens (ClassCatalog: rank <= maxUnlockedLayer)
        var catalog = ClassCatalog.Instance;
        var tierDef = catalog == null ? null
            : catalog.defs.Find(d => d != null && d.rank == depth && d.classIcon != null)
              ?? catalog.defs.Find(d => d != null && d.rank == depth);
        if (tierDef != null)
        {
            string tier = AdventurerRank.Name(depth);
            list.Add(new Unlock { tag = "HIRING", title = $"{tier} classes on the board", brief = $"{tier} classes", icon = tierDef.classIcon });
        }

        if (s >= 1 && s <= ProgressionManager.StarSeals.Length)
        {
            int seals = ProgressionManager.StarSeals[s - 1];
            list.Add(new Unlock { tag = "SEALS", title = $"+{seals} Guild Seals", brief = $"+{seals} seals", iconClass = "btn-seal" });
        }

        // Star rewards grant upgrades/recipes, not gold — the card names the reward instead of "+N gold"
        var reward = ProgressionManager.Instance != null ? ProgressionManager.Instance.GetStarReward(s) : null;
        if (reward != null && !string.IsNullOrEmpty(reward.rewardTitle)
            && (reward.upgradesGranted?.Length > 0 || reward.upgradesAvailableForPurchase?.Length > 0 || reward.recipesUnlocked?.Length > 0))
            list.Add(new Unlock { tag = "REWARD", title = reward.rewardTitle, brief = reward.rewardTitle,
                                  icon = reward.rewardIcon, iconClass = reward.rewardIcon == null ? "btn-coin" : null });
        return list;
    }

    /// <summary>Tracker card: "★ 3rd Guild Star earned" + "Deep Caverns · Bronze classes · +80 seals".</summary>
    public static string Headline(int s) => $"★ {Ordinal[Mathf.Clamp(s, 1, 5) - 1]} Guild Star earned";

    public static string Summary(int s)
    {
        var parts = new List<string>();
        foreach (var u in Unlocks(s)) parts.Add(u.brief);
        return string.Join(" · ", parts);
    }

    // ═════════════════════════════════════════════
    // PLAY
    // ═════════════════════════════════════════════

    private void Begin(int s)
    {
        star = s;
        t0 = Time.unscaledTime;
        prevT = -1f;
        foldAt = float.MaxValue;
        revealed = false;
        sub.text = $"{Ordinals[s - 1]} of five · the guild is {RankWords[s - 1]}";

        cardRow.Clear();
        cards.Clear();
        foreach (var u in Unlocks(s))
        {
            if (cards.Count == 4) break;   // phase 1 has at most 4 (GUILD HALL lands in phase 2)
            var card = new VisualElement { pickingMode = PickingMode.Ignore };   // picks once stamped
            card.AddToClassList("sf-card");
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("sf-card__icon");
            if (u.iconClass != null) icon.AddToClassList(u.iconClass);
            if (u.icon != null) icon.style.backgroundImage = new StyleBackground(u.icon);
            card.Add(icon);
            var title = new Label(u.title) { pickingMode = PickingMode.Ignore };
            title.AddToClassList("sf-card__title");
            card.Add(title);
            var tag = new Label(u.tag) { pickingMode = PickingMode.Ignore };
            tag.AddToClassList("sf-card__tag");
            card.Add(tag);   // last, so it draws over the paper edge
            card.style.opacity = 0f;
            cardRow.Add(card);
            cards.Add(card);
        }

        ribbon.pickingMode = PickingMode.Ignore;
        vignette.style.opacity = 0f;
        ribbon.style.opacity = 0f;
        bigStar.style.opacity = 0f;
        hint.style.opacity = 0f;
        host.style.display = DisplayStyle.Flex;
        if (showDebugLogs) Debug.Log($"[StarFanfare] {s}★ — {cards.Count} cards");
    }

    /// <summary>Skip or the 4.6 s mark: ribbon and cards fly into the tracker; the star card slides in behind them.</summary>
    private void Fold(float at)
    {
        foldAt = at;
        ribbon.pickingMode = PickingMode.Ignore;
        foreach (var c in cards) c.pickingMode = PickingMode.Ignore;
        if (at < PopAt) SetSlot(star, true);   // skipped before the star landed
    }

    private void Finish()
    {
        foreach (var sp in sparks) sp.el.RemoveFromHierarchy();
        sparks.Clear();
        if (star != 0) SetSlot(star, ProgressionManager.Instance == null || star <= ProgressionManager.Instance.GetCurrentStars());
        star = 0;
        if (host != null) host.style.display = DisplayStyle.None;
    }

    void Update()
    {
        if (pendingStar != 0 && Time.unscaledTime >= pendingAt)
        {
            Begin(pendingStar);
            pendingStar = 0;
        }
        if (star == 0) return;

        float t = Time.unscaledTime - t0;
        if (t >= AutoFoldAt && foldAt == float.MaxValue) Fold(AutoFoldAt);
        float tf = foldAt;

        // Vignette in at 0, out with the tracker card
        vignette.style.opacity = Seg(t, 0f, VigIn) * (1f - Seg(t, tf + RevealAfter, VigOut));

        // Ribbon slam: scale 2.2 → .96 (55%) → 1, drops 40px, fades in over the first leg
        if (Crossed(t, SlamAt) && SlamAt < tf)
        {
            ribbon.pickingMode = PickingMode.Position;
            Burst(Center(ribbon), 28);
        }
        float u = Seg(t, SlamAt, SlamDur);
        float a = Ease(Mathf.Clamp01(u / 0.55f));
        float s = u < 0.55f ? Mathf.Lerp(2.2f, 0.96f, a) : Mathf.Lerp(0.96f, 1f, Smooth((u - 0.55f) / 0.45f));
        Vector2 off = new Vector2(0f, Mathf.Lerp(-40f, 0f, a));
        // Fold: shrink to .25 and fly at the tracker with a small wind-up (ease-in-back)
        float f = InBack(Seg(t, tf, RibbonFoldDur));
        off += (TrackerCenter() - Center(ribbon)) * f;
        Apply(ribbon, off, s * Mathf.Lerp(1f, 0.25f, f), 0f, a * (1f - Seg(t, tf, RibbonFoldDur)));

        // Big star: spins in (0 → 1, -180° → 0, overshoot), then flies to the pennant slot shrinking to 32px and fading
        float sIn = Seg(t, StarInAt, StarInDur), sFly = Smooth(Seg(t, FlyAt, FlyDur));
        var slot = Slot(star);
        Vector2 fly = slot != null ? (slot.worldBound.center - Center(bigStar)) * sFly : Vector2.zero;
        float starAlpha = t < StarInAt || sFly >= 1f ? 0f : Ease(sIn) * (1f - sFly) * (1f - Seg(t, tf, CardFoldDur));
        Apply(bigStar, fly, OutBack(sIn) * Mathf.Lerp(1f, 1f / 3f, sFly), -180f * (1f - Ease(sIn)), starAlpha);

        // Star lands: the slot fills with a pop (.3 → 1.45 → 1), the flag swings, 14 sparks
        if (Crossed(t, PopAt) && PopAt < tf && slot != null)
        {
            SetSlot(star, true);
            UIManager.WiggleOnce(Flag(), FlagStepMs);
            Burst(slot.worldBound.center, 14);
        }
        if (slot != null && t >= PopAt && foldAt >= PopAt)
        {
            float p = Seg(t, PopAt, PopDur);
            float ps = p < 0.6f ? Mathf.Lerp(0.3f, 1.45f, Ease(p / 0.6f)) : Mathf.Lerp(1.45f, 1f, Smooth((p - 0.6f) / 0.4f));
            slot.style.scale = p >= 1f ? new StyleScale(StyleKeyword.Null) : new StyleScale(new Scale(new Vector2(ps, ps)));
        }

        // Cards stamp in one by one: 1.8 / -6° → .94 / 1° (60%) → 1 / 0°, then fold into the tracker
        for (int i = 0; i < cards.Count; i++)
        {
            float at = CardAt + i * CardStep;
            var card = cards[i];
            if (at >= tf) { card.style.opacity = 0f; continue; }   // never stamped before the skip
            if (Crossed(t, at)) card.pickingMode = PickingMode.Position;
            float c = Seg(t, at, CardDur);
            float k = Ease(Mathf.Clamp01(c / 0.6f));
            float cs = c < 0.6f ? Mathf.Lerp(1.8f, 0.94f, k) : Mathf.Lerp(0.94f, 1f, Smooth((c - 0.6f) / 0.4f));
            float cr = c < 0.6f ? Mathf.Lerp(-6f, 1f, k) : Mathf.Lerp(1f, 0f, Smooth((c - 0.6f) / 0.4f));
            float cf = Seg(t, tf, CardFoldDur);
            cf *= cf;
            Apply(card, (TrackerCenter() - Center(card)) * cf, cs * Mathf.Lerp(1f, 0.3f, cf), cr, k * (1f - cf));
        }

        hint.style.opacity = Seg(t, HintAt, HintDur) * (1f - Seg(t, tf, 0.15f));

        UpdateSparks();

        if (!revealed && t >= tf + RevealAfter)
        {
            revealed = true;
            Playing = false;
            FindAnyObjectByType<MilestoneTracker>()?.RevealStarRow();
        }
        if (t >= tf + DoneAfter && sparks.Count == 0) Finish();
        prevT = t;
    }

    // ═════════════════════════════════════════════
    // SPARKS (scheduler-free lerp, like MilestoneTracker.Coin)
    // ═════════════════════════════════════════════

    // n 10px squares from a panel point: random angle, 120–380 px, y squashed ×.6
    private void Burst(Vector2 worldPoint, int n)
    {
        Vector2 from = host.WorldToLocal(worldPoint);
        float now = Time.unscaledTime;
        for (int i = 0; i < n; i++)
        {
            var el = new VisualElement { pickingMode = PickingMode.Ignore };
            el.AddToClassList("sf-spark");
            el.style.backgroundColor = SparkColors[i % SparkColors.Length];
            host.Add(el);
            float ang = Random.value * Mathf.PI * 2f, dist = Random.Range(120f, 380f);
            sparks.Add(new Spark { el = el, from = from, to = from + new Vector2(Mathf.Cos(ang) * dist, Mathf.Sin(ang) * dist * 0.6f), t0 = now });
        }
    }

    private void UpdateSparks()
    {
        float now = Time.unscaledTime;
        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            var sp = sparks[i];
            float t = (now - sp.t0) / SparkDur;
            if (t >= 1f)
            {
                sp.el.RemoveFromHierarchy();
                sparks.RemoveAt(i);
                continue;
            }
            float e = Ease(t);
            Vector2 p = Vector2.Lerp(sp.from, sp.to, e) - new Vector2(5f, 5f);
            float s = Mathf.Lerp(1.3f, 0.2f, e);
            sp.el.style.translate = new Translate(p.x, p.y);
            sp.el.style.scale = new Scale(new Vector2(s, s));
            sp.el.style.opacity = 1f - e;
        }
    }

    // ═════════════════════════════════════════════
    // HELPERS
    // ═════════════════════════════════════════════

    private bool Crossed(float t, float at) => prevT < at && t >= at;
    private static float Seg(float t, float start, float dur) => Mathf.Clamp01((t - start) / dur);
    private static float Ease(float x) => 1f - (1f - x) * (1f - x) * (1f - x);        // ease-out cubic
    private static float Smooth(float x) => x * x * (3f - 2f * x);                    // ease-in-out
    private static float OutBack(float x) { float y = x - 1f; return 1f + 2.70158f * y * y * y + 1.70158f * y * y; }
    private static float InBack(float x) => x * x * (2.70158f * x - 1.70158f);

    // Layout centre in panel space — ignores this element's own translate/scale, so lerps never feed back
    private static Vector2 Center(VisualElement e) => e.parent.LocalToWorld(e.layout.center);

    private static void Apply(VisualElement e, Vector2 offset, float scale, float rot, float alpha)
    {
        e.style.translate = new Translate(offset.x, offset.y);
        e.style.scale = new Scale(new Vector2(scale, scale));
        e.style.rotate = new Rotate(rot);
        e.style.opacity = alpha;
    }

    private Vector2 TrackerCenter()
    {
        var tracker = uiDocument.rootVisualElement.Q("milestone-tracker");
        return tracker != null ? tracker.worldBound.center : new Vector2(host.worldBound.xMax, host.worldBound.yMin);
    }

    private VisualElement Slot(int s) => uiDocument.rootVisualElement.Q("stars-container")?.Q($"star-{s}");
    private VisualElement Flag() => uiDocument.rootVisualElement.Q("guild-button");

    private void SetSlot(int s, bool filled)
    {
        var slot = Slot(s);
        if (slot == null) return;
        slot.EnableInClassList("star-filled", filled);
        slot.EnableInClassList("star-empty", !filled);
        if (!filled) slot.style.scale = StyleKeyword.Null;
    }
}
