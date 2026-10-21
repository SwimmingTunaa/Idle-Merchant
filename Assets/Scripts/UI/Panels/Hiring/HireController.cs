using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections;
using System.Collections.Generic;

// Hire panel controller - manages candidate hiring UI with stack-based navigation and animations.
// Candidates have a rank (intrinsic quality), players choose which layer to deploy them to.
// Inherits from BasePanelController, overrides Open() for roster rebuild and animations for card stack.
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

    [Header("Stack Visual Settings")]
    [SerializeField] private float stackRotationMax = 3f;
    [SerializeField] private float stackScaleFactor = 0.025f;

    [Header("Animation")]
    [SerializeField] private float hireAnimationDuration = 0.5f;

    [Header("Top-Bar Sign")]
    [Tooltip("Stopwatch frames shown on the hiring sign before pools refresh (full → empty).")]
    [SerializeField] private Sprite[] stopwatchFrames;
    [Tooltip("Stopwatch appears when the next pool refresh is within this many seconds")]
    [SerializeField] private float stopwatchShowSeconds = 120f;

    // UI Elements
    private VisualElement stackContainer;
    private VisualElement emptyState;
    private Button prevButton;
    private Button nextButton;
    private Label countLabel;
    private Label emptyTimerLabel;
    private TabView tabView;

    // Layer selector UI
    private Button layerPrevButton;
    private Button layerNextButton;
    private Label layerLabel;
    private int selectedLayer = 1;

    // Tabs
    private enum TabType { Adventurers = 0, Porters = 1 }
    private TabType currentTab = TabType.Adventurers;

    private int adventurerViewIndex = 0;
    private int porterViewIndex = 0;

    private int CurrentViewIndex
    {
        get => currentTab == TabType.Adventurers ? adventurerViewIndex : porterViewIndex;
        set
        {
            if (currentTab == TabType.Adventurers) adventurerViewIndex = value;
            else porterViewIndex = value;
        }
    }

    // Pools
    private readonly Dictionary<(HireRole role, int rank, EntityDef def), CandidatePool> candidatePools = new();
    private readonly List<CandidatePool> adventurerPools = new();
    private readonly List<CandidatePool> porterPools = new();

    // Roster + cards
    private List<HiringCandidate> adventurerRoster = new();
    private List<HiringCandidate> porterRoster = new();
    private List<VisualElement> adventurerCards = new();
    private List<VisualElement> porterCards = new();

    private readonly Dictionary<HiringCandidate, CandidatePool> candidateToPool = new();
    private readonly Dictionary<VisualElement, float> rotationByCard = new();

    private bool isHireAnimating;

    // Newest tier's odds boost after its layer unlocks (ClassCatalog.unlockBoost / unlockBoostSeconds).
    // ponytail: not saved — quitting inside the window drops the rest of the boost.
    private int boostTier;
    private float boostUntil;

    // Top-bar sign: badge = coin (can afford a hire, wiggles) > "!" (new stock while closed); stopwatch = refresh countdown
    private Label hiringBadge;
    private VisualElement hiringStopwatch;
    private bool hasNewStock;
    private bool affordPending = true;

    private static readonly string[] ShadowClasses =
    {
        "card-shadow-0",
        "card-shadow-1",
        "card-shadow-2",
        "card-shadow-3"
    };

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
        {
            affordPending = true;
            if (State != PanelState.Open)
                SetHiringBadge(true);
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
        RebuildRosters();
        ClampViewIndexToRoster();
        BuildStack();
        UpdateCount();
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

        // Query elements
        stackContainer = panel.Q<VisualElement>("newspaper-container");
        emptyState = panel.Q<VisualElement>("text-container");
        prevButton = panel.Q<Button>("previous-button");
        nextButton = panel.Q<Button>("next-button");
        countLabel = panel.Q<Label>("count");
        emptyTimerLabel = panel.Q<Label>("empty-timer");
        tabView = panel.Q<TabView>("unit-type-tabs");

        // Layer selector
        layerPrevButton = panel.Q<Button>("layer-prev-button");
        layerNextButton = panel.Q<Button>("layer-next-button");
        layerLabel = panel.Q<Label>("layer-label");

        // Fallback: use template instance from UXML if no asset assigned
        if (candidateSlotAsset == null)
        {
            var templateInstance = stackContainer.Q<TemplateContainer>("TemplateCandidate");
            if (templateInstance != null)
                candidateSlotAsset = templateInstance.templateSource;
        }

        // Hook up callbacks
        prevButton.clicked += Prev;
        nextButton.clicked += Next;
        tabView.activeTabChanged += OnTabChanged;

        // Layer selector callbacks
        if (layerPrevButton != null)
            layerPrevButton.clicked += OnLayerPrev;
        if (layerNextButton != null)
            layerNextButton.clicked += OnLayerNext;

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
            layerLabel.text = $"Dungeon Level {selectedLayer}";

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

    private void OnPoolsChanged()
    {
        affordPending = true;
        if (State != PanelState.Open)
        {
            SetHiringBadge(true);
            return;
        }
        if (isHireAnimating) return;
        RebuildRosters();
        ClampViewIndexToRoster();
        BuildStack();
        UpdateCount();
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
        rotationByCard.Clear();
        CurrentCards.Clear();

        if (CurrentRoster.Count == 0)
        {
            emptyState.style.display = DisplayStyle.Flex;
            UpdateCount();
            return;
        }

        emptyState.style.display = DisplayStyle.None;

        for (int i = CurrentRoster.Count - 1; i >= 0; i--)
        {
            var card = candidateSlotAsset.CloneTree();
            rotationByCard[card] = UnityEngine.Random.Range(-stackRotationMax, stackRotationMax);

            CandidateUIMapper.PopulateUI(card, CurrentRoster[i]);
            
            var hireBtn = card.Q<Button>("hire-button");
            if (hireBtn != null)
                hireBtn.clicked += () => TryHire(card);

            stackContainer.Add(card);
            CurrentCards.Insert(0, card);
        }

        UpdateStackVisuals();
    }

    private void UpdateStackVisuals()
    {
        for (int i = 0; i < CurrentCards.Count; i++)
        {
            var card = CurrentCards[i];
            float scale = 1f - (i * stackScaleFactor);
            card.style.scale = new Scale(new Vector3(scale, scale, 1f));
            
            // Darken card color based on index
            float colorFactor = Mathf.Clamp(1f - (i * 0.1f), 0.3f, 1f);
            card.Q<VisualElement>("newspaper").style.unityBackgroundImageTintColor = new Color(colorFactor, colorFactor, colorFactor);

            card.pickingMode = i == 0 ? PickingMode.Position : PickingMode.Ignore;
            card.style.rotate = new Rotate(new Angle(rotationByCard[card], AngleUnit.Degree));

            ApplyShadowClass(card, i);
        }

        UpdateHireButtonState();
    }

    private void ApplyShadowClass(VisualElement card, int index)
    {
        foreach (var cls in ShadowClasses)
            card.RemoveFromClassList(cls);

        int clamped = Mathf.Clamp(index, 0, ShadowClasses.Length - 1);
        card.AddToClassList(ShadowClasses[clamped]);
    }

    // ═════════════════════════════════════════════
    // NAVIGATION
    // ═════════════════════════════════════════════

    private void Prev()
    {
        if (CurrentRoster.Count <= 1) return;

        CurrentViewIndex--;
        if (CurrentViewIndex < 0) CurrentViewIndex = CurrentRoster.Count - 1;

        RotateBottomToTop();
    }

    private void Next()
    {
        if (CurrentRoster.Count <= 1) return;

        CurrentViewIndex++;
        if (CurrentViewIndex >= CurrentRoster.Count) CurrentViewIndex = 0;

        RotateTopToBottom();
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
        UpdateCount();
    }

    private void RotateBottomToTop()
    {
        var lastC = CurrentRoster[^1];
        var lastCard = CurrentCards[^1];

        CurrentRoster.RemoveAt(CurrentRoster.Count - 1);
        CurrentCards.RemoveAt(CurrentCards.Count - 1);

        CurrentRoster.Insert(0, lastC);
        CurrentCards.Insert(0, lastCard);

        lastCard.BringToFront();

        UpdateStackVisuals();
        UpdateCount();
    }

    // ═════════════════════════════════════════════
    // HIRING
    // ═════════════════════════════════════════════

    private void TryHire(VisualElement card)
    {
        if (isHireAnimating || State != PanelState.Open)
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

    private IEnumerator AnimateHire(VisualElement card, HiringCandidate c)
    {
        isHireAnimating = true;
        card.BringToFront();

        // Use the card's actual height so the animation scales with the layout
        float flyDistance = card.resolvedStyle.height > 0f ? card.resolvedStyle.height : 750f;

        float t = 0f;
        while (t < hireAnimationDuration)
        {
            t += Time.deltaTime;
            card.style.translate = new Translate(0, -flyDistance * (t / hireAnimationDuration));
            yield return null;
        }

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
        card.style.display = DisplayStyle.None;

        ClampViewIndexToRoster();

        isHireAnimating = false;
        UpdateStackVisuals();
        UpdateCount();
        if (CurrentRoster.Count == 0)
            emptyState.style.display = DisplayStyle.Flex;
    }

    // ═════════════════════════════════════════════
    // UI UPDATES
    // ═════════════════════════════════════════════

    // Check hire button state against the candidate's pool layer.
    // Disables the button if the player can't afford the hire cost or the target layer is full.
    // Applies "cost--insufficient" USS class to the cost label when gold is the blocker.
    private void UpdateHireButtonState()
    {
        if (CurrentCards.Count == 0 || CurrentRoster.Count == 0)
            return;

        var card = CurrentCards[0];
        var candidate = CurrentRoster[0];

        var hireButton = card.Q<Button>("hire-button");
        if (hireButton == null)
            return;

        var costLabel = card.Q<Label>("cost");

        // Resolve manager via the selected layer in the UI
        candidateToPool.TryGetValue(candidate, out var pool);

        IUnitManager manager = null;
        if (currentTab == TabType.Adventurers)
            manager = adventurerManagers.Find(m => m != null && m.LayerIndex == selectedLayer);
        else
            manager = porterManagers.Find(m => m != null && m.LayerIndex == selectedLayer);

        if (manager == null)
        {
            hireButton.SetEnabled(false);
            costLabel?.AddToClassList("cost--insufficient");
            return;
        }

        // Check gold affordability
        bool canAfford = Inventory.Instance != null && Inventory.Instance.CanAfford(candidate.hireCost);

        // Check total layer capacity
        bool layerFull = manager.GetTotalCount() >= manager.MaxUnits;

        // Check optional per-type limit
        int typeLimit = manager.GetUnitLimit(candidate.entityDef);
        bool typeFull = typeLimit >= 0 && manager.GetUnitCount(candidate.entityDef) >= typeLimit;

        bool canHire = canAfford && !layerFull && !typeFull;

        // Warn when the destination layer is deeper than the recruit's rank (allowed — they'll just struggle)
        var rankWarning = card.Q<Label>("rank-warning");
        if (rankWarning != null)
            rankWarning.style.display = currentTab == TabType.Adventurers
                && Mathf.Max(candidate.startRank, candidate.entityDef.rank) < selectedLayer
                ? DisplayStyle.Flex : DisplayStyle.None;
        hireButton.SetEnabled(canHire);

        if (costLabel != null)
        {
            if (canHire)
                costLabel.RemoveFromClassList("cost--insufficient");
            else
                costLabel.AddToClassList("cost--insufficient");
        }
    }

    private void UpdateCount()
    {
        int current = CurrentViewIndex + 1;
        int total = CurrentRoster.Count;
        countLabel.text = total > 0 ? $"{current}/{total}" : "0/0";
    }

    private void OnTabChanged(Tab _, Tab newTab)
    {
        currentTab = (TabType)tabView.IndexOf(newTab);
        ClampViewIndexToRoster();
        BuildStack();
        UpdateCount();
    }

    private void ClampViewIndexToRoster()
    {
        int count = CurrentRoster.Count;
        if (count <= 0)
        {
            CurrentViewIndex = 0;
            return;
        }

        if (CurrentViewIndex < 0) CurrentViewIndex = 0;
        if (CurrentViewIndex >= count) CurrentViewIndex = 0;
    }

    private void OnOpenButtonClicked()
    {
        if (State == PanelState.Closed)
            UIManager.Instance.OpenPanel(this);
        else if (State == PanelState.Open)
            UIManager.Instance.ClosePanel(this);
    }

    private void UpdateEmptyTimer()
    {
        if (CurrentRoster.Count > 0)
            return;

        float t = HireRoster.GetNextRefreshTime(CurrentPools);
        if (t < 0f) t = 0f;

        if (emptyTimerLabel != null)
            emptyTimerLabel.text = $"{Mathf.FloorToInt(t / 60f)}:{Mathf.FloorToInt(t % 60f):00}";

        // Check if any pool has candidates again
        if (!isHireAnimating)
        {
            foreach (var pool in CurrentPools)
            {
                if (pool.CandidateCount > 0)
                {
                    RebuildRosters();
                    ClampViewIndexToRoster();
                    BuildStack();
                    break;
                }
            }
        }
    }
}