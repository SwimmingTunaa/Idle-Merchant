using UnityEngine;

/// <summary>
/// ScriptableObject definition for adventurer units.
/// Adventurers are hired to specific layers and auto-attack mobs to generate idle loot income.
/// Each adventurer type (Miner, Militia, Scout, etc.) has unique stats and is layer-locked.
/// </summary>
[CreateAssetMenu(menuName = "Data/Adventurer")]
public class AdventurerDef : EntityDef
{
    [Header("Adventurer Identity")]
    [Tooltip("Display type name (Miner, Militia, Scout, etc.)")]
    public string adventurerType = "Miner";
    
    [Header("Adventurer Def")]
    [Tooltip("Starting state when adventurer spawns")]
    public AdventurerState startingState = AdventurerState.Wander;
    
    [Header("Combat Stats")]
    [Tooltip("Damage dealt per attack hit")]
    public float attackDamage = 2f;
    
    [Tooltip("Time between attack hits (lower = faster attacks)")]
    public float attackInterval = 1f;

    [Tooltip("Delay before damage is applied after attack animation starts (seconds)")]
    [Range(0f, 2f)]
    public float attackHitDelay = 0.4f; // 40% through a ~1s animation

    [Tooltip("How close adventurer must be to attack target")]
    public float attackRange = 1.5f;

    [Tooltip("Distance at which to stop chasing and return to idle (should be > attackRange)")]
    public float chaseBreakRange = 2.5f;
    
    [Tooltip("How far adventurer scans for new targets")]
    public float scanRange = 10f;
    
    [Header("Behavior")]
    [Tooltip("Time range for wander state before going idle")]
    public Vector2 wanderTimeRange = new Vector2(5f, 8f);
    
    [Tooltip("Should adventurer return to spawn point when idle?")]
    public bool returnToSpawn = false;
    
    [Tooltip("Max distance adventurer will chase a target (0 = unlimited)")]
    public float leashRange = 0f;
    
    [Header("Visual")]
    [Tooltip("Color tint when adventurer is idle")]
    public Color idleColor = Color.white;
    
    [Tooltip("Color tint when adventurer is wandering")]
    public Color wanderColor = Color.cyan;
    
    [Tooltip("Color tint when adventurer is seeking target")]
    public Color seekColor = Color.yellow;
    
    [Tooltip("Color tint when adventurer is attacking")]
    public Color attackColor = new Color(1f, 0.3f, 0.3f, 1f);
    
    /// <summary>
    /// Calculated DPS (Damage Per Second) for balancing purposes
    /// </summary>
    public float DPS => attackDamage / attackInterval;
}