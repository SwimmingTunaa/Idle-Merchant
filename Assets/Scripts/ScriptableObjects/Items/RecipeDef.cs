using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Guild station a recipe runs at (one per chain). Values index CraftingManager's station arrays.</summary>
public enum CraftingStation { Forging, Smithy, ArcaneWorkshop }

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
    [SerializeField] private List<Ingredient> ingredients = new();

    [Header("Outputs")]
    [SerializeField] private ItemDef output;
    [Min(1)][SerializeField] private int outputQty = 1;

    [Header("Crafting")]
    [Min(0.01f)][SerializeField] private float craftSeconds = 5f;
    [Tooltip("Station that runs this recipe: Forging = blades, Smithy = armor, Arcane Workshop = charms/jewelry")]
    [SerializeField] private CraftingStation station;

    public IReadOnlyList<Ingredient> Ingredients => ingredients;
    public ItemDef Output => output;
    public int OutputQty => outputQty;
    public float CraftSeconds => craftSeconds;
    public CraftingStation Station => station;
}
