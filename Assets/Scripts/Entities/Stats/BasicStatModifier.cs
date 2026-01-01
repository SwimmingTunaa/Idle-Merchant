using System;
using UnityEngine;

/// <summary>
/// Basic stat modifier that applies an operation to a specific stat type.
/// Most common use case: buffs/debuffs that add or multiply stats.
/// 
/// Examples:
/// - Speed buff: new BasicStatModifier(StatType.MoveSpeed, 30f, v => v * 1.2f)
/// - Damage boost: new BasicStatModifier(StatType.AttackDamage, -1f, v => v + 5f)
/// - Stun: new BasicStatModifier(StatType.MoveSpeed, STUN_ID, v => 0f)
/// </summary>
public class BasicStatModifier : StatModifier
{
    readonly StatType type;
    readonly Func<float, float> operation;

    /// <summary>
    /// Create timed modifier with automatic ID
    /// </summary>
    public BasicStatModifier(StatType type, float duration, Func<float, float> operation) : base(duration)
    {
        this.type = type;
        this.operation = operation;
    }

    /// <summary>
    /// Create modifier with manual ID (for removal by ID)
    /// Duration: positive = timed, -1 = permanent
    /// </summary>
    public BasicStatModifier(StatType type, int id, float duration, Func<float, float> operation) : base(id, duration)
    {
        this.type = type;
        this.operation = operation;
    }

    public override void Handle(object sender, Query query)
    {
        if (query.StatType == type)
        {
            query.Value = operation(query.Value);
        }
    }
}

/// <summary>
/// Base class for all stat modifiers.
/// Handles timing, disposal, and integration with the mediator pattern.
/// 
/// Duration:
/// - Positive value = timed modifier (auto-expires)
/// - -1 or negative = permanent modifier
/// </summary>
public abstract class StatModifier : IDisposable
{
    public readonly Sprite icon; // Optional: for UI display
    public readonly int ID; // For manual removal (if specified)
    public bool MarkedForRemoval { get; set; }

    public event Action<StatModifier> OnDispose = delegate { };

    private CountdownTimer timer; // Not readonly - initialized in InitTimer()
    static int nextAutoID = 0; // Auto-increment ID for modifiers without manual ID

    /// <summary>
    /// Create modifier with automatic ID
    /// </summary>
    protected StatModifier(float duration)
    {
        ID = nextAutoID++;
        InitTimer(duration);
    }

    /// <summary>
    /// Create modifier with manual ID (for removal by ID)
    /// </summary>
    protected StatModifier(int id, float duration)
    {
        ID = id;
        InitTimer(duration);
    }

    private void InitTimer(float duration)
    {
        if (duration <= 0) return; // Permanent modifier

        timer = new CountdownTimer(duration);
        timer.Start();
    }

    /// <summary>
    /// Called each frame to update timer (if timed).
    /// Override for custom update logic (e.g., passive effects).
    /// </summary>
    public virtual void Update(float deltaTime)
    {
        if (timer != null)
        {
            timer.Tick(deltaTime);
            
            if (timer.IsFinished)
            {
                MarkedForRemoval = true;
            }
        }
    }

    /// <summary>
    /// Handle a stat query. Modify query.Value if this modifier applies.
    /// </summary>
    public abstract void Handle(object sender, Query query);

    public void Dispose()
    {
        OnDispose.Invoke(this);
    }
}