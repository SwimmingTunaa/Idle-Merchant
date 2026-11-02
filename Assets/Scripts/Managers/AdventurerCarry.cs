using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;

/// <summary>
/// Pick up an adventurer in the world and drop it on another depth. Hold the left button on a hero for
/// <see cref="holdSeconds"/> without moving (quick clicks stay clicker attacks) → the unit lifts and follows the
/// cursor, AI paused (AdventurerAgent.SetCarried). A label under it names the depth below the cursor: green = drop
/// moves it there (AdventurerManager.Move, same as the Roster), white = its own depth (just repositions), red =
/// blocked (Locked / Full). Release on a hero of another depth swaps the two. A released hero falls to the floor under
/// gravity, keeping the swing's sideways speed, and lands with dust + a squash (AI stays paused until then).
/// Right-click, death, or a panel opening sets it back where it was. Edge scrolling keeps working while carrying.
/// </summary>
public class AdventurerCarry : MonoBehaviour
{
    [SerializeField] private float holdSeconds = 0.25f;
    [Tooltip("Screen pixels the cursor may drift during the hold before it counts as a sweep, not a grab")]
    [SerializeField] private float holdSlopPixels = 6f;
    [Tooltip("World units from the cursor down to the carried unit's feet (cursor sits at its head)")]
    [SerializeField] private float hangDepth = 1.6f;
    [Tooltip("Degrees of tilt per world unit/s of horizontal cursor speed (swing while carried)")]
    [SerializeField] private float swayPerSpeed = 1.5f;
    [SerializeField] private float maxSway = 25f;
    [Header("Fall")]
    [Tooltip("World units/s² pulling a released hero to the floor")]
    [SerializeField] private float gravity = 60f;
    [Tooltip("Cap on the sideways speed a release carries from the cursor's swing")]
    [SerializeField] private float maxThrowSpeed = 12f;
    [SerializeField] private float landSquashTime = 0.12f;
    [SerializeField] private TMP_FontAsset labelFont;
    [SerializeField] private float labelFontSize = 7f;

    private AdventurerAgent pressed;
    private Vector2 pressScreen;
    private float pressTime;

    private AdventurerAgent carried;
    private Vector3 carriedFrom;
    private Vector3 lastCursor;
    private float sway, throwVx;
    private TextMeshPro label;

    private static readonly Color Ok = new Color(0.45f, 0.85f, 0.3f), Bad = new Color(0.9f, 0.3f, 0.25f), Here = new Color(0.96f, 0.9f, 0.76f);

    void Awake()
    {
        label = new GameObject("CarryLabel").AddComponent<TextMeshPro>();
        label.transform.SetParent(transform, false);
        if (labelFont != null) label.font = labelFont;
        label.fontSize = labelFontSize;
        label.alignment = TextAlignmentOptions.Top;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.rectTransform.pivot = new Vector2(0.5f, 1f);
        var mr = label.GetComponent<MeshRenderer>();
        mr.sortingOrder = 10000;
        mr.renderingLayerMask = 1 | HoverOutlineFeature.OverlayRenderingLayer;
        label.gameObject.SetActive(false);
    }

    private static bool Blocked =>
        UIManager.Instance != null && (UIManager.Instance.IsBlockingWorldInput || UIManager.Instance.IsPanelOpen("BookPanel"));

    private static Vector3 CursorWorld()
    {
        var p = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        p.z = 0f;
        return p;
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || Camera.main == null) return;

        if (carried != null) { UpdateCarry(mouse); return; }

