using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections;
using System.Collections.Generic;

// Hire panel controller - the hiring board: a cork notice board with one pinned poster (Poster.uxml) per candidate.
// Posters sit in one pile; ▶ / tack = slide the front off to the back, ◀ = back poster slides in, Hire = tear off (HIRING_BOARD_DESIGN.md).
// Candidates have a rank (intrinsic quality), players choose which layer to deploy them to.
public class HireController : BasePanelController
{
    public override string PanelID => "HirePanel";

    [Header("UXML References")]
    [SerializeField] private VisualTreeAsset candidateSlotAsset;

    [Header("Manager References")]
    [SerializeField] private List<AdventurerManager> adventurerManagers = new();
    [SerializeField] private List<PorterManager> porterManagers = new();

    [Header("Refresh Settings")]
    [SerializeField] private float refreshIntervalSeconds = 300f;
    [SerializeField][Range(0f, 1f)] private float traitChance = 0.7f;

    [Header("Roster Settings")]
    [SerializeField] private int maxRosterSize = 5;
    [Tooltip("Porter weight per pool layer (index 0 = layer 1). Adventurer tier odds live on Resources/ClassCatalog.")]
    [SerializeField] private float[] rankWeights = { 40f, 25f, 20f, 10f, 5f };

    [Header("Top-Bar Sign")]
    [Tooltip("Stopwatch frames shown on the hiring sign before pools refresh (full → empty).")]
    [SerializeField] private Sprite[] stopwatchFrames;
    [Tooltip("Stopwatch appears when the next pool refresh is within this many seconds")]
    [SerializeField] private float stopwatchShowSeconds = 120f;

    [Header("Hire Stamp")]
    [Tooltip("Stamp hand slam (Book UI Stamp.png frames, same as the Crafting page); the HIRED badge shows on its impact frame.")]
    [SerializeField] private FrameAnim stampSlamAnim = new() { frameMs = 40, fadeMs = 150, end = UIFrameAnimator.EndBehaviour.HideAndReset };

    // Board pile: poster i (0 = front) at left 60 + 4i, top 16 + 4i, persistent tilt in ±1.5°
    private const float PileLeft = 60f, PileTop = 16f, PileStep = 4f, PosterTiltMax = 1.5f;
    private const float SlideSeconds = 0.28f; // matches .poster-card in HirePanel.uss
    private const float TearSeconds = 0.35f; // matches .poster-card--torn in HirePanel.uss
    private const float SlamPerPosterWidth = 2.2f; // stamp overlay size vs poster width (face ≈ poster wide)
    private const float StampHoldSeconds = 0.4f;   // HIRED stays readable before the tear-off

    // UI Elements
    private VisualElement stackContainer;
    private VisualElement scrapLayer;
    private VisualElement slamOverlay;
    private Label noticeLabel;
    private Label emptyTimerLabel;
    private int shownTimerSeconds = -1;
    private TabView tabView;

    // Layer selector UI
    private Button layerPrevButton;
    private Button layerNextButton;
    private Label layerLabel;
    private int selectedLayer = 1;

    // Tabs
    private enum TabType { Adventurers = 0, Porters = 1 }
    private TabType currentTab = TabType.Adventurers;

    // Pools
    private readonly Dictionary<(HireRole role, int rank, EntityDef def), CandidatePool> candidatePools = new();
    private readonly List<CandidatePool> adventurerPools = new();
    private readonly List<CandidatePool> porterPools = new();

    // Roster + cards
    private List<HiringCandidate> adventurerRoster = new();
    private List<HiringCandidate> porterRoster = new();
    // The dealt posters persist until the stock changes (pool restock, favour, rush order, new depth);
    // hiring only removes. Flushed in Update while open, or on the next Open.
    private bool rosterDirty = true;
    private List<VisualElement> adventurerCards = new();
    private List<VisualElement> porterCards = new();

    private readonly Dictionary<HiringCandidate, CandidatePool> candidateToPool = new();
    private readonly Dictionary<VisualElement, float> rotationByCard = new();

    private bool isHireAnimating;
    private bool isSliding;

    // Newest tier's odds boost after its layer unlocks (ClassCatalog.unlockBoost / unlockBoostSeconds).
    // ponytail: not saved — quitting inside the window drops the rest of the boost.
    private int boostTier;
    private float boostUntil;

    // Top-bar sign: badge = coin (can afford a hire, wiggles) > "!" (new stock while closed); stopwatch = refresh countdown
    private Label hiringBadge;
    private VisualElement hiringStopwatch;
    private bool hasNewStock;
    private bool affordPending = true;

    private List<HiringCandidate> CurrentRoster =>
        currentTab == TabType.Adventurers ? adventurerRoster : porterRoster;

