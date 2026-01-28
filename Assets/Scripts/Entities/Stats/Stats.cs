using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stat system with dirty flag caching.
/// Provides property access to modified stats (base + modifiers).
/// Only recalculates when modifiers change, not every frame.
/// 
/// Usage:
///   float speed = entity.Stats.MoveSpeed; // Fast cache read
///   entity.Stats.Mediator.AddModifier(buff); // Auto-invalidates cache
/// </summary>

[Serializable]
public class Stats
{
    readonly StatsMediator mediator;
    public BaseStats BaseStats { get; private set; }

    // Cache for computed stat values
    private Dictionary<StatType, float> cache = new();
    private bool dirty = true;

    public StatsMediator Mediator => mediator;

    // ===== STAT PROPERTIES (Add more as needed) =====

    public float MoveSpeed
    {
        get
        {
            if (dirty) RefreshCache();
            return cache.TryGetValue(StatType.MoveSpeed, out float value) ? value : 0f;
        }
    }

    public float AttackDamage
    {
        get
        {
            if (dirty) RefreshCache();
            return cache.TryGetValue(StatType.AttackDamage, out float value) ? value : 0f;
        }
    }

    public float AttackInterval
    {
        get
        {
            if (dirty) RefreshCache();
            return cache.TryGetValue(StatType.AttackSpeed, out float value) ? value : 0f;
        }
    }

    public float AttackRange
    {
        get
        {
            if (dirty) RefreshCache();
            return cache.TryGetValue(StatType.AttackRange, out float value) ? value : 0f;
        }
    }

    public float ChaseBreakRange
    {
        get
        {
            if (dirty) RefreshCache();
            return cache.TryGetValue(StatType.ChaseBreakRange, out float value) ? value : 0f;
        }
    }

    public float ScanRange
    {
        get
        {
            if (dirty) RefreshCache();
            return cache.TryGetValue(StatType.ScanRange, out float value) ? value : 0f;
        }
    }

    public float CarryCapacity
    {
        get
        {
            if (dirty) RefreshCache();
            return cache.TryGetValue(StatType.CarryCapacity, out float value) ? value : 0f;
        }
    }

    public float PickupTime
    {
        get
        {
            if (dirty) RefreshCache();
            return cache.TryGetValue(StatType.PickupTime, out float value) ? value : 0f;
        }
    }

    public float DepositTime
    {
        get
        {
            if (dirty) RefreshCache();
            return cache.TryGetValue(StatType.DepositTime, out float value) ? value : 0f;
        }
    }

    // ===== INITIALIZATION =====

    public Stats(StatsMediator mediator, BaseStats baseStats)
    {
        this.mediator = mediator;
        this.BaseStats = baseStats;

        // Initialize cache with default values to prevent NullRef
        cache[StatType.MoveSpeed] = 0f;
        cache[StatType.AttackDamage] = 0f;
        cache[StatType.AttackSpeed] = 0f;
        cache[StatType.AttackRange] = 0f;
        cache[StatType.ChaseBreakRange] = 0f;
        cache[StatType.ScanRange] = 0f;
        cache[StatType.CarryCapacity] = 0f;
        cache[StatType.PickupTime] = 0f;
        cache[StatType.DepositTime] = 0f;

        // Hook cache invalidation to modifier changes
        mediator.OnModifiersChanged += MarkDirty;
        
        // Force initial cache refresh
        RefreshCache();
    }

    // ===== CACHE MANAGEMENT =====

    /// <summary>
    /// Mark cache as dirty (stale).
    /// Next stat access will trigger recalculation.
    /// Call this when base stats change (e.g., level-up).
    /// </summary>
    public void MarkDirty()
    {
        dirty = true;
    }

    /// <summary>
    /// Recalculate all stats through modifier chain.
    /// Only called when cache is dirty (dirty flag optimization).
    /// </summary>
    private void RefreshCache()
    {
        cache[StatType.MoveSpeed] = ComputeStat(StatType.MoveSpeed);
        cache[StatType.AttackDamage] = ComputeStat(StatType.AttackDamage);
        cache[StatType.AttackSpeed] = ComputeStat(StatType.AttackSpeed);
        cache[StatType.AttackRange] = ComputeStat(StatType.AttackRange);
        cache[StatType.ChaseBreakRange] = ComputeStat(StatType.ChaseBreakRange);
        cache[StatType.ScanRange] = ComputeStat(StatType.ScanRange);
        cache[StatType.CarryCapacity] = ComputeStat(StatType.CarryCapacity);
        cache[StatType.PickupTime] = ComputeStat(StatType.PickupTime);
        cache[StatType.DepositTime] = ComputeStat(StatType.DepositTime);

        dirty = false;
    }

    /// <summary>
    /// Compute final value for a stat (base + all modifiers).
    /// Creates query, runs through mediator, returns modified value.
    /// </summary>
    private float ComputeStat(StatType type)
    {
        float baseValue = BaseStats.GetStat(type);
        var query = new Query(type, baseValue);
        mediator.PerformQuery(this, query);
        return query.Value;
    }

    /// <summary>
    /// Generic stat getter (use for dynamic/data-driven access).
    /// Prefer properties (MoveSpeed, AttackDamage) for performance.
    /// </summary>
    public float GetStat(StatType type)
    {
        if (dirty) RefreshCache();
        return cache.TryGetValue(type, out float value) ? value : 0f;
    }

    // ===== UTILITY =====

    /// <summary>
    /// Update mediator (timers, passive effects).
    /// Call from entity's Update() or staggered tick.
    /// </summary>
    public void Update(float deltaTime)
    {
        mediator.Update(deltaTime);
    }

    /// <summary>
    /// Cleanup on entity despawn.
    /// </summary>
    public void Cleanup()
    {
        mediator.ClearAllModifiers();
    }

#if UNITY_EDITOR
    /// <summary>
    /// Debug: Print all stats (base vs modified).
    /// </summary>
    public void DebugPrintStats()
    {
        Debug.Log($"=== Stats (Base → Modified) ===");
        Debug.Log($"MoveSpeed: {BaseStats.moveSpeed} → {MoveSpeed}");
        Debug.Log($"AttackDamage: {BaseStats.attackDamage} → {AttackDamage}");
        Debug.Log($"AttackInterval: {BaseStats.attackSpeed} → {AttackInterval}");
        Debug.Log($"AttackRange: {BaseStats.attackRange} → {AttackRange}");
        Debug.Log($"ScanRange: {BaseStats.scanRange} → {ScanRange}");
        Debug.Log($"CarryCapacity: {BaseStats.carryCapacity} → {CarryCapacity}");
        
        mediator.DebugPrintModifiers();
    }
#endif
}