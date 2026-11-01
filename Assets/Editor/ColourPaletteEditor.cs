using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ColourPalette))]
public class ColourPaletteEditor : Editor
{
    private SerializedProperty colourTypeProperty;
    private SerializedProperty colourSetProperty;
    private SerializedProperty defaultGradientValueProperty;
    private SerializedProperty gradientProperty;
    private SerializedProperty brightnessRangeProperty;

    private void OnEnable()
    {
        colourTypeProperty = serializedObject.FindProperty("colourType");
        colourSetProperty = serializedObject.FindProperty("colourSet");
        defaultGradientValueProperty = serializedObject.FindProperty("defaultGradientValue");
        gradientProperty = serializedObject.FindProperty("gradient");
        brightnessRangeProperty = serializedObject.FindProperty("brightnessRange");
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

            // Brightness range: what the game actually hands out, previewed underneath
            EditorGUILayout.Space();
            Vector2 range = brightnessRangeProperty.vector2Value;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.MinMaxSlider(new GUIContent("Brightness Range", brightnessRangeProperty.tooltip), ref range.x, ref range.y, 0f, 1f);
            range.x = EditorGUILayout.FloatField(range.x, GUILayout.Width(40));
            range.y = EditorGUILayout.FloatField(range.y, GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();
            range.x = Mathf.Clamp01(Mathf.Min(range.x, range.y)); range.y = Mathf.Clamp01(Mathf.Max(range.x, range.y));
            brightnessRangeProperty.vector2Value = range;
            Rect strip = EditorGUILayout.GetControlRect(false, 18f);
            strip = EditorGUI.PrefixLabel(strip, new GUIContent("In game"));
            int n = colourSetProperty.arraySize;
            for (int i = 0; i < n; i++)
            {
                Color c = colourSetProperty.GetArrayElementAtIndex(i).colorValue;
                Color.RGBToHSV(c, out float h, out float s, out float v);
                Color shown = Color.HSVToRGB(h, s, Mathf.Lerp(range.x, range.y, v));
                EditorGUI.DrawRect(new Rect(strip.x + strip.width * i / n, strip.y, strip.width / n - 2f, strip.height), shown);
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