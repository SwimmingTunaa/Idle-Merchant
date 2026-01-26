using System;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Linq;

/// <summary>
/// Base implementation of IPanelController with virtual methods for complex panels.
/// Provides default implementations, registration, state management, and optional overlay.
/// Override Open/Close/animations for custom behavior.
/// </summary>
public abstract class BasePanelController : MonoBehaviour, IPanelController
{
    [Header("UXML References")]
    [SerializeField] protected UIDocument uiDocument;
    [SerializeField] protected VisualTreeAsset panelAsset;


    [Header("Panel Settings")]
    [SerializeField] protected bool blocksWorldInput = true;
    [SerializeField] protected bool isModal = true;
    [SerializeField] protected float openCloseDuration = 0.3f;
    
    [Header("Visual Settings")]
    [SerializeField] protected bool hasOverlay = true;
    [Tooltip("Background overlay color (only if hasOverlay = true)")]
    [SerializeField] protected Color overlayColor = new Color(0, 0, 0, 0.7f);
    
    [Header("Debug")]
    [SerializeField] protected bool showDebugLogs = false;
    
    // Abstract - must implement in subclass
    public abstract string PanelID { get; }
    public VisualElement RootElement => panel;
    protected VisualElement panel;
    
    // Managed by base class
    public PanelState State { get; protected set; } = PanelState.Closed;
    public bool BlocksWorldInput => blocksWorldInput;
    public bool IsModal => isModal;
    
    public event Action<IPanelController> OnOpenComplete;
    public event Action<IPanelController> OnCloseComplete;
    
    protected VisualElement overlayElement;
    
    // Protected methods for subclasses to invoke events
    protected void InvokeOnOpenComplete() => OnOpenComplete?.Invoke(this);
    protected void InvokeOnCloseComplete() => OnCloseComplete?.Invoke(this);
    
    // ═════════════════════════════════════════════
    // LIFECYCLE
    // ═════════════════════════════════════════════
    
    protected virtual void Start()
    {
        SetupOverlay();
        
        if (UIManager.Instance != null)
        {
            UIManager.Instance.RegisterPanel(this);
        }
        else
        {
            Debug.LogError($"[{PanelID}] UIManager.Instance is null!");
        }
    }
    
