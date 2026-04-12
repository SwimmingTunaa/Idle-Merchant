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
    [Tooltip("Weight per rank (index 0 = Rank 1). Higher = more likely to appear. Normalized at runtime.")]
    [SerializeField] private float[] rankWeights = { 40f, 25f, 20f, 10f, 5f };

    [Header("Stack Visual Settings")]
    [SerializeField] private float stackRotationMax = 3f;
    [SerializeField] private float stackScaleFactor = 0.025f;

    [Header("Animation")]
    [SerializeField] private float hireAnimationDuration = 0.5f;

    // UI Elements
    private VisualElement stackContainer;
    private VisualElement emptyState;
    private Button openButton;
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
    }

    void OnDisable()
    {
        GameSignals.OnLayerUnlocked -= OnLayerUnlocked;
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

        // Start closed
        if (panel != null) panel.style.display = DisplayStyle.None;
    }

    void Update()
    {
        // Update candidate pools
        foreach (var pool in candidatePools.Values)
            pool.Update(Time.deltaTime);

        if (State == PanelState.Open)
            UpdateEmptyTimer();
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

        // Query the open button (if it exists in root)
        openButton = uiDocument.rootVisualElement.Q<Button>("hiring-board-button");

        // Fallback: use template instance from UXML if no asset assigned
        if (candidateSlotAsset == null)
        {
            var templateInstance = stackContainer.Q<TemplateContainer>("TemplateCandidate");
            if (templateInstance != null)
                candidateSlotAsset = templateInstance.templateSource;
        }

        // Hook up callbacks
        if (openButton != null)
            openButton.clicked += OnOpenButtonClicked;
        prevButton.clicked += Prev;
        nextButton.clicked += Next;
        tabView.activeTabChanged += OnTabChanged;

        // Layer selector callbacks
        if (layerPrevButton != null)
            layerPrevButton.clicked += OnLayerPrev;
        if (layerNextButton != null)
            layerNextButton.clicked += OnLayerNext;

        // Initial visibility
        panel.style.display = DisplayStyle.None;
        panel.style.opacity = 0f;

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
        CreatePools(adventurerManagers, HireRole.Adventurer, adventurerPools);
        CreatePools(porterManagers, HireRole.Porter, porterPools);
    }

    // Creates candidate pools gated by maxUnlockedLayer.
    // Pools are keyed by (role, manager.LayerIndex, def) so each layer gets its own pool per def.
    private void CreatePools<T>(List<T> managers, HireRole role, List<CandidatePool> outList)
        where T : IUnitManager
    {
        int maxLayer = ProgressionManager.Instance != null
            ? ProgressionManager.Instance.maxUnlockedLayer
            : 1;

        foreach (var m in managers)
        {
            foreach (var def in m.HireableDefs)
            {
                if (def == null) continue;

                // Gate by rank — higher rank candidates only appear when that layer is unlocked
                if (def.rank > maxLayer)
                    continue;

                var key = (role, m.LayerIndex, def);
                if (candidatePools.ContainsKey(key)) continue;

                var pool = new CandidatePool(role, m.LayerIndex, def, refreshIntervalSeconds, traitChance);
                candidatePools[key] = pool;
                outList.Add(pool);
            }
        }
    }

    // Called when a new layer is unlocked mid-session.
    // Delegates to CreatePools which already skips existing keys.
    private void OnLayerUnlocked(int layer)
    {
        CreatePools(adventurerManagers, HireRole.Adventurer, adventurerPools);
        CreatePools(porterManagers, HireRole.Porter, porterPools);
        UpdateLayerSelector();

        if (showDebugLogs)
            Debug.Log($"[HirePanel] Layer {layer} unlocked — new rank {layer} candidate pools created");
    }

    // ═════════════════════════════════════════════
    // ROSTER & STACK
    // ═════════════════════════════════════════════

    private void RebuildRosters()
    {
        candidateToPool.Clear();

        adventurerRoster = HireRoster.BuildRoster(adventurerPools, rankWeights, maxRosterSize);
        porterRoster = HireRoster.BuildRoster(porterPools, rankWeights, maxRosterSize);

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
                if (m.LayerIndex == selectedLayer)
                    return m.HireUnit(c);
            }
        }
        else
        {
            foreach (var m in porterManagers)
            {
                if (m.LayerIndex == selectedLayer)
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
            manager = adventurerManagers.Find(m => m.LayerIndex == selectedLayer);
        else
            manager = porterManagers.Find(m => m.LayerIndex == selectedLayer);

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