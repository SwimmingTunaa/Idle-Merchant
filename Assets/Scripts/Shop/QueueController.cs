using System.Collections.Generic;
using UnityEngine;

public class QueueController : MonoBehaviour
{

    public static QueueController Instance;

    [Header("Queue Layout")]
    public Transform counterPoint;           // where the head stands to be served
    public int maxQueueAmount;

    public Vector2 lineDirection = new Vector2(-1f, 0f);


    [Header("Spacing")]
    [Tooltip("Minimum extra gap added between customers in a fully packed line.")]
    public float minGap = 0.08f;
    [Tooltip("Maximum extra gap when only a few people are in line.")]
    public float maxGap = 0.35f;
    [Tooltip("At this queue size or bigger, gaps are at minGap; at size 1, gaps approach maxGap.")]
    public int tightQueueSize = 6;


    private readonly List<CustomerAgent> queue = new();

    public int Count => queue.Count;
    public bool IsFull => queue.Count >= maxQueueAmount;

    void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(this);
        else
            Instance = this;
    }

    /// <summary>
    /// Get the position where the next customer should stand (end of queue).
    /// Returns counter position if queue is empty.
    /// </summary>
    public Vector3 GetQueueEndPosition()
    {
        // Return calculated position for next spot, not last customer's current position
        return CalculatePositionForIndex(queue.Count);
    }

    /// <summary>
    /// Calculate where a customer at given index should stand in the queue.
    /// Index 0 = counter position, Index 1+ = behind previous customers.
    /// </summary>
    private Vector3 CalculatePositionForIndex(int index)
    {
        if (counterPoint == null) return Vector3.zero;
        if (index == 0) return counterPoint.position;

        // Calculate gap scaling based on queue size
        float t = Mathf.InverseLerp(1, Mathf.Max(2, tightQueueSize), queue.Count);
        float extraGap = Mathf.Lerp(maxGap, minGap, t);

        // Direction away from counter
        Vector3 back = -((Vector3)lineDirection).normalized;

        // Accumulate offset from counter
        float offset = 0f;
        float prevHalf = queue.Count > 0 ? queue[0].GetWidth() * 0.5f : 0.5f;

        // Calculate total offset for this index
        for (int i = 1; i <= index; i++)
        {
            float currHalf;
            if (i < queue.Count)
            {
                currHalf = queue[i].GetWidth() * 0.5f;
            }
            else
            {
                // For positions beyond current queue, use average width
                currHalf = 0.5f;
            }

            float step = prevHalf + currHalf + extraGap;
            offset += step;
            prevHalf = currHalf;
        }

        return counterPoint.position + back * offset;
    }

    /// Enqueue at the end; returns false if full.
    public bool TryEnqueue(CustomerAgent agent)
    {
        if (IsFull || agent == null) return false;
        
        queue.Add(agent);
        agent.SetTarget(counterPoint.position);
        agent.ChangeState(CustomerState.Queueing);
        ReflowQueue();
        return true;
    }

    /// Who is at the counter next? (null if no one)
    public CustomerAgent PeekHead()
    {
        if (queue.Count == 0) return null;
        return queue[0];
    }

    /// Pull the head to the counter when ready
    public void BringHeadToCounter()
    {
        if (queue.Count == 0) return;

        var head = queue[0];
        head.SetTarget(counterPoint.position);
    }

    /// Remove the head (after served), then shift the line forward
    public void DequeueHeadAndShift()
    {
        if (queue.Count == 0) return;
        queue.RemoveAt(0);
        ReflowQueue();
    }

    /// Remove a specific agent (e.g., leaves angry) and reflow
    public void Remove(CustomerAgent agent)
    {
        if (agent == null) return;
            int idx = queue.IndexOf(agent);
        if (idx < 0) return;
            queue.RemoveAt(idx);
        ReflowQueue();
    }

  /// Recompute all target positions based on sizes + queue length
    public void ReflowQueue()
    {
        if (counterPoint == null) return;
        if (queue.Count == 0) return;

        // gap scale shrinks as queue grows: size 1 => maxGap, size >= tightQueueSize => minGap
        float t = Mathf.InverseLerp(1, Mathf.Max(2, tightQueueSize), queue.Count);
        float extraGap = Mathf.Lerp(maxGap, minGap, t);

        // normalized direction away from counter
        Vector3 back = -((Vector3)lineDirection).normalized;

        // Position head first (at counter)
        float offset = 0f;

        // Head footprint matters for the next person behind
        float prevHalf = queue[0].GetWidth() * 0.5f;

        // Head goes to the exact counter point
        queue[0].SetTarget(counterPoint.position);

        // Each subsequent person: move back by prevHalf + currHalf + extraGap
        for (int i = 1; i < queue.Count; i++)
        {
            float currHalf = queue[i].GetWidth() * 0.5f;
            float step = prevHalf + currHalf + extraGap;

            offset += step; // accumulate distance from counter
            Vector3 target = counterPoint.position + back * offset;
            queue[i].SetTarget(target);

            prevHalf = currHalf;
        }
    }
}