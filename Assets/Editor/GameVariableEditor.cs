using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameVariable))]
public class GameVariableEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var idProp = serializedObject.FindProperty("id");
        var typeProp = serializedObject.FindProperty("type");
        var intProp = serializedObject.FindProperty("intValue");
        var floatProp = serializedObject.FindProperty("floatValue");
        var boolProp = serializedObject.FindProperty("boolValue");
        var descProp = serializedObject.FindProperty("description");

        EditorGUILayout.PropertyField(idProp);
        EditorGUILayout.PropertyField(typeProp);
        EditorGUILayout.Space(5);

        // Show only the relevant value field
        switch ((GameVariable.VarType)typeProp.enumValueIndex)
        {
            case GameVariable.VarType.Integer:
                EditorGUILayout.PropertyField(intProp, new GUIContent("Integer Value"));
                break;
            case GameVariable.VarType.Float:
                EditorGUILayout.PropertyField(floatProp, new GUIContent("Float Value"));
                break;
            case GameVariable.VarType.Bool:
                EditorGUILayout.PropertyField(boolProp, new GUIContent("Bool Value"));
                break;
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.PropertyField(descProp);

        serializedObject.ApplyModifiedProperties();
    }
}
