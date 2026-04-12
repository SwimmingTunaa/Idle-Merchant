using UnityEditor;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/Adventurer")]
public class AdventurerDef : EntityDef
{
    [Header("Adventurer Identity")]
    [Tooltip("Display type name (Miner, Militia, Scout, etc.)")]
    public string adventurerType = "Miner";
    
    public float baseHealth = 10f;
    public float reviveDelay = 5f;
    public float hitStaggerDuration = 0.2f;
    public float hitStaggerCooldown = 1f;

    [Header("Adventurer Def")]
    [Tooltip("Starting state when adventurer spawns")]
    public AdventurerState startingState = AdventurerState.Wander;

    public CombatConfig combatConfig;
    
    [Header("Behavior")]
    [Tooltip("Time range for wander state before going idle")]
    public Vector2 wanderTimeRange = new(5f, 8f);
    
    [Tooltip("Should adventurer return to spawn point when idle?")]
    public bool returnToSpawn = false;
    
    [Tooltip("Max distance adventurer will chase a target (0 = unlimited)")]
    public float leashRange = 0f;
        
    public float DPS => attackDamage / attackInterval;

    [Header("Rank & XP")]
    [Tooltip("XP required to advance each rank. Index 0 = Rank 1→2, index 1 = Rank 2→3, etc.")]
    public float[] xpThresholds = { 100f, 300f, 900f, 2700f };

    [Tooltip("Maximum rank an adventurer can reach.")]
    public int maxRank = 5;

    [Tooltip("Flat percentage bonus to AttackDamage applied on each rank-up (0.20 = +20% per rank).")]
    public float rankDamageMultiplier = 0.20f;

    [Tooltip("Flat percentage reduction to AttackInterval (faster attacks) applied on each rank-up (0.10 = 10% faster per rank).")]
    public float rankAttackSpeedMultiplier = 0.10f;

    [Tooltip("Flat percentage bonus to max HP applied on each rank-up (0.25 = +25% per rank).")]
    public float rankHPMultiplier = 0.25f;
}