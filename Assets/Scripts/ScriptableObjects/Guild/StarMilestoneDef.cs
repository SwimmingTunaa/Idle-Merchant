using UnityEngine;

/// <summary>
/// Defines a milestone requirement for earning a guild star.
/// Multiple milestones combine to unlock a star tier (2★, 3★, etc.).
/// Created as ScriptableObject asset for designer configuration.
/// </summary>
[CreateAssetMenu(fileName = "Milestone_", menuName = "Guild/Star Milestone", order = 1)]
public class StarMilestoneDef : ScriptableObject
{
    [Header("UI Display")]
    public string displayName = "Milestone Name";
    public string description = "Earn 1000 gold";
    [TextArea(2, 4)]
    public string flavorText = "";
    public Sprite icon;
    
    [Header("Star Configuration")]
    [Tooltip("Which star tier does this milestone count toward? (2★ = 2, 3★ = 3, etc.)")]
    [Range(2, 5)]
    public int starLevel = 2;

    [Header("Milestone Type")]
    [Tooltip("What action does this milestone track?")]
    public MilestoneType milestoneType = MilestoneType.GoldEarned;
    
    [Tooltip("Target value to complete this milestone (1000 gold, 100 mobs, etc.)")]
    public int targetValue = 1000;

    [Header("Upgrade Purchase (if applicable)")]
    [Tooltip("If milestoneType is UpgradePurchased, which upgrade is required?")]
    public GuildUpgradeDef requiredUpgrade;

    [Header("Reward (Optional)")]
    [Tooltip("Reward granted when this milestone is completed")]
    public MilestoneRewardDef reward;
}

/// <summary>
/// Types of milestones that can be tracked for star progression.
/// Add new types here as gameplay systems expand.
/// </summary>
public enum MilestoneType
{
    // Economy
    GoldEarned,           // Total gold earned (lifetime accumulative)
    GoldSold,             // Total gold from sales
    CraftedItemsSold,     // Sold X crafted items (premium goods)
    
    // Combat
    MobsKilled,           // Total mobs killed
    BossesKilled,         // Kill specific tough mobs
    
    // Workforce
    UnitsHired,           // Total units hired (adventurers + porters)
    AdventurersHired,     // Adventurers only
    PortersHired,         // Porters only
    UnitsPromoted,        // Promoted X units to higher roles
    
    // Production
    ItemsCrafted,         // Total items crafted
    RecipesCrafted,       // Unique recipes crafted at least once
    SpecificItemCrafted,  // Craft X of specific item (e.g., 50 swords)
    
    // Collection
    LootCollected,        // Total loot items collected
    UniqueItemsOwned,     // Own at least 1 of X different items
    
    // Upgrades/Purchases
    UpgradePurchased,     // Own specific upgrade (Forging Station)
    ResearchUnlocked,     // Unlock X research nodes
    
    // Layers
    LayerCleared,         // Clear all mobs on layer X
    AllLayersActive,      // Have units working on X layers simultaneously
    
    // Customers
    CustomersServed,      // Total customers who made a purchase
    HighValueSales,       // Sell item worth X+ gold
}