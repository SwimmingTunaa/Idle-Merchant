using UnityEngine;

/// <summary>
/// Passive effect that restores health over time.
/// Works on any entity with health (adventurers, mobs, etc.)
/// 
/// Usage:
///   var regenSkill = new RegenerationEffect(owner, hpPerTick: 5f, interval: 1f, duration: 10f);
///   owner.Stats.Mediator.AddModifier(regenSkill);
/// 
/// Note: Your entities don't have a standardized health system yet.
/// This is a placeholder implementation - adapt to your health system when ready.
/// </summary>
public class RegenerationEffect : PassiveEffect
{
    private readonly float hpPerTick;

    /// <summary>
    /// Create regeneration effect
    /// </summary>
    /// <param name="owner">Entity to heal</param>
    /// <param name="hpPerTick">Health restored each tick</param>
    /// <param name="tickInterval">Seconds between ticks</param>
    /// <param name="duration">Effect duration (-1 = permanent)</param>
    public RegenerationEffect(EntityBase owner, float hpPerTick, float tickInterval, float duration = -1f) 
        : base(owner, tickInterval, duration)
    {
        this.hpPerTick = hpPerTick;
    }

    /// <summary>
    /// Heal owner each tick
    /// </summary>
    protected override void OnTick()
    {
        if (owner == null || owner.gameObject == null)
        {
            MarkedForRemoval = true;
            return;
        }

        // TODO: Implement when you add a standardized health system
        // For now, this is a placeholder showing the pattern
        
        // Example future implementation:
        // var healthComponent = owner.GetComponent<IHealable>();
        // healthComponent?.Heal(hpPerTick);
        
        // Or if health is in stats:
        // owner.CurrentHealth = Mathf.Min(owner.MaxHealth, owner.CurrentHealth + hpPerTick);

#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            Debug.Log($"[RegenerationEffect] {owner.name} would heal {hpPerTick} HP (not implemented yet)");
        }
#endif
    }
}