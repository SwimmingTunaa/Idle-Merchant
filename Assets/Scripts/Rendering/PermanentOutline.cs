using UnityEngine;

/// <summary>
/// Permanently flags this object's child SpriteRenderers for the world black-outline pass
/// (the second HoverOutlineFeature instance on Renderer2D). Drop it on friendly-unit prefabs
/// — adventurers, porters — so they always carry a black silhouette outline that reads them as
/// "ours" against the background. Mobs deliberately don't get it.
///
/// The hover / Roster-selection yellow outline uses a DIFFERENT rendering-layer bit and is
/// composited on top, so a hovered/selected hero's outline simply "turns yellow".
/// </summary>
[DisallowMultipleComponent]
public class PermanentOutline : MonoBehaviour
{
    [Tooltip("Rendering-layer bit for the permanent (black) outline feature. Must match that " +
             "feature's selectedRenderingLayer, and differ from the hover/selection bit (2).")]
    [SerializeField] private uint outlineRenderingLayer = 4;

    // OnEnable (not Start) so it re-applies on every pool activation, not just first spawn.
    void OnEnable() => Apply();

    /// <summary>Flag the child renderers for the permanent outline. Safe to call again after the
    /// modular character is rebuilt (the renderers are sprite-swapped, not re-instantiated, so
    /// once is normally enough).</summary>
    public void Apply()
    {
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            if (sr != null) sr.renderingLayerMask |= outlineRenderingLayer;
    }

    /// <summary>Strip the permanent-outline bit from all child renderers. Used by the off-screen
    /// icon baker so the world feature never strokes the baked portrait — the icon gets its own
    /// baked outline at the correct resolution instead.</summary>
    public void Clear()
    {
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            if (sr != null) sr.renderingLayerMask &= ~outlineRenderingLayer;
    }
}
