using System;
using UnityEngine;

[Serializable]
public class BaseStats
{
    [Header("Universal Stats")]
    public float moveSpeed;
    public float stopDistance;

    [Header("Combat Stats")]
    public float attackDamage;
    public float attackSpeed;
    public float attackRange;
    public float chaseBreakRange;
    public float scanRange;

    [Header("Porter Stats")]
    public float carryCapacity;
    public float pickupTime;
    public float depositTime;

    public float GetStat(StatType type)
    {
        return type switch
        {
            StatType.MoveSpeed => moveSpeed,
            StatType.AttackDamage => attackDamage,
            StatType.AttackSpeed => attackSpeed,
            StatType.AttackRange => attackRange,
            StatType.ChaseBreakRange => chaseBreakRange,
            StatType.ScanRange => scanRange,
            StatType.CarryCapacity => carryCapacity,
            StatType.PickupTime => pickupTime,
            StatType.DepositTime => depositTime,
            _ => 0f
        };
    }

    public void SetStat(StatType type, float value)
    {
        switch (type)
        {
            case StatType.MoveSpeed: moveSpeed = value; break;
            case StatType.AttackDamage: attackDamage = value; break;
            case StatType.AttackSpeed: attackSpeed = value; break;
            case StatType.AttackRange: attackRange = value; break;
            case StatType.ChaseBreakRange: chaseBreakRange = value; break;
            case StatType.ScanRange: scanRange = value; break;
            case StatType.CarryCapacity: carryCapacity = value; break;
            case StatType.PickupTime: pickupTime = value; break;
            case StatType.DepositTime: depositTime = value; break;
        }
    }

    public static BaseStats FromEntityDef(EntityDef def)
    {
        var stats = new BaseStats
        {
            moveSpeed = def.moveSpeed,
            stopDistance = def.stopDistance,
            attackDamage = def.attackDamage,
            attackSpeed = def.attackInterval,
            attackRange = def.attackRange,
            chaseBreakRange = def.chaseBreakRange,
            scanRange = def.scanRange
        };

        if (def is PorterDef porterDef)
        {
            stats.carryCapacity = porterDef.carryCapacity;
            stats.pickupTime = porterDef.pickupTime;
            stats.depositTime = porterDef.depositTime;
        }

        return stats;
    }

#if UNITY_EDITOR
    public void DebugPrintStats()
    {
        Debug.Log($"=== Base Stats ===");
        Debug.Log($"MoveSpeed: {moveSpeed}");
        Debug.Log($"AttackDamage: {attackDamage}");
        Debug.Log($"AttackInterval: {attackSpeed}");
        Debug.Log($"AttackRange: {attackRange}");
        Debug.Log($"ScanRange: {scanRange}");
        Debug.Log($"CarryCapacity: {carryCapacity}");
    }
#endif
}