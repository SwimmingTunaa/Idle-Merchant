using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.Events;

[Serializable]
public class UIBinding 
{   
    [Tooltip("Action name (e.g., 'Crafting Menu')")]
    [SerializeField]
    public InputActionReference inputActionRef;
    public UnityEvent onActionPerformed;

    [Tooltip("Button element name in UXML")]
    public string buttonName;
    
    [Tooltip("If true, pressing again closes the panel")]
    public bool allowToggle = true;

    public void OnActionTriggered(InputAction.CallbackContext context)
    {
        onActionPerformed.Invoke();
    }

    public void OnActionTriggered()
    {
        onActionPerformed.Invoke();
    }
}

public class UIManager : PersistentSingleton<UIManager>
{

    [SerializeField] private UIDocument uiDocument;

    [Header("Input System")]
    [SerializeField] private InputActionAsset inputActions;
    
    [Header("ESC Navigation")]
    [Tooltip("Cooldown between ESC presses to prevent spam")]
    [SerializeField] private float inputCooldown = 0.2f;

    [Header("Panel Hotkey Bindings")]
    [Tooltip("Map action names to panel IDs (e.g., 'Crafting Menu' → 'CraftingPanel')")]
    [SerializeField] private List<UIBinding> uiBindings = new();


    [Header("World Input Blocking")]
    [Tooltip("If true, world input is blocked when any modal panel is open")]
    public bool IsBlockingWorldInput { get; private set; }

    // Event for WorldInputBlocker components
    public event Action<bool> OnWorldInputBlockedChanged;


    // Panel registry
    private Dictionary<string, IPanelController> panels = new Dictionary<string, IPanelController>();
    // Store delegate references
    private Dictionary<string, Action> buttonCallbacks = new();
    private Stack<IPanelController> modalStack = new Stack<IPanelController>();
    private Queue<PanelRequest> requestQueue = new Queue<PanelRequest>();

    private float lastCancelTime;

    protected override void Awake()
    {
        base.Awake();   
        if(uiDocument == null)
            uiDocument = GetComponent<UIDocument>();
    }

    void Start()
    {
        SetUpUIBindings();

        // Sync the top-bar guild stars to actual progression on game start
        // (clears the placeholder markup) and keep them in sync as stars are earned.
        ProgressionManager.OnStarEarned += OnStarEarned;
        RefreshGuildStars();

        SetUpClickUpgradeButton();
        SetUpUnitsBadge();

        sealLabel = uiDocument != null ? uiDocument.rootVisualElement?.Q<Label>("seal-label") : null;
        GameSignals.OnSealsChanged += OnSealsChanged;
        if (sealLabel != null) sealLabel.text = (ContractManager.Instance != null ? ContractManager.Instance.Seals : 0).ToString("N0");
    }

    private void OnDisable() => CleanupInputActions();

    void OnDestroy()
    {
        CleanupInputActions();
        ProgressionManager.OnStarEarned -= OnStarEarned;

        if (cursorBoardButton != null)
            cursorBoardButton.clicked -= OnCursorBoardClicked;
        GameSignals.GoldChanged -= OnGoldChangedForClickUpgrade;
        GameSignals.OnClickUpgradePurchased -= OnClickUpgradePurchased;
        GameSignals.OnSealsChanged -= OnSealsChanged;
        GameSignals.GoldChanged -= OnGoldChangedForUnits;
        GameSignals.OnAdventurerLeveledUp -= OnLeveledUpForUnits;
        GameSignals.OnAdventurerPromoted -= OnPromotedForUnits;
        GameSignals.OnUnitHired -= OnHiredForUnits;
    }

    // ═════════════════════════════════════════════
    // SEAL COUNTER (top bar) — count + a pop on change, no shower
    // ═════════════════════════════════════════════

    private Label sealLabel;

    private void OnSealsChanged(int balance)
    {
        if (sealLabel == null) return;
        sealLabel.text = balance.ToString("N0");
        var icon = sealLabel.parent?.Q<VisualElement>("seal-icon");
        if (icon == null) return;
        icon.style.scale = new Scale(new Vector2(1.35f, 1.35f));
        icon.schedule.Execute(() => icon.style.scale = StyleKeyword.Null).StartingIn(180);
    }

