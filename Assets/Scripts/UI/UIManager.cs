using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Centralized UI management system.
/// - Manages panel lifecycle (open/close/focus)
/// - Handles ESC navigation via stack
/// - Blocks world input when modal panels are open
/// - Prevents edge cases with animation states and queued requests
/// </summary>
public class UIManager : MonoBehaviour
{
    private static UIManager _instance;
    public static UIManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[UIManager]");
                _instance = go.AddComponent<UIManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    [Header("Input")]
    [SerializeField] private InputActionReference cancelAction;
    
    [Header("Settings")]
    [SerializeField] private float inputCooldown = 0.2f;
    
    // Panel registry: all panels by ID
    private readonly Dictionary<string, IPanelController> panels = new();
    
    // Modal panel stack: panels that participate in ESC navigation
    private readonly Stack<IPanelController> modalStack = new();
    
    // Queued requests during transitions
    private readonly Queue<PanelRequest> requestQueue = new();
    
    private float lastInputTime;

    public bool IsBlockingWorldInput
    {
        get
        {
            foreach (var panel in modalStack)
            {
                if (panel.BlocksWorldInput && (panel.State == PanelState.Open || panel.State == PanelState.Opening))
                    return true;
            }
            return false;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        if (cancelAction != null)
            cancelAction.action.performed += OnCancelPerformed;
    }

    private void OnDisable()
    {
        if (cancelAction != null)
            cancelAction.action.performed -= OnCancelPerformed;
    }

    private void Update()
    {
        ProcessRequestQueue();
    }

    // ─────────────────────────────────────────────
    // PANEL REGISTRATION
    // ─────────────────────────────────────────────

    /// <summary>
    /// Register a panel with the UIManager.
    /// Call this in panel's Start() or Awake().
    /// </summary>
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
        
        // Subscribe to completion events
        panel.OnOpenComplete += OnPanelOpenComplete;
        panel.OnCloseComplete += OnPanelCloseComplete;
    }

    /// <summary>
    /// Unregister a panel (call in OnDestroy)
    /// </summary>
    public void UnregisterPanel(IPanelController panel)
    {
        if (panel == null) return;

        if (panels.Remove(panel.PanelID))
        {
            panel.OnOpenComplete -= OnPanelOpenComplete;
            panel.OnCloseComplete -= OnPanelCloseComplete;
            
            // Remove from stack if present
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
        }
    }

    // ─────────────────────────────────────────────
    // PANEL OPERATIONS
    // ─────────────────────────────────────────────

    /// <summary>
    /// Open a panel by ID
    /// </summary>
    public void OpenPanel(string panelID)
    {
        if (!panels.TryGetValue(panelID, out var panel))
        {
            Debug.LogError($"[UIManager] Panel '{panelID}' not registered");
            return;
        }

        OpenPanel(panel);
    }

    /// <summary>
    /// Open a panel by reference
    /// </summary>
    public void OpenPanel(IPanelController panel)
    {
        if (panel == null) return;

        // Already open or opening
        if (panel.State == PanelState.Open || panel.State == PanelState.Opening)
            return;

        // Currently closing - queue the open
        if (panel.State == PanelState.Closing)
        {
            requestQueue.Enqueue(new PanelRequest(PanelRequestType.Open, panel));
            return;
        }

        // Lose focus on current top panel
        if (panel.IsModal && modalStack.Count > 0)
        {
            var currentTop = modalStack.Peek();
            if (currentTop.State == PanelState.Open)
                currentTop.OnLoseFocus();
        }

        // Execute open
        if (panel.Open())
        {
            if (panel.IsModal)
                modalStack.Push(panel);
        }
    }

    /// <summary>
    /// Close a panel by ID
    /// </summary>
    public void ClosePanel(string panelID)
    {
        if (!panels.TryGetValue(panelID, out var panel))
        {
            Debug.LogError($"[UIManager] Panel '{panelID}' not registered");
            return;
        }

        ClosePanel(panel);
    }

    /// <summary>
    /// Close a panel by reference
    /// </summary>
    public void ClosePanel(IPanelController panel)
    {
        if (panel == null) return;

        // Already closed or closing
        if (panel.State == PanelState.Closed || panel.State == PanelState.Closing)
            return;

        // Check if panel allows closing
        if (!panel.CanClose())
            return;

        // Currently opening - queue the close
        if (panel.State == PanelState.Opening)
        {
            requestQueue.Enqueue(new PanelRequest(PanelRequestType.Close, panel));
            return;
        }

        // Execute close
        panel.Close();
    }

    /// <summary>
    /// Close the top modal panel (called by ESC key)
    /// </summary>
    public void CloseTopPanel()
    {
        if (modalStack.Count == 0)
            return;

        var topPanel = modalStack.Peek();
        ClosePanel(topPanel);
    }

    /// <summary>
    /// Check if a specific panel is open
    /// </summary>
    public bool IsPanelOpen(string panelID)
    {
        if (!panels.TryGetValue(panelID, out var panel))
            return false;

        return panel.State == PanelState.Open || panel.State == PanelState.Opening;
    }

    // ─────────────────────────────────────────────
    // EVENT HANDLERS
    // ─────────────────────────────────────────────

    private void OnCancelPerformed(InputAction.CallbackContext context)
    {
        // Input cooldown to prevent spam (use unscaledTime for pause compatibility)
        if (Time.unscaledTime - lastInputTime < inputCooldown)
            return;

        lastInputTime = Time.unscaledTime;
        
        // If no panels open, try to open pause menu
        if (modalStack.Count == 0)
        {
            // Try to open pause menu if it exists
            if (panels.ContainsKey("PauseMenu"))
            {
                OpenPanel("PauseMenu");
            }
        }
        else
        {
            // Close top panel
            CloseTopPanel();
        }
    }

    private void OnPanelOpenComplete(IPanelController panel)
    {
        if (panel.IsModal && modalStack.Count > 0 && modalStack.Peek() == panel)
            panel.OnFocus();
    }

    private void OnPanelCloseComplete(IPanelController panel)
    {
        // Remove from stack
        if (panel.IsModal && modalStack.Count > 0 && modalStack.Peek() == panel)
        {
            modalStack.Pop();
            
            // Restore focus to new top panel
            if (modalStack.Count > 0)
            {
                var newTop = modalStack.Peek();
                if (newTop.State == PanelState.Open)
                    newTop.OnFocus();
            }
        }
    }

    // ─────────────────────────────────────────────
    // REQUEST QUEUE
    // ─────────────────────────────────────────────

    private void ProcessRequestQueue()
    {
        if (requestQueue.Count == 0)
            return;

        // Process one request per frame to avoid stacking operations
        var request = requestQueue.Dequeue();

        // Only process if panel is in valid state
        if (request.type == PanelRequestType.Open && request.panel.State == PanelState.Closed)
        {
            OpenPanel(request.panel);
        }
        else if (request.type == PanelRequestType.Close && request.panel.State == PanelState.Open)
        {
            ClosePanel(request.panel);
        }
    }

    // ─────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────

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