using UnityEngine;
public enum CustomerArcheType
{
    Commoner, Adventurer, Noble
}

[CreateAssetMenu(menuName = "Data/Customer")]
public class CustomerDef : EntityDef
{
    [Header("Customer Def")]
    public CustomerState startingState = CustomerState.Entering;
    public CustomerArcheType customerArcheType = CustomerArcheType.Commoner;
    public ItemCategory itemPreferance;

  

    [Header("Wander Behaviour")]
    [Tooltip("How long customer browses before deciding to buy")]
    public Vector2 wanderDuration = new Vector2(5f, 10f);
    [Tooltip("Speed multiplier during wander (e.g. 0.4 = 40% of normal speed)")]
    public float wanderSpeedMultiplier = 0.8f;
    [Tooltip("How long customer pauses at each wander point before picking the next")]
    public Vector2 wanderPauseDuration = new Vector2(0.5f, 2f);

    [Header("Budget")]
    public Vector2 budget = new Vector2(8, 16);
    public Vector2Int batchRange = new Vector2Int(1, 3); // items per visit

}