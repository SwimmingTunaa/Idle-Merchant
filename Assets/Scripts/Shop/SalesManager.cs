using System.Collections.Generic;
using UnityEngine;

public class SalesManager : PersistentSingleton<SalesManager>
{
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    private Dictionary<ItemDef, bool> forSale = new();
    private HashSet<ItemDef> hasBeenSeen = new();
    private readonly List<ItemDef> _sellableBuffer = new(64);

    void Start()
    {
        GameSignals.OnItemAdded += OnItemAddedToInventory;
    }

    void OnDestroy()
    {
        GameSignals.OnItemAdded -= OnItemAddedToInventory;
    }

    public void SetMarkedForSale(ItemDef item, bool enabled)
    {
        if (item == null) return;

        forSale[item] = enabled;

        if (showDebugLogs)
            Debug.Log($"[SalesManager] Auto-sell {(enabled ? "enabled" : "disabled")} for {item.displayName}");
    }

    public bool IsMarkedForSale(ItemDef item)
    {
        if (item == null) return false;
        return forSale.TryGetValue(item, out bool enabled) && enabled;
    }

    public bool CanSell(ItemDef item)
    {
        return GetAvailableForSale(item) > 0;
    }

    public int GetAvailableForSale(ItemDef item)
    {
        if (item == null) return 0;

        if (!IsMarkedForSale(item))
            return 0;

        int totalStock = Inventory.Instance.Get(item.itemCategory, item);
        int reserved = Inventory.Instance.GetReserve(item);
        return Mathf.Max(0, totalStock - reserved);
    }


    public int GetReservedAmount(ItemDef item)
    {
        if (item == null) return 0;
        return Inventory.Instance.GetReserve(item);
    }

    public int GetTotalStock(ItemDef item)
    {
        if (item == null) return 0;
        return Inventory.Instance.Get(item.itemCategory, item);
    }

    public int GetInStorage(ItemDef item)
    {
        if (item == null) return 0;

        int total = GetTotalStock(item);
        int reserved = GetReservedAmount(item);
        int forSale = GetAvailableForSale(item);

        return Mathf.Max(0, total - reserved - forSale);
    }

    public bool TrySellItem(ItemDef item, int quantity, out int goldEarned)
    {
        goldEarned = 0;

        if (item == null || quantity <= 0)
            return false;

        int availableForSale = GetAvailableForSale(item);

        if (availableForSale < quantity)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[SalesManager] Insufficient stock for sale. Requested: {quantity}, Available: {availableForSale}");
            return false;
        }

        goldEarned = item.sellPrice * quantity;

        var inventory = Inventory.Instance.GetInventoryType(item.itemCategory);
        if (!Inventory.Instance.TryRemove(inventory, item, quantity))
        {
            Debug.LogError($"[SalesManager] Failed to remove {quantity}x {item.displayName} from inventory!");
            return false;
        }

        Inventory.Instance.AddGold(goldEarned);

        if (showDebugLogs)
            Debug.Log($"[SalesManager] Sold {quantity}x {item.displayName} for {goldEarned}g");

        GameSignals.RaiseItemSold(new ResourceStack(item, quantity, goldEarned));