    private List<VisualElement> CurrentCards =>
        currentTab == TabType.Adventurers ? adventurerCards : porterCards;

    private List<CandidatePool> CurrentPools =>
        currentTab == TabType.Adventurers ? adventurerPools : porterPools;

    // ═════════════════════════════════════════════
    // LIFECYCLE
    // ═════════════════════════════════════════════

    void OnEnable()
    {
        GameSignals.OnLayerUnlocked += OnLayerUnlocked;
        GameSignals.GoldChanged += OnGoldChanged;
    }

    void OnDisable()
    {
        GameSignals.OnLayerUnlocked -= OnLayerUnlocked;
        GameSignals.GoldChanged -= OnGoldChanged;
    }

    private void OnGoldChanged(int total)
    {
        affordPending = true;
        UpdateHireButtonState();
    }

    void Awake()
    {
        hideFlags = HideFlags.HideInInspector;
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();
    }

    protected override void Start()
    {
        BuildUI();
        InitializeCandidatePools();
        base.Start(); // Registers with UIManager
    }

    void Update()
    {
        // Update candidate pools; badge the top-bar sign when fresh candidates
        // arrive while the player isn't looking (cleared on open).
        bool poolRefreshed = false;
        foreach (var pool in candidatePools.Values)
            poolRefreshed |= pool.Update(Time.deltaTime);

        if (poolRefreshed)
            OnPoolsChanged();

        if (rosterDirty && State == PanelState.Open && !isHireAnimating && !isSliding)
        {
            rosterDirty = false;
            RebuildRosters();
            BuildStack();
        }

        if (affordPending)
        {
            affordPending = false;
            RefreshHiringBadge();
        }

        if (State == PanelState.Open)
            UpdateEmptyTimer();
    }

    // ═════════════════════════════════════════════
    // OPEN AT SPECIFIC LAYER (called from Roster empty slots)
    // ═════════════════════════════════════════════

    public void OpenAtLayer(int layerIndex)
    {
        int maxLayer = ProgressionManager.Instance != null ? ProgressionManager.Instance.maxUnlockedLayer : 1;
        selectedLayer = Mathf.Clamp(layerIndex, 1, maxLayer);
        UIManager.Instance.OpenPanel(PanelID);
    }

    // ═════════════════════════════════════════════
    // OVERRIDE OPEN FOR ROSTER REBUILD
    // ═════════════════════════════════════════════

    public override bool Open()
    {
        if (State != PanelState.Closed)
            return false;

        State = PanelState.Opening;
        if (rosterDirty)
        {
            rosterDirty = false;
            RebuildRosters();
        }
        BuildStack();
        UpdateLayerSelector();

        OnOpenStart();
        StartCoroutine(OpenAnimation());
        return true;
    }

    // ═════════════════════════════════════════════
    // TOP-BAR SIGN STATE (hiring-board-button affordances)
    // ═════════════════════════════════════════════

    protected override void OnOpenStart()
    {
        base.OnOpenStart();
        SetSignOpenState(true);
        SetHiringBadge(false); // player is looking — clear the attention badge
    }

    protected override void OnCloseStart()
    {
        base.OnCloseStart();
        SetSignOpenState(false);
    }

    // Keeps the top-bar hanging sign visually "pressed" while the panel is open.
    private void SetSignOpenState(bool open)
    {
        uiDocument?.rootVisualElement?.Q<Button>("hiring-board-button")
            ?.EnableInClassList("hiring-menu-button--open", open);
    }

    /// <summary>
    /// Shows/hides the new-candidates ("!") badge on the top-bar hiring sign.
    /// Cleared automatically when the panel opens.
    /// </summary>
    public void SetHiringBadge(bool visible)
    {
        hasNewStock = visible;
        RefreshHiringBadge();
        UpdateStopwatch();
    }

    // Coin look + wiggle when gold covers the cheapest candidate in any pool; else "!" while new stock is unseen.
    private void RefreshHiringBadge()
    {
        if (hiringBadge == null)
            return;

        int minCost = int.MaxValue;
        foreach (var pool in candidatePools.Values)
            minCost = Mathf.Min(minCost, pool.MinHireCost);

        bool affordable = minCost != int.MaxValue && Inventory.Instance != null && Inventory.Instance.CanAfford(minCost);
        hiringBadge.EnableInClassList("hiring-badge--coin", affordable);
        hiringBadge.text = affordable ? "" : "!";
        UIManager.SetBoardBadge(hiringBadge, affordable || hasNewStock, affordable);
    }

