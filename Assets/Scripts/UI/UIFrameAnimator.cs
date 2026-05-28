using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Reusable frame-by-frame sprite animator for UI Toolkit elements.
///
/// Swaps a VisualElement's <c>backgroundImage</c> through a sequence of Sprites
/// at a fixed cadence — the standard pixel-art approach to dissolves, poofs,
/// reveals, sparkles, etc. Use this any time you have a sprite sheet (or any
/// ordered Sprite[]) and want it to play on a UI element.
///
/// Tracks one active animation per element, so calling Play again on the same
/// element cancels the previous run automatically.
///
/// Example:
/// <code>
///   element.PlayFrames(frames, frameMs: 30, end: EndBehaviour.HideAndReset,
///                      onComplete: () => DoNextThing());
/// </code>
/// </summary>
public static class UIFrameAnimator
{
    /// <summary>What to do with the element after the last frame plays.</summary>
    public enum EndBehaviour
    {
        /// <summary>Element keeps showing the last frame.</summary>
        KeepLastFrame,
        /// <summary>Element resets its <c>backgroundImage</c> to frame 0 (ready to replay).</summary>
        ResetToFirstFrame,
        /// <summary>Element hides via <c>display: none</c>. Background stays on the last frame.</summary>
        Hide,
        /// <summary>Element hides AND resets to frame 0. Use when the element is shown/hidden repeatedly.</summary>
        HideAndReset,
    }

    // Active animations keyed by element so calling Play again cancels the previous.
    private static readonly Dictionary<VisualElement, IVisualElementScheduledItem> Active = new();
    private static readonly Dictionary<VisualElement, IVisualElementScheduledItem> EndTasks = new();

    /// <summary>
    /// Play a sprite-sheet animation on the given element. Cancels any previous animation on the same element.
    /// </summary>
    /// <param name="target">The element whose <c>backgroundImage</c> will be animated.</param>
    /// <param name="frames">Ordered Sprite array. Frame 0 plays first.</param>
    /// <param name="frameMs">Milliseconds per frame. Clamped to a minimum of 1.</param>
    /// <param name="reverse">If true, plays from the last frame to the first.</param>
    /// <param name="loop">If true, restarts after the last frame and ignores <paramref name="end"/>.</param>
    /// <param name="end">What to do after the final frame (ignored when looping).</param>
    /// <param name="onComplete">Invoked once after the final frame (or never, if looping).</param>
    /// <param name="onFrame">Invoked each time a frame is applied, with the displayed frame index
    /// (already accounts for <paramref name="reverse"/>). Use to trigger side-effects partway through —
    /// e.g. reveal another element when the animation reaches a specific frame.</param>
    /// <param name="fadeMs">If &gt; 0, the element's opacity fades 0→1 over this many ms at the start.
    /// When the <paramref name="end"/> behaviour hides the element, opacity also fades 1→0 first
    /// (the hide is deferred until the fade-out completes). Uses a USS opacity transition.</param>
    public static void PlayFrames(
        this VisualElement target,
        Sprite[] frames,
        int frameMs = 40,
        bool reverse = false,
        bool loop = false,
        EndBehaviour end = EndBehaviour.KeepLastFrame,
        Action onComplete = null,
        Action<int> onFrame = null,
        int fadeMs = 0)
    {
        if (target == null) return;
        if (frames == null || frames.Length == 0)
        {
            ApplyEndBehaviour(target, frames, end);
            onComplete?.Invoke();
            return;
        }

        // Cancel any in-flight animation on this element.
        Stop(target);

        int totalMs = Mathf.Max(1, frameMs) * frames.Length;
        int frameIndex = 0;

        // Fade in: set an opacity transition, start transparent, then kick to opaque next tick
        // (a one-tick delay is needed so the transition registers the change).
        if (fadeMs > 0)
        {
            SetOpacityTransition(target, fadeMs);
            target.style.opacity = 0f;
            target.schedule.Execute(() => { if (target.panel != null) target.style.opacity = 1f; }).StartingIn(20);
        }

        var play = target.schedule.Execute(() =>
        {
            if (target.panel == null) return; // element detached mid-anim

            int idx = reverse ? (frames.Length - 1 - frameIndex) : frameIndex;
            if (idx >= 0 && idx < frames.Length && frames[idx] != null)
                target.style.backgroundImage = new StyleBackground(frames[idx]);

            onFrame?.Invoke(idx);

            frameIndex++;

            if (loop && frameIndex >= frames.Length)
                frameIndex = 0;
        }).Every(Mathf.Max(1, frameMs));

        if (!loop)
            play.Until(() => frameIndex >= frames.Length);

        Active[target] = play;

        if (!loop)
        {
            // Schedule end-of-animation cleanup
            var endTask = target.schedule.Execute(() =>
            {
                Active.Remove(target);
                EndTasks.Remove(target);
                if (target.panel == null) { onComplete?.Invoke(); return; }

                bool willHide = end == EndBehaviour.Hide || end == EndBehaviour.HideAndReset;
                if (fadeMs > 0 && willHide)
                {
                    // Fade out, then hide once the fade completes.
                    target.style.opacity = 0f;
                    target.schedule.Execute(() =>
                    {
                        if (target.panel != null)
                        {
                            ApplyEndBehaviour(target, frames, end);
                            target.style.opacity = 1f; // restore so it's visible next time it's shown
                        }
                        onComplete?.Invoke();
                    }).StartingIn(fadeMs + 10);
                }
                else
                {
                    ApplyEndBehaviour(target, frames, end);
                    onComplete?.Invoke();
                }
            }).StartingIn(totalMs + 10);
            EndTasks[target] = endTask;
        }
    }

