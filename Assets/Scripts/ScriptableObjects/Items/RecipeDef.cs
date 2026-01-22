using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/Recipe")]
public class RecipeDef : ScriptableObject
{
    [Serializable]
    public struct Ingredient
    {
        [SerializeField] private ItemDef item;
        [Min(1)][SerializeField] private int qty;

        public ItemDef Item => item;
        public int Qty => qty;
    }

    [Header("Inputs")]
    [SerializeField] private List<Ingredient> inputs = new();

    [Header("Outputs")]
    [SerializeField] private ItemDef output;
    [Min(1)][SerializeField] private int outputQty = 1;

    [Header("Crafting")]
    [Min(0.01f)][SerializeField] private float craftSeconds = 5f;

    public IReadOnlyList<Ingredient> Inputs => inputs;
    public ItemDef Output => output;
    public int OutputQty => outputQty;
    public float CraftSeconds => craftSeconds;
}
