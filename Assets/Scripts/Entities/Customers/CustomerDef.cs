using UnityEngine;
public enum CustomerArcheType
{
    Commoner, Adventurer, Noble, Royalty   // serialized ints: append only
}

[CreateAssetMenu(menuName = "Data/Customer")]
public class CustomerDef : EntityDef
{
    [Header("Customer Def")]
    public CustomerState startingState = CustomerState.Entering;
    public CustomerArcheType customerArcheType = CustomerArcheType.Commoner;
    public ItemCategory itemPreferance;
    [Tooltip("Shopping list: these items first (any category), then the preferred category, then anything affordable. Empty = category only.")]
    public ItemDef[] preferredItems;
    [Tooltip("Pays this × sellPrice (Bulk Trader 0.8, Collector 2). The want pick scales the budget to match.")]
    public float priceMultiplier = 1f;

  

    [Header("Wander Behaviour")]
    [Tooltip("How long customer browses before deciding to buy")]
    public Vector2 wanderDuration = new Vector2(5f, 10f);
    [Tooltip("Speed multiplier during wander (e.g. 0.4 = 40% of normal speed)")]
    public float wanderSpeedMultiplier = 0.8f;
    [Tooltip("How long customer pauses at each wander point before picking the next")]
    public Vector2 wanderPauseDuration = new Vector2(0.5f, 2f);

    [Tooltip("Pirates: the sheet's death clip, played through a Playable when the raider is clicked (the customer controller has no Dead state)")]
    public AnimationClip deathClip;

    [Header("Decisive")]
    [Tooltip("Chance this customer already knows what they want: they pick at the door and go straight to the line (no browse)")]
    [Range(0f, 1f)] public float decisiveChance = 0.1f;

    [Header("Patience")]
    [Tooltip("Seconds they'll wait to buy (queue full / standing in line) before walking out Impatient; tints red over the last quarter")]
    public float patienceSeconds = 60f;

    [Header("Budget")]
    public Vector2 budget = new Vector2(80, 160);
    public Vector2Int batchRange = new Vector2Int(1, 3); // items per visit

}