    void Update()
    {
        ProcessRequestQueue();

        if (unitsBadgeDirty)
        {
            unitsBadgeDirty = false;
            RefreshUnitsBadge();
        }
    }

    // ═════════════════════════════════════════════
    // UNITS BOOKMARK — badge = promote-ready count, wiggle = a promotion is affordable
    // ═════════════════════════════════════════════

    private Label unitsBadge;
    private AdventurerManager[] advManagers;
    private bool unitsBadgeDirty = true;   // dirty-flag: gold ticks often; scan at most once per frame

    private void SetUpUnitsBadge()
    {
        unitsBadge = uiDocument != null ? uiDocument.rootVisualElement?.Q<Button>("units-button")?.Q<Label>("units-badge") : null;
        GameSignals.GoldChanged += OnGoldChangedForUnits;
        GameSignals.OnAdventurerLeveledUp += OnLeveledUpForUnits;
        GameSignals.OnAdventurerPromoted += OnPromotedForUnits;
        GameSignals.OnUnitHired += OnHiredForUnits;
    }

    private void OnGoldChangedForUnits(int _) => unitsBadgeDirty = true;
    private void OnLeveledUpForUnits(AdventurerAgent _, int __) => unitsBadgeDirty = true;
    private void OnPromotedForUnits(EntityBase _, string __, string ___) => unitsBadgeDirty = true;
    private void OnHiredForUnits(EntityDef _, HireRole __) => unitsBadgeDirty = true;

    private void RefreshUnitsBadge()
    {
        if (unitsBadge == null) return;

        // Layer managers are scene objects; re-find only when the scene's set is gone (UIManager persists).
        if (advManagers == null || advManagers.Length == 0 || advManagers[0] == null)
            advManagers = FindObjectsByType<AdventurerManager>(FindObjectsInactive.Include);

        int gold = Inventory.Instance != null ? Inventory.Instance.Gold : 0;
        int ready = 0;
        bool affordable = false;
        foreach (var m in advManagers)
            if (m != null) m.CountPromoteReady(gold, ref ready, ref affordable);

        unitsBadge.text = ready.ToString();
        SetBoardBadge(unitsBadge, ready > 0, affordable);
    }

    // ═════════════════════════════════════════════
    // INPUT SYSTEM SETUP
    // ═════════════════════════════════════════════

    private void SetUpUIBindings()
    {
        if (inputActions == null)
        {
            Debug.LogError("[UIManager] InputActionAsset not assigned!");
            return;
        }

        // Bind UI 
        foreach (var binding in uiBindings)
        {
            // Setup input action
            if (binding.inputActionRef != null && binding.inputActionRef.action != null)
            {
                binding.inputActionRef.action.performed += binding.OnActionTriggered;
                binding.inputActionRef.action.Enable();
            }
            else
            {
                Debug.LogWarning($"[UIManager] UIBinding has null InputActionReference");
            }

           // Setup UI button (optional - only if buttonName is provided)
            if (!string.IsNullOrEmpty(binding.buttonName))
            {
                var button = uiDocument.rootVisualElement.Q<Button>(binding.buttonName);
                
                if (button != null)
                {
                    // Create callback and store reference for cleanup
                    var callback = new Action(binding.OnActionTriggered);
                    button.clicked += callback;
                    buttonCallbacks[binding.buttonName] = callback;
                    
                    Debug.Log($"[UIManager] Bound button '{binding.buttonName}' to action");
                }
                else
                {
                    Debug.LogWarning($"[UIManager] Button '{binding.buttonName}' not found in UXML");
                }
            }
        }
    }

    public void OnCancelPressed(VisualTreeAsset visualTreeAsset)
    {
        // Cooldown to prevent spam (use unscaledTime for pause compatibility)
        if (Time.unscaledTime - lastCancelTime < inputCooldown)
            return;

        lastCancelTime = Time.unscaledTime;

        // Close top modal panel if any
        if (modalStack.Count > 0)
        {
            var topPanel = modalStack.Peek();

            if (topPanel.State == PanelState.Open)
            {
                ClosePanel(topPanel);
            }
        }
        // Open pause menu if no panels are open
        else if (!string.IsNullOrEmpty(visualTreeAsset.name))
        {
            OpenPanel(visualTreeAsset.name);
        }
    }

