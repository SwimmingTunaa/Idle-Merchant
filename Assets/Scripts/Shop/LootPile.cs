using UnityEngine;

/// <summary>
/// World heap behind the shopkeeper that mirrors inventory stock — a gauge, not a warehouse:
/// up to <see cref="maxSprites"/> item sprites inside a bin, round-robin across item types,
/// newest item on top. Rebuilt once per frame after OnItemAdded / OnItemSold (dirty flag).
/// Also the landing point for item fly-ins (vacuumed loot, porter deposits) and the launch
/// point for the sale relay (pile → counter → buyer, see <see cref="FlySale"/>).
/// </summary>
public class LootPile : PersistentSingleton<LootPile>
{
    [Header("Heap")]
    [Tooltip("Max item sprites shown; the heap stops growing past this much stock.")]
    [SerializeField] private int maxSprites = 16;
    [Tooltip("Width of the heap base inside the bin (world units).")]
    [SerializeField] private float heapWidth = 1.4f;
    [Tooltip("Height of a full heap above the base (world units).")]
    [SerializeField] private float heapHeight = 0.9f;
    [Tooltip("Local Y of the bottom item row — just under the bin rim so it reads as contained.")]
    [SerializeField] private float baseY = 0.5f;
    [Tooltip("Order of the bottom item; items stack upward and the bin front draws over them all. " +
             "Scenery band: above shop walls/counter (≤2), below entities (2000+).")]
    [SerializeField] private int baseSortingOrder = 3;
    [Tooltip("Front wall of the bin — drawn in front of the items.")]
    [SerializeField] private SpriteRenderer binFront;

    [Header("Fly")]
    [Tooltip("Where sold items hop to. Empty = the queue's counter point, lifted onto the counter top.")]
    [SerializeField] private Transform saleTarget;
    [Tooltip("Speed multiplier for both sale-relay legs (1 = the flyer prefab's own timing, ~0.7s per leg).")]
    [SerializeField] private float saleHopSpeed = 2f;
    [Tooltip("Shopkeeper sprite: turns to face the pile (flipX inverted from rest) while any sale's pile→counter leg is in flight. Optional.")]
    [SerializeField] private SpriteRenderer shopkeeper;
    [Tooltip("Arc multiplier for tossed fly-ins (porter deposits, station workers) — a touch loftier than the default hop (the arc already grows with distance: 1.25 keeps the forge→bin throw under the hall beams).")]
    [SerializeField] private float tossArcScale = 1.25f;

    private SpriteRenderer[] slots;
    private int visibleCount;
    private bool pending = true;
    private ItemDef lastAdded;
    private int summoning;          // pile→counter legs in flight
    private bool shopkeeperRestFlip;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;

        // Pre-allocated slots at deterministic jittered positions (seeded by index): bottom → top, narrowing into a mound.
        slots = new SpriteRenderer[maxSprites];
        for (int i = 0; i < maxSprites; i++)
        {
            float t = (float)i / maxSprites;
            float halfSpread = heapWidth * 0.5f * (1f - 0.7f * t);
            var go = new GameObject($"Pile Item {i}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3((Hash(i) * 2f - 1f) * halfSpread, baseY + t * heapHeight + Hash(i + 101) * 0.08f, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, (Hash(i + 211) - 0.5f) * 50f);

            var sr = go.AddComponent<SpriteRenderer>();
            if (binFront != null) sr.sharedMaterial = binFront.sharedMaterial;
            sr.sortingOrder = baseSortingOrder + i;
            sr.enabled = false;
            slots[i] = sr;
        }
        if (binFront != null) binFront.sortingOrder = baseSortingOrder + maxSprites;
        if (shopkeeper != null) shopkeeperRestFlip = shopkeeper.flipX;

        GameSignals.OnItemAdded += OnItemAdded;
        GameSignals.OnItemSold += OnItemSold;
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        GameSignals.OnItemAdded -= OnItemAdded;
        GameSignals.OnItemSold -= OnItemSold;
    }