    // Once per second: stopwatch drains over the final stopwatchShowSeconds of the (synced) pool refresh cycle.
    private void UpdateStopwatch()
    {
        if (hiringStopwatch == null)
            return;

        float remaining = float.MaxValue;
        foreach (var pool in candidatePools.Values)
            remaining = Mathf.Min(remaining, pool.GetTimeUntilRefresh());

        bool show = !hasNewStock && remaining <= stopwatchShowSeconds && stopwatchFrames != null && stopwatchFrames.Length > 0;
        hiringStopwatch.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        if (!show)
            return;

        int frame = Mathf.Clamp((int)(stopwatchFrames.Length * (stopwatchShowSeconds - remaining) / stopwatchShowSeconds), 0, stopwatchFrames.Length - 1);
        hiringStopwatch.style.backgroundImage = new StyleBackground(stopwatchFrames[frame]);
    }

    // ═════════════════════════════════════════════
    // OVERRIDE CANCLOSE TO PREVENT CLOSE DURING HIRE
    // ═════════════════════════════════════════════

    public override bool CanClose()
    {
        return !isHireAnimating;
    }

    // ═════════════════════════════════════════════
    // OVERRIDE ANIMATIONS (Simple fade for now)
    // ═════════════════════════════════════════════

    protected override IEnumerator OpenAnimation()
    {
        if (RootElement == null)
        {
            State = PanelState.Open;
            InvokeOnOpenComplete();
            yield break;
        }

        if (panel.parent != null) panel.parent.style.display = DisplayStyle.Flex;
        panel.style.display = DisplayStyle.Flex;
        if (hasOverlay && overlayElement != null)
            overlayElement.style.display = DisplayStyle.Flex;

        float elapsed = 0f;
        while (elapsed < openCloseDuration)
        {
            elapsed += GetDeltaTime();
            float t = Mathf.Clamp01(elapsed / openCloseDuration);

            panel.style.opacity = t;
            if (hasOverlay && overlayElement != null)
                overlayElement.style.opacity = t * overlayColor.a;

            yield return null;
        }

        panel.style.opacity = 1f;
        if (hasOverlay && overlayElement != null)
            overlayElement.style.opacity = overlayColor.a;

        State = PanelState.Open;
        InvokeOnOpenComplete();

        if (showDebugLogs)
            Debug.Log("[HirePanel] Open animation complete");
    }

    protected override IEnumerator CloseAnimation()
    {
        if (RootElement == null)
        {
            State = PanelState.Closed;
            InvokeOnCloseComplete();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < openCloseDuration)
        {
            elapsed += GetDeltaTime();
            float t = 1f - Mathf.Clamp01(elapsed / openCloseDuration);
            
            panel.style.opacity = t;
            if (hasOverlay && overlayElement != null)
                overlayElement.style.opacity = t * overlayColor.a;
            
            yield return null;
        }

        panel.style.opacity = 0f;
        panel.style.display = DisplayStyle.None;
        if (panel.parent != null) panel.parent.style.display = DisplayStyle.None;
        if (hasOverlay && overlayElement != null)
        {
            overlayElement.style.opacity = 0f;
            overlayElement.style.display = DisplayStyle.None;
        }

        State = PanelState.Closed;
        InvokeOnCloseComplete();

        if (showDebugLogs)
            Debug.Log("[HirePanel] Close animation complete");
    }

    // ═════════════════════════════════════════════
    // UI SETUP
    // ═════════════════════════════════════════════

    protected override void BuildUI()
    {
        if (uiDocument == null) return;

        hiringBadge = uiDocument.rootVisualElement.Q<Label>("hiring-badge");
        hiringStopwatch = uiDocument.rootVisualElement.Q<VisualElement>("hiring-stopwatch");
        hiringStopwatch?.schedule.Execute(UpdateStopwatch).Every(1000);
        panel = uiDocument.rootVisualElement.Q<VisualElement>("hire-panel");
        if (panel == null)
        {
            Debug.LogError("[HirePanel] 'hire-panel' not found in UIDocument. Ensure it is present in MainUI.uxml.");
            return;
        }
        slamOverlay = panel.Q<VisualElement>("Stampanim");

        // Query elements
        stackContainer = panel.Q<VisualElement>("poster-stack");
        scrapLayer = panel.Q<VisualElement>("poster-scraps");
        noticeLabel = panel.Q<Label>("notice-count");
        emptyTimerLabel = panel.Q<Label>("empty-timer");
        tabView = panel.Q<TabView>("unit-type-tabs");

        // Two nails on each wooden tab sign
        foreach (var header in tabView.Query(className: "unity-tab__header").ToList())
            foreach (var side in new[] { "nail--left", "nail--right" })
            {
                var nail = new VisualElement { pickingMode = PickingMode.Ignore };
                nail.AddToClassList("nail");
                nail.AddToClassList(side);
                header.Add(nail);
            }

        // Layer selector
        layerPrevButton = panel.Q<Button>("layer-prev-button");
        layerNextButton = panel.Q<Button>("layer-next-button");
        layerLabel = panel.Q<Label>("layer-label");

        if (candidateSlotAsset == null)
            Debug.LogError("[HirePanel] Candidate Slot Asset (Poster.uxml) not assigned on the Hiring Menu");

        // Hook up callbacks
        tabView.activeTabChanged += OnTabChanged;

        // Layer selector callbacks
        if (layerPrevButton != null)
            layerPrevButton.clicked += OnLayerPrev;
        if (layerNextButton != null)
            layerNextButton.clicked += OnLayerNext;

        panel.Q<Button>("pile-prev-button").clicked += OnPrevPoster;
        panel.Q<Button>("pile-next-button").clicked += OnNextPoster;

        // Initial visibility — hide TC so it doesn't intercept events when closed
        panel.style.display = DisplayStyle.None;
        panel.style.opacity = 0f;
        if (panel.parent != null) panel.parent.style.display = DisplayStyle.None;

        UpdateLayerSelector();
    }

