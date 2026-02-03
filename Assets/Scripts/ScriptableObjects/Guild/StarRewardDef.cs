using UnityEngine;

/// <summary>
/// Defines what happens when a star is earned.
/// Rewards are granted when ALL milestones for that star are completed.
/// Cleaner than per-milestone rewards for major feature unlocks.
/// </summary>
[CreateAssetMenu(fileName = "StarReward", menuName = "Guild/Star Reward")]
public class StarRewardDef : ScriptableObject
{
    [Header("Star Configuration")]
    [Tooltip("Which star does this reward? (1★, 2★, etc.)")]
    [Range(1, 5)]
    public int starLevel = 1;

    [Header("Rewards")]
    [Tooltip("Upgrades that become available for purchase in Guild Shop")]
    public GuildUpgradeDef[] upgradesAvailableForPurchase;
    
    [Tooltip("Upgrades granted for free (no gold cost)")]
    public GuildUpgradeDef[] upgradesGranted;

    [Tooltip("Recipes unlocked when star is earned")]
    public RecipeDef[] recipesUnlocked;

    [Tooltip("Features enabled when star is earned")]
    public FeatureType[] featuresUnlocked;

    [Header("Feedback")]
    [Tooltip("Title shown to player (e.g., 'Forging Station Unlocked!')")]
    public string rewardTitle = "New Features Unlocked!";

    [Tooltip("Description of what was unlocked")]
    [TextArea(2, 4)]
    public string rewardDescription = "You can now craft items!";

    [Tooltip("Icon for reward notification")]
    public Sprite rewardIcon;
}
