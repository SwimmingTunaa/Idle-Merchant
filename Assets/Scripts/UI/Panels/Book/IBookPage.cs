using UnityEngine.UIElements;

/// <summary>
/// Implemented by MonoBehaviour page controllers that live inside the BookPanelController.
/// OnPageShown/OnPageHidden replace BasePanelController's open/close lifecycle.
/// All signal subscriptions must happen in OnPageShown and be cleaned up in OnPageHidden.
/// </summary>
public interface IBookPage
{
    string PageTitle { get; }

    /// <summary>
    /// Called when this tab becomes active. Clone UXML into the provided containers,
    /// query elements, subscribe to signals, and populate data.
    /// </summary>
    void OnPageShown(VisualElement leftPage, VisualElement rightPage);

    /// <summary>
    /// Called when switching away or when the book closes.
    /// Unsubscribe all signals and null out element references.
    /// </summary>
    void OnPageHidden();
}