    protected virtual void OnDestroy()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UnregisterPanel(this);
        }
    }

    protected virtual void BuildUI()
    {
        var root = uiDocument.rootVisualElement.Q<VisualElement>("body");

        panel = panelAsset.CloneTree().Children().First();
        
        root.Add(panel);

    }
    
    // ═════════════════════════════════════════════
    // IPANELCONTROLLER IMPLEMENTATION (Virtual)
    // ═════════════════════════════════════════════
    
    /// <summary>
    /// Virtual Open - override for custom logic (e.g., pause game, rebuild data)
    /// </summary>
    public virtual bool Open()
    {
        if (State != PanelState.Closed)
            return false;
        
        State = PanelState.Opening;
        OnOpenStart();
        StartCoroutine(OpenAnimation());
        return true;
    }
    
    /// <summary>
    /// Virtual Close - override for custom logic (e.g., unpause game, cleanup)
    /// </summary>
    public virtual bool Close()
    {
        if (State != PanelState.Open)
            return false;
        
        State = PanelState.Closing;
        OnCloseStart();
        StartCoroutine(CloseAnimation());
        return true;
    }
    
    public virtual void OnFocus()
    {
        if (showDebugLogs)
            Debug.Log($"[{PanelID}] Gained focus");
    }
    
    public virtual void OnLoseFocus()
    {
        if (showDebugLogs)
            Debug.Log($"[{PanelID}] Lost focus");
    }
    
    public virtual bool CanClose() => true;
    
    // ═════════════════════════════════════════════
    // LIFECYCLE HOOKS
    // ═════════════════════════════════════════════
    
    /// <summary>
    /// Called before open animation starts. Override to populate data, etc.
    /// </summary>
    protected virtual void OnOpenStart()
    {        
        if (showDebugLogs)
            Debug.Log($"[{PanelID}] OnOpenStart");
    }
    
    /// <summary>
    /// Called before close animation starts. Override to cleanup, save data, etc.
    /// </summary>
    protected virtual void OnCloseStart()
    {
        if (showDebugLogs)
            Debug.Log($"[{PanelID}] OnCloseStart");
    }
    
    // ═════════════════════════════════════════════
    // ANIMATIONS (Virtual - override for custom)
    // ═════════════════════════════════════════════
    
    /// <summary>
    /// Default fade-in animation. Override for custom animations.
    /// </summary>
    protected virtual IEnumerator OpenAnimation()
    {
        if (RootElement == null)
        {
            Debug.LogWarning($"[{PanelID}] RootElement is null in OpenAnimation!");
            State = PanelState.Open;
            OnOpenComplete?.Invoke(this);
            yield break;
        }
        
        // Show elements
        RootElement.style.display = DisplayStyle.Flex;
        if (hasOverlay && overlayElement != null)
            overlayElement.style.display = DisplayStyle.Flex;
        
        // Fade in
        float elapsed = 0f;
        while (elapsed < openCloseDuration)
        {
            elapsed += GetDeltaTime();
            float t = Mathf.Clamp01(elapsed / openCloseDuration);
            
            RootElement.style.opacity = t;
            if (hasOverlay && overlayElement != null)
                overlayElement.style.opacity = t * overlayColor.a;
            
            yield return null;
        }
        
        RootElement.style.opacity = 1f;
        if (hasOverlay && overlayElement != null)
            overlayElement.style.opacity = overlayColor.a;
        
        State = PanelState.Open;
        OnOpenComplete?.Invoke(this);
        
        if (showDebugLogs)
            Debug.Log($"[{PanelID}] Open animation complete");
    }
    
    /// <summary>
    /// Default fade-out animation. Override for custom animations.
    /// </summary>
    protected virtual IEnumerator CloseAnimation()
    {
        if (RootElement == null)
        {
            Debug.LogWarning($"[{PanelID}] RootElement is null in CloseAnimation!");
            State = PanelState.Closed;
            OnCloseComplete?.Invoke(this);
            yield break;
        }
        
        // Fade out
        float elapsed = 0f;
        while (elapsed < openCloseDuration)
        {
            elapsed += GetDeltaTime();
            float t = 1f - Mathf.Clamp01(elapsed / openCloseDuration);
            
            RootElement.style.opacity = t;
            if (hasOverlay && overlayElement != null)
                overlayElement.style.opacity = t * overlayColor.a;
            
            yield return null;
        }
        
        // Hide elements
        RootElement.style.opacity = 0f;
        RootElement.style.display = DisplayStyle.None;
        if (hasOverlay && overlayElement != null)
        {
            overlayElement.style.opacity = 0f;
            overlayElement.style.display = DisplayStyle.None;
        }
        
        State = PanelState.Closed;
        OnCloseComplete?.Invoke(this);
        
        if (showDebugLogs)
            Debug.Log($"[{PanelID}] Close animation complete");
    }
    
    /// <summary>
    /// Virtual for pause-compatible panels. Override to return Time.unscaledDeltaTime.
    /// </summary>
    protected virtual float GetDeltaTime() => Time.deltaTime;
    
    // ═════════════════════════════════════════════
    // OVERLAY SETUP
    // ═════════════════════════════════════════════
    
    private void SetupOverlay()
    {
        if (!hasOverlay || RootElement == null)
            return;
        
        // Create overlay element
        overlayElement = new VisualElement();
        overlayElement.name = $"overlay";
        overlayElement.style.position = Position.Absolute;
        overlayElement.style.left = 0;
        overlayElement.style.top = 0;
        overlayElement.style.right = 0;
        overlayElement.style.bottom = 0;
        overlayElement.style.width = Length.Percent(100);
        overlayElement.style.height = Length.Percent(100);
        overlayElement.style.backgroundColor = overlayColor;
        overlayElement.style.opacity = 0f;
        overlayElement.style.display = DisplayStyle.None;
        
        // Insert overlay behind panel (at same parent level)
        var root = uiDocument.rootVisualElement.Q<VisualElement>("main-root");
        if (root != null)
        {
            root.Insert(0, overlayElement);
            
            if (showDebugLogs)
                Debug.Log($"[{PanelID}] Overlay created");
        }
        else
        {
            Debug.LogWarning($"[{PanelID}] Cannot create overlay - RootElement has no parent!");
        }
    }
}