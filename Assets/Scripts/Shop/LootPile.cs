using UnityEngine;

/// <summary>
/// World heap behind the shopkeeper that mirrors inventory stock — a gauge, not a warehouse:
/// up to <see cref="maxSprites"/> item sprites inside a bin, round-robin across item types,
/// newest item on top. Rebuilt once per frame after OnItemAdded / OnItemSold (dirty flag).
/// Also the landing point for item fly-ins (vacuumed loot, porter deposits) and the launch
/// point for the sale hop to the counter.
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
    [SerializeField] private float itemScale = 0.6f;
    [Tooltip("Order of the bottom item; items stack upward and the bin front draws over them all. " +
             "Scenery band: above shop walls/counter (≤2), below entities (2000+).")]
    [SerializeField] private int baseSortingOrder = 3;
    [Tooltip("Front wall of the bin — drawn in front of the items.")]
    [SerializeField] private SpriteRenderer binFront;

    [Header("Fly")]
    [SerializeField] private float flyScale = 0.5f;
    [Tooltip("Where sold items hop to. Empty = the queue's counter point, lifted onto the counter top.")]
    [SerializeField] private Transform saleTarget;

    private SpriteRenderer[] slots;
    private int visibleCount;
    private bool pending = true;
    private ItemDef lastAdded;

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
            go.transform.localScale = Vector3.one * itemScale;

            var sr = go.AddComponent<SpriteRenderer>();
            if (binFront != null) sr.sharedMaterial = binFront.sharedMaterial;
            sr.sortingOrder = baseSortingOrder + i;
            sr.enabled = false;
            slots[i] = sr;
        }
        if (binFront != null) binFront.sortingOrder = baseSortingOrder + maxSprites;

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

    private void OnItemSold(ResourceStack stack)
    {
        pending = true;
        if (ItemExplosionVFX.Instance == null || stack.itemDef == null) return;
        Vector3 to = saleTarget != null ? saleTarget.position
            : QueueController.Instance != null ? QueueController.Instance.counterPoint.position + Vector3.up * 0.8f
            : transform.position;
        ItemExplosionVFX.Instance.FlyToWorld(Icon(stack.itemDef), TopOfHeap, to, flyScale);
    }

    /// <summary>Fly one item icon from a world point into the pile (VFX only — the item is already banked).</summary>
    public void FlyIn(ItemDef item, Vector3 from)
    {
        if (item == null || ItemExplosionVFX.Instance == null) return;
        ItemExplosionVFX.Instance.FlyToWorld(Icon(item), from, TopOfHeap, flyScale);
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
