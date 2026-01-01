using UnityEngine;

/// <summary>
/// ScriptableObject definition for porter units.
/// Porters collect loot from dungeon layers and transport it to the shop.
/// Each layer has its own porter with upgradeable transport (Ladder → Elevator → Teleporter).
/// </summary>
[CreateAssetMenu(menuName = "Data/Porter")]
public class PorterDef : EntityDef
{
    [Header("Porter Identity")]
    
    [Tooltip("Display name for this porter type")]
    public string porterType = "Porter";
    

    
    [Header("Porter Stats")]
    [Tooltip("How many loot items this porter can carry at once")]
    public int carryCapacity = 5;
    
    [Tooltip("How far porter scans for loot to collect")]
    public float scanRange = 10f;
    
    [Tooltip("Time it takes to pick up a loot stack (animation time)")]
    public float pickupTime = 0.5f;
    
    [Tooltip("Time it takes to deposit loot at shop")]
    public float depositTime = 0.5f;
    
    [Header("Behavior")]
    [Tooltip("Starting state when porter spawns")]
    public PorterState startingState = PorterState.Idle;
    
    [Tooltip("Time range for wander state before going idle")]
    public Vector2 wanderTimeRange = new Vector2(3f, 6f);
    
    [Tooltip("Should porter return to spawn point when idle?")]
    public bool returnToSpawn = true;
    
    [Header("Visual")]
    [Tooltip("Color tint when porter is idle")]
    public Color idleColor = Color.white;
    
    [Tooltip("Color tint when porter is wandering")]
    public Color wanderColor = Color.cyan;
    
    [Tooltip("Color tint when porter is seeking loot")]
    public Color seekColor = Color.yellow;
    
    [Tooltip("Color tint when porter is carrying loot")]
    public Color carryingColor = Color.green;
    
    [Tooltip("Color tint when porter is traveling")]
    public Color travelColor = Color.cyan;
}