    // ═════════════════════════════════════════════
    // LAYER SELECTOR
    // ═════════════════════════════════════════════

    private void OnLayerPrev()
    {
        if (selectedLayer > 1)
        {
            selectedLayer--;
            UpdateLayerSelector();
            UpdateHireButtonState();
        }
    }

    private void OnLayerNext()
    {
        int maxLayer = ProgressionManager.Instance != null
            ? ProgressionManager.Instance.maxUnlockedLayer
            : 1;

        if (selectedLayer < maxLayer)
        {
            selectedLayer++;
            UpdateLayerSelector();
            UpdateHireButtonState();
        }
    }

    private void UpdateLayerSelector()
    {
        int maxLayer = ProgressionManager.Instance != null
            ? ProgressionManager.Instance.maxUnlockedLayer
            : 1;

        // Clamp selected layer
        selectedLayer = Mathf.Clamp(selectedLayer, 1, maxLayer);

        if (layerLabel != null)
            layerLabel.text = $"Depth {selectedLayer}";

        // Disable arrows at bounds
        if (layerPrevButton != null)
            layerPrevButton.SetEnabled(selectedLayer > 1);
        if (layerNextButton != null)
            layerNextButton.SetEnabled(selectedLayer < maxLayer);
    }

    // ═════════════════════════════════════════════
    // POOL MANAGEMENT
    // ═════════════════════════════════════════════

    private void InitializeCandidatePools()
    {
        for (int i = 0; i < adventurerManagers.Count; i++)
            if (adventurerManagers[i] == null)
                Debug.LogError($"[HireController] Adventurer manager slot {i + 1} is empty — assign that layer's manager on the Hiring Menu");

        CreatePools(HireRole.Adventurer);
        CreatePools(HireRole.Porter);
    }

    // Creates candidate pools gated by maxUnlockedLayer (def.rank <= unlocked layer); skips existing keys.
    // Adventurers: one pool per ClassCatalog def, pool "layer" = the def's tier, which HireRoster weights by.
    // Porters: one pool per (manager layer, hireable def).
    private void CreatePools(HireRole role)
    {
        int maxLayer = ProgressionManager.Instance != null
            ? ProgressionManager.Instance.maxUnlockedLayer
            : 1;

        bool adv = role == HireRole.Adventurer;
        var outList = adv ? adventurerPools : porterPools;
        Func<int> owned = adv ? () => OwnedCount(adventurerManagers) : () => OwnedCount(porterManagers);

        foreach (var (layer, def) in HireableDefs(role))
        {
            if (def == null) continue;

            // Gate by rank — higher rank candidates only appear when that layer is unlocked
            if (def.rank > maxLayer)
                continue;

            var key = (role, layer, def);
            if (candidatePools.ContainsKey(key)) continue;

            var pool = new CandidatePool(role, layer, def, refreshIntervalSeconds, traitChance, owned);
            candidatePools[key] = pool;
            outList.Add(pool);
        }
    }

    private IEnumerable<(int layer, EntityDef def)> HireableDefs(HireRole role)
    {
        if (role == HireRole.Adventurer)
        {
            var catalog = ClassCatalog.Instance;
            if (catalog == null)
            {
                Debug.LogError("[HireController] Resources/ClassCatalog missing — no adventurer candidates");
                yield break;
            }
            foreach (var d in catalog.defs)
                if (d != null) yield return (d.rank, d);
            yield break;
        }

        // A rebuilt dungeon level leaves empty slots here; one empty slot used to NRE every pool's cost lambda
        for (int i = 0; i < porterManagers.Count; i++)
        {
            var m = porterManagers[i];
            if (m == null)
            {
                Debug.LogError($"[HireController] Porter manager slot {i + 1} is empty — assign that layer's manager on the Hiring Menu");
                continue;
            }
            foreach (var d in ((IUnitManager)m).HireableDefs)
                yield return (m.LayerIndex, d);
        }
    }

