using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Hire panel controller - manages candidate hiring UI with stack-based navigation and animations.
/// Inherits from BasePanelController, overrides Open() for roster rebuild and animations for card stack.
/// </summary>
public class HireController : BasePanelController
{
    [Header("UXML References")]
    [SerializeField] private VisualTreeAsset candidateSlotAsset;

    [Header("Manager References")]
    [SerializeField] private List<AdventurerManager> adventurerManagers = new();
    [SerializeField] private List<PorterManager> porterManagers = new();

    [Header("Refresh Settings")]
    [SerializeField] private float refreshIntervalSeconds = 300f;
    [SerializeField][Range(0f, 1f)] private float traitChance = 0.7f;

    [Header("Stack Visual Settings")]
    [SerializeField] private float stackRotationMax = 3f;
    [SerializeField] private float stackScaleFactor = 0.025f;

    [Header("Animation")]
    [SerializeField] private float hireAnimationDuration = 0.5f;

    // IPanelController implementation
    public override string PanelID => "HirePanel";
    //public override VisualElement RootElement => panel;

    // UI Elements
    private VisualElement stackContainer;
    private VisualElement emptyState;
    private Button openButton;
    private Button prevButton;
    private Button nextButton;
    private Label countLabel;
    private Label emptyTimerLabel;
    private TabView tabView;

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
    private readonly Dictionary<(HireRole role, int layer, EntityDef def), CandidatePool> candidatePools = new();
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
        BuildUI();
    }

    void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();
    }

    protected override void Start()
    {
        InitializeCandidatePools();
        base.Start(); // Registers with UIManager
        
        // Start closed
        panel.style.display = DisplayStyle.None;
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
        base.BuildUI();
        
        // Query the open button (if it exists in root)
        openButton = uiDocument.rootVisualElement.Q<Button>("ShopButton");

        // Query elements
        stackContainer = panel.Q<VisualElement>("newspaper-container");
        emptyState = panel.Q<VisualElement>("text-container");
        prevButton = panel.Q<Button>("previous-button");
        nextButton = panel.Q<Button>("next-button");
        countLabel = panel.Q<Label>("count");
        emptyTimerLabel = panel.Q<Label>("empty-timer");
        tabView = panel.Q<TabView>("unit-type-tabs");

        // Hook up callbacks
        if (openButton != null)
            openButton.clicked += OnOpenButtonClicked;
        prevButton.clicked += Prev;
        nextButton.clicked += Next;
        tabView.activeTabChanged += OnTabChanged;

        // Initial visibility
        panel.style.display = DisplayStyle.None;
        panel.style.opacity = 0f;
    }

    // ═════════════════════════════════════════════
    // POOL MANAGEMENT
    // ═════════════════════════════════════════════

    private void InitializeCandidatePools()
    {
        CreatePools(adventurerManagers, HireRole.Adventurer, adventurerPools);
        CreatePools(porterManagers, HireRole.Porter, porterPools);
    }

    private void CreatePools<T>(List<T> managers, HireRole role, List<CandidatePool> outList)
        where T : IUnitManager
    {
        foreach (var m in managers)
        {
            foreach (var limit in m.UnitLimits)
            {
                var key = (role, m.LayerIndex, limit.unitDef);
                if (candidatePools.ContainsKey(key)) continue;

                var pool = new CandidatePool(role, m.LayerIndex, limit.unitDef, refreshIntervalSeconds, traitChance);
                candidatePools[key] = pool;
                outList.Add(pool);
            }
        }
    }

    // ═════════════════════════════════════════════
    // ROSTER & STACK
    // ═════════════════════════════════════════════

    private void RebuildRosters()
    {
        candidateToPool.Clear();

        adventurerRoster = HireRoster.BuildRoster(adventurerPools);
        porterRoster = HireRoster.BuildRoster(porterPools);

        foreach (var pool in adventurerPools)
            foreach (var c in pool.GetCandidates())
                candidateToPool[c] = pool;

        foreach (var pool in porterPools)
            foreach (var c in pool.GetCandidates())
                candidateToPool[c] = pool;
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

            card.pickingMode = i == 0 ? PickingMode.Position : PickingMode.Ignore;
            card.style.rotate = new Rotate(new Angle(rotationByCard[card], AngleUnit.Degree));

            ApplyShadowClass(card, i);

            if (i == 0)
                card.BringToFront();
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

    private bool HireCandidate(HiringCandidate c)
    {
        if (!candidateToPool.TryGetValue(c, out var pool) || pool == null)
            return false;

        if (pool.Role == HireRole.Adventurer)
        {
            foreach (var m in adventurerManagers)
                if (m.LayerIndex == pool.LayerIndex)
                    return m.HireUnit(c);
        }
        else
        {
            foreach (var m in porterManagers)
                if (m.LayerIndex == pool.LayerIndex)
                    return m.HireUnit(c);
        }

        GameSignals.RaiseUnitHired(c.entityDef);

        return false;
    }

    private IEnumerator AnimateHire(VisualElement card, HiringCandidate c)
    {
        isHireAnimating = true;
        card.BringToFront();

        float t = 0f;
        while (t < hireAnimationDuration)
        {
            t += Time.deltaTime;
            card.style.translate = new Translate(0, -800f * (t / hireAnimationDuration));
            yield return null;
        }

        if (showDebugLogs)
            Debug.Log($"[HirePanel] Hired {c.DisplayName}");

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
    }

    // ═════════════════════════════════════════════
    // UI UPDATES
    // ═════════════════════════════════════════════

    private void UpdateHireButtonState()
    {
        if (CurrentCards.Count == 0 || CurrentRoster.Count == 0)
            return;

        var card = CurrentCards[0];
        var candidate = CurrentRoster[0];

        var hireButton = card.Q<Button>("hire-button");
        if (hireButton == null)
            return;

        IUnitManager manager = null;

        if (currentTab == TabType.Adventurers)
            manager = adventurerManagers.Find(m => m.LayerIndex == candidate.entityDef.assignedLayer);
        else
            manager = porterManagers.Find(m => m.LayerIndex == candidate.entityDef.assignedLayer);

        if (manager == null)
        {
            hireButton.text = "Hire";
            hireButton.SetEnabled(false);
            return;
        }

        int current = manager.GetUnitCount(candidate.entityDef);
        int max = manager.GetUnitLimit(candidate.entityDef);

        if (current >= max)
        {
            hireButton.text = $"Lvl {manager.LayerIndex} Full ({current}/{max})";
            hireButton.SetEnabled(false);
        }
        else
        {
            hireButton.text = "Hire";
            hireButton.SetEnabled(true);
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
                if (pool.GetCandidates().Count > 0)
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