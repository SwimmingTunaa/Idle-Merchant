using UnityEngine;

/// <summary>
/// ScriptableObject definition for gold generation passive skill.
/// Designer-friendly configuration for "X gold per second" abilities.
/// 
/// How to create:
/// 1. Right-click in Project → Create → Skills/Passive/Gold Generation
/// 2. Configure gold amount and interval in inspector
/// 3. Add to EntityDef.startingSkills or attach at runtime
/// 
/// Example uses:
/// - Promoted Trader: 1 gold/sec permanent
/// - Temporary merchant buff: 5 gold/sec for 30 seconds
/// - Guild bonus: 10 gold/sec while layer active
/// </summary>
[CreateAssetMenu(fileName = "New Gold Generation Skill", menuName = "Skills/Passive/Gold Generation")]
public class GoldGenerationSkillDef : SkillDef
{
    [Header("Gold Generation Settings")]
    [Tooltip("Gold generated per tick")]
    [Min(1)]
    public int goldPerTick = 1;

    [Tooltip("Seconds between gold generation ticks (1.0 = once per second)")]
    [Range(0.1f, 10f)]
    public float tickInterval = 1f;

    [Tooltip("Skill duration in seconds (-1 = permanent)")]
    public float duration = -1f;

    /// <summary>
    /// Create GoldGenerationEffect modifier
    /// </summary>
    public override StatModifier CreateModifier(EntityBase owner)
    {
        return new GoldGenerationEffect(owner, goldPerTick, tickInterval, duration);
    }

    /// <summary>
    /// Validation: Ensure sensible values
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();

        // Auto-calculate gold per second for description
        if (tickInterval > 0f)
        {
            float goldPerSecond = goldPerTick / tickInterval;
            
            if (string.IsNullOrEmpty(description))
            {
                if (duration <= 0f)
                {
                    description = $"Generates {goldPerSecond:F1} gold per second.";
                }
                else
                {
                    description = $"Generates {goldPerSecond:F1} gold per second for {duration} seconds.";
                }
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Display helpful info in inspector
    /// </summary>
    [ContextMenu("Calculate Gold Per Second")]
    private void CalculateGoldPerSecond()
    {
        if (tickInterval > 0f)
        {
            float goldPerSecond = goldPerTick / tickInterval;
            Debug.Log($"[{name}] Generates {goldPerSecond:F2} gold/sec");
            
            if (duration > 0f)
            {
                float totalGold = (duration / tickInterval) * goldPerTick;
                Debug.Log($"[{name}] Total gold over {duration}s: {totalGold:F0}");
            }
        }
    }
#endif
}