        return true;
    }

    public bool TryPickDesiredForCustomer(
        Inventory inventory,
        ItemCategory preference,
        int budget,
        Vector2Int batchRange,
        out ItemDef desiredItem,
        out int desiredQty)
    {
        desiredItem = null;
        desiredQty = 0;

        if (inventory == null) return false;

        _sellableBuffer.Clear();

        // Pull the right inventory dictionary (same mapping your Inventory already uses).
        var dict = inventory.GetInventoryType(preference);
        if (dict == null || dict.Count == 0) return false;

        // Build candidate list from *sellable* stock, not raw inventory.
        foreach (var kvp in dict)
        {
            var item = kvp.Key;
            if (item == null) continue;

            if (!CanSell(item)) continue;                 // your existing rule
            int available = GetAvailableForSale(item);    // your existing rule
            if (available <= 0) continue;

            if (item.sellPrice <= 0) continue;
            if (item.sellPrice > budget) continue;

            _sellableBuffer.Add(item);
        }

        if (_sellableBuffer.Count == 0) return false;

        // Pick 1 item. Simple approach: "best value I can afford" but only among sellables.
        // You can swap this to weighted random later if you want variety.
        ItemDef best = null;
        int bestPrice = int.MinValue;

        for (int i = 0; i < _sellableBuffer.Count; i++)
        {
            var item = _sellableBuffer[i];
            if (item.sellPrice > bestPrice)
            {
                bestPrice = item.sellPrice;
                best = item;
            }
        }

        if (best == null) return false;

        // Qty must be affordable AND in available-for-sale.
        int maxByBudget = budget / best.sellPrice;
        if (maxByBudget <= 0) return false;

        int maxByStock = GetAvailableForSale(best);
        int max = Mathf.Min(maxByBudget, maxByStock);
        if (max <= 0) return false;

        // IMPORTANT: don’t clamp up to batchMin if you can’t afford it.
        int min = Mathf.Max(1, batchRange.x);
        int qty = Mathf.Clamp(max, 1, batchRange.y);

        if (qty < min)
        {
            // If you want: allow "buy less than batchMin" as fallback, or fail.
            // I recommend failing, so customers don't constantly do tiny buys if your design expects batches.
            //may add an ability or stat in the future, to upsell cusomters
            return false;
        }

        // If you want a bit of randomness inside range:
        // qty = Random.Range(min, Mathf.Min(batchRange.y, max) + 1);

        desiredItem = best;
        desiredQty = qty;
        return true;
    }


    public bool SellAllNow(ItemDef item)
    {
        int available = GetAvailableForSale(item);
        if (available <= 0)
            return false;

        return TrySellItem(item, available, out _);
    }

    private void OnItemAddedToInventory(ResourceStack stack)
    {
        if (stack.itemDef == null) return;

        if (!hasBeenSeen.Contains(stack.itemDef))
        {
            hasBeenSeen.Add(stack.itemDef);
            SetMarkedForSale(stack.itemDef, true);

            if (showDebugLogs)
                Debug.Log($"[SalesManager] First time seeing {stack.itemDef.displayName}, auto-marked for sale");
        }
    }
    

    [System.Serializable]
    public class SalesManagerSaveData
    {
        public List<string> markedForSaleItemNames = new();
        public List<string> hasBeenSeenItemNames = new();
    }

    public SalesManagerSaveData GetSaveData()
    {
        var saveData = new SalesManagerSaveData();

        foreach (var kvp in forSale)
        {
            if (kvp.Key != null && kvp.Value)
            {
                saveData.markedForSaleItemNames.Add(kvp.Key.name);
            }
        }

        foreach (var item in hasBeenSeen)
        {
            if (item != null)
            {
                saveData.hasBeenSeenItemNames.Add(item.name);
            }
        }

        return saveData;
    }

    public void LoadSaveData(SalesManagerSaveData saveData)
    {
        if (saveData == null)
        {
            Debug.LogWarning("[SalesManager] LoadSaveData called with null data");
            return;
        }

        forSale.Clear();
        hasBeenSeen.Clear();

        foreach (var itemName in saveData.markedForSaleItemNames)
        {
            var item = Resources.Load<ItemDef>($"Items/{itemName}");
            if (item != null)
            {
                forSale[item] = true;
            }
            else
            {
                Debug.LogWarning($"[SalesManager] Could not find ItemDef '{itemName}' when loading save data");
            }
        }

        foreach (var itemName in saveData.hasBeenSeenItemNames)
        {
            var item = Resources.Load<ItemDef>($"Items/{itemName}");
            if (item != null)
            {
                hasBeenSeen.Add(item);
            }
            else
            {
                Debug.LogWarning($"[SalesManager] Could not find ItemDef '{itemName}' when loading save data");
            }
        }

        if (showDebugLogs)
            Debug.Log($"[SalesManager] Loaded save data: {forSale.Count} for sale, {hasBeenSeen.Count} seen items");
    }

    [ContextMenu("Debug/Print All Auto-Sell Items")]
    private void DebugPrintAutoSellItems()
    {
        Debug.Log("=== AUTO-SELL ENABLED ITEMS ===");
        foreach (var kvp in forSale)
        {
            if (kvp.Value)
            {
                int total = GetTotalStock(kvp.Key);
                int reserved = GetReservedAmount(kvp.Key);
                int forSale = GetAvailableForSale(kvp.Key);
                Debug.Log($"{kvp.Key.displayName}: Total={total}, Reserved={reserved}, For Sale={forSale}");
            }
        }
    }

    [ContextMenu("Debug/Print Has Been Seen Items")]
    private void DebugPrintHasBeenSeen()
    {
        Debug.Log("=== HAS BEEN SEEN ITEMS ===");
        foreach (var item in hasBeenSeen)
        {
            if (item != null)
            {
                Debug.Log($"- {item.displayName}");
            }
        }
    }

    [ContextMenu("Debug/Test Save/Load")]
    private void DebugTestSaveLoad()
    {
        var saveData = GetSaveData();
        Debug.Log($"[SalesManager] Save data: {saveData.markedForSaleItemNames.Count} for sale, {saveData.hasBeenSeenItemNames.Count} seen");
        
        string json = JsonUtility.ToJson(saveData, true);
        Debug.Log($"[SalesManager] JSON:\n{json}");
        
        var loadedData = JsonUtility.FromJson<SalesManagerSaveData>(json);
        LoadSaveData(loadedData);
        Debug.Log("[SalesManager] Save/Load test complete");
    }
}