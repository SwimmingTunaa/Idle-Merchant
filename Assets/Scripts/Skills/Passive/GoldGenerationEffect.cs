using UnityEngine;

/// <summary>
/// Generates gold at fixed intervals.
/// Example: Promoted Trader generates 1 gold/sec
/// </summary>
public class GoldGenerationEffect : PassiveEffect
{
    private readonly int goldPerTick;

    public GoldGenerationEffect(EntityBase owner, int goldPerTick, float tickInterval, float duration = -1f) 
        : base(owner, tickInterval, duration)
    {
        this.goldPerTick = goldPerTick;
    }

    protected override void OnTick()
    {
        if (owner == null || owner.gameObject == null)
        {
            MarkedForRemoval = true;
            return;
        }

        GameSignals.RaiseGoldEarned(goldPerTick);

#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            Debug.Log($"[GoldGenerationEffect] {owner.name} generated {goldPerTick} gold");
        }
#endif
    }
}