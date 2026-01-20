using UnityEngine;
using System;

/// <summary>
/// Passive effect that modifies owner's stats.
/// This is a convenience wrapper around BasicStatModifier for passive skills.
/// 
/// Use this for:
/// - Permanent stat boosts ("+10% attack speed" trait)
/// - Temporary buffs ("+50% damage for 10 seconds")
/// - Conditional stat changes ("Double speed while in combat")
/// 
/// Usage:
///   // Permanent +20% attack speed
///   var boost = new PassiveStatBoostEffect(
///       owner, 
///       StatType.AttackInterval, 
///       v => v * 0.8f, 
///       duration: -1f
///   );
///   owner.Stats.Mediator.AddModifier(boost);
/// 
/// Note: This doesn't need ticking - stats are queried on-demand.
/// </summary>
public class PassiveStatBoostEffect : PassiveEffect
{
    private readonly StatType statType;
    private readonly Func<float, float> operation;

    /// <summary>
    /// Create stat boost effect
    /// </summary>
    /// <param name="owner">Entity receiving boost</param>
    /// <param name="statType">Which stat to modify</param>
    /// <param name="operation">How to modify it (e.g., v => v * 1.2f for +20%)</param>
    /// <param name="duration">Boost duration (-1 = permanent)</param>
    public PassiveStatBoostEffect(
        EntityBase owner, 
        StatType statType, 
        Func<float, float> operation,
        float duration = -1f) 
        : base(owner, tickInterval: 0f, duration) // No ticking needed
    {
        this.statType = statType;
        this.operation = operation;
    }

    /// <summary>
    /// Modify stat when queried.
    /// This is called by the stats system, not on a timer.
    /// </summary>
    public override void Handle(object sender, Query query)
    {
        if (query.StatType == statType)
        {
            query.Value = operation(query.Value);
        }
    }

    /// <summary>
    /// No tick behavior needed - stats are queried on-demand
    /// </summary>
    protected override void OnTick()
    {
        // Not used - stat boosts don't tick
    }
}