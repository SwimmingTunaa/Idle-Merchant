using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Central UI management system.
/// Handles panel lifecycle, navigation stack, ESC behavior, and hotkey panel opening.
/// Supports new Input System for panel hotkeys.
/// </summary>
public class UIManager : PersistentSingleton<UIManager>
{
    [Header("Input System")]
    [SerializeField] private InputActionAsset inputActions;
    
    [Header("ESC Navigation")]
    [Tooltip("Action map name for cancel/ESC (e.g., 'UI')")]
    [SerializeField] private string cancelActionMapName = "UI";
    
    [Tooltip("Action name for cancel/ESC (e.g., 'Cancel')")]
    [SerializeField] private string cancelActionName = "Cancel";
    
    [Tooltip("Panel ID to open when ESC pressed with no panels open (e.g., 'PauseMenu')")]
    [SerializeField] private string pauseMenuPanelID = "PauseMenu";
    
    [Tooltip("Cooldown between ESC presses to prevent spam")]
    [SerializeField] private float inputCooldown = 0.2f;

    [Header("Panel Hotkey Bindings")]
    [Tooltip("Map action names to panel IDs (e.g., 'Crafting Menu' → 'CraftingPanel')")]
    [SerializeField] private List<PanelHotkeyBinding> panelHotkeys = new List<PanelHotkeyBinding>();

    [Serializable]
    public class PanelHotkeyBinding
    {
        [Tooltip("Action map name (e.g., 'UI')")]
        public string actionMapName = "UI";
        
        [Tooltip("Action name (e.g., 'Crafting Menu')")]
        public string actionName;
        
        [Tooltip("Panel ID to open/toggle (e.g., 'CraftingPanel')")]
        public string panelID;
        
        [Tooltip("If true, pressing again closes the panel")]
        public bool allowToggle = true;
    }

    [Header("World Input Blocking")]
    [Tooltip("If true, world input is blocked when any modal panel is open")]
    public bool IsBlockingWorldInput { get; private set; }

    // Event for WorldInputBlocker components
    public event Action<bool> OnWorldInputBlockedChanged;

    // Panel registry
    private Dictionary<string, IPanelController> panels = new Dictionary<string, IPanelController>();
    private Stack<IPanelController> modalStack = new Stack<IPanelController>();
    private Queue<PanelRequest> requestQueue = new Queue<PanelRequest>();

    // Input System
    private InputAction cancelAction;
    private Dictionary<string, InputAction> boundActions = new Dictionary<string, InputAction>();
    private float lastCancelTime;

    protected override void Awake()
    {
        base.Awake();   
        SetupInputActions();
    }

    void OnDestroy()
    {
        CleanupInputActions();
    }

    void Update()
    {
        ProcessRequestQueue();
    }

    // ═════════════════════════════════════════════
    // INPUT SYSTEM SETUP
    // ═════════════════════════════════════════════

    private void SetupInputActions()
    {
        if (inputActions == null)
        {
            //Debug.LogError("[UIManager] InputActionAsset not assigned!");
            return;
        }

        // Setup Cancel/ESC action
        SetupCancelAction();

        // Bind panel hotkeys
        foreach (var binding in panelHotkeys)
        {
            if (string.IsNullOrEmpty(binding.actionMapName) || string.IsNullOrEmpty(binding.actionName))
            {
                //Debug.LogWarning($"[UIManager] Invalid hotkey binding: empty action map or action name");
                continue;
            }

            var actionMap = inputActions.FindActionMap(binding.actionMapName);
            if (actionMap == null)
            {
                //Debug.LogWarning($"[UIManager] Action map '{binding.actionMapName}' not found!");
                continue;
            }

            var action = actionMap.FindAction(binding.actionName);
            if (action == null)
            {
                Debug.LogWarning($"[UIManager] Action '{binding.actionName}' not found in map '{binding.actionMapName}'!");
                continue;
            }

            // Subscribe to action
            action.performed += ctx => OnPanelHotkeyPressed(binding);
            action.Enable();

            boundActions[binding.actionName] = action;

            Debug.Log($"[UIManager] Bound '{binding.actionName}' → '{binding.panelID}'");
        }
    }

