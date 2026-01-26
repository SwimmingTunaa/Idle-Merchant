using UnityEngine;

/// <summary>
/// Persistent debug settings for GameDebugger EditorWindow.
/// Stores default values that apply on playmode start.
/// </summary>
[CreateAssetMenu(menuName = "Debug/Game Debug Settings", fileName = "GameDebugSettings")]
public class GameDebugSettings : ScriptableObject
{
    [Header("Startup Options")]
    [Tooltip("Apply these settings automatically when entering play mode")]
    public bool applyOnPlaymodeStart = false;

    [Header("Starting Gold")]
    public bool setStartingGold = false;
    [Min(0)] public int startingGold = 1000;

    [Header("Starting Items")]
    public bool addStartingItems = false;
    [Tooltip("Items to add on playmode start")]
    public ItemQuantityPair[] startingItems = new ItemQuantityPair[0];

    [Header("Progression")]
    public bool setStartingLayer = false;
    [Range(1, 10)] public int startingLayer = 1;

    [Header("Time Control")]
    public bool setCustomTimeScale = false;
    [Range(0.1f, 10f)] public float customTimeScale = 1f;

    [Header("Crafting")]
    public bool enableAllRecipes = false;

    [System.Serializable]
    public struct ItemQuantityPair
    {
        public ItemDef item;
        [Min(1)] public int quantity;
    }
}
