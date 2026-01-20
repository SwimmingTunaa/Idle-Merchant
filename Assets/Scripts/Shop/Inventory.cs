using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

public class Inventory : MonoBehaviour {
    public static Inventory Instance { get; private set; }

    [Header("Config")]
    [SerializeField] private GameVariable gold;
    [SerializeField] private GameObject inventoryHolder;
    [SerializeField] private TMP_Text debugText;

    [Header("Debug - Add Items")]
    [Tooltip("Items to add in bulk (inspector helper)")]
    [SerializeField] private List<ItemStack> itemsToAdd = new List<ItemStack>();

    [System.Serializable]
    public class ItemStack
    {
        public ItemDef item;
        public int quantity = 10;
    }

    private float goldFrac;

    private readonly Dictionary<ItemDef, int> commonInventory  = new();
    private readonly Dictionary<ItemDef, int> craftedInventory = new();
    private readonly Dictionary<ItemDef, int> luxuryInventory  = new();

    void Awake() {
        Instance = this;
        gold.ResetToDefault();
        InventoryDebugUi();
        GameSignals.OnGoldEarned += AddGold;
    }

    void OnDestroy() {
        GameSignals.OnGoldEarned -= AddGold;
    }

    // -------- Inventory Core --------
    public Dictionary<ItemDef,int> GetInventoryType(ItemCategory itemCategory) => itemCategory switch {
        ItemCategory.Common  => commonInventory,
        ItemCategory.Crafted => craftedInventory,
        ItemCategory.Luxury  => luxuryInventory,
        _ => commonInventory
    };

    public int Get(Dictionary<ItemDef,int> inventory, ItemDef item) =>
        inventory.TryGetValue(item, out var q) ? q : 0;

    public int Get(ItemCategory cat, ItemDef item) =>
        Get(GetInventoryType(cat), item);

    public void Add(Dictionary<ItemDef,int> inventory, ResourceStack stack) {
        inventory.TryGetValue(stack.itemDef, out var q);
        inventory[stack.itemDef] = q + stack.qty;
        InventoryDebugUi();
    }

    public bool TryRemove(Dictionary<ItemDef,int> inventory, ItemDef item, int qty) {
        if (Get(inventory, item) < qty) return false;
        inventory[item] -= qty;
        return true;
    }

    // -------- Gold --------
    public void AddGoldFloat(float amount) {
        goldFrac += amount;
        int whole = Mathf.FloorToInt(goldFrac);
        if (whole > 0) {
            gold.Add(whole);
            goldFrac -= whole;
            GameSignals.RaiseGoldChanged(gold.Int);
            InventoryDebugUi();
        }
    }

    public void AddGold(int amount) {
        gold.Add(amount);
        GameSignals.RaiseGoldChanged(gold.Int);
    }

    public int Gold => gold.Int;

    // -------- Inspector Helpers --------
    
    [ContextMenu("Debug/Add Items From List")]
    private void AddItemsFromList()
    {
        if (itemsToAdd == null || itemsToAdd.Count == 0)
        {
            Debug.LogWarning("[Inventory] No items in itemsToAdd list!");
            return;
        }

        foreach (var stack in itemsToAdd)
        {
            if (stack.item == null)
            {
                Debug.LogWarning("[Inventory] Null item in itemsToAdd list, skipping");
                continue;
            }

            var inv = GetInventoryType(stack.item.itemCategory);
            Add(inv, new ResourceStack(stack.item, stack.quantity, 0));
            
            Debug.Log($"[Inventory] Added {stack.quantity}x {stack.item.displayName}");
        }

        InventoryDebugUi();
    }

    [ContextMenu("Debug/Clear All Items")]
    private void ClearAllItems()
    {
        commonInventory.Clear();
        craftedInventory.Clear();
        luxuryInventory.Clear();
        Debug.Log("[Inventory] Cleared all items");
        InventoryDebugUi();
    }

    // -------- Debug / Snapshot --------
    public struct InventoryRow {
        public ItemDef item;
        public ItemCategory category;
        public int qty;
        public float unitPrice;
        public float totalValue;
    }

    public List<InventoryRow> SnapshotAll() {
        var rows = new List<InventoryRow>(64);
        AppendSnapshot(commonInventory,  ItemCategory.Common,  rows);
        AppendSnapshot(craftedInventory, ItemCategory.Crafted, rows);
        AppendSnapshot(luxuryInventory,  ItemCategory.Luxury,  rows);
        rows.Sort((a,b) => {
            int c = a.category.CompareTo(b.category);
            if (c != 0) return c;
            return string.Compare(a.item.displayName, b.item.displayName, System.StringComparison.Ordinal);
        });
        return rows;
    }

    private void AppendSnapshot(Dictionary<ItemDef,int> inv, ItemCategory cat, List<InventoryRow> outRows) {
        foreach (var kv in inv) {
            var item = kv.Key; int qty = kv.Value;
            if (item == null || qty <= 0) continue;
            float price = item.sellPrice;
            outRows.Add(new InventoryRow {
                item = item,
                category = cat,
                qty = qty,
                unitPrice = price,
                totalValue = price * qty
            });
        }
    }

    [ContextMenu("Debug/Print Inventory To Console")]
    public void InventoryDebugLog() {
        var rows = SnapshotAll();
        float grand = 0f;
        System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
        sb.AppendLine("=== Inventory ===");
        foreach (var r in rows) {
            grand += r.totalValue;
            sb.AppendLine($"{r.category,-7} | {r.item.displayName,-18} x{r.qty} @ {r.unitPrice:0.##} = {r.totalValue:0.##}");
        }
        sb.AppendLine($"Gold: {Gold}");
        sb.AppendLine($"Total Stock Value: {grand:0.##}");
        UnityEngine.Debug.Log(sb.ToString());
    }

    [ContextMenu("Debug/Show Inventory On-Screen")]
    public void InventoryDebugUi() {
        if (debugText == null) { InventoryDebugLog(); return; }
        var rows = SnapshotAll();
        float grand = 0f;
        System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
        sb.AppendLine("<b>Inventory</b>");
        foreach (var r in rows) {
            grand += r.totalValue;
            sb.AppendLine($"{r.category}: {r.item.displayName} x{r.qty}  <i>@{r.unitPrice:0.##}</i> = {r.totalValue:0.##}");
        }
        sb.AppendLine($"\nGold: {Gold}");
        sb.AppendLine($"Total Stock Value: {grand:0.##}");
        debugText.text = sb.ToString();
    }
}