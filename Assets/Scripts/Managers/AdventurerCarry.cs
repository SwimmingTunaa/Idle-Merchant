using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;

/// <summary>
/// Pick up a unit in the world (a hero or a porter — anything <see cref="ICarriable"/>) and drop it on another depth.
/// Press the left button on it and either hold still for <see cref="holdSeconds"/> or drag past
/// <see cref="holdSlopPixels"/> (quick clicks stay clicker attacks) → the unit lifts and follows the cursor, AI paused
/// (SetCarried). A label under it names the depth below the cursor: green = drop moves it there (AdventurerManager /
/// PorterManager.Move, same as the Roster), white = its own depth (just repositions), red = blocked (Locked / Full).
/// Release on a unit of the same kind on another depth swaps the two. A released unit falls to the floor under
/// gravity, keeping the swing's sideways speed, and lands with dust + a squash (AI stays paused until then).
/// Right-click, death, or a panel opening sets it back where it was. Edge scrolling keeps working while carrying.
/// </summary>
public class AdventurerCarry : MonoBehaviour
{
    [SerializeField] private float holdSeconds = 0.25f;
    [Tooltip("Screen pixels of drag that lifts the unit at once (before the hold is up)")]
    [SerializeField] private float holdSlopPixels = 6f;
    [Tooltip("World units from the cursor down to the carried unit's feet (cursor sits at its head)")]
    [SerializeField] private float hangDepth = 1.6f;
    [Tooltip("Degrees of tilt per world unit/s of horizontal cursor speed (swing while carried)")]
    [SerializeField] private float swayPerSpeed = 1.5f;
    [SerializeField] private float maxSway = 25f;
    [Header("Fall")]
    [Tooltip("World units/s² pulling a released unit to the floor")]
    [SerializeField] private float gravity = 60f;
    [Tooltip("Cap on the sideways speed a release carries from the cursor's swing")]
    [SerializeField] private float maxThrowSpeed = 12f;
    [SerializeField] private float landSquashTime = 0.12f;
    [SerializeField] private TMP_FontAsset labelFont;
    [SerializeField] private float labelFontSize = 7f;

    private EntityBase pressed;
    private Vector2 pressScreen;
    private float pressTime;

    private EntityBase carried;
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

