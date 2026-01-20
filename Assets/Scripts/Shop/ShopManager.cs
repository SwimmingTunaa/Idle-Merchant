using System.Collections.Generic;
using UnityEngine;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance;
    private QueueController queue;
    public List<CustomerAgent> NonQueueingCustomers = new List<CustomerAgent>();

    [Header("Queue Entry")]
    [Tooltip("Distance at which seeking customers join the queue")]
    public float queueJoinDistance = 1.0f;
    
    // Cache for performance
    private float queueJoinDistanceSqr;

    public void Register(CustomerAgent a)   {NonQueueingCustomers.Add(a); }
    public void Unregister(CustomerAgent a) { NonQueueingCustomers.Remove(a); }

    void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(this);
        else
            Instance = this;
        
        queueJoinDistanceSqr = queueJoinDistance * queueJoinDistance;
    }

    void Start()
    {
        queue = QueueController.Instance;  
    }

    void Update()
    {
        if (queue == null || NonQueueingCustomers.Count == 0) return;

        // PERFORMANCE: Cache queue end position once per frame
        // OLD: Called N times per frame (once per customer) = O(N) overhead
        // NEW: Called once, reused = O(1) overhead
        Vector3 queueEndPos = queue.GetQueueEndPosition();

        for (int i = NonQueueingCustomers.Count - 1; i >= 0; i--)
        {
            var customer = NonQueueingCustomers[i];
            if (customer == null) 
            {
                NonQueueingCustomers.RemoveAt(i);
                continue;
            }

            // Only process customers actively seeking queue
            if (customer.State != CustomerState.SeekingQueue) continue;

            // Early exit if queue just filled
            if (queue.IsFull)
            {
                // Don't break - let remaining customers timeout naturally
                continue;
            }

            // PERFORMANCE: Use sqrMagnitude instead of Distance (avoids sqrt)
            // Distance: ~30 cycles, sqrMagnitude: ~10 cycles
            float sqrDist = (customer.transform.position - queueEndPos).sqrMagnitude;

            if (sqrDist <= queueJoinDistanceSqr)
            {
                if (queue.TryEnqueue(customer))
                {
                    customer.ChangeState(CustomerState.Queueing);
                    NonQueueingCustomers.RemoveAt(i);
                    
                    // Recalculate queue end since it moved
                    queueEndPos = queue.GetQueueEndPosition();
                }
            }
        }
    }
}