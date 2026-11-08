using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One display shelf on the shop's back wall (Guild/Shop/Shelves/Shelf N), switched on by the ProgressionActivator on
/// the same object when its Shelves rank is owned. Shows the icons of the stock that's for sale, ranked by price:
/// shelf 0 the three priciest, shelf 1 the next three … Re-ranks once a second (a sort of a few dozen items, shared
/// by every shelf that ticks that frame; no signal plumbing). The shelves are the visible half of the Shelves upgrade;
/// the browse-time cut is CustomerAgent.ShelfBrowseScale.
/// </summary>
public class ShelfProp : MonoBehaviour
{
    [Tooltip("Which three of the price-ranked sellables this shelf shows: 0 = priciest three, 1 = the next three …")]
    [SerializeField] private int shelfIndex;
    [Tooltip("Icon renderers sitting on the plank, left to right (scaled in the scene so a 1-unit icon fits the gap).")]
    [SerializeField] private SpriteRenderer[] slots;
    [SerializeField] private float refreshSeconds = 1f;

    private static readonly ItemCategory[] Categories = { ItemCategory.Basic, ItemCategory.Advanced, ItemCategory.Premium };
    private static readonly List<ItemDef> ranked = new(64);
    private static int rankedFrame = -1;
    private float next;

    void OnEnable() => next = 0f;

    void Update()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + refreshSeconds;
        Rank();
        for (int i = 0; i < slots.Length; i++)
        {
            int k = shelfIndex * slots.Length + i;
            slots[i].sprite = k < ranked.Count ? ranked[k].icon : null;
        }
    }

    // for-sale stock by sell price, high to low; shared across shelves within a frame
    private static void Rank()
    {
        if (rankedFrame == Time.frameCount) return;
        rankedFrame = Time.frameCount;
        ranked.Clear();
        var sales = SalesManager.Instance; var inv = Inventory.Instance;
        if (sales == null || inv == null) return;
        foreach (var cat in Categories)
        {
            var dict = inv.GetInventoryType(cat);
            if (dict == null) continue;
            foreach (var kvp in dict)
                if (kvp.Key != null && sales.CanSell(kvp.Key) && sales.GetAvailableForSale(kvp.Key) > 0) ranked.Add(kvp.Key);
        }
        ranked.Sort((a, b) => b.sellPrice.CompareTo(a.sellPrice));
    }
}
