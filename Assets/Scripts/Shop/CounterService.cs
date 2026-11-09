using UnityEngine;

public class CounterService : MonoBehaviour
{
    [Header("Service Settings")]
    public float serveTime = 3f;
    [Tooltip("Seconds off serveTime per owned Quick Counter rank (UpgradeType.QuickCounter); never below 0.5 s.")]
    public float serveTimePerRank = 0.6f;
    [Tooltip("Click the customer at the counter to serve them now: the sale pays this much extra (active income, not passive).")]
    [Range(0f, 1f)] public float serveBonus = 0.15f;
    public Transform exitPoint;

    [Header("Gold VFX Settings")]
    [Tooltip("Position to spawn gold VFX (usually counter position)")]
    public Transform goldSpawnPoint;

    private CustomerAgent currentCustomer;
    private float t;
    private bool rushed;
    private QueueController queue;
    private ClickerManager clicker;
    private float effectiveServeTime;

    /// <summary>The customer being served right now (click target), or null.</summary>
    public CustomerAgent Serving => currentCustomer;

    void Start()
    {
        queue = QueueController.Instance;
        clicker = FindAnyObjectByType<ClickerManager>();
        ProgressionManager.OnUpgradePurchased += OnUpgradePurchased;
        ApplyQuickCounter();
        
        // Default to counter point if goldSpawnPoint not assigned
        if (goldSpawnPoint == null && queue != null)
        {
            goldSpawnPoint = queue.counterPoint;
        }
    }

    void OnDestroy() => ProgressionManager.OnUpgradePurchased -= OnUpgradePurchased;

    private void OnUpgradePurchased(GuildUpgradeDef u) { if (u != null && u.upgradeType == UpgradeType.QuickCounter) ApplyQuickCounter(); }

    private void ApplyQuickCounter()
    {
        int ranks = ProgressionManager.Instance != null ? ProgressionManager.Instance.CountUpgrades(UpgradeType.QuickCounter) : 0;
        effectiveServeTime = Mathf.Max(0.5f, serveTime - serveTimePerRank * ranks);
    }

    void Update()
    {
        if (currentCustomer == null)
        {
            var head = queue.PeekHead();
            if (head == null) return;

            // Ensure head is moving to counter
            queue.BringHeadToCounter();

            // If head has reached the counter, either start service or leave immediately
            if (head.State == CustomerState.Queueing &&
                Vector2.Distance(head.transform.position, queue.counterPoint.position) < 0.05f)
            {
                if (!IsPurchasableNow(head, out _, out _))
                {
                    LeaveNow(head);  // No stock / no budget → leave with no purchase
                    queue.DequeueHeadAndShift();
                    return;
                }

                currentCustomer = head;
                currentCustomer.ChangeState(CustomerState.Buying);
                t = 0f;
            }
            return;
        }

        // Serving
        t += Time.deltaTime;
        if (t < effectiveServeTime * (EventManager.Instance != null && EventManager.Instance.RushHour ? EventManager.RushServeScale : 1f)) return;

        CompleteService(currentCustomer);
        currentCustomer = null;
    }

    /// <summary>Serve the customer at the counter now (player click): the sale lands immediately with the serve bonus.</summary>
    public bool TryServe(CustomerAgent c)
    {
        if (c == null || c != currentCustomer) return false;
        rushed = true;
        CompleteService(c);
        currentCustomer = null;
        return true;
    }

    // ===== HELPERS =====

    bool IsPurchasableNow(CustomerAgent c, out int maxAffordable, out float unitPrice)
    {
        maxAffordable = 0;
        unitPrice = 0f;

        if (c == null || c.desiredItem == null || c.desiredQty <= 0) return false;

        if (SalesManager.Instance != null && !SalesManager.Instance.CanSell(c.desiredItem))
            return false;

        var inv = Inventory.Instance.GetInventoryType(c.desiredItem.itemCategory);
        int stock = SalesManager.Instance.GetAvailableForSale(c.desiredItem);
        if (stock <= 0) return false;

        unitPrice = c.desiredItem.sellPrice * c.PriceMultiplier;
        if (unitPrice > c.budget + 0.001f) return false;

        maxAffordable = Mathf.Min(
            stock,
            c.desiredQty,
            Mathf.FloorToInt(c.budget / Mathf.Max(0.01f, unitPrice))
        );

        return maxAffordable > 0;
    }

