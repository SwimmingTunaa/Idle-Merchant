using UnityEngine;

/// <summary>
/// ScriptableObject definition for health regeneration passive skill.
/// Designer-friendly configuration for "Heal X HP per second" abilities.
/// 
/// How to create:
/// 1. Right-click in Project → Create → Skills/Passive/Regeneration
/// 2. Configure heal amount and interval in inspector
/// 3. Add to EntityDef.startingSkills
/// 
/// Example uses:
/// - Cleric passive: 2 HP/sec regeneration
/// - Potion effect: 50 HP over 10 seconds
/// - Guild upgrade: 5 HP/sec while in shop
/// 
/// Note: Requires health system implementation.
/// Currently a placeholder - update RegenerationEffect when you add health.
/// </summary>
[CreateAssetMenu(fileName = "New Regeneration Skill", menuName = "Skills/Passive/Regeneration")]
public class RegenerationSkillDef : SkillDef
{
    [Header("Regeneration Settings")]
    [Tooltip("Health restored per tick")]
    [Min(0.1f)]
    public float hpPerTick = 5f;

    [Tooltip("Seconds between heal ticks (1.0 = once per second)")]
    [Range(0.1f, 10f)]
    public float tickInterval = 1f;

    [Tooltip("Skill duration in seconds (-1 = permanent)")]
    public float duration = -1f;

    [Header("VFX")]
    [Tooltip("Particle effect spawned on each heal tick (optional)")]
    public GameObject healTickVFX;

    /// <summary>
    /// Create RegenerationEffect modifier
    /// </summary>
    public override StatModifier CreateModifier(EntityBase owner)
    {
        return new RegenerationEffect(owner, hpPerTick, tickInterval, duration);
    }

    /// <summary>
    /// Validation: Ensure sensible values and auto-generate description
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();

        // Auto-calculate HP per second for description
        if (tickInterval > 0f)
        {
            float hpPerSecond = hpPerTick / tickInterval;
            
            if (string.IsNullOrEmpty(description))
            {
                if (duration <= 0f)
                {
                    description = $"Restores {hpPerSecond:F1} health per second.";
                }
                else
                {
                    description = $"Restores {hpPerSecond:F1} health per second for {duration} seconds.";
                }
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Display helpful info in inspector
    /// </summary>
    [ContextMenu("Calculate Total Healing")]
    private void CalculateTotalHealing()
    {
        if (tickInterval > 0f)
        {
            float hpPerSecond = hpPerTick / tickInterval;
            Debug.Log($"[{name}] Heals {hpPerSecond:F2} HP/sec");
            
            if (duration > 0f)
            {
                float totalHealing = (duration / tickInterval) * hpPerTick;
                Debug.Log($"[{name}] Total healing over {duration}s: {totalHealing:F0} HP");
            }
        }
    }
#endif
}