    private void SetupCancelAction()
    {
        var actionMap = inputActions.FindActionMap(cancelActionMapName);
        if (actionMap == null)
        {
            //Debug.LogWarning($"[UIManager] Cancel action map '{cancelActionMapName}' not found!");
            return;
        }

        cancelAction = actionMap.FindAction(cancelActionName);
        if (cancelAction == null)
        {
            //Debug.LogWarning($"[UIManager] Cancel action '{cancelActionName}' not found!");
            return;
        }

        cancelAction.performed += OnCancelPressed;
        
        // CRITICAL: Enable action map with unscaled time for pause compatibility
        actionMap.Enable();

        //Debug.Log($"[UIManager] Bound ESC/Cancel action: {cancelActionMapName}/{cancelActionName}");
    }

    private void OnCancelPressed(InputAction.CallbackContext context)
    {
        // Cooldown to prevent spam (use unscaledTime for pause compatibility)
        if (Time.unscaledTime - lastCancelTime < inputCooldown)
            return;

        lastCancelTime = Time.unscaledTime;

        //Debug.Log($"[UIManager] ESC pressed. Modal stack count: {modalStack.Count}");

        // Close top modal panel if any
        if (modalStack.Count > 0)
        {
            var topPanel = modalStack.Peek();
            //Debug.Log($"[UIManager] Top panel: {topPanel.PanelID}, State: {topPanel.State}");
            
            if (topPanel.State == PanelState.Open)
            {
                //Debug.Log($"[UIManager] Closing panel: {topPanel.PanelID}");
                ClosePanel(topPanel);
            }
        }
        // Open pause menu if no panels are open
        else if (!string.IsNullOrEmpty(pauseMenuPanelID))
        {
            //Debug.Log($"[UIManager] Opening pause menu: {pauseMenuPanelID}");
            OpenPanel(pauseMenuPanelID);
        }
    }

    private void CleanupInputActions()
    {
        // Cleanup cancel action
        if (cancelAction != null)
        {
            cancelAction.performed -= OnCancelPressed;
            cancelAction.Disable();
        }

        // Cleanup hotkey actions
        foreach (var action in boundActions.Values)
        {
            action.Disable();
        }
        boundActions.Clear();
    }

    private void OnPanelHotkeyPressed(PanelHotkeyBinding binding)
    {
        if (string.IsNullOrEmpty(binding.panelID))
        {
            //Debug.LogWarning($"[UIManager] Hotkey action '{binding.actionName}' has no panel ID assigned");
            return;
        }

        if (!panels.TryGetValue(binding.panelID, out var panel))
        {
            //Debug.LogWarning($"[UIManager] Panel '{binding.panelID}' not registered for hotkey '{binding.actionName}'");
            return;
        }

        // Toggle behavior
        if (binding.allowToggle && panel.State == PanelState.Open)
        {
            ClosePanel(panel);
        }
        else if (panel.State == PanelState.Closed)
        {
            OpenPanel(panel);
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

        Debug.Log($"[UIManager] Registered panel: {panel.PanelID}");
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
            //Debug.LogError($"[UIManager] Panel '{panelID}' not registered");
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

        if (panel.Open())
        {
            // Bring to front by moving to end of parent's children
            if (panel.RootElement != null && panel.RootElement.parent != null)
            {
                panel.RootElement.BringToFront();
            }
            
            if (panel.IsModal)
            {
                modalStack.Push(panel);
            }
        }

        // Push to stack BEFORE opening so it's there when OnOpenComplete fires
        if (panel.IsModal)
        {
            modalStack.Push(panel);
            Debug.Log($"[UIManager] Pushed {panel.PanelID} to modal stack. Stack count: {modalStack.Count}");
        }

        if (!panel.Open())
        {
            // Open failed, remove from stack
            if (panel.IsModal && modalStack.Count > 0 && modalStack.Peek() == panel)
            {
                modalStack.Pop();
                //Debug.LogWarning($"[UIManager] Open failed for {panel.PanelID}, removed from stack");
            }
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

    public void TogglePanel(string panelID)
    {
        if (!panels.TryGetValue(panelID, out var panel))
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