    void Update()
    {
        if (!pending || Inventory.Instance == null) return;
        pending = false;
        Rebuild();
    }

    // ponytail: TryRemove raises no signal, so crafting-ingredient removals show on the next add/sale.
    private void OnItemAdded(ResourceStack stack) { lastAdded = stack.itemDef; pending = true; }

    // The sale hop itself is driven by the buyer (CustomerCarryDisplay → FlySale), so it can hand off to them.
    private void OnItemSold(ResourceStack stack) => pending = true;

    /// <summary>Sale relay (VFX only): item hops pile → counter, then homes on <paramref name="buyer"/>.
    /// <paramref name="onArrive"/> fires when it lands — or right away if a leg can't run / the buyer is gone.</summary>
    public void FlySale(ItemDef item, Transform buyer, System.Action onArrive)
    {
        var vfx = ItemExplosionVFX.Instance;
        if (vfx == null || item == null || buyer == null) { onArrive?.Invoke(); return; }

        Sprite icon = Icon(item);
        Vector3 counter = saleTarget != null ? saleTarget.position
            : QueueController.Instance != null ? QueueController.Instance.counterPoint.position + Vector3.up * 0.8f
            : transform.position;

        Summon(+1);
        vfx.FlyToWorld(icon, TopOfHeap, counter, speed: saleHopSpeed, onArrive: () =>
        {
            Summon(-1);   // leg-1 callback always fires (arrival, despawn, or sync on spawn failure) → counter can't leak
            if (buyer == null || !buyer.gameObject.activeInHierarchy) { onArrive?.Invoke(); return; }   // left mid-hop
            vfx.FlyToWorld(icon, counter, buyer.position, homing: buyer, speed: saleHopSpeed, onArrive: onArrive);
        });
    }

    // Shopkeeper faces the pile while any leg 1 is flying ("summoning" the item), back to the customers otherwise.
    private void Summon(int delta)
    {
        summoning = Mathf.Max(0, summoning + delta);
        if (shopkeeper != null) shopkeeper.flipX = summoning > 0 ? !shopkeeperRestFlip : shopkeeperRestFlip;
    }

    /// <summary>Fly one item icon from a world point into the pile (VFX only — the item is already banked).
    /// <paramref name="toss"/> = thrown by hand (porter, station worker): a loftier arc.</summary>
    public void FlyIn(ItemDef item, Vector3 from, bool toss = false)
    {
        if (item == null || ItemExplosionVFX.Instance == null) return;
        ItemExplosionVFX.Instance.FlyToWorld(Icon(item), from, TopOfHeap, arcScale: toss ? tossArcScale : 1f);
    }

    private void Rebuild()
    {
        var rows = Inventory.Instance.SnapshotAll();   // allocates, but only once per dirty frame

        // Round-robin across item types: variety over volume, capped at the slot count.
        int filled = 0;
        for (int pass = 0; filled < slots.Length; pass++)
        {
            int before = filled;
            foreach (var r in rows)
            {
                if (filled >= slots.Length) break;
                if (r.qty > pass) slots[filled++].sprite = Icon(r.item);
            }
            if (filled == before) break;
        }

        if (filled > 0 && lastAdded != null && Inventory.Instance.Get(lastAdded.itemCategory, lastAdded) > 0)
            slots[filled - 1].sprite = Icon(lastAdded);   // newest on top

        for (int i = 0; i < slots.Length; i++) slots[i].enabled = i < filled;
        visibleCount = filled;
    }

    private Vector3 TopOfHeap => visibleCount > 0
        ? slots[visibleCount - 1].transform.position
        : transform.position + Vector3.up * baseY;

    private static Sprite Icon(ItemDef item) => item.spriteDrop != null ? item.spriteDrop : item.icon;

    private static float Hash(int n) => Mathf.Repeat(Mathf.Sin(n * 12.9898f + 1.7f) * 43758.5453f, 1f);
}
