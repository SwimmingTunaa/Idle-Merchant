using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit version of GameVariableReader.
/// Reads GameVariable ScriptableObject and updates UI Toolkit Label.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GameVariableReaderUITK : MonoBehaviour
{
    [Header("Source Variable")]
    [SerializeField] private GameVariable variable;

    [Header("UI Element")]
    [Tooltip("Name of the Label element in UXML (e.g., 'GoldLabel')")]
    [SerializeField] private string labelName = "GoldLabel";

    [Header("Formatting")]
    [SerializeField] private string prefix = "";
    [SerializeField] private string suffix = "";
    [SerializeField] private string format = "0";

    private Label label;
    private UIDocument uiDocument;

    void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    void OnEnable()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogError("[GameVariableReaderUITK] UIDocument or root element is null");
            return;
        }

        // Find label by name
        label = uiDocument.rootVisualElement.Q<Label>(labelName);
        
        if (label == null)
        {
            Debug.LogError($"[GameVariableReaderUITK] Label '{labelName}' not found in UXML");
            return;
        }

        // Subscribe to gold changes
        GameSignals.GoldChanged += OnGoldChanged;
        
        // Initial update
        UpdateLabel();
    }

    void OnDisable()
    {
        GameSignals.GoldChanged -= OnGoldChanged;
    }

    private void OnGoldChanged(int newValue)
    {
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (variable == null || label == null) return;

        string value = variable.type switch
        {
            GameVariable.VarType.Integer => variable.Int.ToString(format),
            GameVariable.VarType.Float => variable.Float.ToString(format),
            GameVariable.VarType.Bool => variable.Bool ? "True" : "False",
            _ => "???"
        };

        label.text = $"{prefix}{value}{suffix}";
    }
}