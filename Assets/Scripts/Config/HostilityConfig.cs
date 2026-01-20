using System;

/// <summary>
/// Defines what entity types can be targeted for combat.
/// Uses flags so entities can be hostile to multiple types.
/// </summary>
[Flags]
public enum HostilityTargets
{
    None = 0,
    Mobs = 1 << 0,          // Can attack other mobs
    Adventurers = 1 << 1,   // Can attack adventurers
    Customers = 1 << 2,     // Can attack customers
    Porters = 1 << 3,       // Can attack porters
    All = ~0                // Can attack everything
}

/// <summary>
/// Defines combat behavior type.
/// </summary>
public enum CombatBehaviorType
{
    Passive,        // Never attacks, even when hit
    Defensive,      // Only attacks if damaged first
    Aggressive,     // Actively seeks targets within scan range
    Territorial     // Attacks if targets get too close (uses different scan range)
}

/// <summary>
/// Configuration for combat behavior.
/// Stored in EntityDef and applied to CombatBehavior component.
/// </summary>
[Serializable]
public struct CombatConfig
{
    public bool canAttack;
    public CombatBehaviorType behaviorType;
    public HostilityTargets hostileTo;
    
    // Territorial-specific
    public float territorialRadius;
    
    public static CombatConfig Passive => new CombatConfig
    {
        canAttack = false,
        behaviorType = CombatBehaviorType.Passive,
        hostileTo = HostilityTargets.None,
        territorialRadius = 0f
    };
    
    public static CombatConfig AggressiveToAdventurers => new CombatConfig
    {
        canAttack = true,
        behaviorType = CombatBehaviorType.Aggressive,
        hostileTo = HostilityTargets.Adventurers,
        territorialRadius = 0f
    };
}