    private static ICarriable C(EntityBase e) => e as ICarriable;
    private static bool Alive(EntityBase e) => e != null && e.isActiveAndEnabled && C(e).IsAlive;

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
            pressed = UnitUnder(CursorWorld(), null);
            pressScreen = mouse.position.ReadValue();
            pressTime = Time.unscaledTime;
        }
        if (pressed == null) return;
        if (!mouse.leftButton.isPressed || !Alive(pressed) || Blocked) { pressed = null; return; }
        // Hold still for holdSeconds, or drag past the slop: either lifts the unit (a drag used to cancel the grab, so
        // "click and drag" only worked when the cursor happened to stay put for the hold)
        bool dragged = (mouse.position.ReadValue() - pressScreen).sqrMagnitude > holdSlopPixels * holdSlopPixels;
        if (dragged || Time.unscaledTime - pressTime >= holdSeconds) Grab(pressed);
    }

    private void Grab(EntityBase unit)
    {
        pressed = null;
        carried = unit;
        carriedFrom = unit.transform.position;
        lastCursor = CursorWorld();
        sway = throwVx = 0f;
        C(unit).SetCarried(true);
        ClickHitVFX.Instance?.PlayDust(carriedFrom);
        label.gameObject.SetActive(true);
    }

    private void UpdateCarry(Mouse mouse)
    {
        if (!Alive(carried) || Blocked || mouse.rightButton.wasPressedThisFrame)
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

        var depth = DepthUnder(cursor);
        var other = UnitUnder(cursor, carried);
        bool swap = other != null && SwapBlocker(carried, other) == null;
        string blocker = depth == null ? "—" : MoveBlocker(carried, depth);

        if (swap) { label.text = "Swap with " + NameOf(other); label.color = Ok; }
        else if (depth == null) { label.text = "Drop on a depth"; label.color = Bad; }
        else
        {
            string name = RosterPanelController.GetLayerName(depth.LayerIndex);
            label.text = blocker == null || blocker == "Here" ? name : $"{name} · {blocker}";
            label.color = blocker == null ? Ok : blocker == "Here" ? Here : Bad;
        }
        label.transform.position = carried.transform.position + Vector3.down * 0.2f;

        if (!mouse.leftButton.isPressed) Drop(cursor, depth, blocker, swap ? other : null);
    }

    private void Drop(Vector3 cursor, AdventurerManager depth, string blocker, EntityBase swapWith)
    {
        var unit = carried;
        Vector3 air = unit.transform.position;
        float tilt = sway, vx = Mathf.Clamp(throwVx, -maxThrowSpeed, maxThrowSpeed);
        if (swapWith != null)
        {
            Vector3 theirSpot = swapWith.transform.position;
            var theirArea = AreaOf(swapWith).bounds;
            Release(unit);
            if (Swap(unit, swapWith, out var newA, out var newB))
            {
                newB.transform.position = carriedFrom;
                StartCoroutine(Fall(newA, air, tilt, vx, theirSpot.y, theirArea));
                Announce(newA, newA.layerIndex);
            }
            return;
        }
        if (depth == null || (blocker != null && blocker != "Here")) { SetDown(unit, carriedFrom); return; }

        var area = AreaFor(unit, depth.LayerIndex).bounds;
        if (blocker == "Here") { ReleaseHold(); StartCoroutine(Fall(unit, air, tilt, vx, area.max.y, area)); return; }

        Release(unit);
        var moved = Move(unit, depth.LayerIndex);
        if (moved == null) { unit.transform.position = carriedFrom; return; }
        StartCoroutine(Fall(moved, air, tilt, vx, area.max.y, area));
        Announce(moved, depth.LayerIndex);
    }

    // ===== HERO / PORTER DISPATCH — each kind moves through its own manager; swaps only within a kind =====

    private static string MoveBlocker(EntityBase unit, AdventurerManager depth) => unit switch
    {
        AdventurerAgent a => AdventurerManager.MoveBlocker(a, depth),
        PorterAgent p => PorterManager.MoveBlocker(p, PorterManager.ManagerForLayer(depth.LayerIndex)),
        _ => "—",
    };

    private static EntityBase Move(EntityBase unit, int layer) => unit switch
    {
        AdventurerAgent a => AdventurerManager.Move(a, AdventurerManager.ManagerForLayer(layer)),
        PorterAgent p => PorterManager.Move(p, PorterManager.ManagerForLayer(layer)),
        _ => null,
    };

    private static string SwapBlocker(EntityBase a, EntityBase b) => (a, b) switch
    {
        (AdventurerAgent x, AdventurerAgent y) => AdventurerManager.SwapBlocker(x, y),
        (PorterAgent x, PorterAgent y) => PorterManager.SwapBlocker(x, y),
        _ => "—",
    };

    private static bool Swap(EntityBase a, EntityBase b, out EntityBase newA, out EntityBase newB)
    {
        newA = newB = null;
        switch (a, b)
        {
            case (AdventurerAgent x, AdventurerAgent y):
                if (!AdventurerManager.Swap(x, y, out var ax, out var ay)) return false;
                newA = ax; newB = ay; return true;
            case (PorterAgent x, PorterAgent y):
                if (!PorterManager.Swap(x, y, out var px, out var py)) return false;
                newA = px; newB = py; return true;
        }
        return false;
    }

    private static Collider2D AreaOf(EntityBase unit) => AreaFor(unit, unit.layerIndex);
    private static Collider2D AreaFor(EntityBase unit, int layer) => unit is PorterAgent
        ? PorterManager.ManagerForLayer(layer).operationArea
        : AdventurerManager.ManagerForLayer(layer).operationArea;

    // Gravity drop from the release point to the floor, sideways speed kept (clamped to the floor's extent), tilt
    // settling upright; then dust + a squash on the root (pivot at the feet). The unit stays carried until it's down.
    private IEnumerator Fall(EntityBase unit, Vector3 p, float tilt, float vx, float floorY, Bounds area)
    {
        C(unit).SetCarried(true);
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
        C(unit).SetCarried(false);
    }

    private void SetDown(EntityBase unit, Vector3 at)
    {
        Release(unit);
        if (unit != null && unit.isActiveAndEnabled) unit.transform.position = at;
    }

    private void Release(EntityBase unit)
    {
        ReleaseHold();
        if (unit == null) return;
        unit.transform.rotation = Quaternion.identity;
        C(unit).SetCarried(false);
    }

    private void ReleaseHold()
    {
        carried = null;
        label.gameObject.SetActive(false);
    }

    private static void Announce(EntityBase unit, int depth)
    {
        ClickHitVFX.Instance?.PlayDust(unit.transform.position);
        if (DamageNumberManager.Instance != null)
            DamageNumberManager.Instance.ShowText($"Depth {depth}!", unit.transform, Ok);
    }

    private static string NameOf(EntityBase a) =>
        a.TryGetComponent(out IdentityComponent ic) && ic.Identity != null ? ic.Identity.DisplayName : a.name;

    // Heroes and porters under the cursor (mobs and customers aren't ICarriable); one still falling doesn't count
    private static EntityBase UnitUnder(Vector3 world, EntityBase except)
    {
        foreach (var hit in Physics2D.OverlapPointAll(world))
            if (hit.TryGetComponent(out EntityBase e) && e is ICarriable c && e != except && e.isActiveAndEnabled && c.IsAlive && !c.IsCarried) return e;
        return null;
    }

    // A depth's band runs from its floor up to the floor above (depth 1: one band-height above its own floor,
    // so the guild hall reads as no depth). Floors are the adventurer managers' operationArea strips; porters share them.
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
