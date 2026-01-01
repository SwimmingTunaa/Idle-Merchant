using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mediator that manages stat modifiers and processes queries.
/// Uses event-driven chain of responsibility pattern.
/// 
/// Flow:
/// 1. Query created with base stat value
/// 2. Mediator fires Queries event
/// 3. Each modifier handles the query in order
/// 4. Final modified value returned
/// </summary>
public class StatsMediator
{
    readonly LinkedList<StatModifier> modifiers = new();

    // Event fired when any modifier changes (for cache invalidation)
    public event Action OnModifiersChanged = delegate { };

    // Chain of responsibility - modifiers subscribe to this
    public event EventHandler<Query> Queries;

    /// <summary>
    /// Process a stat query through all modifiers.
    /// Modifiers transform the value in the order they were added.
    /// </summary>
    public void PerformQuery(object sender, Query query)
    {
        Queries?.Invoke(sender, query);
    }

    /// <summary>
    /// Add a modifier to the chain.
    /// Triggers cache invalidation event.
    /// </summary>
    public void AddModifier(StatModifier modifier)
    {
        modifiers.AddLast(modifier);
        modifier.MarkedForRemoval = false;
        Queries += modifier.Handle;

        modifier.OnDispose += _ =>
        {
            modifiers.Remove(modifier);
            Queries -= modifier.Handle;
            OnModifiersChanged.Invoke(); // Cache invalidation
        };

        OnModifiersChanged.Invoke(); // Cache invalidation
    }

    /// <summary>
    /// Update all modifiers (timers, passive effects).
    /// Call this each frame from owning entity.
    /// </summary>
    public void Update(float deltaTime)
    {
        // Update all modifiers
        var node = modifiers.First;
        while (node != null)
        {
            var modifier = node.Value;
            modifier.Update(deltaTime);
            node = node.Next;
        }

        // Mark and sweep: dispose expired modifiers
        node = modifiers.First;
        while (node != null)
        {
            var nextNode = node.Next;

            if (node.Value.MarkedForRemoval)
            {
                node.Value.Dispose();
            }

            node = nextNode;
        }
    }

    /// <summary>
    /// Get all active modifiers (for debugging/UI).
    /// </summary>
    public IEnumerable<StatModifier> GetModifiers()
    {
        return modifiers;
    }

    /// <summary>
    /// Remove all modifiers (for cleanup/reset).
    /// </summary>
    public void ClearAllModifiers()
    {
        var node = modifiers.First;
        while (node != null)
        {
            var nextNode = node.Next;
            node.Value.Dispose();
            node = nextNode;
        }
    }

    /// <summary>
    /// Remove modifier by ID.
    /// Returns true if modifier was found and removed.
    /// </summary>
    public bool RemoveModifier(int id)
    {
        var node = modifiers.First;
        while (node != null)
        {
            if (node.Value.ID == id)
            {
                node.Value.Dispose();
                return true;
            }
            node = node.Next;
        }
        return false;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Debug: Print all active modifiers to console.
    /// </summary>
    public void DebugPrintModifiers()
    {
        Debug.Log($"=== Active Modifiers ({modifiers.Count}) ===");
        foreach (var mod in modifiers)
        {
            Debug.Log($"  - {mod.GetType().Name} (Marked for removal: {mod.MarkedForRemoval})");
        }
    }
#endif
}