        if (mouse.leftButton.wasPressedThisFrame && !Blocked)
        {
            pressed = AdventurerUnder(CursorWorld(), null);
            pressScreen = mouse.position.ReadValue();
            pressTime = Time.unscaledTime;
        }
        if (pressed == null) return;
        if (!mouse.leftButton.isPressed || !pressed.IsAlive || Blocked
            || (mouse.position.ReadValue() - pressScreen).sqrMagnitude > holdSlopPixels * holdSlopPixels)
        {
            pressed = null;
            return;
        }
        if (Time.unscaledTime - pressTime >= holdSeconds) Grab(pressed);
    }

    private void Grab(AdventurerAgent agent)
    {
        pressed = null;
        carried = agent;
        carriedFrom = agent.transform.position;
        lastCursor = CursorWorld();
        sway = throwVx = 0f;
        agent.SetCarried(true);
        ClickHitVFX.Instance?.PlayDust(carriedFrom);
        label.gameObject.SetActive(true);
    }

    private void UpdateCarry(Mouse mouse)
    {
        if (!carried.isActiveAndEnabled || !carried.IsAlive || Blocked || mouse.rightButton.wasPressedThisFrame)
        {
            SetDown(carried, carriedFrom);
            return;
        }

        Vector3 cursor = CursorWorld();
        float vx = Time.unscaledDeltaTime > 0f ? (cursor.x - lastCursor.x) / Time.unscaledDeltaTime : 0f;
        lastCursor = cursor;
        float k = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
        throwVx = Mathf.Lerp(throwVx, vx, k);
        sway = Mathf.Lerp(sway, Mathf.Clamp(-vx * swayPerSpeed, -maxSway, maxSway), k);
        carried.transform.SetPositionAndRotation(cursor + Vector3.down * hangDepth, Quaternion.Euler(0f, 0f, sway));

        var to = DepthUnder(cursor);
        var other = AdventurerUnder(cursor, carried);
        bool swap = other != null && AdventurerManager.SwapBlocker(carried, other) == null;
        string blocker = to == null ? "—" : AdventurerManager.MoveBlocker(carried, to);

        if (swap) { label.text = "Swap with " + NameOf(other); label.color = Ok; }
        else if (to == null) { label.text = "Drop on a depth"; label.color = Bad; }
        else
        {
            string depth = RosterPanelController.GetLayerName(to.LayerIndex);
            label.text = blocker == null || blocker == "Here" ? depth : $"{depth} · {blocker}";
            label.color = blocker == null ? Ok : blocker == "Here" ? Here : Bad;
        }
        label.transform.position = carried.transform.position + Vector3.down * 0.2f;

        if (!mouse.leftButton.isPressed) Drop(cursor, to, blocker, swap ? other : null);
    }

    private void Drop(Vector3 cursor, AdventurerManager to, string blocker, AdventurerAgent swapWith)
    {
        var agent = carried;
        Vector3 air = agent.transform.position;
        float tilt = sway, vx = Mathf.Clamp(throwVx, -maxThrowSpeed, maxThrowSpeed);
        if (swapWith != null)
        {
            Vector3 theirSpot = swapWith.transform.position;
            var theirArea = AdventurerManager.ManagerFor(swapWith).operationArea.bounds;
            Release(agent);
            if (AdventurerManager.Swap(agent, swapWith, out var newA, out var newB))
            {
                newB.transform.position = carriedFrom;
                StartCoroutine(Fall(newA, air, tilt, vx, theirSpot.y, theirArea));
                Announce(newA, newA.layerIndex);
            }
            return;
        }
        if (to == null || (blocker != null && blocker != "Here")) { SetDown(agent, carriedFrom); return; }

        var area = to.operationArea.bounds;
        if (blocker == "Here") { ReleaseHold(); StartCoroutine(Fall(agent, air, tilt, vx, area.max.y, area)); return; }

        Release(agent);
        var moved = AdventurerManager.Move(agent, to);
        if (moved == null) { agent.transform.position = carriedFrom; return; }
        StartCoroutine(Fall(moved, air, tilt, vx, area.max.y, area));
        Announce(moved, to.LayerIndex);
    }

    // Gravity drop from the release point to the floor, sideways speed kept (clamped to the floor's extent), tilt
    // settling upright; then dust + a squash on the root (pivot at the feet). The unit stays carried until it's down.
    private IEnumerator Fall(AdventurerAgent unit, Vector3 p, float tilt, float vx, float floorY, Bounds area)
    {
        unit.SetCarried(true);
        float vy = 0f;
        while (p.y > floorY)
        {
            if (unit == null || !unit.isActiveAndEnabled) yield break;
            float dt = Time.deltaTime;
            vy -= gravity * dt;
            p += new Vector3(vx, vy, 0f) * dt;
            p.x = Mathf.Clamp(p.x, area.min.x, area.max.x);
            tilt = Mathf.MoveTowards(tilt, 0f, 90f * dt);
            unit.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, 0f, tilt));
            yield return null;
        }
        if (unit == null || !unit.isActiveAndEnabled) yield break;
        p.y = floorY;
        unit.transform.SetPositionAndRotation(p, Quaternion.identity);
        ClickHitVFX.Instance?.PlayDust(p);

        Vector3 baseScale = unit.transform.localScale;
        for (float t = 0f; t < landSquashTime && unit.isActiveAndEnabled; t += Time.deltaTime)
        {
            float k = 1f - t / landSquashTime;
            unit.transform.localScale = new Vector3(baseScale.x * (1f + 0.2f * k), baseScale.y * (1f - 0.25f * k), baseScale.z);
            yield return null;
        }
        unit.transform.localScale = baseScale;
        unit.SetCarried(false);
    }

    private void SetDown(AdventurerAgent agent, Vector3 at)
    {
        Release(agent);
        if (agent != null && agent.isActiveAndEnabled) agent.transform.position = at;
    }

    private void Release(AdventurerAgent agent)
    {
        ReleaseHold();
        if (agent == null) return;
        agent.transform.rotation = Quaternion.identity;
        agent.SetCarried(false);
    }

    private void ReleaseHold()
    {
        carried = null;
        label.gameObject.SetActive(false);
    }

    private static void Announce(AdventurerAgent unit, int depth)
    {
        ClickHitVFX.Instance?.PlayDust(unit.transform.position);
        if (DamageNumberManager.Instance != null)
            DamageNumberManager.Instance.ShowText($"Depth {depth}!", unit.transform, Ok);
    }

    private static string NameOf(AdventurerAgent a) =>
        a.TryGetComponent(out IdentityComponent ic) && ic.Identity != null ? ic.Identity.DisplayName : a.name;

    private static AdventurerAgent AdventurerUnder(Vector3 world, AdventurerAgent except)
    {
        foreach (var hit in Physics2D.OverlapPointAll(world))
            if (hit.TryGetComponent(out AdventurerAgent a) && a != except && a.IsAlive && !a.IsCarried) return a;   // not one still falling
        return null;
    }

    // A depth's band runs from its floor up to the floor above (depth 1: one band-height above its own floor,
    // so the guild hall reads as no depth). Floors are the managers' operationArea strips.
    private static AdventurerManager DepthUnder(Vector3 world)
    {
        AdventurerManager best = null;
        float bestFloor = float.MinValue, gap = float.MaxValue;
        var all = FindObjectsByType<AdventurerManager>();
        foreach (var m in all)
        {
            if (m.operationArea == null) continue;
            float floor = m.operationArea.bounds.min.y;
            foreach (var n in all)
                if (n != m && n.operationArea != null && n.operationArea.bounds.min.y > floor)
                    gap = Mathf.Min(gap, n.operationArea.bounds.min.y - floor);
            if (floor <= world.y && floor > bestFloor) { best = m; bestFloor = floor; }
        }
        // ponytail: O(n²) over ~5 managers per frame while carrying; cache floors if depths ever grow
        return best != null && world.y < bestFloor + gap ? best : null;
    }
}