    /// <summary>
    /// Cancel any active animation on the element. Does not invoke onComplete.
    /// Leaves the element on whatever frame was last applied.
    /// </summary>
    public static void Stop(this VisualElement target)
    {
        if (target == null) return;
        if (Active.TryGetValue(target, out var item)) { item.Pause(); Active.Remove(target); }
        if (EndTasks.TryGetValue(target, out var endItem)) { endItem.Pause(); EndTasks.Remove(target); }
    }

    /// <summary>True if this element currently has an animation in flight.</summary>
    public static bool IsPlaying(this VisualElement target) => target != null && Active.ContainsKey(target);

    // ─────────────────────────────────────────────────────────────────────

    private static void ApplyEndBehaviour(VisualElement target, Sprite[] frames, EndBehaviour end)
    {
        if (target == null) return;
        switch (end)
        {
            case EndBehaviour.KeepLastFrame:
                break;
            case EndBehaviour.ResetToFirstFrame:
                SetFrame(target, frames, 0);
                break;
            case EndBehaviour.Hide:
                target.style.display = DisplayStyle.None;
                break;
            case EndBehaviour.HideAndReset:
                target.style.display = DisplayStyle.None;
                SetFrame(target, frames, 0);
                break;
        }
    }

    private static void SetFrame(VisualElement target, Sprite[] frames, int idx)
    {
        if (frames == null || idx < 0 || idx >= frames.Length || frames[idx] == null) return;
        target.style.backgroundImage = new StyleBackground(frames[idx]);
    }

    private static void SetOpacityTransition(VisualElement target, int ms)
    {
        target.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("opacity") };
        target.style.transitionDuration = new List<TimeValue> { new TimeValue(ms, TimeUnit.Millisecond) };
    }

    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Plays this animation config on the given element. The serialized config holds the
    /// tunable data (frames, timing, behaviour); callbacks are passed at call time.
    /// </summary>
    public static void Play(
        this FrameAnim cfg,
        VisualElement target,
        Action onComplete = null,
        Action<int> onFrame = null)
    {
        if (cfg == null || target == null) { onComplete?.Invoke(); return; }
        target.PlayFrames(
            cfg.frames,
            frameMs: cfg.frameMs,
            reverse: cfg.reverse,
            loop: cfg.loop,
            end: cfg.end,
            onComplete: onComplete,
            onFrame: onFrame,
            fadeMs: cfg.fadeMs);
    }
}

/// <summary>
/// Serializable, designer-tunable configuration for a <see cref="UIFrameAnimator"/> sprite animation.
/// Expose one of these per animation on a controller, drag the frames in, and call
/// <c>config.Play(element, onComplete, onFrame)</c>. Group all tunables in one inspector foldout.
/// </summary>
[Serializable]
public class FrameAnim
{
    [Tooltip("Ordered sprite frames. Frame 0 plays first (or last, if Reverse is on).")]
    public Sprite[] frames;

    [Tooltip("Milliseconds per frame.")]
    public int frameMs = 40;

    [Tooltip("Play from the last frame to the first.")]
    public bool reverse = false;

    [Tooltip("Loop forever (ignores End behaviour). Stop manually with element.Stop().")]
    public bool loop = false;

    [Tooltip("What happens to the element after the final frame.")]
    public UIFrameAnimator.EndBehaviour end = UIFrameAnimator.EndBehaviour.KeepLastFrame;

    [Tooltip("Fade in/out duration (ms). 0 = no fade. Fade-out only applies when End hides the element.")]
    public int fadeMs = 0;
}
