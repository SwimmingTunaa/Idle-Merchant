/// <summary>
/// All stat types that can be queried and modified.
/// Add new stats here as you expand the game.
/// </summary>
public enum StatType
{
    // Universal stats (all entities)
    MoveSpeed,
    
    // Combat stats (Adventurers)
    AttackDamage,
    AttackInterval,
    AttackRange,
    ChaseBreakRange,
    ScanRange,
    
    // Porter stats
    CarryCapacity,
    PickupTime,
    DepositTime,
    
    // Future: Passive stats (implement when ready)
    // GoldPerSec,
    // AoERadius,
    // AoEDamage,
    // CritChance,
    // CritMultiplier
}