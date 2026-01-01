using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Data/Mob Def")]
public class MobDef : EntityDef
{
    [Header("Mob Def")]
    public MobState startingState = MobState.Wander;
    public bool isBoss = false;

    [Header("Stats")]
    public float baseHealth = 1;
    public AnimationCurve hpMultiplierByLayer = AnimationCurve.Linear(1, 1, 10, 3);
    
    [Header("Damage Response")]
    [Tooltip("Time mob is paused/stunned when hit")]
    public float stunTime = 0.2f;

    [Header("Targeting")]
    [Tooltip("Max adventurers that can target this mob simultaneously. 0 = use MobManager default (3)")]
    public int maxSimultaneousAttackers = 0;

    [Header("Loot Table")]
    public List<ItemDef> loot = new();
    public Vector2Int lootDropAmount;
}