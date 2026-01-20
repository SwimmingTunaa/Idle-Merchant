using UnityEngine;

[CreateAssetMenu(menuName = "Data/Game Variable", fileName = "NewGameVariable")]
public class GameVariable : ScriptableObject
{
    public enum VarType { Integer, Float, Bool }

    [Header("Variable Info")]
    public string id;
    public VarType type = VarType.Float;

    [Header("Default Value")]
    public int intValue;
    public float floatValue;
    public bool boolValue;

    [Tooltip("Optional description for designers.")]
    [TextArea] public string description;

    private int runtimeInt;
    private float runtimeFloat;
    private bool runtimeBool;

    void OnEnable() => ResetToDefault();

    public void ResetToDefault()
    {
        runtimeInt = intValue;
        runtimeFloat = floatValue;
        runtimeBool = boolValue;
    }

    public int Int => runtimeInt;
    public float Float => runtimeFloat;
    public bool Bool => runtimeBool;

    public void Set(int val) => runtimeInt = val;
    public void Set(float val) => runtimeFloat = val;
    public void Set(bool val) => runtimeBool = val;

    public void Add(float val) => runtimeFloat += val;
    public void Add(int val) => runtimeInt += val;
}
