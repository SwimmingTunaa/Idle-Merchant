using UnityEditor;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/Adventurer")]
public class AdventurerDef : EntityDef
{
    [Header("Class")]
    [Tooltip("Class line; tier = the inherited `rank` field (1 = Wood … 5 = Gold). displayName is the class name, description its one-line role.")]
    public ClassLine classLine = ClassLine.Novice;

    [Tooltip("Job icon shown on the Roster badge, hire card and path picker.")]
    public Sprite classIcon;

    [Tooltip("AttackDamage multiplier on the def's attackDamage (the CLASSES_DESIGN power-curve table).")]
    public float damageMultiplier = 1f;

    [Tooltip("Max HP multiplier on baseHealth.")]
    public float hpMultiplier = 1f;

    public float MaxHealth => baseHealth * hpMultiplier;

    [Header("Adventurer Identity")]
    [Tooltip("Display type name (Miner, Militia, Scout, etc.)")]
    public string adventurerType = "Miner";
    
    public float baseHealth = 100f;
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
    // Damage/HP per rank come from the tier def's multipliers (promotion swaps defs); only attack speed still steps per rank.
    [Tooltip("Maximum rank an adventurer can reach.")]
    public int maxRank = 5;

    [Tooltip("Gold cost to promote from rank 1; multiplied by Promote Cost Growth each rank.")]
    public int basePromoteCost = 1500;

    [Tooltip("Exponential growth of the promote gold cost per rank (2.5 = ×2.5 each rank).")]
    public float promoteCostGrowth = 2.5f;

    [Tooltip("Attack-interval reduction (faster attacks) applied on each promote / rank-up (0.10 = 10% faster).")]
    public float rankAttackSpeedMultiplier = 0.10f;

    // Class signature mechanics (CLASSES_DESIGN phase 2). Data-driven: a mechanic is on iff its number is > 0,
    // so the line is just whichever fields its tier defs fill in.
    [Header("Signature: Warrior cleave")]
    [Tooltip("Each hit also deals this fraction of the hit to every other mob within Cleave Radius of the primary target (never chains). 0 = off.")]
    public float cleaveFraction;
    public float cleaveRadius = 1.5f;

    [Header("Signature: Guardian hold the line")]
    [Tooltip("Mobs within this radius that are attacking another adventurer switch to this unit. 0 = off.")]
    public float tauntRadius;
    [Tooltip("Seconds between taunt re-checks.")]
    public float tauntInterval = 1f;
    [Tooltip("Fraction of incoming damage ignored (0.15 = takes 15% less).")]
    [Range(0f, 0.9f)] public float damageReduction;

    [Header("Signature: Rogue crits + pursuit")]
    [Tooltip("Chance per hit to crit. 0 = off.")]
    [Range(0f, 1f)] public float critChance;
    public float critMultiplier = 2f;
    [Tooltip("After a kill, dash to the nearest live mob within this range; the next hit is a guaranteed crit. 0 = off.")]
    public float pursuitRange;
    public float pursuitDashTime = 0.12f;

    [Header("Signature: Mage arcane weight")]
    [Tooltip("Bonus damage per hit as a fraction of the target's max HP (0.02 = 2%). 0 = off.")]
    public float maxHpDamage;

    /// <summary>XP required to go from the given global level (1-based) to the next — exponential.</summary>
    public float XPForLevel(int totalLevel) => baseXP * Mathf.Pow(xpGrowth, Mathf.Max(0, totalLevel - 1));

    /// <summary>Gold cost to promote out of the given 1-based rank — exponential.</summary>
    public int PromoteCost(int rank) => Mathf.RoundToInt(basePromoteCost * Mathf.Pow(promoteCostGrowth, Mathf.Max(0, rank - 1)));
}