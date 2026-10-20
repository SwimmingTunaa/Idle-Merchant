using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Icon frame whose border colour carries a recipe's state (styles in CraftingPanelStyle.uss .status-frame--*):
/// idle brown · crafting = gold progress sweep from 12 o'clock (Painter2D, call SetState each tick) ·
/// blocked red · capped solid gold · locked grey + padlock. Used on Crafting tiles and the detail slot.
/// </summary>
public class StatusFrame : VisualElement
{
    public enum State { Idle, Crafting, Blocked, Capped, Locked }

    private static readonly Color SweepColor = new(240f / 255f, 180f / 255f, 41f / 255f); // --color-gold-hi (Painter2D can't read USS vars)
    private static readonly float[] Corners = { 45f, 135f, 225f, 315f };

    public readonly VisualElement Icon;
    private State state = State.Idle;
    private float progress;

    /// <param name="size">Outer size; <paramref name="pad"/> is the visible border; padlock is lockW×lockH.</param>
    public StatusFrame(float size, float pad, float iconSize, float lockW, float lockH, bool big = false)
    {
        AddToClassList("status-frame");
        if (big) AddToClassList("status-frame--big");
        style.width = size;
        style.height = size;
        style.paddingLeft = style.paddingRight = style.paddingTop = style.paddingBottom = pad;
        pickingMode = PickingMode.Ignore;

        var inner = new VisualElement { pickingMode = PickingMode.Ignore };
        inner.AddToClassList("status-frame__in");
        Add(inner);

        Icon = new VisualElement { pickingMode = PickingMode.Ignore };
        Icon.AddToClassList("status-frame__icon");
        Icon.style.width = Icon.style.height = iconSize;
        inner.Add(Icon);

        var padlock = new VisualElement { pickingMode = PickingMode.Ignore };
        padlock.AddToClassList("status-frame__lock");
        padlock.style.width = lockW;
        padlock.style.height = lockH;
        padlock.style.translate = new Translate(-lockW / 2f, -lockH / 2f);
        Add(padlock);

        generateVisualContent += DrawSweep;
    }

    public void SetState(State s, float craftProgress = 0f)
    {
        if (s != state)
        {
            RemoveFromClassList(ClassFor(state));
            AddToClassList(ClassFor(s));
            state = s;
            MarkDirtyRepaint();
        }
        float p = s == State.Crafting ? Mathf.Clamp01(craftProgress) : 0f;
        if (!Mathf.Approximately(p, progress))
        {
            progress = p;
            MarkDirtyRepaint();
        }
    }

    private static string ClassFor(State s) => "status-frame--" + s.ToString().ToLowerInvariant();

    // Pie wedge clipped to the frame's square: centre → 12 o'clock edge → passed corners → current edge.
    // The opaque inner panel covers the middle, so only the border ring shows gold.
    private void DrawSweep(MeshGenerationContext ctx)
    {
        if (state != State.Crafting || progress <= 0f) return;
        float w = layout.width, h = layout.height;
        if (w <= 0f || h <= 0f) return;
        var c = new Vector2(w / 2f, h / 2f);
        float end = progress * 360f;

        var p = ctx.painter2D;
        p.fillColor = SweepColor;
        p.BeginPath();
        p.MoveTo(c);
        p.LineTo(Edge(c, 0f));
        foreach (float a in Corners)
            if (a < end) p.LineTo(Edge(c, a));
        p.LineTo(Edge(c, end));
        p.ClosePath();
        p.Fill();
    }

    private static Vector2 Edge(Vector2 c, float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        var d = new Vector2(Mathf.Sin(r), -Mathf.Cos(r));
        float t = Mathf.Min(c.x / Mathf.Max(Mathf.Abs(d.x), 1e-5f), c.y / Mathf.Max(Mathf.Abs(d.y), 1e-5f));
        return c + d * t;
    }
}
