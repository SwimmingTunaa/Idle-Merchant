using UnityEngine;


[CreateAssetMenu(menuName = "Data/Customer")]
public class CustomerDef : EntityDef
{
    [Header("Customer Def")]
    public CustomerState startingState = CustomerState.Wander;
    public ItemCategory itemPreferance;

  

    [Header("Weights & Budget")]
    public float baseWeight = 1f;      // baseline chance before shelf traffic
    public Vector2 budget = new Vector2(8, 16);
    public Vector2Int batchRange = new Vector2Int(1, 3); // items per visit

}