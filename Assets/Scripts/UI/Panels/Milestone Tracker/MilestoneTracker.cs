using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Always-visible HUD milestone tracker.
// Expects milestone-tracker elements to already exist in the UIDocument
// (added manually via UXML or UI Builder). Just queries and drives them.
public class MilestoneTracker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UIDocument uiDocument;
    [Tooltip("Row template (MilestoneCard.uxml): torn-paper card with progress, reward and claim button.")]
    [SerializeField] private VisualTreeAsset cardTemplate;

    [Header("Settings")]
    [Tooltip("Seconds between live progress refreshes")]
    [SerializeField] private float refreshInterval = 1f;
    [SerializeField] private bool startExpanded = true;

    [Header("Claim Coin Shower")]
    [Tooltip("Seconds each coin takes from the claim button to the gold counter (unscaled)")]
    [SerializeField] private float coinFlightTime = 0.65f;
    [Tooltip("Seconds between coin departures")]
    [SerializeField] private float coinStagger = 0.04f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // Cached element refs
    private Label starLabel;
    private Label countLabel;
    private Label chevron;
    private Label claimBadge;
    private VisualElement body;
    private VisualElement roll;
    private ScrollView scroll;
    private VisualElement list;

    // State
    private bool isExpanded;
    private List<(StarMilestoneDef def, VisualElement row)> rows = new();
    private Coroutine refreshCoroutine;
    private Coroutine accordionCoroutine;
    private MilestoneProgressEvents progressEvents;

    // Coin shower: UI elements over the whole HUD; each banks its share on landing
    private const float CoinSize = 26f;
    private class Coin { public VisualElement el; public Vector2 from, kick; public float t0, wobble; public System.Action land; }
    private readonly List<Coin> coins = new();
    private VisualElement coinTarget;
    private IconPulse iconPulse;

    // ═════════════════════════════════════════════
    // LIFECYCLE
    // ═════════════════════════════════════════════

    void OnEnable()
    {
        if (uiDocument == null) return;
        var btn = uiDocument.rootVisualElement?.Q<Button>("mt-toggle-button");
        if (btn == null) return;
        btn.clicked += Toggle;
        Debug.Log("[MilestoneTracker] clicked registered");
    }

    void OnDisable()
    {
        // Coins can't fly while disabled — land them now, gold is never dropped
        foreach (var c in coins) { c.el.RemoveFromHierarchy(); c.land(); }
        coins.Clear();

        if (uiDocument == null) return;
        var btn = uiDocument.rootVisualElement?.Q<Button>("mt-toggle-button");
        if (btn != null) btn.clicked -= Toggle;
    }

    void Start()
    {
        if (!QueryElements()) return;

        progressEvents = new MilestoneProgressEvents(RefreshProgress, Refresh);
        progressEvents.Subscribe();

        Refresh();

        isExpanded = startExpanded;
        if (!isExpanded) SnapCollapsed();
        // The scroll view is absolute inside the clip, so the clip mirrors its height whenever open and idle.
        scroll.RegisterCallback<GeometryChangedEvent>(_ =>
        {
            if (isExpanded && accordionCoroutine == null) body.style.height = scroll.layout.height;
        });

        refreshCoroutine = StartCoroutine(LiveRefreshLoop());
    }

    void OnDestroy()
    {
        progressEvents?.Unsubscribe();

        if (refreshCoroutine != null)
            StopCoroutine(refreshCoroutine);
    }

    private void OnTogglePressed(PointerDownEvent evt)
    {
        Debug.Log("[MilestoneTracker] PointerDown received");
        Toggle();
    }

    // ═════════════════════════════════════════════
    // ELEMENT QUERY
    // ═════════════════════════════════════════════

    private bool QueryElements()
    {
        if (uiDocument == null)
        {
            Debug.LogError("[MilestoneTracker] UIDocument not assigned!");
            return false;
        }

        var root = uiDocument.rootVisualElement;

        starLabel  = root.Q<Label>("mt-star-label");
        countLabel = root.Q<Label>("mt-count-label");
        chevron    = root.Q<Label>("mt-chevron");
        claimBadge = root.Q<Label>("mt-claim-badge");
        body       = root.Q<VisualElement>("mt-body");
        roll       = root.Q<VisualElement>("mt-roll");
        scroll     = root.Q<ScrollView>("mt-scroll");
        list       = root.Q<VisualElement>("mt-list");
        coinTarget = root.Q<VisualElement>("coin-icon");
        iconPulse  = FindAnyObjectByType<IconPulse>();

        if (body == null || scroll == null || list == null || cardTemplate == null)
        {
            Debug.LogError("[MilestoneTracker] Required elements not found.");
            return false;
        }

        return true;
    }

    // ═════════════════════════════════════════════
    // REFRESH
    // ═════════════════════════════════════════════

    // Full rebuild: recreates all rows from current milestone state.
    private void Refresh()
    {
        if (list == null || ProgressionManager.Instance == null) return;

        list.Clear();
        rows.Clear();

        int currentStars = ProgressionManager.Instance.GetCurrentStars();
        int nextStar     = currentStars + 1;

        if (starLabel != null)
            starLabel.text = currentStars >= 5 ? "Max Stars!" : $"{nextStar}★ Progress";

        var milestones = currentStars >= 5
            ? new List<StarMilestoneDef>()
            : ProgressionManager.Instance.GetMilestonesForStar(nextStar);

        foreach (var m in milestones)
        {
            var row = CreateRow(m);
            list.Add(row);
            rows.Add((m, row));
        }

        RefreshProgress();
    }

    // Lightweight: updates bar values + claim state on existing rows without rebuilding DOM.
    private void RefreshProgress()
    {
        var pm = ProgressionManager.Instance;
        if (pm == null) return;

        int claimed = 0;
        foreach (var (def, row) in rows)
        {
            RefreshCard(row, def);
            if (pm.IsMilestoneClaimed(def)) claimed++;
        }

        // Claimable → in progress → claimed. Stable: rows stays in asset order, we only move DOM children.
        int slot = 0;
        for (int group = 0; group < 3; group++)
            foreach (var (def, row) in rows)
            {
                int g = pm.IsMilestoneClaimed(def) ? 2 : pm.IsMilestoneReached(def) ? 0 : 1;
                if (g != group) continue;
                if (list.IndexOf(row) != slot) list.Insert(slot, row);
                slot++;
            }

        UpdateCountLabel(claimed, rows.Count);

        int claimable = pm.ClaimableCount;
        if (claimBadge != null) claimBadge.text = claimable.ToString();
        UIManager.SetBoardBadge(claimBadge, claimable > 0, claimable > 0);
    }

    private void UpdateCountLabel(int completed, int total)
    {
        if (countLabel != null)
            countLabel.text = $"{completed}/{total}";
    }

    private IEnumerator LiveRefreshLoop()
    {
        var wait = new WaitForSeconds(refreshInterval);
        while (true)
        {
            yield return wait;
            RefreshProgress();
        }
    }

    // ═════════════════════════════════════════════
    // ROW CREATION
    // ═════════════════════════════════════════════

    // Clones the card; state classes/values are applied by RefreshProgress.
    private VisualElement CreateRow(StarMilestoneDef def)
    {
        var row = cardTemplate.CloneTree().Q<VisualElement>("card-root");
        BindCard(row, def, LaunchCoins);
        return row;
    }

    // ── Shared with the book's Guild page (GuildUpgradePanel): same card markup, same three states ──

    /// <summary>Binds static text + the claim button on a milestone card-root. coinFlight(source, coins, land)
    /// runs a UI coin shower (see LaunchCoins); null or false → ProgressionManager's world-burst fallback.</summary>
    public static void BindCard(VisualElement row, StarMilestoneDef def,
        System.Func<VisualElement, int, System.Action, bool> coinFlight)
    {
        row.Q<Label>("card-name").text = def.displayName;
        row.Q<Label>("card-desc").text = def.description;
        if (def.icon != null)
            row.Q<VisualElement>("card-icon").style.backgroundImage = new StyleBackground(def.icon);

        string reward = AnimatedNumberUITK.Abbrev(def.goldReward);
        row.Q<Label>("card-reward-label").text = $"Reward: {reward}";
        if (def.goldReward <= 0)   // inline only for "off" — USS hides it while claimable
            row.Q<VisualElement>("card-reward").style.display = DisplayStyle.None;

        var claimButton = row.Q<Button>("card-claim");
        row.Q<Label>("card-claim-amount").text = def.goldReward > 0 ? $"+{reward}" : "";
        claimButton.clicked += () =>
        {
            claimButton.SetEnabled(false);   // no double-claims; the row flips to claimed on the signal
            ProgressionManager.Instance?.TryClaimMilestone(def, BurstOrigin(row),
                coinFlight == null ? null : (count, land) => coinFlight(claimButton, count, land));
        };
    }

    /// <summary>Applies progress + in-progress / claimable / claimed state to a bound card-root.</summary>
    public static void RefreshCard(VisualElement row, StarMilestoneDef def)
    {
        var pm = ProgressionManager.Instance;
        if (pm == null) return;

        bool isClaimed = pm.IsMilestoneClaimed(def);
        bool reached   = pm.IsMilestoneReached(def);
        int  current   = reached ? def.targetValue : pm.GetMilestoneCurrentValue(def);
        float progress = reached ? 1f : pm.GetMilestoneProgress(def);

        row.EnableInClassList("mt-card--claimable", reached && !isClaimed);
        row.EnableInClassList("mt-card--claimed", isClaimed);
        row.Q<VisualElement>("progress-fill").style.width = Length.Percent(Mathf.Clamp01(progress) * 100f);
        row.Q<Label>("progress-label").text =
            $"{AnimatedNumberUITK.Abbrev(current)} / {AnimatedNumberUITK.Abbrev(def.targetValue)}";
        if (isClaimed && def.goldReward > 0)
            row.Q<Label>("card-reward-label").text = $"Reward: {AnimatedNumberUITK.Abbrev(def.goldReward)} - collected";
    }

    // Panel point → world point (z=0) just LEFT of the card: world sprites draw under UI Toolkit,
    // so a burst at the button itself would fly hidden behind the tracker column.
    private static Vector3 BurstOrigin(VisualElement element)
    {
        Camera cam = Camera.main;
        if (cam == null || element.panel == null) return Vector3.zero;

        Vector2 center = new Vector2(element.worldBound.xMin - 80f, element.worldBound.center.y);
        // Card may be half scrolled out of view — keep the origin inside the visible list.
        var view = element.GetFirstAncestorOfType<ScrollView>();
        if (view != null) center.y = Mathf.Clamp(center.y, view.worldBound.yMin, view.worldBound.yMax);
        Vector2 panelSize = element.panel.visualTree.layout.size;
        var screen = new Vector3(
            center.x / panelSize.x * Screen.width,
            (1f - center.y / panelSize.y) * Screen.height,
            -cam.transform.position.z);
        return cam.ScreenToWorldPoint(screen);
    }

    // ═════════════════════════════════════════════
    // COIN SHOWER
    // ═════════════════════════════════════════════

    // Called synchronously from the claim, before the row re-sorts/rebuilds — the button's bound is still live.
    // Public: the book's Guild page flies its claims through the same HUD shower (same UIDocument).
    public bool LaunchCoins(VisualElement source, int count, System.Action land)
    {
        var root = uiDocument != null ? uiDocument.rootVisualElement : null;
        if (!isActiveAndEnabled || coinTarget == null || root == null || source.panel != root.panel) return false;

        Vector2 origin = source.worldBound.center;
        // Card may be half scrolled out of view — burst from the visible part of its list
        var view = source.GetFirstAncestorOfType<ScrollView>();
        if (view != null) origin.y = Mathf.Clamp(origin.y, view.worldBound.yMin, view.worldBound.yMax);
        origin = root.WorldToLocal(origin);
        var sprite = coinTarget.resolvedStyle.backgroundImage;
        float now = Time.unscaledTime;

        for (int i = 0; i < count; i++)
        {
            var el = new VisualElement { pickingMode = PickingMode.Ignore };
            el.style.position = Position.Absolute;
            el.style.left = el.style.top = 0f;
            el.style.width = el.style.height = CoinSize;
            el.style.backgroundImage = new StyleBackground(sprite);
            el.style.visibility = Visibility.Hidden;   // until its staggered departure
            root.Add(el);                                // last child of the root → draws over everything

            coins.Add(new Coin
            {
                el = el,
                from = origin + Random.insideUnitCircle * 40f,
                kick = new Vector2(Random.Range(-170f, -30f), Random.Range(-50f, 70f)),   // spray out left, then swoop up
                t0 = now + i * coinStagger + Random.Range(0f, coinStagger),
                wobble = Random.Range(-25f, 25f),
                land = land,
            });
        }
        return true;
    }

    void Update()
    {
        if (coins.Count == 0) return;

        var root = uiDocument.rootVisualElement;
        Vector2 to = root.WorldToLocal(coinTarget.worldBound.center);
        float now = Time.unscaledTime;

        for (int i = coins.Count - 1; i >= 0; i--)
        {
            var c = coins[i];
            float t = (now - c.t0) / coinFlightTime;
            if (t < 0f) continue;
            if (t >= 1f)
            {
                c.el.RemoveFromHierarchy();
                coins.RemoveAt(i);
                c.land();
                iconPulse?.Pulse();
                continue;
            }

            // Quadratic bezier from → (from + kick) → counter, eased in so coins accelerate into the counter
            float u = t * t * (3f - 2f * t);
            Vector2 ctrl = c.from + c.kick;
            Vector2 p = Vector2.Lerp(Vector2.Lerp(c.from, ctrl, u), Vector2.Lerp(ctrl, to, u), u);
            // Pop 0.3→1.3→1 over the first 25%, shrink to 0.55 over the last 30%
            float s = t < 0.12f ? Mathf.Lerp(0.3f, 1.3f, t / 0.12f)
                    : t < 0.25f ? Mathf.Lerp(1.3f, 1f, (t - 0.12f) / 0.13f)
                    : t > 0.7f  ? Mathf.Lerp(1f, 0.55f, (t - 0.7f) / 0.3f)
                    : 1f;

            c.el.style.visibility = Visibility.Visible;
            c.el.style.translate = new Translate(p.x - CoinSize * 0.5f, p.y - CoinSize * 0.5f);
            c.el.style.scale = new Scale(new Vector2(s, s));
            c.el.style.rotate = new Rotate(Mathf.Sin(t * 14f) * c.wobble);
        }
    }

    // ═════════════════════════════════════════════
    // ACCORDION
    // ═════════════════════════════════════════════

    // Collapsed = header + roll only. visibility (not display) keeps the scroll view laid out so its height is known.
    private void SnapCollapsed()
    {
        body.style.height = 0f;
        body.style.visibility = Visibility.Hidden;
        chevron.EnableInClassList("mt-chevron--collapsed", true);
        roll?.EnableInClassList("mt-roll--collapsed", true);
    }

    private void Toggle()
    {
        isExpanded = !isExpanded;

        Debug.Log($"[MilestoneTracker] Toggle → isExpanded={isExpanded}");

        if (accordionCoroutine != null)
            StopCoroutine(accordionCoroutine);

        accordionCoroutine = StartCoroutine(AnimateAccordion(isExpanded));
        chevron.EnableInClassList("mt-chevron--collapsed", !isExpanded);
        roll?.EnableInClassList("mt-roll--collapsed", !isExpanded);
    }

    // Scroll unroll: the clip grows 0 → scroll-view height (roll rides its bottom edge), or rolls back up.
    private IEnumerator AnimateAccordion(bool expand)
    {
        body.style.visibility = Visibility.Visible;
        float from = body.layout.height;
        float to = expand ? scroll.layout.height : 0f;
        float duration = expand ? 0.35f : 0.25f;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / duration)
        {
            body.style.height = Mathf.SmoothStep(from, to, t);
            yield return null;
        }

        if (expand) body.style.height = scroll.layout.height;   // then the GeometryChanged mirror takes over
        else SnapCollapsed();
        accordionCoroutine = null;
    }
}