    // Guild-wide units of a role (null slots skipped) — drives the ×1.25-per-owned price
    private static int OwnedCount<T>(List<T> managers) where T : IUnitManager
    {
        int owned = 0;
        foreach (var m in managers) if (m != null) owned += m.GetTotalCount();
        return owned;
    }

    // Catalog tier weights, newest tier ×unlockBoost while its post-unlock window runs
    private float[] AdventurerTierWeights()
    {
        var catalog = ClassCatalog.Instance;
        if (catalog == null) return rankWeights;
        var w = (float[])catalog.tierWeights.Clone();
        if (Time.time < boostUntil && boostTier >= 1 && boostTier <= w.Length)
            w[boostTier - 1] *= catalog.unlockBoost;
        return w;
    }

    // Called when a new layer is unlocked mid-session (and re-raised by a save restore).
    // Delegates to CreatePools which already skips existing keys.
    private void OnLayerUnlocked(int layer)
    {
        CreatePools(HireRole.Adventurer);
        CreatePools(HireRole.Porter);
        UpdateLayerSelector();
        affordPending = true;
        rosterDirty = true;

        // A real unlock (not the load-time replay) boosts that tier's odds for a while
        var catalog = ClassCatalog.Instance;
        if (catalog != null && (SaveManager.Instance == null || !SaveManager.Instance.IsRestoring))
        {
            boostTier = layer;
            boostUntil = Time.time + catalog.unlockBoostSeconds;
        }

        if (showDebugLogs)
            Debug.Log($"[HirePanel] Layer {layer} unlocked — new rank {layer} candidate pools created");
    }

    // ═════════════════════════════════════════════
    // SEAL SHOP (ContractManager)
    // ═════════════════════════════════════════════

    public bool CanGrantFavor => adventurerPools.Count > 0;

    /// <summary>Recruitment Favor: a Novice that joins promoted (rank = guild stars + 1, min Bronze) and survives
    /// pool refreshes until hired; as a Novice above Wood it gets the free path pick on its first Roster visit.</summary>
    // Regular pools regenerate on load; only unhired favors persist (SaveManager → ContractSave.favors).
    public void GrantRecruitmentFavor()
    {
        var best = NovicePool();
        if (best == null) return;

        int stars = ProgressionManager.Instance != null ? ProgressionManager.Instance.GetCurrentStars() : 0;
        int maxRank = best.EntityDef is AdventurerDef ad ? ad.maxRank : 5;
        var c = best.AddFavored(Mathf.Clamp(stars + 1, 2, maxRank));
        OnPoolsChanged();
        if (showDebugLogs) Debug.Log($"[HirePanel] Recruitment Favor: {c.DisplayName} joins at rank {c.startRank}");
    }

    /// <summary>Unhired favors, one entry each. Hiring consumes them from the pool, so they drop out of the next save.</summary>
    public List<FavorSave> FavorsToSave()
    {
        var list = new List<FavorSave>();
        foreach (var p in adventurerPools)
            foreach (var c in p.Favored)
                list.Add(new FavorSave
                {
                    def = p.EntityDef.name, layer = p.LayerIndex, identity = c.identity, traits = c.traits,
                    appearance = c.appearanceIndices, startRank = c.startRank, hireCost = c.hireCost,
                    cursedCombo = c.cursedCombo != null ? c.cursedCombo.name : null,
                });
        return list;
    }

    /// <summary>Cold-boot restore (SaveManager.ApplyLate, after the layer signals so every saved pool exists).</summary>
    public void RestoreFavors(List<FavorSave> favors)
    {
        foreach (var f in favors)
        {
            // By def only: pools are per catalog def now (pre-classes saves keyed by layer too)
            var pool = adventurerPools.Find(p => p.EntityDef.name == f.def) ?? NovicePool();
            if (pool == null)
            {
                Debug.LogError($"[HirePanel] No pool for saved favor '{f.identity?.DisplayName}' ({f.def}, layer {f.layer}) — dropped");
                continue;
            }
            pool.AddFavored(new HiringCandidate
            {
                entityDef = pool.EntityDef, identity = f.identity, traits = f.traits,
                appearanceIndices = f.appearance, startRank = f.startRank, hireCost = f.hireCost,
                cursedCombo = f.cursedCombo != null ? CursedComboDatabase.GetAllCombos().Find(x => x.name == f.cursedCombo) : null,
            });
        }
        if (favors.Count > 0) OnPoolsChanged();
    }

    private CandidatePool NovicePool() =>
        adventurerPools.Find(p => p.EntityDef is AdventurerDef ad && ad.classLine == ClassLine.Novice);

