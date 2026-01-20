using UnityEditor.Rendering;
using UnityEngine;

    public enum ColourType
    {
        Palette,
        Gradient
    }

    [CreateAssetMenu(menuName = "Data/Colour Palette")]
    public class ColourPalette : ScriptableObject
    {
        public ColourType colourType = ColourType.Palette;


        [Header("Palette Colors")]
        public Color[] colourSet = new Color[] { Color.white };

        [Header("Gradient Colors")]
        public float defaultGradientValue;
        public Gradient gradient;

        public Color GetPaletteColour(int index)
        {
            if (index < 0 || index >= colourSet.Length)
            {
                Debug.LogWarning("Color index out of range.");
                return Color.white; // Return a default color
            }
            return colourSet[index];
        }

        public Color GetRandomPaletteColour()
        {
            int randomInt = Random.Range(0, colourSet.Length);
            return colourSet[randomInt];
        }

          public int GetRandomPaletteIndex()
        {
            int randomInt = Random.Range(0, colourSet.Length);
            return randomInt;
        }

        public Color GetRandomGradientColour()
        {
            float randomFloat = Random.Range(0f, 1f);
            return gradient.Evaluate(randomFloat);
        }

         public float GetRandomGradientValue()
        {
            float randomFloat = Random.Range(0f, 1f);
            return randomFloat;
        }

        public Color GetGradientColour(float value)
        {
            return gradient.Evaluate(value);
        }

        public Color GetDefaultGradientColour()
        {
            return gradient.Evaluate(defaultGradientValue);
        }
    }