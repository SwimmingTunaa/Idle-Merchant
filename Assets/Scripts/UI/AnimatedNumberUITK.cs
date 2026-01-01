using UnityEngine;
using UnityEngine.UIElements;

/// Smoothly animates a UI Toolkit Label's number (e.g., coins).
public class AnimatedNumberUITK : MonoBehaviour
{
    [Header("UI Toolkit")]
    [SerializeField] private UIDocument uiDocument;     // auto-found if null
    [SerializeField] private Label label;               // direct ref if you drag it at runtime (via runtime panel)
    [SerializeField] private string labelName = "CoinLabel"; // fallback query if label not assigned

    [Header("Behaviour")]
    [SerializeField] private float speed = 10f;         // higher = faster catch-up
    [SerializeField] private bool abbreviate = false;   // 12,300 -> 12.3K (optional)
    [SerializeField] private string format = "N0";      // default 1,234

    private float currentValue;
    private float targetValue;
    private bool animating;

    void OnEnable()
    {
        ResolveLabel();
        // Optional: auto-bind to your gold event
        GameEvents.GoldChanged += SetTargetValue;
        // Initialize to current gold if available
        if (Inventory.Instance != null) SetInstant(Inventory.Instance.Gold);
    }

    void OnDisable()
    {
        GameEvents.GoldChanged -= SetTargetValue;
    }

    void Update()
    {
        if (!animating || label == null) return;

        currentValue = Mathf.Lerp(currentValue, targetValue, Time.deltaTime * speed);
        if (Mathf.Abs(targetValue - currentValue) < 0.01f)
        {
            currentValue = targetValue;
            animating = false;
        }

        label.text = abbreviate
            ? Abbrev(Mathf.FloorToInt(currentValue))
            : Mathf.FloorToInt(currentValue).ToString(format);
    }

    public void SetTargetValue(int value)
    {
        targetValue = value;
        // optional pop
        PopOnce();
        animating = true;
    }

    public void SetInstant(int value)
    {
        currentValue = targetValue = value;
        label.text = abbreviate ? Abbrev(value) : value.ToString(format);
        animating = false;
    }

    private void ResolveLabel()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        if (label == null && uiDocument) label = uiDocument.rootVisualElement?.Q<Label>(labelName);
    }

    // Tiny pop using UITK style.scale
    private void PopOnce()
    {
        if (label == null) return;
        label.style.scale = new Scale(new Vector3(1.05f, 1.05f, 1f));
        // schedule back to 1 after ~0.18s
        label.schedule.Execute(() => label.style.scale = new Scale(Vector3.one)).StartingIn(180);
    }

    private static string Abbrev(int n)
    {
        if (n >= 1_000_000_000) return (n / 1_000_000_000f).ToString("0.#") + "B";
        if (n >= 1_000_000)     return (n / 1_000_000f).ToString("0.#") + "M";
        if (n >= 1_000)         return (n / 1_000f).ToString("0.#") + "K";
        return n.ToString("N0");
    }
}
