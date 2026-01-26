using UnityEngine;


[CreateAssetMenu(menuName = "Data/Customer")]
public class CustomerDef : EntityDef
{
    [Header("Customer Def")]
    public CustomerState startingState = CustomerState.Wander;
    public ItemCategory itemPreferance;

  

    [Header("Budget")]
    public Vector2 budget = new Vector2(8, 16);
    public Vector2Int batchRange = new Vector2Int(1, 3); // items per visit

}