using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ColourPalette))]
public class ColourPaletteEditor : Editor
{
    private SerializedProperty colourTypeProperty;
    private SerializedProperty colourSetProperty;
    private SerializedProperty defaultGradientValueProperty;
    private SerializedProperty gradientProperty;

    private void OnEnable()
    {
        colourTypeProperty = serializedObject.FindProperty("colourType");
        colourSetProperty = serializedObject.FindProperty("colourSet");
        defaultGradientValueProperty = serializedObject.FindProperty("defaultGradientValue");
        gradientProperty = serializedObject.FindProperty("gradient");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(colourTypeProperty, new GUIContent("Colour Type"));

        ColourType colourType = (ColourType)colourTypeProperty.enumValueIndex;

        EditorGUILayout.Space();

        if (colourType == ColourType.Palette)
        {
            // int newSize = EditorGUILayout.IntField("Palette Size", colourSetProperty.arraySize);
            // if (newSize != colourSetProperty.arraySize)
            // {
            //     colourSetProperty.arraySize = newSize;
            // }

            // Display colors as editable fields
            for (int i = 0; i < colourSetProperty.arraySize; i++)
            {
                SerializedProperty colorElement = colourSetProperty.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(colorElement, new GUIContent($"#{ColorUtility.ToHtmlStringRGB(colorElement.colorValue)}"));

                if (GUILayout.Button("Remove", GUILayout.Width(100)))
                {
                    colourSetProperty.DeleteArrayElementAtIndex(i);
                    i--; // Adjust index after removal
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Color"))
            {
                colourSetProperty.InsertArrayElementAtIndex(colourSetProperty.arraySize);
                SerializedProperty newColorElement = colourSetProperty.GetArrayElementAtIndex(colourSetProperty.arraySize - 1);
                newColorElement.colorValue = Color.white; // Set default color
            }
        }
        else
        {
            // EditorGUILayout.LabelField("Gradient Colors", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(gradientProperty);
            EditorGUILayout.PropertyField(defaultGradientValueProperty, new GUIContent("Default Gradient Value"));
        }

        serializedObject.ApplyModifiedProperties();
    }
}