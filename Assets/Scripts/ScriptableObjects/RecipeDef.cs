
using UnityEngine;

[CreateAssetMenu(menuName = "Data/Recipe")]
public class RecipeDef : ScriptableObject {
    public ItemDef input;
    public int inputQty = 2;
    public ItemDef output;
    public int outputQty = 1;
    public float craftSeconds = 5f;
}