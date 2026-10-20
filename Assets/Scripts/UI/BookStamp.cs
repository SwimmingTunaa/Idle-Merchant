using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The book's rubber-stamp grammar, shared by the Inventory (FOR SALE) and Crafting (CRAFTING) pages:
/// a big Stamp.png slam overlay that reveals a persistent badge on its impact frame, and a dither-erase
/// (for-sale-stamp-sheet frames) that takes the badge off. The overlay element must live at page-root level
/// (sibling of left/right-content) so the stamp hand isn't clipped by the page.
/// </summary>
public static class BookStamp
{
    // Stamp face centre at the impact frame, as a fraction of the 700px Stamp.png frame.
    private static readonly Vector2 ImpactFace = new(0.563f, 0.591f);

    /// <summary>Size the (square) overlay and move it so the stamp face lands on <paramref name="target"/>'s centre.
    /// Size is passed in because a display:none overlay has no resolved size. Needs a laid-out target.</summary>
    public static void AimAt(VisualElement overlay, VisualElement target, float size)
    {
        if (overlay?.parent == null || target == null) return;
        Vector2 c = overlay.parent.WorldToLocal(target.worldBound.center);
        overlay.style.width = overlay.style.height = size;
        overlay.style.left = c.x - ImpactFace.x * size;
        overlay.style.top = c.y - ImpactFace.y * size;
    }

    /// <summary>Plays the slam overlay; <paramref name="onImpact"/> fires on the middle frame (the "lands" moment).</summary>
    public static void SlamIn(FrameAnim slam, VisualElement overlay, Action onImpact, Action onComplete = null)
    {
        if (overlay == null || slam?.frames == null || slam.frames.Length == 0)
        {
            onImpact?.Invoke();
            onComplete?.Invoke();
            return;
        }
        int impactFrame = slam.frames.Length / 2;
        overlay.style.display = DisplayStyle.Flex;
        slam.Play(overlay, onComplete, idx => { if (idx == impactFrame) onImpact?.Invoke(); });
    }

    /// <summary>Dither-erases <paramref name="badge"/>. Its text label hides for the run (the frames have no text)
    /// and is restored after, so the next reveal shows frame 0 + text again.</summary>
    public static void DitherOut(FrameAnim dither, VisualElement badge, VisualElement badgeText, Action onComplete)
    {
        if (badge == null) { onComplete?.Invoke(); return; }
        if (badgeText != null) badgeText.style.display = DisplayStyle.None;
        dither.Play(badge, () =>
        {
            if (badgeText != null) badgeText.style.display = DisplayStyle.Flex;
            onComplete?.Invoke();
        });
    }
}
