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

    void OnEnable() => SetUpUIBindings();

    private void OnDisable() => CleanupInputActions();

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

        // Push to stack BEFORE opening so it's there when OnOpenComplete fires, allows you to close pause menu
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