    private void CleanupInputActions()
    {
        // Cleanup input actions
        foreach (var binding in uiBindings)
        {
            if (binding.inputActionRef != null && binding.inputActionRef.action != null)
            {
                binding.inputActionRef.action.performed -= binding.OnActionTriggered;
                binding.inputActionRef.action.Disable();
            }
        }

        // Cleanup button callbacks
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            foreach (var kvp in buttonCallbacks)
            {
                var button = uiDocument.rootVisualElement.Q<Button>(kvp.Key);
                if (button != null)
                {
                    button.clicked -= kvp.Value;
                }
            }
        }

        buttonCallbacks.Clear();
    }
    
    // ═════════════════════════════════════════════
    // GUILD STARS (top bar)
    // ═════════════════════════════════════════════

    private void OnStarEarned(int newStarCount) => RefreshGuildStars();

    /// <summary>
    /// Syncs the top-bar guild star icons to the current guild rating.
    /// Runs on game start — resetting the hardcoded placeholder stars to the
    /// real count (0 for a fresh guild) — and again whenever a star is earned.
    /// </summary>
    private void RefreshGuildStars()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null)
            return;

        // Scoped to the top-bar container ("stars-container"); the Guild book page
        // uses a separate "stars-display" container with its own star-1..5 icons.
        var starsContainer = uiDocument.rootVisualElement.Q<VisualElement>("stars-container");
        if (starsContainer == null)
            return;

        int currentStars = ProgressionManager.Instance != null
            ? ProgressionManager.Instance.GetCurrentStars()
            : 0;

        for (int i = 1; i <= 5; i++)
        {
            var star = starsContainer.Q<VisualElement>($"star-{i}");
            if (star == null)
                continue;

            star.EnableInClassList("star-filled", i <= currentStars);
            star.EnableInClassList("star-empty", i > currentStars);
        }
    }

    // ═════════════════════════════════════════════
    // CURSOR BOARD (top bar) — opens the Book's Cursor page; badge = upgrade affordable
    // ═════════════════════════════════════════════

    private Button cursorBoardButton;
    private VisualElement cursorBadge;
    private ClickerManager clickerManager;

    private void SetUpClickUpgradeButton()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null)
            return;

        cursorBoardButton = uiDocument.rootVisualElement.Q<Button>("cursor-board-button");
        if (cursorBoardButton == null)
        {
            Debug.LogWarning("[UIManager] 'cursor-board-button' not found in UXML");
            return;
        }

        cursorBadge = cursorBoardButton.Q<VisualElement>("cursor-badge");
        clickerManager = FindAnyObjectByType<ClickerManager>();

        cursorBoardButton.clicked += OnCursorBoardClicked;
        GameSignals.GoldChanged += OnGoldChangedForClickUpgrade;
        GameSignals.OnClickUpgradePurchased += OnClickUpgradePurchased;

        RefreshCursorBadge();
    }

    private void OnCursorBoardClicked()
    {
        FindAnyObjectByType<BookPanelController>()?.OpenToTab(5);
    }

    private void OnGoldChangedForClickUpgrade(int _) => RefreshCursorBadge();
    private void OnClickUpgradePurchased(int _) => RefreshCursorBadge();

    /// <summary>Shows the cursor board's badge while the next click upgrade is affordable.</summary>
    private void RefreshCursorBadge()
    {
        if (cursorBadge == null || clickerManager == null)
            return;

        bool affordable = Inventory.Instance != null && clickerManager.AnyCursorUpgradeAffordable(Inventory.Instance.Gold);
        SetBoardBadge(cursorBadge, affordable, affordable);
    }

    // Board-button attention wiggle: one scheduled item per button, stepped every WiggleStepMs;
    // the first WiggleAngles.Length steps tilt (USS transitions the rotate), the rest of the cycle idles.
    private static readonly float[] WiggleAngles = { 6f, -6f, 3f, 0f };
    private const long WiggleStepMs = 80;
    private const int WiggleCycleSteps = 62; // ~5s period

    /// <summary>
    /// Toggles a top-bar board badge (class "hiring-badge--visible") and, independently, the parent
    /// button's periodic tilt wiggle. The wiggle's scheduled item lives in the badge's userData.
    /// </summary>
    public static void SetBoardBadge(VisualElement badge, bool visible, bool wiggle)
    {
        if (badge == null)
            return;

        badge.EnableInClassList("hiring-badge--visible", visible);
        VisualElement button = badge.parent;

        if (badge.userData is not IVisualElementScheduledItem wiggleItem)
        {
            if (!wiggle)
                return;
            int step = 0;
            badge.userData = button.schedule.Execute(() =>
            {
                if (step < WiggleAngles.Length)
                    button.style.rotate = new Rotate(Angle.Degrees(WiggleAngles[step]));
                step = (step + 1) % WiggleCycleSteps;
            }).Every(WiggleStepMs);
            return;
        }

        if (wiggle == wiggleItem.isActive)
            return;
        if (wiggle)
        {
            wiggleItem.Resume();
        }
        else
        {
            wiggleItem.Pause();
            button.style.rotate = StyleKeyword.Null;
        }
    }

    // ═════════════════════════════════════════════
    // PANEL REGISTRATION
    // ═════════════════════════════════════════════

    public void RegisterPanel(IPanelController panel)
    {
        if (panel == null)
        {
            Debug.LogError("[UIManager] Cannot register null panel");
            return;
        }

        if (panels.ContainsKey(panel.PanelID))
        {
            Debug.LogWarning($"[UIManager] Panel '{panel.PanelID}' already registered");
            return;
        }

        panels[panel.PanelID] = panel;
        
        panel.OnOpenComplete += OnPanelOpenComplete;
        panel.OnCloseComplete += OnPanelCloseComplete;

        // Debug.Log($"[UIManager] Registered panel: {panel.PanelID}");
    }

    public void UnregisterPanel(IPanelController panel)
    {
        if (panel == null) return;

        if (panels.Remove(panel.PanelID))
        {
            panel.OnOpenComplete -= OnPanelOpenComplete;
            panel.OnCloseComplete -= OnPanelCloseComplete;
            
            if (modalStack.Contains(panel))
            {
                var tempStack = new Stack<IPanelController>();
                while (modalStack.Count > 0)
                {
                    var p = modalStack.Pop();
                    if (p != panel)
                        tempStack.Push(p);
                }
                
                while (tempStack.Count > 0)
                    modalStack.Push(tempStack.Pop());
            }

            //Debug.Log($"[UIManager] Unregistered panel: {panel.PanelID}");
        }
    }

    // ═════════════════════════════════════════════
    // PANEL OPERATIONS
    // ═════════════════════════════════════════════

    public void OpenPanel(string panelID)
    {
        if (!panels.TryGetValue(panelID, out var panel))
        {
            Debug.LogError($"[UIManager] Panel '{panelID}' not registered");
            return;
        }

        OpenPanel(panel);
    }

    public void OpenPanel(IPanelController panel)
    {
        if (panel == null) return;

        if (panel.State == PanelState.Open || panel.State == PanelState.Opening)
            return;

        if (panel.State == PanelState.Closing)
        {
            requestQueue.Enqueue(new PanelRequest(PanelRequestType.Open, panel));
            return;
        }

        if (panel.IsModal && modalStack.Count > 0)
        {
            var currentTop = modalStack.Peek();
            if (currentTop.State == PanelState.Open)
                currentTop.OnLoseFocus();
        }

        if (panel.IsModal)
            modalStack.Push(panel);

        if (panel.Open())
        {
            // Bring the TemplateContainer (not the inner element) to front so
            // the active panel always renders above inactive TCs in body.
            if (panel.RootElement?.parent != null)
                panel.RootElement.parent.BringToFront();
        }
        else
        {
            if (panel.IsModal && modalStack.Count > 0 && modalStack.Peek() == panel)
                modalStack.Pop();
        }
    }

    public void ClosePanel(string panelID)
    {
        if (!panels.TryGetValue(panelID, out var panel))
        {
            //Debug.LogError($"[UIManager] Panel '{panelID}' not registered");
            return;
        }

        ClosePanel(panel);
    }

    public void ClosePanel(IPanelController panel)
    {
        if (panel == null) return;

        if (panel.State == PanelState.Closed || panel.State == PanelState.Closing)
            return;

        if (!panel.CanClose())
        {
            Debug.Log($"[UIManager] Panel '{panel.PanelID}' vetoed close request");
            return;
        }

        if (panel.State == PanelState.Opening)
        {
            requestQueue.Enqueue(new PanelRequest(PanelRequestType.Close, panel));
            return;
        }

        if (panel.Close())
        {
            // Stack cleanup happens in OnPanelCloseComplete
        }
    }

    public void TogglePanel(VisualTreeAsset panelID)
    {
        if (!panels.TryGetValue(panelID.name, out var panel))
        {
            Debug.LogError($"[UIManager] Panel '{panelID}' not registered");
            return;
        }

        if (panel.State == PanelState.Open)
            ClosePanel(panel);
        else if (panel.State == PanelState.Closed)
            OpenPanel(panel);
    }

    public bool IsPanelOpen(string panelID)
    {
        return panels.TryGetValue(panelID, out var panel) && panel.State == PanelState.Open;
    }

    // ═════════════════════════════════════════════
    // PANEL LIFECYCLE CALLBACKS
    // ═════════════════════════════════════════════

    private void OnPanelOpenComplete(IPanelController panel)
    {
        Debug.Log($"[UIManager] Panel opened: {panel.PanelID}, IsModal: {panel.IsModal}, Stack count: {modalStack.Count}");
        
        UpdateWorldInputBlockState();

        if (panel.IsModal && modalStack.Count > 0 && modalStack.Peek() == panel)
            panel.OnFocus();
    }

    private void OnPanelCloseComplete(IPanelController panel)
    {
        if (panel.IsModal && modalStack.Count > 0 && modalStack.Peek() == panel)
        {
            modalStack.Pop();
            
            if (modalStack.Count > 0)
            {
                var newTop = modalStack.Peek();
                if (newTop.State == PanelState.Open)
                    newTop.OnFocus();
            }
        }

        UpdateWorldInputBlockState();
    }

    // ═════════════════════════════════════════════
    // WORLD INPUT BLOCKING
    // ═════════════════════════════════════════════

    private void UpdateWorldInputBlockState()
    {
        bool shouldBlock = false;

        foreach (var panel in panels.Values)
        {
            if (panel.State == PanelState.Open && panel.BlocksWorldInput)
            {
                shouldBlock = true;
                break;
            }
        }

        if (IsBlockingWorldInput != shouldBlock)
        {
            IsBlockingWorldInput = shouldBlock;
            BroadcastInputBlockState();
        }
    }

    private void BroadcastInputBlockState()
    {
        OnWorldInputBlockedChanged?.Invoke(IsBlockingWorldInput);
    }

    // ═════════════════════════════════════════════
    // REQUEST QUEUE
    // ═════════════════════════════════════════════

    private void ProcessRequestQueue()
    {
        if (requestQueue.Count == 0)
            return;

        var request = requestQueue.Dequeue();

        if (request.type == PanelRequestType.Open && request.panel.State == PanelState.Closed)
        {
            OpenPanel(request.panel);
        }
        else if (request.type == PanelRequestType.Close && request.panel.State == PanelState.Open)
        {
            ClosePanel(request.panel);
        }
    }

    // ═════════════════════════════════════════════
    // HELPERS
    // ═════════════════════════════════════════════

    private struct PanelRequest
    {
        public PanelRequestType type;
        public IPanelController panel;

        public PanelRequest(PanelRequestType type, IPanelController panel)
        {
            this.type = type;
            this.panel = panel;
        }
    }

    private enum PanelRequestType
    {
        Open,
        Close
    }
}