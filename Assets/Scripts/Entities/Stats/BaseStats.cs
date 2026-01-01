using System;
using UnityEngine;

/// <summary>
/// Runtime container for base (unmodified) stat values.
/// Created from EntityDef at spawn, then can be mutated (e.g., level-up).
/// Each entity instance gets its own copy.
/// </summary>
[Serializable]
public class BaseStats
{
    [Header("Universal Stats")]
    public float moveSpeed;
    public float stopDistance;

    [Header("Combat Stats (Adventurers)")]
    public float attackDamage;
    public float attackInterval;
    public float attackRange;
    public float chaseBreakRange;
    public float scanRange;

    [Header("Porter Stats")]
    public float carryCapacity;
    public float pickupTime;
    public float depositTime;

    // Future stats added here as needed

    /// <summary>
    /// Get base value for a specific stat type.
    /// Used by Stats system to build queries.
    /// </summary>
    public float GetStat(StatType type)
    {
        return type switch
        {
            StatType.MoveSpeed => moveSpeed,
            StatType.AttackDamage => attackDamage,
            StatType.AttackInterval => attackInterval,
            StatType.AttackRange => attackRange,
            StatType.ChaseBreakRange => chaseBreakRange,
            StatType.ScanRange => scanRange,
            StatType.CarryCapacity => carryCapacity,
            StatType.PickupTime => pickupTime,
            StatType.DepositTime => depositTime,
            _ => 0f
        };
    }

    /// <summary>
    /// Set base value for a specific stat type.
    /// Use for level-up or permanent stat changes.
    /// Don't forget to call Stats.MarkDirty() after!
    /// </summary>
    public void SetStat(StatType type, float value)
    {
        switch (type)
        {
            case StatType.MoveSpeed: moveSpeed = value; break;
            case StatType.AttackDamage: attackDamage = value; break;
            case StatType.AttackInterval: attackInterval = value; break;
            case StatType.AttackRange: attackRange = value; break;
            case StatType.ChaseBreakRange: chaseBreakRange = value; break;
            case StatType.ScanRange: scanRange = value; break;
            case StatType.CarryCapacity: carryCapacity = value; break;
            case StatType.PickupTime: pickupTime = value; break;
            case StatType.DepositTime: depositTime = value; break;
        }
    }

    /// <summary>
    /// Factory: Create BaseStats from EntityDef.
    /// Handles inheritance (AdventurerDef, PorterDef, etc).
    /// </summary>
    public static BaseStats FromEntityDef(EntityDef def)
    {
        var stats = new BaseStats
        {
            // Universal stats (all entities)
            moveSpeed = def.moveSpeed,
            stopDistance = def.stopDistance
        };

        // Type-specific stats
        if (def is AdventurerDef advDef)
        {
            stats.attackDamage = advDef.attackDamage;
            stats.attackInterval = advDef.attackInterval;
            stats.attackRange = advDef.attackRange;
            stats.chaseBreakRange = advDef.chaseBreakRange;
            stats.scanRange = advDef.scanRange;
        }
        else if (def is PorterDef porterDef)
        {
            stats.carryCapacity = porterDef.carryCapacity;
            stats.pickupTime = porterDef.pickupTime;
            stats.depositTime = porterDef.depositTime;
            stats.scanRange = porterDef.scanRange;
        }

        return stats;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Debug: Print all base stats.
    /// </summary>
    public void DebugPrintStats()
    {
        Debug.Log($"=== Base Stats ===");
        Debug.Log($"MoveSpeed: {moveSpeed}");
        Debug.Log($"AttackDamage: {attackDamage}");
        Debug.Log($"AttackInterval: {attackInterval}");
        Debug.Log($"AttackRange: {attackRange}");
        Debug.Log($"ScanRange: {scanRange}");
        Debug.Log($"CarryCapacity: {carryCapacity}");
    }
#endif
}