using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Passive effect that deals periodic damage to nearby enemies.
/// Uses existing spatial grid for efficient AOE queries.
/// 
/// Example use cases:
/// - Adventurer with "Aura of Flames" passive
/// - Promoted Guard with area damage
/// - Thorns effect (damage attackers)
/// 
/// Usage:
///   var aoeSkill = new PassiveAOEDamageEffect(owner, damage: 5f, radius: 3f, interval: 2f);
///   owner.Stats.Mediator.AddModifier(aoeSkill);
/// 
/// Phase 2 Note: Active skills can create this effect
/// Example: Mage casts Fire Storm → Creates PassiveAOEDamageEffect for 10 seconds
/// </summary>
public class PassiveAOEDamageEffect : PassiveEffect
{
    private readonly float damagePerTick;
    private readonly float radius;
    private readonly LayerMask targetLayerMask;

    /// <summary>
    /// Create AOE damage effect
    /// </summary>
    /// <param name="owner">Entity dealing damage</param>
    /// <param name="damagePerTick">Damage dealt each tick</param>
    /// <param name="radius">AOE radius</param>
    /// <param name="tickInterval">Seconds between damage ticks</param>
    /// <param name="duration">Effect duration (-1 = permanent)</param>
    /// <param name="targetLayerMask">Which layers to damage (default: Mobs)</param>
    public PassiveAOEDamageEffect(
        EntityBase owner, 
        float damagePerTick, 
        float radius, 
        float tickInterval, 
        float duration = -1f,
        LayerMask? targetLayerMask = null) 
        : base(owner, tickInterval, duration)
    {
        this.damagePerTick = damagePerTick;
        this.radius = radius;
        this.targetLayerMask = targetLayerMask ?? LayerMask.GetMask("Mobs");
    }

    /// <summary>
    /// Deal AOE damage each tick
    /// </summary>
    protected override void OnTick()
    {
        if (owner == null || owner.gameObject == null)
        {
            MarkedForRemoval = true;
            return;
        }

        // Use Unity's built-in 2D overlap for AOE damage
        // This is simpler than spatial grid for damage (don't need per-layer tracking)
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            owner.transform.position, 
            radius, 
            targetLayerMask
        );

        int damageCount = 0;
        foreach (var hit in hits)
        {
            // Skip self
            if (hit.gameObject == owner.gameObject) continue;

            // Try to damage
            var damageable = hit.GetComponent<IDamageable>();
            if (damageable != null)
            {
                float appliedDamage = damageable.OnDamage(damagePerTick);
                if (appliedDamage > 0f)
                {
                    damageCount++;
                }
            }
        }

#if UNITY_EDITOR
        if (Application.isPlaying && damageCount > 0)
        {
            Debug.Log($"[PassiveAOEDamageEffect] {owner.name} damaged {damageCount} enemies for {damagePerTick} each");
        }
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// Debug visualization in editor
    /// </summary>
    public void DrawGizmo()
    {
        if (owner == null) return;
        
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(owner.transform.position, radius);
    }
#endif
}