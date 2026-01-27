using UnityEngine;

/// <summary>
/// Defines a purchasable guild upgrade (Forging Station, Research Station, Elevator, etc.).
/// Created as ScriptableObject asset for designer configuration.
/// </summary>
[CreateAssetMenu(fileName = "GuildUpgrade_", menuName = "Guild/Guild Upgrade", order = 0)]
public class GuildUpgradeDef : ScriptableObject
{
    [Header("Basic Info")]
    [Tooltip("Display name shown in UI")]
    public string upgradeName = "Forging Station";
    
    [Tooltip("Description shown in upgrade card")]
    [TextArea(2, 4)]
    public string description = "Unlock the crafting system to turn raw materials into valuable goods.";
    
    [Tooltip("Icon shown on upgrade card")]
    public Sprite icon;

    [Header("Requirements")]
    [Tooltip("Gold cost to purchase this upgrade")]
    public int goldCost = 500;
    
    [Tooltip("Minimum stars required to see/purchase this upgrade (0 = always available)")]
    [Range(0, 5)]
    public int starRequirement = 1;

    [Header("Upgrade Type")]
    [Tooltip("What type of upgrade is this?")]
    public UpgradeType upgradeType = UpgradeType.ForgingStation;
    
    [Tooltip("Is this upgrade per-layer (Elevator) or global (Forging Station)?")]
    public bool layerSpecific = false;
    
    [Tooltip("If layer-specific, which layer does this affect? (1-10)")]
    [Range(1, 10)]
    public int targetLayer = 1;

    [Header("Research Unlock (Optional)")]
    [Tooltip("Does this upgrade require a research node to be unlocked first? (for Elevator/Teleporter)")]
    public bool requiresResearch = false;
    
    [Tooltip("If requiresResearch is true, which research node unlocks this? (optional for Phase 1)")]
    public string requiredResearchNodeID = "";
}

/// <summary>
/// Types of guild upgrades available.
/// Add new types here as systems are implemented.
/// </summary>
public enum UpgradeType
{
    ForgingStation,   // Unlocks crafting system
    ResearchStation,  // Unlocks research tree
    Elevator,         // Faster porter transport (per layer)
    Teleporter,       // Instant porter transport (per layer)
    PorterLodging,    // Increases porter capacity
    StorageExpansion, // Increases inventory size
    GuildHall,        // Visual upgrade
}
