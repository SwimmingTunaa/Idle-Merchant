using UnityEngine;

[CreateAssetMenu(menuName = "Data/Adventurer")]
public class AdventurerDef : EntityDef
{
    [Header("Adventurer Identity")]
    [Tooltip("Display type name (Miner, Militia, Scout, etc.)")]
    public string adventurerType = "Miner";
    
    public float baseHealth = 10f;

    [Header("Adventurer Def")]
    [Tooltip("Starting state when adventurer spawns")]
    public AdventurerState startingState = AdventurerState.Wander;
    
    [Header("Behavior")]
    [Tooltip("Time range for wander state before going idle")]
    public Vector2 wanderTimeRange = new(5f, 8f);
    
    [Tooltip("Should adventurer return to spawn point when idle?")]
    public bool returnToSpawn = false;
    
    [Tooltip("Max distance adventurer will chase a target (0 = unlimited)")]
    public float leashRange = 0f;
        
    public float DPS => attackDamage / attackInterval;
}