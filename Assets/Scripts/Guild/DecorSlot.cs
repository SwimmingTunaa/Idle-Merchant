using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// One fixed guild-hall decor slot (GUILD_HALL_DESIGN.md), unlocked by the ProgressionActivator on the same GameObject.
/// "Seen" rule: a live unlock goes pending and pops in (scale 0 → 1.15 → 1, dust puff, tick) once a camera move brings
/// it fully on screen; a slot already unlocked when the save loads shows silently. Pending ids ride the save
/// (decor.pending), so a quit before looking still pops next session. Holds the slot's sprite for variant swaps later.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class DecorSlot : MonoBehaviour
{
    [Header("Slot")]
    [Tooltip("Save key (decor.pending) — never rename")]
    [SerializeField] private string slotId;
    [Tooltip("Shown while locked, e.g. the blank notice board (empty = hidden until unlocked)")]
    [SerializeField] private Sprite lockedSprite;

    // PLACEHOLDER crest: Guild/Shop/Decor/crest shows the HUD pennant (Sprites/UI/Book UI/Content/Boxes/Dark/Flag_banner.png)
    // on the Outfit palette material, tinted bronze 1★ → silver 2★ → gold 4★, until Tuan's crest sprite replaces it.
    [Header("Star Tint")]
    [Tooltip("Tint per guild star via the palette material's _New_Colour (index 0 = 1★); empty = untinted")]
    [SerializeField] private Color[] starTints = new Color[0];

    // Hall rect in world units (x 8–44, y −2–16): the cheap gate before testing each pending prop
    private static readonly Rect HallRect = Rect.MinMaxRect(8f, -2f, 44f, 16f);
    private const float PopDuration = 0.35f, PopOvershoot = 1.15f, PopStagger = 0.12f;
    private static readonly int NewColourId = Shader.PropertyToID("_New_Colour");

    private static readonly List<DecorSlot> pendingSlots = new List<DecorSlot>();
    private static readonly HashSet<string> savedPending = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        pendingSlots.Clear();
        savedPending.Clear();
    }

    private SpriteRenderer sr;
    private Vector3 baseScale;

    public string SlotId => slotId;
    public SpriteRenderer Renderer => sr;
    /// <summary>Sprite shown once unlocked (the placed variant).</summary>
    public Sprite CurrentSprite { get; private set; }
    public bool IsUnlocked { get; private set; }

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        CurrentSprite = sr.sprite;
        baseScale = transform.localScale;
        sr.sprite = lockedSprite;
        sr.enabled = lockedSprite != null;
    }

    void OnEnable()
    {
        if (starTints.Length > 0) ProgressionManager.OnStarEarned += ApplyTint;
    }

    void OnDisable()
    {
        ProgressionManager.OnStarEarned -= ApplyTint;
        pendingSlots.Remove(this);
    }

    void Start()
    {
        if (starTints.Length > 0 && ProgressionManager.Instance != null) ApplyTint(ProgressionManager.Instance.GetCurrentStars());
    }

    private void ApplyTint(int stars)
    {
        var mpb = new MaterialPropertyBlock();
        sr.GetPropertyBlock(mpb);
        mpb.SetColor(NewColourId, starTints[Mathf.Clamp(stars - 1, 0, starTints.Length - 1)]);
        sr.SetPropertyBlock(mpb);
    }

    /// <summary>ProgressionActivator: fresh = a live unlock (pending until seen); otherwise shown silently,
    /// unless the save left it pending.</summary>
    public void SetUnlocked(bool on, bool fresh)
    {
        if (!on || IsUnlocked) return;
        IsUnlocked = true;
        if (fresh || savedPending.Contains(slotId)) pendingSlots.Add(this);
        else Show();
    }

    private void Show()
    {
        sr.sprite = CurrentSprite;
        sr.enabled = true;
        transform.localScale = baseScale;
    }

    /// <summary>CameraScrollController, after each move: pops pending slots now fully on screen, staggered.</summary>
    public static void CheckSeen(Camera cam)
    {
        if (pendingSlots.Count == 0 || cam == null) return;
        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector3 c = cam.transform.position;
        var view = Rect.MinMaxRect(c.x - w, c.y - h, c.x + w, c.y + h);
        if (!view.Overlaps(HallRect)) return;

        int n = 0;
        for (int i = 0; i < pendingSlots.Count;)
        {
            var slot = pendingSlots[i];
            Rect b = slot.WorldRect();
            if (!view.Contains(b.min) || !view.Contains(b.max)) { i++; continue; }
            pendingSlots.RemoveAt(i);
            slot.StartCoroutine(slot.Pop(n++ * PopStagger));
        }
    }

    private Rect WorldRect()
    {
        Bounds b = CurrentSprite != null ? CurrentSprite.bounds : default;
        Vector2 a = transform.TransformPoint(b.min), z = transform.TransformPoint(b.max);
        return Rect.MinMaxRect(Mathf.Min(a.x, z.x), Mathf.Min(a.y, z.y), Mathf.Max(a.x, z.x), Mathf.Max(a.y, z.y));
    }

    private IEnumerator Pop(float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        savedPending.Remove(slotId);
        Show();
        ClickHitVFX.Instance?.PlayDust(sr.bounds.center, 6);
        SfxManager.Instance?.Play(SfxId.UiTick);
        for (float t = 0f; t < PopDuration; t += Time.unscaledDeltaTime)
        {
            // 0 → 1.15 over the first 60% (ease out), settle to 1 over the rest
            float k = t / PopDuration;
            float s = k < 0.6f ? PopOvershoot * Mathf.Sin(k / 0.6f * Mathf.PI * 0.5f) : Mathf.Lerp(PopOvershoot, 1f, (k - 0.6f) / 0.4f);
            transform.localScale = baseScale * s;
            yield return null;
        }
        transform.localScale = baseScale;
    }

    // ===== SAVE (SaveManager) =====

    public static List<string> PendingIds() => pendingSlots.Select(s => s.slotId).ToList();

    public static void LoadPending(List<string> ids)
    {
        savedPending.Clear();
        if (ids != null) savedPending.UnionWith(ids);
    }
}
