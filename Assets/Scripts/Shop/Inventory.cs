using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;


public class Inventory : PersistentSingleton<Inventory> 
{
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
    private readonly Dictionary<ItemDef, int> itemReserves = new();

    protected override void Awake() 
    {
        base.Awake();
        gold.ResetToDefault();
        InventoryDebugUi();
        GameSignals.OnGoldEarned += AddGold;
    }

    void OnDestroy() 
    {
        GameSignals.OnGoldEarned -= AddGold;
    }

    // ===== INVENTORY CORE =====
    
    public Dictionary<ItemDef,int> GetInventoryType(ItemCategory itemCategory) => itemCategory switch 
    {
        ItemCategory.Common  => commonInventory,
        ItemCategory.Crafted => craftedInventory,
        ItemCategory.Luxury  => luxuryInventory,
        _ => commonInventory
    };

    public int Get(Dictionary<ItemDef,int> inventory, ItemDef item) =>
        inventory.TryGetValue(item, out var q) ? q : 0;

    public int Get(ItemCategory cat, ItemDef item) =>
        Get(GetInventoryType(cat), item);

    public void Add(Dictionary<ItemDef,int> inventory, ResourceStack stack) 
    {
        inventory.TryGetValue(stack.itemDef, out var q);
        inventory[stack.itemDef] = q + stack.qty;
        GameSignals.RaiseItemAdded(stack);
        InventoryDebugUi();
    }

    public bool TryRemove(Dictionary<ItemDef,int> inventory, ItemDef item, int qty) 
    {
        if (Get(inventory, item) < qty) return false;
        inventory[item] -= qty;
        return true;
    }

    public void SetReserve(ItemDef item, int amount)
    {
        if (item == null) return;
        itemReserves[item] = Mathf.Max(0, amount);
    }

    public int GetReserve(ItemDef item)
    {
        if (item == null) return 0;
        return itemReserves.TryGetValue(item, out int qty) ? qty : 0;
    }

    // ===== GOLD MANAGEMENT =====
    
    /// <summary>
    /// Add fractional gold (for continuous income sources like clicks).
    /// Accumulates fractional amounts and adds whole numbers when threshold is reached.
    /// </summary>
    public void AddGoldFloat(float amount) 
    {
        goldFrac += amount;
        int whole = Mathf.FloorToInt(goldFrac);
        if (whole > 0) 
        {
            goldFrac -= whole;
            gold.Add(gold.Int);
            GameSignals.RaiseGoldChanged(gold.Int);
            InventoryDebugUi();
        }
    }

    /// <summary>
    /// Add integer gold amount.
    /// Use for discrete transactions (hiring, sales, rewards).
    /// </summary>
    public void AddGold(int amount) 
    {
        gold.Add(amount);
        GameSignals.RaiseGoldChanged(gold.Int);
    }

    /// <summary>
    /// Current gold balance (read-only).
    /// </summary>
    public int Gold => gold.Int;

    /// <summary>
    /// NEW: Check if player can afford a specific cost.
    /// Read-only check with no side effects.
    /// Use this for validation before attempting purchases.
    /// </summary>
    /// <param name="cost">Amount to check</param>
    /// <returns>True if current gold >= cost</returns>
    public bool CanAfford(int cost)
    {
        return gold.Int >= cost;
    }

    /// <summary>
    /// NEW: Attempt to spend gold atomically.
    /// Validates affordability and deducts in one operation.
    /// Preferred over manual check + AddGold(-amount) for safety.
    /// </summary>
    /// <param name="cost">Amount to deduct</param>
    /// <returns>True if gold was deducted, false if insufficient funds</returns>
    public bool TrySpendGold(int cost)
    {
        if (cost < 0)
        {
            Debug.LogWarning($"[Inventory] TrySpendGold called with negative cost: {cost}. Use AddGold() for adding gold.");
            return false;
        }

        if (gold.Int < cost)
        {
            return false;
        }

        gold.Add(-cost);
        GameSignals.RaiseGoldChanged(gold.Int);
        return true;
    }

    // ===== INSPECTOR HELPERS =====
    
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

    [ContextMenu("Debug/Add 100 Gold")]
    private void DebugAdd100Gold()
    {
        AddGold(100);
        Debug.Log($"[Inventory] Added 100 gold. New total: {Gold}");
    }

    [ContextMenu("Debug/Test TrySpendGold")]
    private void DebugTestSpendGold()
    {
        Debug.Log($"[Inventory] Current gold: {Gold}");
        Debug.Log($"[Inventory] Can afford 50g? {CanAfford(50)}");
        
        if (TrySpendGold(50))
        {
            Debug.Log($"[Inventory] Successfully spent 50 gold. New total: {Gold}");
        }
        else
        {
            Debug.Log("[Inventory] Failed to spend 50 gold - insufficient funds");
        }
    }

    // ===== DEBUG / SNAPSHOT =====
    
    public struct InventoryRow 
    {
        public ItemDef item;
        public ItemCategory category;
        public int qty;
        public float unitPrice;
        public float totalValue;
    }

    public List<InventoryRow> SnapshotAll() 
    {
        var rows = new List<InventoryRow>(64);
        AppendSnapshot(commonInventory,  ItemCategory.Common,  rows);
        AppendSnapshot(craftedInventory, ItemCategory.Crafted, rows);
        AppendSnapshot(luxuryInventory,  ItemCategory.Luxury,  rows);
        rows.Sort((a,b) => 
        {
            int c = a.category.CompareTo(b.category);
            if (c != 0) return c;
            return string.Compare(a.item.displayName, b.item.displayName, System.StringComparison.Ordinal);
        });
        return rows;
    }

    private void AppendSnapshot(Dictionary<ItemDef,int> inv, ItemCategory cat, List<InventoryRow> outRows) 
    {
        foreach (var kv in inv) 
        {
            var item = kv.Key; 
            int qty = kv.Value;
            if (item == null || qty <= 0) continue;
            float price = item.sellPrice;
            outRows.Add(new InventoryRow 
            {
                item = item,
                category = cat,
                qty = qty,
                unitPrice = price,
                totalValue = price * qty
            });
        }
    }

    [ContextMenu("Debug/Print Inventory To Console")]
    public void InventoryDebugLog() 
    {
        var rows = SnapshotAll();
        float grand = 0f;
        System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
        sb.AppendLine("=== Inventory ===");
        foreach (var r in rows) 
        {
            grand += r.totalValue;
            sb.AppendLine($"{r.category,-7} | {r.item.displayName,-18} x{r.qty} @ {r.unitPrice:0.##} = {r.totalValue:0.##}");
        }
        sb.AppendLine($"Gold: {Gold}");
        sb.AppendLine($"Total Stock Value: {grand:0.##}");
        UnityEngine.Debug.Log(sb.ToString());
    }

    [ContextMenu("Debug/Show Inventory On-Screen")]
    public void InventoryDebugUi() 
    {
        if (debugText == null) 
        { 
            InventoryDebugLog(); 
            return; 
        }
        
        var rows = SnapshotAll();
        float grand = 0f;
        System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
        sb.AppendLine("<b>Inventory</b>");
        foreach (var r in rows) 
        {
            grand += r.totalValue;
            sb.AppendLine($"{r.category}: {r.item.displayName} x{r.qty}  <i>@{r.unitPrice:0.##}</i> = {r.totalValue:0.##}");
        }
        sb.AppendLine($"\nGold: {Gold}");
        sb.AppendLine($"Total Stock Value: {grand:0.##}");
        debugText.text = sb.ToString();
    }
}