    /// <summary>Rush Order: every candidate pool restocks now (timers restart).</summary>
    public void RefreshAllPools()
    {
        foreach (var pool in candidatePools.Values) pool.ForceRefresh();
        OnPoolsChanged();
    }

    // Stock changed: redeal the board (Update flushes it while open; Open does it otherwise)
    private void OnPoolsChanged()
    {
        affordPending = true;
        rosterDirty = true;
        if (State != PanelState.Open)
            SetHiringBadge(true);
    }

    // ═════════════════════════════════════════════
    // ROSTER & STACK
    // ═════════════════════════════════════════════

    private void RebuildRosters()
    {
        candidateToPool.Clear();

        adventurerRoster = HireRoster.BuildRoster(adventurerPools, AdventurerTierWeights(), maxRosterSize);
        porterRoster = HireRoster.BuildRoster(porterPools, rankWeights, maxRosterSize);

        // Recruitment Favor candidates are guaranteed: pinned to the top of the stack, never left to the weighted draw
        foreach (var pool in adventurerPools)
            foreach (var c in pool.Favored)
            {
                adventurerRoster.Remove(c);
                adventurerRoster.Insert(0, c);
            }

        // Map candidates back to their pools for hire tracking
        foreach (var pool in adventurerPools)
            if (pool.CandidateCount > 0)
                foreach (var c in pool.GetCandidates())
                    candidateToPool.TryAdd(c, pool);

        foreach (var pool in porterPools)
            if (pool.CandidateCount > 0)
                foreach (var c in pool.GetCandidates())
                    candidateToPool.TryAdd(c, pool);
    }

    private void BuildStack()
    {
        stackContainer.Clear();
        scrapLayer.Clear();
        rotationByCard.Clear();
        CurrentCards.Clear();

        // Back to front, so the front poster is the last sibling (drawn on top)
        for (int i = CurrentRoster.Count - 1; i >= 0; i--)
        {
            var card = candidateSlotAsset.CloneTree();
            card.AddToClassList("poster-card");
            rotationByCard[card] = UnityEngine.Random.Range(-PosterTiltMax, PosterTiltMax);

            CandidateUIMapper.PopulateUI(card, Priced(CurrentRoster[i]));

            card.Q<Button>("hire-button").clicked += () => TryHire(card);
            card.Q<Button>("tack").clicked += OnNextPoster;

            stackContainer.Add(card);
            CurrentCards.Insert(0, card);
        }

        UpdateStackVisuals();
        UpdateCount();
    }

    private void UpdateStackVisuals()
    {
        for (int i = 0; i < CurrentCards.Count; i++)
        {
            var card = CurrentCards[i];
            bool front = i == 0;
            card.style.left = PileLeft + i * PileStep;
            card.style.top = PileTop + i * PileStep;
            card.style.rotate = new Rotate(new Angle(rotationByCard[card], AngleUnit.Degree));

            // Only the front poster is pickable and shows its body
            var pick = front ? PickingMode.Position : PickingMode.Ignore;
            card.pickingMode = pick;
            card.Q("poster").pickingMode = pick;
            card.Q("tack").pickingMode = pick;
            card.Q("poster-body").style.display = front ? DisplayStyle.Flex : DisplayStyle.None;
        }

        UpdateHireButtonState();
    }

    // ═════════════════════════════════════════════
    // NAVIGATION
    // ═════════════════════════════════════════════

    // ▶ / tack = slide the front poster off (.poster--pulled), then it goes to the back of the pile
    private void OnNextPoster()
    {
        if (isHireAnimating || isSliding || CurrentCards.Count < 2) return;
        StartCoroutine(PullFront(CurrentCards[0]));
    }

    private IEnumerator PullFront(VisualElement card)
    {
        isSliding = true;
        card.AddToClassList("poster--pulled");
        card.style.rotate = new Rotate(new Angle(rotationByCard[card] + 12f, AngleUnit.Degree));
        yield return new WaitForSecondsRealtime(SlideSeconds);

        card.RemoveFromClassList("poster--pulled");
        if (CurrentCards.Count > 0 && CurrentCards[0] == card) // not rebuilt mid-slide
        {
            card.AddToClassList("poster--snap"); // back into the pile without flying back
            RotateTopToBottom();
            for (int f = 0; f < 10 && card.resolvedStyle.opacity < 0.99f; f++) yield return null; // reset resolved
            card.RemoveFromClassList("poster--snap");
        }
        isSliding = false;
    }

    // ◀ = the back poster comes to the front, sliding in from the pulled-off offset (.poster--arriving)
    private void OnPrevPoster()
    {
        if (isHireAnimating || isSliding || CurrentCards.Count < 2) return;
        StartCoroutine(BringBackToFront());
    }

