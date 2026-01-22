using UnityEngine;

public class CounterService : MonoBehaviour
{
    [Header("Service Settings")]
    public float serveTime = 1.5f;
    public Transform exitPoint;

    [Header("Gold VFX Settings")]
    [Tooltip("Position to spawn gold VFX (usually counter position)")]
    public Transform goldSpawnPoint;

    private CustomerAgent currentCustomer;
    private float t;
    private QueueController queue;

    void Start()
    {
        queue = QueueController.Instance;
        
        // Default to counter point if goldSpawnPoint not assigned
        if (goldSpawnPoint == null && queue != null)
        {
            goldSpawnPoint = queue.counterPoint;
        }
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
        if (t < serveTime) return;

        CompleteService(currentCustomer);
        currentCustomer = null;
    }

    // ===== HELPERS =====

    bool IsPurchasableNow(CustomerAgent c, out int maxAffordable, out float unitPrice)
    {
        maxAffordable = 0;
        unitPrice = 0f;

        if (c == null || c.desiredItem == null || c.desiredQty <= 0) return false;

        var inv = Inventory.Instance.GetInventoryType(c.desiredItem.itemCategory);
        int stock = Inventory.Instance.Get(inv, c.desiredItem);
        if (stock <= 0) return false;

        unitPrice = c.desiredItem.sellPrice;
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
        // No purchase. Send to exit and switch state.
        c.SetTarget(exitPoint.position);
        c.ChangeState(CustomerState.Leaving);
    }

    void CompleteService(CustomerAgent c)
    {
        // Re-check at completion in case stock changed during the wait
        if (!IsPurchasableNow(c, out int toBuy, out float unit)) 
        {
            LeaveNow(c);
            queue.DequeueHeadAndShift();
            return;
        }

        var inv = Inventory.Instance.GetInventoryType(c.desiredItem.itemCategory);

        if (Inventory.Instance.TryRemove(inv, c.desiredItem, toBuy))
        {
            int goldGain = Mathf.RoundToInt(unit * toBuy);
            Inventory.Instance.AddGold(goldGain);

            // ADDED: Spawn gold VFX when customer purchases
            SpawnGoldVFX(c.transform.position, goldGain);
        }

        Inventory.Instance.InventoryDebugUi();
        LeaveNow(c);
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