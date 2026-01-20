/// <summary>
/// Interface for entities that can be identified by combat system.
/// Allows CombatBehavior to determine if an entity is a valid target.
/// </summary>
public interface IEntity
{
    /// <summary>
    /// What type of entity is this?
    /// Used by combat system to check hostility rules.
    /// </summary>
    EntityType EntityType { get;}
    
    /// <summary>
    /// Is this entity currently alive and targetable?
    /// </summary>
    bool IsAlive { get; }
}

/// <summary>
/// Entity type classification for combat targeting.
/// </summary>
public enum EntityType
{
    Mob,
    Adventurer,
    Customer,
    Porter
}
