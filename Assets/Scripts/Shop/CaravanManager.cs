using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Caravan: a trader wagon that pulls up outside the guild every <see cref="VisitEvery"/> s of game time and
/// offers flat Guild Seals for a bulk lot of ONE raw item — the one with the most gold value above its floor,
/// 70 % of that surplus. A stock valve, not income: no gold, no OnItemSold, never feeds offline rates.
/// Lives on the wagon prop itself (scene singleton): timer, offer, slide in/out with turning wheels, hover, save chunk.
/// </summary>
[RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
public class CaravanManager : MonoBehaviour
{
    public const float VisitEvery = 480f;   // game seconds between visits (and after a skipped visit)
    public const float StayFor = 90f;       // the offer stays open this long (parked in the opening camera view)
    public const int SealReward = 5;
    public const int MinQty = 20;           // a smaller best lot = no visit this time
    private const float SlideDistance = 8f, SlideTime = 1.2f, WheelFps = 10f;

    public static CaravanManager Instance { get; private set; }

    [Tooltip("Wagon frames, wheels turning. 0 = parked.")]
    [SerializeField] private Sprite[] frames;
    [Tooltip("Rendering-layer bit HoverOutlineFeature strokes (2 = hover/selection, yellow).")]
    [SerializeField] private uint hoverOutlineLayer = 2;

    public bool Present { get; private set; }
    public ItemDef Item { get; private set; }
    public int Qty { get; private set; }
    public float LeaveIn { get; private set; }
    public Sprite Parked => frames[0];

    private float nextIn = VisitEvery;
    private ItemDef[] raws;
    private SpriteRenderer sr;
    private Collider2D col;
    private Vector3 park;
    private float offset = SlideDistance;   // 0 = parked, SlideDistance = gone (off to the left, the village side)
    private float frameTimer;
    private int frame;

    void Awake()
    {
        Instance = this;
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        park = transform.position;
        raws = Resources.LoadAll<ItemDef>("Items/RawMaterials");
        Place();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        float dt = Time.deltaTime;
        if (Present) { if ((LeaveIn -= dt) <= 0f) Leave(); }
        else if ((nextIn -= dt) <= 0f) Arrive();

        float target = Present ? 0f : SlideDistance;
        if (offset == target) return;
        offset = Mathf.MoveTowards(offset, target, SlideDistance / SlideTime * dt);
        if ((frameTimer += dt) >= 1f / WheelFps) { frameTimer = 0f; frame = (frame + 1) % frames.Length; }
        if (offset == target)
        {
            frame = 0;
            if (Present)
            {
                if (SfxManager.Instance != null) SfxManager.Instance.Play(SfxId.MilestoneDing);
                if (DamageNumberManager.Instance != null) DamageNumberManager.Instance.ShowText("Caravan!", transform);
            }
        }
        Place();
    }

    private void Place()
    {
        transform.position = park + Vector3.left * offset;
        sr.sprite = frames[frame];
        sr.enabled = offset < SlideDistance;
        col.enabled = Present;
    }

    /// <summary>Roll an offer and pull up (debugger "Summon caravan now" too). False = already here or no lot ≥ MinQty.</summary>
    public bool Arrive()
    {
        nextIn = VisitEvery;
        if (Present) return false;
        var inv = Inventory.Instance;
        ItemDef best = null;
        int bestSpare = 0;
        long bestValue = 0;
        foreach (var it in raws)
        {
            int spare = inv.Get(it.itemCategory, it) - inv.GetFloor(it);
            long value = (long)spare * it.sellPrice;
            if (spare > 0 && value > bestValue) (best, bestSpare, bestValue) = (it, spare, value);
        }
        int qty = bestSpare * 7 / 10;
        if (qty < MinQty) return false;
        (Present, Item, Qty, LeaveIn) = (true, best, qty, StayFor);
        GameSignals.RaiseCaravanChanged();
        return true;
    }

    /// <summary>Hand over the lot for the seals; fails (offer stays) if stock fell below the snapshot qty.</summary>
    public bool Accept()
    {
        var inv = Inventory.Instance;
        if (!Present || !inv.TryRemove(inv.GetInventoryType(Item.itemCategory), Item, Qty)) return false;
        inv.InventoryDebugUi();
        ContractManager.Instance.GrantSeals(SealReward);
        Leave();
        return true;
    }

    /// <summary>Decline / timeout / after Accept: drive off; next visit in VisitEvery.</summary>
    public void Leave()
    {
        if (!Present) return;
        (Present, Item, Qty, nextIn) = (false, null, 0, VisitEvery);
        GameSignals.RaiseCaravanChanged();
    }

    public void SetHover(bool on) =>
        sr.renderingLayerMask = on ? sr.renderingLayerMask | hoverOutlineLayer : sr.renderingLayerMask & ~hoverOutlineLayer;

    public CaravanSave ToSave() => new CaravanSave
    {
        nextIn = nextIn, present = Present, item = Item != null ? Item.name : null, qty = Qty, leaveIn = LeaveIn,
    };

    /// <summary>Null chunk (older save) keeps the defaults; an unknown item name drops the offer.</summary>
    public void LoadFrom(CaravanSave s, Dictionary<string, ItemDef> items)
    {
        if (s == null) return;
        (Present, Item, Qty, nextIn) = (false, null, 0, s.nextIn);
        if (s.present && s.item != null && items.TryGetValue(s.item, out var it))
            (Present, Item, Qty, LeaveIn) = (true, it, s.qty, s.leaveIn);
        if (s.present && !Present) nextIn = VisitEvery;
        offset = Present ? 0f : SlideDistance;
        frame = 0;
        Place();
        GameSignals.RaiseCaravanChanged();
    }
}
