using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Tracks the screen position of a UI Toolkit element.
/// Used for VFX that need to fly toward UI (coins → gold counter).
/// Updates every frame to handle UI layout changes and resolution scaling.
/// </summary>
public class UITargetMarker : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private string elementName = "CoinLabel";
    
    [Header("Panel Settings")]
    [Tooltip("Reference resolution from PanelSettings (must match your PanelSettings asset)")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    public static Vector2 ScreenPosition { get; private set; }

    private VisualElement trackedElement;
    private bool isInitialized = false;

    void Awake()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }
    }

    void Start()
    {
        InitializeTracking();
    }

    private void InitializeTracking()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogError("[UITargetMarker] UIDocument or root element is null!");
            return;
        }

        trackedElement = uiDocument.rootVisualElement.Q<VisualElement>(elementName);

        if (trackedElement == null)
        {
            Debug.LogError($"[UITargetMarker] Element '{elementName}' not found in UI Document!");
            return;
        }

        isInitialized = true;
    }

    void LateUpdate()
    {
        UpdateScreenPosition();
    }

    private void UpdateScreenPosition()
    {
        if (!isInitialized || trackedElement == null)
        {
            return;
        }

        Rect worldBound = trackedElement.worldBound;
        
        if (worldBound.width <= 0 || worldBound.height <= 0)
        {
            return;
        }

        Vector2 panelCenter = new Vector2(
            worldBound.x + worldBound.width * 0.5f,
            worldBound.y + worldBound.height * 0.5f
        );
        
        IPanel panel = trackedElement.panel;
        
        if (panel == null)
        {
            return;
        }
        
        Vector2 screenPosition = new Vector2(
            (panelCenter.x / referenceResolution.x) * Screen.width,
            (panelCenter.y / referenceResolution.y) * Screen.height
        );
        
        screenPosition.y = Screen.height - screenPosition.y;

        ScreenPosition = screenPosition;
    }
}