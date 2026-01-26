using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Hire panel controller - implements IPanelController for UIManager integration.
/// Manages candidate hiring UI with stack-based navigation and animations.
/// </summary>
public class HireController : MonoBehaviour, IPanelController
{
    [Header("UXML References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VisualTreeAsset hirePanelAsset;
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
    [SerializeField] private float openCloseDuration = 0.3f;
    [SerializeField] private float hireAnimationDuration = 0.5f;

    [Header("Panel Settings")]
    [SerializeField] private bool blocksWorldInput = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // IPanelController implementation
    public string PanelID => "HirePanel";
    public VisualElement RootElement => hirePanel;
    public PanelState State { get; private set; } = PanelState.Closed;
    public bool BlocksWorldInput => blocksWorldInput;
    public bool IsModal => true;

    public event Action<IPanelController> OnOpenComplete;
    public event Action<IPanelController> OnCloseComplete;

    // UI
    private VisualElement root;
    private VisualElement hirePanel;
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

    // ─────────────────────────────────────────────
    // LIFECYCLE
    // ─────────────────────────────────────────────

    private void OnEnable()
    {
        BuildUI();
    }

    private void Start()
    {
        InitializeCandidatePools();
        UIManager.Instance.RegisterPanel(this);
        
        // Start closed
        hirePanel.style.display = DisplayStyle.None;
        State = PanelState.Closed;
    }

    private void OnDestroy()
    {
        UIManager.Instance.UnregisterPanel(this);
    }

    private void Update()
    {
        foreach (var pool in candidatePools.Values)
            pool.Update(Time.deltaTime);

        if (State == PanelState.Open)
            UpdateEmptyTimer();
    }

    // ─────────────────────────────────────────────
    // UI SETUP
    // ─────────────────────────────────────────────

    private void BuildUI()
    {
        root = uiDocument.rootVisualElement;
        openButton = root.Q<Button>("ShopButton");

        hirePanel = hirePanelAsset.CloneTree().Q<VisualElement>("hire-panel");
        root.Add(hirePanel);

        stackContainer = hirePanel.Q<VisualElement>("newspaper-container");
        emptyState = hirePanel.Q<VisualElement>("text-container");
        prevButton = hirePanel.Q<Button>("previous-button");
        nextButton = hirePanel.Q<Button>("next-button");
        countLabel = hirePanel.Q<Label>("count");
        emptyTimerLabel = hirePanel.Q<Label>("empty-timer");
        tabView = hirePanel.Q<TabView>("unit-type-tabs");

        openButton.clicked += OnOpenButtonClicked;
        prevButton.clicked += Prev;
        nextButton.clicked += Next;
        tabView.activeTabChanged += OnTabChanged;
    }

    private void InitializeCandidatePools()
    {
        candidatePools.Clear();
        adventurerPools.Clear();
        porterPools.Clear();

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

    // ─────────────────────────────────────────────
    // IPANELCONTROLLER IMPLEMENTATION
    // ─────────────────────────────────────────────

    public bool Open()
    {
        if (State != PanelState.Closed)
            return false;

        State = PanelState.Opening;
        RebuildRosters();
        ClampViewIndexToRoster();
        BuildStack();
        UpdateCount();
        
        StartCoroutine(OpenAnimation());
        return true;
    }

    public bool Close()
    {
        if (State != PanelState.Open)
            return false;

        State = PanelState.Closing;
        StartCoroutine(CloseAnimation());
        return true;
    }

    public void OnFocus()
    {
        if (showDebugLogs)
            Debug.Log("[HirePanel] Gained focus");
    }

    public void OnLoseFocus()
    {
        if (showDebugLogs)
            Debug.Log("[HirePanel] Lost focus");
    }

    public bool CanClose()
    {
        // Don't allow close during hire animation
        return !isHireAnimating;
    }

    // ─────────────────────────────────────────────
    // ANIMATIONS
    // ─────────────────────────────────────────────

    private IEnumerator OpenAnimation()
    {
        hirePanel.style.display = DisplayStyle.Flex;
        
        // Simple fade-in (you can enhance this)
        float elapsed = 0f;
        while (elapsed < openCloseDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / openCloseDuration);
            hirePanel.style.opacity = alpha;
            yield return null;
        }

        hirePanel.style.opacity = 1f;
        State = PanelState.Open;
        OnOpenComplete?.Invoke(this);
    }

    private IEnumerator CloseAnimation()
    {
        // Simple fade-out
        float elapsed = 0f;
        float startAlpha = hirePanel.resolvedStyle.opacity;
        
        while (elapsed < openCloseDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, 0f, elapsed / openCloseDuration);
            hirePanel.style.opacity = alpha;
            yield return null;
        }

        hirePanel.style.opacity = 0f;
        hirePanel.style.display = DisplayStyle.None;
        State = PanelState.Closed;
        OnCloseComplete?.Invoke(this);
    }

    // ─────────────────────────────────────────────
    // PANEL INTERACTION
    // ─────────────────────────────────────────────

    private void OnOpenButtonClicked()
    {
        if (State == PanelState.Closed)
            UIManager.Instance.OpenPanel(this);
        else if (State == PanelState.Open)
            UIManager.Instance.ClosePanel(this);
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

    // ─────────────────────────────────────────────
    // ROSTER & STACK
    // ─────────────────────────────────────────────

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

            // Populate UI
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

    // ─────────────────────────────────────────────
    // NAVIGATION
    // ─────────────────────────────────────────────

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

    // ─────────────────────────────────────────────
    // HIRING
    // ─────────────────────────────────────────────

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

    // ─────────────────────────────────────────────
    // UI UPDATES
    // ─────────────────────────────────────────────

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
        if (countLabel == null) return;

        if (CurrentRoster.Count == 0)
        {
            countLabel.text = "0/0";
            return;
        }

        int pos = Mathf.Clamp(CurrentViewIndex, 0, CurrentRoster.Count - 1) + 1;
        countLabel.text = $"{pos}/{CurrentRoster.Count}";
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
