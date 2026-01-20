using UnityEngine;

/// <summary>
/// Base class for passive skill effects that tick over time.
/// Extends StatModifier to integrate with existing stat system.
/// 
/// Passive effects don't modify stats directly - they perform world actions (gold generation, AOE damage, etc.)
/// Use BasicStatModifier for stat changes (attack speed, move speed, etc.)
/// 
/// Duration:
/// - Positive = timed effect (buff duration)
/// - -1 or negative = permanent (passive trait)
/// 
/// Phase 2 Note: Active skills will CREATE these effects when activated.
/// Example: Cleric casts buff → Creates PassiveStatBoostEffect on allies
/// </summary>
public abstract class PassiveEffect : StatModifier
{
    protected EntityBase owner;
    protected CyclicTimer tickTimer;
    protected float tickInterval;

    /// <summary>
    /// Create passive effect with auto-ID
    /// </summary>
    protected PassiveEffect(EntityBase owner, float tickInterval, float duration) : base(duration)
    {
        this.owner = owner;
        this.tickInterval = tickInterval;
        
        if (tickInterval > 0f)
        {
            tickTimer = new CyclicTimer(tickInterval);
        }
    }

    /// <summary>
    /// Create passive effect with manual ID (for removal)
    /// </summary>
    protected PassiveEffect(EntityBase owner, float tickInterval, int id, float duration) : base(id, duration)
    {
        this.owner = owner;
        this.tickInterval = tickInterval;
        
        if (tickInterval > 0f)
        {
            tickTimer = new CyclicTimer(tickInterval);
        }
    }

    /// <summary>
    /// Update tick timer and trigger OnTick when ready.
    /// Base class handles duration expiry.
    /// </summary>
    public override void Update(float deltaTime)
    {
        base.Update(deltaTime); // Handle duration timer

        if (tickTimer != null && !MarkedForRemoval)
        {
            tickTimer.Tick(deltaTime);
            
            if (tickTimer.TryConsumeCycle())
            {
                OnTick();
            }
        }
    }

    /// <summary>
    /// Passive effects don't modify stats (they do world actions).
    /// Override this if your passive needs to modify stats (use BasicStatModifier instead).
    /// </summary>
    public override void Handle(object sender, Query query)
    {
        // Default: no stat modification
        // Override if passive needs stat changes
    }

    /// <summary>
    /// Called each tick interval. Implement passive skill behavior here.
    /// Examples: generate gold, heal owner, damage nearby enemies
    /// </summary>
    protected abstract void OnTick();

    /// <summary>
    /// Called when effect first activates (on creation).
    /// Optional override for initialization logic.
    /// </summary>
    protected virtual void OnActivate() { }

    /// <summary>
    /// Called when effect expires or is removed.
    /// Optional override for cleanup logic.
    /// </summary>
    protected virtual void OnDeactivate() { }
}