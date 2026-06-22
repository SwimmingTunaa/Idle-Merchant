using UnityEngine;

/// <summary>Outline corner style, mirroring Aseprite's outline matrix.</summary>
public enum OutlineShape
{
    /// <summary>Full box (Chebyshev) — blocky right-angle corners.</summary>
    Square,
    /// <summary>"+" / diamond (Manhattan) — diagonal corner pixels omitted, corners cut at 45°.</summary>
    Cross,
    /// <summary>Disc (Euclidean) — rounded corners.</summary>
    Circle,
}

/// <summary>
/// Project-wide settings for baked character icons (<see cref="CharacterSpriteGenerator"/>).
/// Must live in a Resources folder so it loads at runtime. Edit in the inspector to tune the
/// baked outline — changes apply to the game roster/cards and the generator sandbox alike
/// (re-enter Play, or re-roll in the sandbox, to re-bake cached sprites).
/// </summary>
[CreateAssetMenu(menuName = "Data/Character Icon Settings", fileName = "CharacterIconSettings")]
public class CharacterIconSettings : ScriptableObject
{
    [Tooltip("Baked outline width in render-texture texels (icons are rendered at 256px). " +
             "Higher = thicker. ~12 roughly matches the live world outline at the default game zoom.")]
    [Range(0, 48)] public int outlineTexels = 18;

    [Tooltip("Baked outline colour.")]
    public Color outlineColor = Color.black;

    [Tooltip("Corner style. Square = full 90° corners; Cross = 90° corners with the tips notched " +
             "(non-diagonal / '+' look); Circle = rounded.")]
    public OutlineShape outlineShape = OutlineShape.Cross;

    [Tooltip("Cross only: how many pixels to notch off each corner. 1 = tiny single-pixel notch; " +
             "larger cuts more of the corner (toward a 45° diamond).")]
    [Min(0)] public int cornerCut = 1;
}
