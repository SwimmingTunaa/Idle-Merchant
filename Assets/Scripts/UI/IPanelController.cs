using System;
using UnityEngine.UIElements;

/// <summary>
/// Interface for all UI panels managed by UIManager.
/// Panels control their content, UIManager controls their lifecycle.
/// </summary>
public interface IPanelController
{
    /// <summary>
    /// Unique identifier for this panel (use GetType().Name or custom string)
    /// </summary>
    string PanelID { get; }
    
    /// <summary>
    /// Root VisualElement of this panel
    /// </summary>
    VisualElement RootElement { get; }
    
    /// <summary>
    /// Current state of the panel
    /// </summary>
    PanelState State { get; }
    
    /// <summary>
    /// If true, blocks world input when this panel is focused
    /// </summary>
    bool BlocksWorldInput { get; }
    
    /// <summary>
    /// If true, panel goes on navigation stack and ESC closes it
    /// Modal panels participate in navigation, persistent panels (HUD) don't
    /// </summary>
    bool IsModal { get; }
    
    /// <summary>
    /// Called when panel should open. Return true if open started successfully.
    /// Panel should transition to Opening state, then call OnOpenComplete when done.
    /// </summary>
    bool Open();
    
    /// <summary>
    /// Called when panel should close. Return true if close started successfully.
    /// Panel should transition to Closing state, then call OnCloseComplete when done.
    /// </summary>
    bool Close();
    
    /// <summary>
    /// Called when panel gains focus (becomes top of stack)
    /// </summary>
    void OnFocus();
    
    /// <summary>
    /// Called when panel loses focus (another panel opened on top)
    /// </summary>
    void OnLoseFocus();
    
    /// <summary>
    /// Check if panel can be closed. Returns false if unsaved changes, mid-transaction, etc.
    /// UIManager will respect this and not force close.
    /// </summary>
    bool CanClose();
    
    /// <summary>
    /// Event raised when panel completes opening animation
    /// </summary>
    event Action<IPanelController> OnOpenComplete;
    
    /// <summary>
    /// Event raised when panel completes closing animation
    /// </summary>
    event Action<IPanelController> OnCloseComplete;
}

/// <summary>
/// Panel lifecycle states
/// </summary>
public enum PanelState
{
    Closed,   // Not visible, not on stack
    Opening,  // Animation in progress, requests queued
    Open,     // Fully open and interactive
    Closing   // Animation in progress, ignores input
}
