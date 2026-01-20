using UnityEngine;

/// <summary>
/// Base ScriptableObject for all skill definitions.
/// Use this to create data-driven, designer-friendly skills.
/// 
/// Phase 1: Passive skills only (gold generation, regen, stat boosts)
/// Phase 2: Active skills (fireball, cleric buff, etc.)
/// 
/// Pattern: Factory Method
/// - SkillDef holds configuration data
/// - CreateModifier() creates runtime StatModifier instance
/// - Derived classes implement specific skill creation logic
/// 
/// Attachment:
/// - Add to EntityDef.startingSkills[] for automatic application
/// - Or manually: entity.Stats.Mediator.AddModifier(skillDef.CreateModifier(entity))
/// 
/// </summary>
public abstract class SkillDef : ScriptableObject
{
    [Header("Skill Identity")]
    [Tooltip("Display name shown in UI")]
    public string displayName = "Unnamed Skill";
    
    [Tooltip("Skill description for tooltips")]
    [TextArea(2, 4)]
    public string description = "";
    
    [Tooltip("Skill icon for UI")]
    public Sprite icon;

    [Header("Skill Type")]
    [Tooltip("Passive = always active, Active = requires activation + cooldown")]
    public SkillType skillType = SkillType.Passive;

    [Header("VFX (Optional)")]
    [Tooltip("Particle effect spawned when skill activates")]
    public GameObject activationVFX;
    
    [Tooltip("Particle effect shown while skill is active (auras, etc.)")]
    public GameObject persistentVFX;

    /// <summary>
    /// Factory method: Create runtime modifier for this skill.
    /// Override in derived classes to create specific skill types.
    /// </summary>
    /// <param name="owner">Entity that will own this skill</param>
    /// <returns>StatModifier (or derived PassiveEffect, ActiveSkill, etc.)</returns>
    public abstract StatModifier CreateModifier(EntityBase owner);

    /// <summary>
    /// Validation in editor.
    /// Override to add skill-specific validation.
    /// </summary>
    protected virtual void OnValidate()
    {
        if (string.IsNullOrEmpty(displayName))
        {
            displayName = name; // Use asset name as fallback
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor context menu: Apply this skill to selected entity in scene.
    /// Useful for testing skills in play mode.
    /// </summary>
    [ContextMenu("Apply to Selected Entity")]
    private void ApplyToSelectedEntity()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[SkillDef] Only works in play mode");
            return;
        }

        var selected = UnityEditor.Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("[SkillDef] No GameObject selected");
            return;
        }

        var entity = selected.GetComponent<EntityBase>();
        if (entity == null)
        {
            Debug.LogWarning($"[SkillDef] {selected.name} has no EntityBase component");
            return;
        }

        var modifier = CreateModifier(entity);
        entity.Stats.Mediator.AddModifier(modifier);
        Debug.Log($"[SkillDef] Applied {displayName} to {entity.name}");
    }
#endif
}

/// <summary>
/// Skill type enum.
/// Determines how skill behaves and which systems handle it.
/// </summary>
public enum SkillType
{
    Passive,    // Always active, no activation needed (gold generation, regen, auras)
    Active,     // Requires activation, has cooldown (fireball, buffs) - Phase 2
    Channeled,  // Hold to maintain effect (future expansion)
    OnHit,      // Triggers when entity hits target (future expansion)
    OnKill,     // Triggers when entity kills target (future expansion)
}