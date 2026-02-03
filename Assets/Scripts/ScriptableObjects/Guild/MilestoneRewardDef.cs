using UnityEngine;

/// <summary>
/// Defines what happens when a milestone is completed.
/// Allows milestones to unlock upgrades, features, recipes, etc.
/// Decouples milestone logic from reward logic.
/// </summary>
[CreateAssetMenu(fileName = "MilestoneReward", menuName = "Guild/Milestone Reward")]
public class MilestoneRewardDef : ScriptableObject
{
    [Header("Reward Type")]
    [Tooltip("What kind of reward does this milestone grant?")]
    public RewardType rewardType;

    [Header("Upgrade Unlock")]
    [Tooltip("If RewardType is UnlockUpgrade, which upgrade to unlock?")]
    public GuildUpgradeDef upgradeToUnlock;

    [Header("Feature Unlock")]
    [Tooltip("If RewardType is UnlockFeature, which feature?")]
    public FeatureType featureToUnlock;

    [Header("Recipe Unlock")]
    [Tooltip("If RewardType is UnlockRecipe, which recipe?")]
    public RecipeDef recipeToUnlock;

    [Header("Feedback")]
    [Tooltip("Message to show player when reward is granted")]
    public string rewardMessage = "New feature unlocked!";

    [Tooltip("Icon to show in unlock notification")]
    public Sprite rewardIcon;
}

public enum RewardType
{
    None,               // No reward, just milestone completion
    UnlockUpgrade,      // Makes an upgrade available for purchase (e.g., Research Station)
    GrantUpgrade,       // Gives upgrade for free (e.g., Forging Station at 1★)
    UnlockFeature,      // Enables a game feature (e.g., Crafting, Trading)
    UnlockRecipe,       // Adds recipe to crafting menu
    UnlockLayer,        // Unlocks a dungeon layer (handled by star system, but could be override)
    SpawnNPC,           // Spawns a special character (future: merchants, quest givers)
}

public enum FeatureType
{
    Crafting,
    Research,
    Trading,
    Promotions,
    LayerManagement,
}
