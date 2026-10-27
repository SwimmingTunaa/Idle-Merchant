using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// One fixed guild-hall decor slot (GUILD_HALL_DESIGN.md), unlocked by the ProgressionActivator on the same GameObject.
/// "Seen" rule: a live unlock goes pending and pops in (scale 0 → 1.15 → 1, dust puff, tick) once a camera move brings
/// it fully on screen; a slot already unlocked when the save loads shows silently. Pending ids ride the save
/// (decor.pending), so a quit before looking still pops next session. A slot with a <see cref="defaultVariant"/> is
/// swappable once shown: hover outlines it, a click (ClickerManager) opens the Decorate panel; bought variants
/// (Guild Seals) are owned forever, placing is free (decor.owned / decor.placed).
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

    [Header("Swapping")]
    [Tooltip("This slot's free default look; its slot group picks the DecorCatalog variants (empty = never swappable)")]
    [SerializeField] private DecorVariantDef defaultVariant;
    [Tooltip("Decorate panel title")]
    [SerializeField] private string displayName;
    [Tooltip("Rendering-layer bit HoverOutlineFeature strokes (2 = hover/selection, yellow).")]
    [SerializeField] private uint hoverOutlineLayer = 2;

    // Hall rect in world units (x 8–44, y −2–16): the cheap gate before testing each pending prop
    private static readonly Rect HallRect = Rect.MinMaxRect(8f, -2f, 44f, 16f);
    private const float PopDuration = 0.35f, PopOvershoot = 1.15f, PopStagger = 0.12f;
    // Parked camera: an unlock already on screen pops this long after (no camera move needed).
    // Boot: no seen-checks until this long after scene start / the save restore, so saved pending pops after the load hitch.
    private const float ParkedCheckDelay = 0.6f, BootQuiet = 1.0f;
    private static readonly int NewColourId = Shader.PropertyToID("_New_Colour");

    private static readonly List<DecorSlot> pendingSlots = new List<DecorSlot>();
    private static readonly HashSet<string> savedPending = new HashSet<string>();
    private static readonly Dictionary<string, string> placed = new Dictionary<string, string>();   // slotId → variant name (non-default only)
    private static readonly HashSet<string> owned = new HashSet<string>();                         // bought variant names
    private static readonly HashSet<DecorVariantDef> defaults = new HashSet<DecorVariantDef>();    // every slot's default is free everywhere
    private static float quietUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        pendingSlots.Clear();
        savedPending.Clear();
        placed.Clear();
        owned.Clear();
        defaults.Clear();
        quietUntil = 0f;
    }

    private SpriteRenderer sr;
    private Vector3 baseScale;

    public string SlotId => slotId;
    public SpriteRenderer Renderer => sr;
    /// <summary>Sprite shown once unlocked (the placed variant).</summary>
    public Sprite CurrentSprite { get; private set; }
    public bool IsUnlocked { get; private set; }
    public string DisplayName => displayName;
    public DecorVariantDef DefaultVariant => defaultVariant;
    public DecorVariantDef Placed { get; private set; }
    /// <summary>Shown (popped) and has variants: hover + click open the Decorate panel.</summary>
    public bool CanDecorate => shown && defaultVariant != null;

    private bool shown;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        CurrentSprite = sr.sprite;
        baseScale = transform.localScale;
        quietUntil = Time.unscaledTime + BootQuiet;   // scene start
        sr.sprite = lockedSprite;
        sr.enabled = lockedSprite != null;
        if (defaultVariant != null) defaults.Add(defaultVariant);
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
        if (defaultVariant == null) return;

        // SaveManager.ApplyEarly (order -100) loaded decor.placed; an unknown name falls back to the default
        var v = defaultVariant;
        if (placed.TryGetValue(slotId, out var name))
        {
            var found = DecorCatalog.Instance != null ? DecorCatalog.Instance.Find(name) : null;
            if (found != null && found.slot == defaultVariant.slot) v = found;
            else
            {
                Debug.LogWarning($"[DecorSlot] Unknown decor variant '{name}' in slot {slotId} (asset renamed?) — default placed");
                placed.Remove(slotId);
            }
        }
        Place(v);
    }

    /// <summary>Star tint changes at this star (fanfare GUILD HALL card lists the slot).</summary>
    public bool TintChangesAt(int star) =>
        star >= 2 && star <= starTints.Length && starTints[star - 1] != starTints[star - 2];

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
        if (!fresh && !savedPending.Contains(slotId)) { Show(); return; }
        pendingSlots.Add(this);
        StartCoroutine(CheckSoon());
    }

    private void Show()
    {
        shown = true;
        sr.sprite = CurrentSprite;
        sr.enabled = true;
        if (Placed != null) sr.color = Placed.tint;
        transform.localScale = baseScale;
    }

    // ===== SWAPPING (Decorate panel) =====

    /// <summary>Any slot's default is always owned (the white blanket is free on every bunk); anything else once bought.</summary>
    public bool IsOwned(DecorVariantDef v) => v != null && (v == defaultVariant || defaults.Contains(v) || owned.Contains(v.name));

    /// <summary>Spends the variant's Guild Seals (ContractManager raises OnSealsChanged). True if now owned.</summary>
    public bool TryBuy(DecorVariantDef v)
    {
        if (IsOwned(v)) return true;
        if (v == null || ContractManager.Instance == null || !ContractManager.Instance.TrySpendSeals(v.price)) return false;
        owned.Add(v.name);
        return true;
    }

    /// <summary>Puts an owned variant in the slot (free). Updates the world sprite at once if the slot is showing.</summary>
    public void Place(DecorVariantDef v)
    {
        if (!IsOwned(v) || defaultVariant == null || v.slot != defaultVariant.slot) return;
        Placed = v;
        if (v == defaultVariant) placed.Remove(slotId);
        else placed[slotId] = v.name;
        CurrentSprite = v.sprite;
        if (shown) Show();
    }

    public void SetHover(bool on) =>
        sr.renderingLayerMask = on ? sr.renderingLayerMask | hoverOutlineLayer : sr.renderingLayerMask & ~hoverOutlineLayer;

    // ===== SEEN =====

    // No seen-checks while the save restores, nor for BootQuiet after it (or after scene start)
    private static bool Quiet
    {
        get
        {
            if (SaveManager.Instance != null && SaveManager.Instance.IsRestoring) quietUntil = Time.unscaledTime + BootQuiet;
            return Time.unscaledTime < quietUntil;
        }
    }

    // Parked camera: no move would ever come, so look once shortly after the unlock (after the boot quiet on load)
    private IEnumerator CheckSoon()
    {
        yield return new WaitForSecondsRealtime(ParkedCheckDelay);
        while (Quiet) yield return null;
        CheckSeen(Camera.main);
    }

    /// <summary>CameraScrollController, after each move (and CheckSoon): pops pending slots now fully on screen, staggered.</summary>
    public static void CheckSeen(Camera cam)
    {
        if (pendingSlots.Count == 0 || cam == null || Quiet) return;
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

    public static DecorSave ToSave() => new DecorSave
    {
        pending = pendingSlots.Select(s => s.slotId).ToList(),
        placed = new Dictionary<string, string>(placed),
        owned = owned.ToList(),
    };

    /// <summary>ApplyEarly, before the slots' Start: pending ids unlock pending, placed variants resolve in Start.</summary>
    public static void LoadFrom(DecorSave d)
    {
        savedPending.Clear();
        placed.Clear();
        owned.Clear();
        if (d == null) return;
        if (d.pending != null) savedPending.UnionWith(d.pending);
        if (d.placed != null) foreach (var kv in d.placed) placed[kv.Key] = kv.Value;
        if (d.owned != null) owned.UnionWith(d.owned);
    }
}
