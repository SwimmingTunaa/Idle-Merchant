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
        [Tooltip("Every colour handed out has its brightness (HSV value) remapped into this range. 0–1 = as authored; raise the min to keep rolls on the lighter end")]
        public Vector2 brightnessRange = new Vector2(0f, 1f);

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
            return Adjust(colourSet[index]);
        }

        /// <summary>A palette colour as handed out: brightness remapped into <see cref="brightnessRange"/>, hue and saturation kept.</summary>
        public Color Adjust(Color c)
        {
            if (brightnessRange.x <= 0f && brightnessRange.y >= 1f) return c;
            Color.RGBToHSV(c, out float h, out float s, out float v);
            Color o = Color.HSVToRGB(h, s, Mathf.Lerp(brightnessRange.x, brightnessRange.y, v));
            o.a = c.a;
            return o;
        }

        public Color GetRandomPaletteColour()
        {
            int randomInt = Random.Range(0, colourSet.Length);
            return Adjust(colourSet[randomInt]);
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