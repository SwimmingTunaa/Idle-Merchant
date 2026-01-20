using UnityEngine;

/// <summary>
/// ScriptableObject definition for passive AOE damage skill.
/// Designer-friendly configuration for "Damage nearby enemies every X seconds" abilities.
/// 
/// How to create:
/// 1. Right-click in Project → Create → Skills/Passive/AOE Damage
/// 2. Configure damage, radius, and interval in inspector
/// 3. Add to EntityDef.startingSkills
/// 
/// Example uses:
/// - Promoted Guard: 10 damage/2sec in 3m radius
/// - Fire mage aura: 5 damage/sec in 5m radius
/// - Temporary "Thorns" buff: 20 damage/hit in melee range
/// 
/// Performance note:
/// - Uses Physics2D.OverlapCircleAll (efficient for small radius)
/// - Avoid giving this to 100+ entities simultaneously
/// - Consider increasing tick interval if many entities have this
/// </summary>
[CreateAssetMenu(fileName = "New AOE Damage Skill", menuName = "Skills/Passive/AOE Damage")]
public class AOEDamageSkillDef : SkillDef
{
    [Header("AOE Damage Settings")]
    [Tooltip("Damage dealt to each enemy per tick")]
    [Min(0.1f)]
    public float damagePerTick = 10f;

    [Tooltip("Radius of damage area")]
    [Range(0.5f, 20f)]
    public float radius = 3f;

    [Tooltip("Seconds between damage ticks (1.0 = once per second)")]
    [Range(0.1f, 10f)]
    public float tickInterval = 2f;

    [Tooltip("Skill duration in seconds (-1 = permanent)")]
    public float duration = -1f;

    [Header("Targeting")]
    [Tooltip("Which layers to damage (typically 'Mobs')")]
    public LayerMask targetLayers = ~0; // Default: all layers

    [Header("VFX")]
    [Tooltip("Particle effect spawned on each damage tick (optional)")]
    public GameObject damageTickVFX;

    [Tooltip("Persistent particle effect shown while skill is active (aura visual)")]
    public GameObject auraVFX;

    /// <summary>
    /// Create PassiveAOEDamageEffect modifier
    /// </summary>
    public override StatModifier CreateModifier(EntityBase owner)
    {
        return new PassiveAOEDamageEffect(
            owner, 
            damagePerTick, 
            radius, 
            tickInterval, 
            duration,
            targetLayers
        );
    }

    /// <summary>
    /// Validation: Ensure sensible values and auto-generate description
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();

        // Auto-generate description
        if (string.IsNullOrEmpty(description))
        {
            float dps = damagePerTick / tickInterval;
            
            if (duration <= 0f)
            {
                description = $"Deals {damagePerTick} damage to enemies within {radius}m every {tickInterval} seconds ({dps:F1} DPS).";
            }
            else
            {
                float totalTicks = Mathf.Floor(duration / tickInterval);
                float totalDamage = totalTicks * damagePerTick;
                description = $"Deals {damagePerTick} damage to enemies within {radius}m every {tickInterval} seconds for {duration}s (Total: {totalDamage:F0} damage).";
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Display helpful info in inspector
    /// </summary>
    [ContextMenu("Calculate DPS")]
    private void CalculateDPS()
    {
        if (tickInterval > 0f)
        {
            float dps = damagePerTick / tickInterval;
            Debug.Log($"[{name}] DPS per enemy: {dps:F2}");
            Debug.Log($"[{name}] Damage radius: {radius}m");
            Debug.Log($"[{name}] Tick interval: {tickInterval}s");
            
            if (duration > 0f)
            {
                float totalTicks = Mathf.Floor(duration / tickInterval);
                float totalDamage = totalTicks * damagePerTick;
                Debug.Log($"[{name}] Total damage over {duration}s: {totalDamage:F0} (per enemy)");
            }
        }
    }

    /// <summary>
    /// Visualize radius in scene view
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // This only works if the SkillDef is selected in the inspector
        // To see radius on entities, add a debug draw to PassiveAOEDamageEffect
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
        // Can't draw without position - this is just a data asset
    }
#endif
}
