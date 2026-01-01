using UnityEngine;

public class CounterService : MonoBehaviour
{
    public float serveTime = 1.5f;
    public Transform exitPoint;

    private CustomerAgent currentCustomer;
    private float t;
    private QueueController queue;

    void Start() => queue = QueueController.Instance;

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
                    LeaveNow(head);                 // â† no stock / no budget â†’ leave with no purchase
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

    // ---- Helpers ----

    bool IsPurchasableNow(CustomerAgent c, out int maxAffordable, out float unitPrice)
    {
        maxAffordable = 0;
        unitPrice = 0f;

        if (c == null || c.desiredItem == null || c.desiredQty <= 0) return false;

        var inv = Inventory.Instance.GetInventoryType(c.desiredItem.itemCategory);
        int stock = Inventory.Instance.Get(inv, c.desiredItem);
        if (stock <= 0) return false;

        unitPrice = c.desiredItem.sellPrice; // or MarketPricing.PriceFor(c.desiredItem)
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
        }

        Inventory.Instance.InventoryDebugUi();   // debug view if you want
        LeaveNow(c);
        queue.DequeueHeadAndShift();
    }
}