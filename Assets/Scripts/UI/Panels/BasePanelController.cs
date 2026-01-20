using System;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

/// <summary>
/// Base implementation of IPanelController.
/// Handles common panel lifecycle and animation patterns.
/// Inherit from this to create new panels quickly.
/// </summary>
public abstract class BasePanelController : MonoBehaviour, IPanelController
{
    [Header("Panel Settings")]
    [SerializeField] protected bool blocksWorldInput = true;
    [SerializeField] protected bool isModal = true;
    [SerializeField] protected float openCloseDuration = 0.3f;
    
    public abstract string PanelID { get; }
    public abstract VisualElement RootElement { get; }
    
    public PanelState State { get; protected set; } = PanelState.Closed;
    public bool BlocksWorldInput => blocksWorldInput;
    public bool IsModal => isModal;

    public event Action<IPanelController> OnOpenComplete;
    public event Action<IPanelController> OnCloseComplete;

    protected virtual void Start()
    {
        UIManager.Instance.RegisterPanel(this);
    }

    protected virtual void OnDestroy()
    {
        UIManager.Instance.UnregisterPanel(this);
    }

    // ─────────────────────────────────────────────
    // IPANELCONTROLLER IMPLEMENTATION
    // ─────────────────────────────────────────────

    public virtual bool Open()
    {
        if (State != PanelState.Closed)
            return false;

        State = PanelState.Opening;
        OnOpenStart();
        StartCoroutine(OpenAnimation());
        return true;
    }

    public virtual bool Close()
    {
        if (State != PanelState.Open)
            return false;

        State = PanelState.Closing;
        OnCloseStart();
        StartCoroutine(CloseAnimation());
        return true;
    }

    public virtual void OnFocus() { }
    public virtual void OnLoseFocus() { }
    public virtual bool CanClose() => true;

    // ─────────────────────────────────────────────
    // LIFECYCLE HOOKS (override in subclasses)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Called when Open() is called, before animation starts
    /// </summary>
    protected virtual void OnOpenStart() { }

    /// <summary>
    /// Called when Close() is called, before animation starts
    /// </summary>
    protected virtual void OnCloseStart() { }

    // ─────────────────────────────────────────────
    // ANIMATIONS (override for custom behavior)
    // ─────────────────────────────────────────────

    protected virtual IEnumerator OpenAnimation()
    {
        if (RootElement == null)
        {
            State = PanelState.Open;
            OnOpenComplete?.Invoke(this);
            yield break;
        }

        RootElement.style.display = DisplayStyle.Flex;
        
        float elapsed = 0f;
        while (elapsed < openCloseDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / openCloseDuration);
            RootElement.style.opacity = alpha;
            yield return null;
        }

        RootElement.style.opacity = 1f;
        State = PanelState.Open;
        OnOpenComplete?.Invoke(this);
    }

    protected virtual IEnumerator CloseAnimation()
    {
        if (RootElement == null)
        {
            State = PanelState.Closed;
            OnCloseComplete?.Invoke(this);
            yield break;
        }

        float elapsed = 0f;
        float startAlpha = RootElement.resolvedStyle.opacity;
        
        while (elapsed < openCloseDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, 0f, elapsed / openCloseDuration);
            RootElement.style.opacity = alpha;
            yield return null;
        }

        RootElement.style.opacity = 0f;
        RootElement.style.display = DisplayStyle.None;
        State = PanelState.Closed;
        OnCloseComplete?.Invoke(this);
    }
}