    void LeaveNow(CustomerAgent c)
    {
        // Turned away: stamp reason + drop the want BEFORE Leaving so carry display / anim / bubble see a walkout.
        // IsPurchasableNow fails on stock first; with stock left, only the budget can have failed.
        c.walkoutReason = SalesManager.Instance.GetAvailableForSale(c.desiredItem) > 0
            ? WalkoutReason.TooExpensive
            : WalkoutReason.NoStock;
        c.desiredItem = null;
        c.desiredQty = 0;
        c.ChangeState(CustomerState.Leaving);
    }

    void CompleteService(CustomerAgent c)
    {
        bool wasRushed = rushed;
        rushed = false;

        // Re-check at completion in case stock changed during the wait
        if (!IsPurchasableNow(c, out int qty, out float price)) 
        {
            LeaveNow(c);
            queue.DequeueHeadAndShift();
            return;
        }
        int totalCost = Mathf.FloorToInt(qty * price);
        var inv = Inventory.Instance.GetInventoryType(c.desiredItem.itemCategory);

        if (Inventory.Instance.TryRemove(inv, c.desiredItem, qty))
        {
            Inventory.Instance.AddGold(totalCost);

            SpawnGoldVFX(c.transform.position, totalCost);
            GameSignals.RaiseItemSold(new ResourceStack(c.desiredItem, qty, totalCost));
            GameSignals.RaiseCustomerServed();

            // Serve bonus: banks on coin arrival like click gold (never raised as ItemSold → not passive income)
            if (wasRushed)
            {
                int tip = Mathf.Max(1, Mathf.RoundToInt(totalCost * serveBonus));
                if (DamageNumberManager.Instance != null)
                {
                    DamageNumberManager.Instance.ShowGoldGain(tip, c.transform);
                    DamageNumberManager.Instance.ShowText($"+{Mathf.RoundToInt(serveBonus * 100f)}%", c.transform, new Color(1f, 0.84f, 0.3f));
                }
                else Inventory.Instance.AddGold(tip);
                if (clicker != null) clicker.Serves++;
            }
        }

        Inventory.Instance.InventoryDebugUi();
        c.LeaveWithPurchase(c.desiredItem, qty);
        queue.DequeueHeadAndShift();
    }

    /// <summary>
    /// Spawn gold coin VFX that flies to UI.
    /// Scales number of coins with gold amount (1 coin per 10 gold, max 15).
    /// </summary>
    private void SpawnGoldVFX(Vector3 customerPosition, int goldAmount)
    {
        if (ItemExplosionVFX.Instance == null)
        {
            Debug.LogWarning("[CounterService] ItemExplosionVFX.Instance is null - can't spawn gold VFX");
            return;
        }

        // Determine spawn position (prefer goldSpawnPoint, fallback to customer)
        Vector3 spawnPos = goldSpawnPoint != null ? goldSpawnPoint.position : customerPosition;

        // Calculate number of coins (1 coin per 2 gold, min 1, max 15)
        int coinCount = Mathf.Clamp(goldAmount / 2, 1, 15);

        // Spawn coin explosion (null sprite uses default coin sprite)
        ItemExplosionVFX.Instance.SpawnExplosion(
            sprite: null,  // Uses defaultCoinSprite from ItemExplosionVFX
            worldPosition: spawnPos,
            count: coinCount,
            maxCount: 15
        );
    }

#if UNITY_EDITOR
    [ContextMenu("Debug: Test Gold VFX")]
    private void DebugTestGoldVFX()
    {
        Vector3 testPos = goldSpawnPoint != null ? goldSpawnPoint.position : transform.position;
        SpawnGoldVFX(testPos, 50);
        Debug.Log("[CounterService] Spawned test gold VFX (50g = 5 coins)");
    }
#endif
}