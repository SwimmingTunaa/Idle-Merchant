using UnityEngine;
using UnityEngine.U2D.Animation;

/// <summary>
/// Keeps this object's SpriteResolver on the same category + label as a source resolver, every frame.
/// Lets an extra renderer (class armour, helmet) ride the existing animation clips without any clip
/// bindings of its own: the source is the cloth sibling the clips already drive, this one just follows
/// it against its own SpriteLibrary. Cheap: a hash compare per frame, resolve only on change.
/// </summary>
[RequireComponent(typeof(SpriteResolver))]
public class ResolverMirror : MonoBehaviour
{
    [SerializeField] private SpriteResolver source;

    private SpriteResolver mine;
    private string lastCategory, lastLabel;

    void Awake() => mine = GetComponent<SpriteResolver>();

    void LateUpdate() => Sync(false);

    /// <summary>Copy the source's key now. force = re-resolve even if the key hasn't changed (the
    /// library asset may have: the icon baker reuses one preview instance across defs and captures
    /// before any LateUpdate runs).</summary>
    public void Sync(bool force)
    {
        if (source == null) return;
        if (mine == null) mine = GetComponent<SpriteResolver>();
        var cat = source.GetCategory(); var label = source.GetLabel();
        if (!force && cat == lastCategory && label == lastLabel) return;
        lastCategory = cat; lastLabel = label;
        mine.SetCategoryAndLabel(cat, label);
        mine.ResolveSpriteToSpriteRenderer();
    }
}
