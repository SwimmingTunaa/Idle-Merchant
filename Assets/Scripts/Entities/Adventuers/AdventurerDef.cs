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

    // NOTE: the old `xpThresholds` array was removed — XP per level is now an
    // exponential curve (XPForLevel) and ranks advance via Promote(), not XP.

    [Header("Leveling")]
    [Tooltip("Levels within each rank (badge shows I..N). Promote unlocks at the top level.")]
    public int levelsPerRank = 5;

    [Tooltip("XP for the first level-up; each subsequent level multiplies by XP Growth.")]
    public float baseXP = 40f;

    [Tooltip("Exponential growth of the per-level XP cost (1.22 = +22% each level).")]
    public float xpGrowth = 1.22f;

    [Tooltip("Per-level AttackDamage bonus applied automatically on each level-up (0.04 = +4%).")]
    public float levelDamageMultiplier = 0.04f;

    [Tooltip("Per-level attack-speed bonus (interval reduction) on each level-up (0.02 = 2% faster).")]
    public float levelAttackSpeedMultiplier = 0.02f;

    [Header("Rank & Promote")]
    [Tooltip("Maximum rank an adventurer can reach.")]
    public int maxRank = 5;

    [Tooltip("Gold cost to promote from rank 1; multiplied by Promote Cost Growth each rank.")]
    public int basePromoteCost = 150;

    [Tooltip("Exponential growth of the promote gold cost per rank (2.5 = ×2.5 each rank).")]
    public float promoteCostGrowth = 2.5f;

    [Tooltip("AttackDamage bonus applied on each promote / rank-up (0.20 = +20%).")]
    public float rankDamageMultiplier = 0.20f;

    [Tooltip("Attack-interval reduction (faster attacks) applied on each promote / rank-up (0.10 = 10% faster).")]
    public float rankAttackSpeedMultiplier = 0.10f;

    [Tooltip("Max HP bonus applied on each promote / rank-up (0.25 = +25%).")]
    public float rankHPMultiplier = 0.25f;

    /// <summary>XP required to go from the given global level (1-based) to the next — exponential.</summary>
    public float XPForLevel(int totalLevel) => baseXP * Mathf.Pow(xpGrowth, Mathf.Max(0, totalLevel - 1));

    /// <summary>Gold cost to promote out of the given 1-based rank — exponential.</summary>
    public int PromoteCost(int rank) => Mathf.RoundToInt(basePromoteCost * Mathf.Pow(promoteCostGrowth, Mathf.Max(0, rank - 1)));
}