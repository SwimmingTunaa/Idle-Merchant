using UnityEngine;
using TMPro;  

[ExecuteAlways] 
public class GameVariableReader : MonoBehaviour
{
    [Header("Source Variable")]
    public GameVariable variable;

    [Header("UI Output")]
    public TMP_Text textField; // Or Text if you use legacy UI

    [Tooltip("Optional prefix/suffix (e.g. 'Gold: ', ' HP')")]
    public string prefix = "";
    public string suffix = "";
    
    [Tooltip("How to display numeric values.")]
    public string format = "0"; // e.g., "0.0", "0.00", "#,0"

    private void Awake()
    {
        if (textField == null)
            textField = GetComponent<TMP_Text>();
    }

    private void Update()
    {
        if (variable == null || textField == null)
            return;

        switch (variable.type)
        {
            case GameVariable.VarType.Integer:
                textField.text = $"{prefix}{variable.Int.ToString(format)}{suffix}";
                break;
            case GameVariable.VarType.Float:
                textField.text = $"{prefix}{variable.Float.ToString(format)}{suffix}";
                break;
            case GameVariable.VarType.Bool:
                textField.text = $"{prefix}{(variable.Bool ? "True" : "False")}{suffix}";
                break;
        }
    }
}
