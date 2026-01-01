using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Pulses a UI Toolkit element using USS transitions.
/// Add 'pulse-target' class to elements in UXML, define animation in USS.
/// Call Pulse() manually to trigger animation.
/// </summary>
public class IconPulse : MonoBehaviour
{
    [Header("UI Toolkit")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private string elementName = "Coin";
    
    [Header("USS Class Names")]
    [SerializeField] private string pulseClassName = "pulsing";
    
    [Header("Timing")]
    [SerializeField] private int pulseDurationMs = 180;
    
    private VisualElement targetElement;
    private IVisualElementScheduledItem scheduledRemoval;
    
    void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();
    }
    
    void Start()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogError("[IconPulse] UIDocument or root element is null!");
            return;
        }
        
        targetElement = uiDocument.rootVisualElement.Q<VisualElement>(elementName);
        
        if (targetElement == null)
        {
            Debug.LogError($"[IconPulse] Element '{elementName}' not found!");
            return;
        }
    }
    
    void OnDisable()
    {
        scheduledRemoval?.Pause();
    }
    
    /// <summary>
    /// Trigger pulse animation. Call from anywhere.
    /// USS handles the actual animation via transitions.
    /// </summary>
    public void Pulse()
    {
        if (targetElement == null) return;
        
        scheduledRemoval?.Pause();
        
        targetElement.AddToClassList(pulseClassName);
        
        scheduledRemoval = targetElement.schedule.Execute(() => {
            targetElement.RemoveFromClassList(pulseClassName);
        }).StartingIn(pulseDurationMs);
    }
}