    private IEnumerator BringBackToFront()
    {
        isSliding = true;
        int last = CurrentCards.Count - 1;
        var c = CurrentRoster[last];
        var card = CurrentCards[last];
        CurrentRoster.RemoveAt(last);
        CurrentCards.RemoveAt(last);
        CurrentRoster.Insert(0, c);
        CurrentCards.Insert(0, card);
        card.BringToFront();

        card.AddToClassList("poster--arriving");
        UpdateStackVisuals();
        float tilt = rotationByCard[card];
        card.style.rotate = new Rotate(new Angle(tilt + 12f, AngleUnit.Degree));
        for (int f = 0; f < 10 && card.resolvedStyle.opacity > 0.01f; f++) yield return null; // offset resolved

        card.RemoveFromClassList("poster--arriving");
        card.style.rotate = new Rotate(new Angle(tilt, AngleUnit.Degree));
        yield return new WaitForSecondsRealtime(SlideSeconds);
        isSliding = false;
    }

    private void RotateTopToBottom()
    {
        var firstC = CurrentRoster[0];
        var firstCard = CurrentCards[0];

        CurrentRoster.RemoveAt(0);
        CurrentCards.RemoveAt(0);

        CurrentRoster.Add(firstC);
        CurrentCards.Add(firstCard);

        firstCard.SendToBack();

        UpdateStackVisuals();
    }

    // ═════════════════════════════════════════════
    // HIRING
    // ═════════════════════════════════════════════

    private void TryHire(VisualElement card)
    {
        if (isHireAnimating || isSliding || State != PanelState.Open)
            return;

        if (CurrentCards.Count == 0 || CurrentCards[0] != card)
            return;

        var candidate = CurrentRoster[0];
        if (!HireCandidate(candidate))
            return;

        StartCoroutine(AnimateHire(card, candidate));
    }

    // Route hire to the manager matching selectedLayer, not the candidate's rank
    private bool HireCandidate(HiringCandidate c)
    {
        if (!candidateToPool.TryGetValue(c, out var pool) || pool == null)
            return false;

        c.hireCost = pool.Price(c); // managers charge candidate.hireCost
        if (pool.Role == HireRole.Adventurer)
        {
            foreach (var m in adventurerManagers)
            {
                if (m != null && m.LayerIndex == selectedLayer)
                    return m.HireUnit(c);
            }
        }
        else
        {
            foreach (var m in porterManagers)
            {
                if (m != null && m.LayerIndex == selectedLayer)
                    return m.HireUnit(c);
            }
        }

        if (showDebugLogs)
            Debug.LogWarning($"[HirePanel] No manager found for layer {selectedLayer}, role {pool.Role}");

        return false;
    }

    // Stamp HIRED across the poster (BookStamp slam, badge on impact), hold, tear it off the board (USS
    // transition), then leave a scrap under the pile
    private IEnumerator AnimateHire(VisualElement card, HiringCandidate c)
    {
        isHireAnimating = true;

        bool landed = false;
        var poster = card.Q("poster") ?? card;
        BookStamp.AimAt(slamOverlay, poster, poster.worldBound.width * SlamPerPosterWidth);
        BookStamp.SlamIn(stampSlamAnim, slamOverlay, onImpact: () =>
        {
            landed = true;
            card.Q("hired-badge")?.AddToClassList("poster-hired--on");
            card.AddToClassList("poster-card--punch");
            card.schedule.Execute(() => card.RemoveFromClassList("poster-card--punch")).ExecuteLater(90);
        });
        while (!landed) yield return null;
        yield return new WaitForSecondsRealtime(StampHoldSeconds);

        card.AddToClassList("poster-card--torn");
        card.style.rotate = new Rotate(new Angle(rotationByCard[card] - 12f, AngleUnit.Degree));

        yield return new WaitForSecondsRealtime(TearSeconds);

        if (showDebugLogs)
            Debug.Log($"[HirePanel] Hired {c.DisplayName} → Layer {selectedLayer}");

        if (candidateToPool.TryGetValue(c, out var pool) && pool != null)
        {
            pool.ConsumeCandidate(c);
            candidateToPool.Remove(c);
            affordPending = true;
        }

        CurrentRoster.RemoveAt(0);
        CurrentCards.RemoveAt(0);
        rotationByCard.Remove(card);
        card.RemoveFromHierarchy();

        // One scrap per hire since the board was built (BuildStack clears them), under the pile, alternating ±7°
        int k = scrapLayer.childCount;
        var scrap = new VisualElement { pickingMode = PickingMode.Ignore };
        scrap.AddToClassList("poster-scrap");
        scrap.style.left = 70 + k * 60;
        scrap.style.rotate = new Rotate(new Angle(k % 2 == 0 ? 7f : -7f, AngleUnit.Degree));
        var scrapTack = new VisualElement();
        scrapTack.AddToClassList("tack");
        scrap.Add(scrapTack);
        scrapLayer.Add(scrap);

        isHireAnimating = false;
        UpdateStackVisuals();
        UpdateCount();
    }

    // ═════════════════════════════════════════════
    // UI UPDATES
    // ═════════════════════════════════════════════

    // Strip costs go red when unaffordable; the front poster's Hire button is btn-success, or btn-disabled
    // "Hire for N" with a NEED N MORE / DEPTH FULL tag when gold or the target layer blocks it.
    private void UpdateHireButtonState()
    {
        if (CurrentCards.Count == 0 || CurrentRoster.Count == 0)
            return;

        int gold = Inventory.Instance != null ? Inventory.Instance.Gold : 0;
        for (int i = 0; i < CurrentCards.Count; i++)
        {
            int p = Price(CurrentRoster[i]);
            var costLabel = CurrentCards[i].Q<Label>("cost");
            costLabel.text = p.ToString("N0");
            costLabel.EnableInClassList("poster-cost--short", p > gold);
        }

        var card = CurrentCards[0];
        var candidate = Priced(CurrentRoster[0]);
        var hireButton = card.Q<Button>("hire-button");

        // Resolve manager via the selected layer in the UI
        IUnitManager manager = currentTab == TabType.Adventurers
            ? adventurerManagers.Find(m => m != null && m.LayerIndex == selectedLayer)
            : porterManagers.Find(m => m != null && m.LayerIndex == selectedLayer);

        bool canAfford = Inventory.Instance != null && Inventory.Instance.CanAfford(candidate.hireCost);

        // Total layer capacity, then the optional per-type limit
        int typeLimit = manager != null ? manager.GetUnitLimit(candidate.entityDef) : -1;
        bool full = manager == null
            || manager.GetTotalCount() >= manager.MaxUnits
            || (typeLimit >= 0 && manager.GetUnitCount(candidate.entityDef) >= typeLimit);

        bool canHire = canAfford && !full;

        // Warn when the destination layer is deeper than the recruit's rank (allowed — they'll just struggle)
        card.Q<Label>("rank-warning").style.display = currentTab == TabType.Adventurers
            && Mathf.Max(candidate.startRank, candidate.entityDef.rank) < selectedLayer
            ? DisplayStyle.Flex : DisplayStyle.None;

        hireButton.SetEnabled(canHire);
        hireButton.EnableInClassList("btn-success", canHire);
        hireButton.EnableInClassList("btn-disabled", !canHire);
        card.Q<Label>("hire-label").text = canHire ? $"Hire {candidate.hireCost:N0}" : $"Hire for {candidate.hireCost:N0}";

        var tag = card.Q<Label>("hire-tag");
        tag.style.display = canHire ? DisplayStyle.None : DisplayStyle.Flex;
        tag.text = full ? "DEPTH FULL" : $"NEED {candidate.hireCost - gold:N0} MORE";
    }

    // Current price (×1.25 per owned unit of the role) — candidate.hireCost holds only the trait-adjusted base
    private int Price(in HiringCandidate c) =>
        candidateToPool.TryGetValue(c, out var pool) && pool != null ? pool.Price(c) : c.hireCost;

    // Copy with hireCost = today's price, for display paths that read candidate.hireCost
    private HiringCandidate Priced(HiringCandidate c)
    {
        c.hireCost = Price(c);
        return c;
    }

    private void UpdateCount()
    {
        int n = CurrentRoster.Count;
        noticeLabel.text = n == 0 ? "Board empty" : $"{n} notice{(n == 1 ? "" : "s")} pinned";
    }

    private void OnTabChanged(Tab _, Tab newTab)
    {
        currentTab = (TabType)tabView.IndexOf(newTab);
        BuildStack();
    }

    private void OnOpenButtonClicked()
    {
        if (State == PanelState.Closed)
            UIManager.Instance.OpenPanel(this);
        else if (State == PanelState.Open)
            UIManager.Instance.ClosePanel(this);
    }

    // Restock note countdown (every frame while open, text only on a new second). An empty board stays empty
    // until a pool restocks (rosterDirty) — the deeper pool stock is not dealt early.
    private void UpdateEmptyTimer()
    {
        int t = Mathf.Max(0, Mathf.FloorToInt(HireRoster.GetNextRefreshTime(CurrentPools)));
        if (emptyTimerLabel != null && t != shownTimerSeconds)
        {
            shownTimerSeconds = t;
            emptyTimerLabel.text = $"{t / 60}:{t % 60:00}";
        }
    }
}
