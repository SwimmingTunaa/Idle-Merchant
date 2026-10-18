using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages guild progression: stars, milestones, upgrades, and layer unlocks.
/// Uses registration instead of scene scanning for performance.
/// Single source of truth for all progression state.
/// </summary>
public class ProgressionManager : PersistentSingleton<ProgressionManager>
{
    [Header("Layer Progression")]
    [Tooltip("Maximum dungeon layer currently unlocked (1-10)")]
    public int maxUnlockedLayer = 1;

    [Header("Star Progression")]
    [Tooltip("Current guild star rating (1-5)")]
    [SerializeField] private int currentStars = 1;

    [Header("Star Rewards")]
    [Tooltip("Rewards granted when each star is earned")]
    [SerializeField] private List<StarRewardDef> starRewards = new List<StarRewardDef>();

    
    [Tooltip("All milestone definitions (loaded from Resources)")]
    [SerializeField] private List<StarMilestoneDef> allMilestones = new();

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // Cache for performance
    private Dictionary<ItemCategory, List<ItemDef>> availableItemsCache = new Dictionary<ItemCategory, List<ItemDef>>();
    private int cachedForLayer = -1;

    // Spawner registration system (replaces FindObjectsByType)
    private static List<Spawner> registeredSpawners = new List<Spawner>();

    // Owned upgrades tracking
    private HashSet<GuildUpgradeDef> ownedUpgrades = new HashSet<GuildUpgradeDef>();
    // Track which upgrades are available for purchase (not yet owned)
    private HashSet<GuildUpgradeDef> availableUpgrades = new HashSet<GuildUpgradeDef>();
    // Reached = target hit (claimable); claimed ⊆ reached. Persisted by SaveManager (ToSave/LoadFrom).
    private HashSet<StarMilestoneDef> reachedMilestones = new HashSet<StarMilestoneDef>();
    private HashSet<StarMilestoneDef> claimedMilestones = new HashSet<StarMilestoneDef>();


    // Milestone counters (accumulative from game start)
    private int totalGoldEarned = 0;
    private int totalMobsKilled = 0;
    private int totalUnitsHired = 0;
    private int totalAdventurersHired = 0;
    private int totalPortersHired = 0;
    private int totalLootCollected = 0;
    private int totalItemsCrafted = 0;
    private int totalCraftedItemsSold = 0;
    private int totalCustomersServed = 0;
    private int totalAdventurersRankedUp = 0;
    // Item types the player has crafted — a sale of one counts toward CraftedItemsSold
    private readonly HashSet<ItemDef> craftedItemDefs = new HashSet<ItemDef>();

    // Dirty flag — set when any counter changes, checked once per frame
    private bool milestoneCheckPending = false;
    

    // Events for decoupling
    public static event Action<int> OnStarEarned;
    public static event Action<GuildUpgradeDef> OnUpgradePurchased;
    // Layer unlock is routed through GameSignals.OnLayerUnlocked
    

    protected override void Awake()
    {
        base.Awake();
        // Load data in Awake so it's ready before any other Start runs
        LoadMilestones();
        LoadStarRewards();
    }

    void Start()
    {
        RebuildAvailableItemsCache();
    }

    void Update()
    {
        if (milestoneCheckPending)
        {
            milestoneCheckPending = false;
            CheckMilestones();
        }
    }

    void OnEnable()
    {
        // Subscribe to game signals for automatic tracking
        GameSignals.OnGoldEarned += IncrementGoldEarned;
        GameSignals.OnEntityDeath += HandleEntityDeath;
        GameSignals.OnLootCollected += HandleLootCollected;
        GameSignals.OnProductCrafted += HandleProductCrafted;
        GameSignals.OnUnitHired += HandleUnitHired;
        GameSignals.OnCustomerServed += HandleCustomerServed;
        GameSignals.OnAdventurerPromoted += HandleAdventurerPromoted;
        GameSignals.OnItemSold += HandleItemSold;
    }

    void OnDisable()
    {
        // Unsubscribe to prevent memory leaks
        GameSignals.OnGoldEarned -= IncrementGoldEarned;
        GameSignals.OnEntityDeath -= HandleEntityDeath;
        GameSignals.OnLootCollected -= HandleLootCollected;
        GameSignals.OnProductCrafted -= HandleProductCrafted;
        GameSignals.OnUnitHired -= HandleUnitHired;
        GameSignals.OnCustomerServed -= HandleCustomerServed;
        GameSignals.OnAdventurerPromoted -= HandleAdventurerPromoted;
        GameSignals.OnItemSold -= HandleItemSold;
    }

    // Signal handlers
    private void HandleEntityDeath(GameObject deadEntity)
    {
        // Check if dead entity was a mob
        deadEntity.TryGetComponent<MobAgent>(out var mobAgent);
        if (mobAgent != null)
        {
            IncrementMobsKilled(1);
        }
    }

    private void HandleLootCollected(ResourceStack stack)
    {
        IncrementLootCollected(stack.qty);
    }

    private void HandleProductCrafted(ResourceStack stack)
    {
        craftedItemDefs.Add(stack.itemDef);
        IncrementItemsCrafted(stack.qty);
    }

    private void HandleItemSold(ResourceStack stack)
    {
        if (craftedItemDefs.Contains(stack.itemDef)) IncrementCraftedItemsSold(stack.qty);
    }

    private void HandleUnitHired(EntityDef def, HireRole role)
    {
        IncrementUnitsHired(1);
        if (role == HireRole.Adventurer) IncrementAdventurersHired(1);
        else if (role == HireRole.Porter) IncrementPortersHired(1);
    }

    private void HandleCustomerServed()
    {
        IncrementCustomersServed(1);
    }

    private void HandleAdventurerPromoted(EntityBase entity, string oldRole, string newRole)
    {
        IncrementAdventurersRankedUp(1);
    }

    // ===== MILESTONE LOADING =====

    private void LoadMilestones()
    {
        // Load all milestone definitions from Resources folder
        var loadedMilestones = Resources.LoadAll<StarMilestoneDef>("Milestones");
        allMilestones = new List<StarMilestoneDef>(loadedMilestones.Where(m => m != null));

        if (showDebugLogs)
        {
            Debug.Log($"[ProgressionManager] Loaded {allMilestones.Count} milestones");
            foreach (var milestone in allMilestones.GroupBy(m => m.starLevel))
            {
                Debug.Log($"  {milestone.Key}★: {milestone.Count()} milestones");
            }
        }
    }

    private void LoadStarRewards()
    {
        var loadedRewards = Resources.LoadAll<StarRewardDef>("Rewards");
        starRewards = new List<StarRewardDef>(loadedRewards);

        if (showDebugLogs)
        {
            Debug.Log($"[ProgressionManager] Loaded {starRewards.Count} star rewards");
            foreach (var reward in starRewards)
            {
                Debug.Log($"  {reward.starLevel}★: {reward.rewardTitle}");
            }
        }
    }

    /// <summary>
    /// Flag next-star milestones that just hit their target as reached (claimable).
    /// Rewards and the star itself are granted by TryClaimMilestone.
    /// </summary>
    private void CheckMilestones()
    {
        if (currentStars >= 5) return; // Already max stars

        int targetStar = currentStars + 1;
        var milestones = GetMilestonesForStar(targetStar);

        if (milestones.Count == 0)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[ProgressionManager] No milestones defined for {targetStar}★");
            return;
        }

        foreach (var milestone in milestones)
        {
            if (!reachedMilestones.Contains(milestone) && IsMilestoneComplete(milestone))
                MarkReached(milestone);
        }
    }

    private void MarkReached(StarMilestoneDef milestone)
    {
        if (!reachedMilestones.Add(milestone)) return;
        GameSignals.RaiseMilestoneReached(milestone);
    }

    public bool IsMilestoneReached(StarMilestoneDef milestone) => milestone != null && reachedMilestones.Contains(milestone);
    public bool IsMilestoneClaimed(StarMilestoneDef milestone) => milestone != null && claimedMilestones.Contains(milestone);

    /// <summary>Reached but not yet claimed (claimed ⊆ reached, so a count difference).</summary>
    public int ClaimableCount => reachedMilestones.Count - claimedMilestones.Count;

    /// <summary>
    /// Claim a reached milestone: pays goldReward as a coin flight to the gold counter, grants its unlock
    /// reward, then awards the star once every milestone of the tier is claimed.
    /// uiCoinFlight(coins, onEachArrival) runs the UI-layer shower; it returns false (having called nothing)
    /// when it can't, and the world burst from coinOrigin takes over.
    /// </summary>
    public bool TryClaimMilestone(StarMilestoneDef milestone, Vector3 coinOrigin,
        System.Func<int, System.Action, bool> uiCoinFlight = null)
    {
        if (!IsMilestoneReached(milestone) || !claimedMilestones.Add(milestone))
            return false;

        PayMilestoneGold(milestone.goldReward, coinOrigin, uiCoinFlight);
        GrantMilestoneReward(milestone);
        GameSignals.RaiseMilestoneClaimed(milestone);

        int targetStar = currentStars + 1;
        var tier = GetMilestonesForStar(targetStar);
        if (tier.Count > 0 && tier.All(claimedMilestones.Contains))
        {
            AwardStar(targetStar);
            milestoneCheckPending = true; // next tier may already be met
        }
        return true;
    }

    // Gold banks as the coins land (house rule): UI shower → world burst → direct, every path banks in full.
    // Public: offline earnings (SaveManager) pay out through the same chain. bank defaults to Inventory.AddGold.
    public static void PayMilestoneGold(int amount, Vector3 from, System.Func<int, System.Action, bool> uiCoinFlight,
        System.Action<int> bank = null)
    {
        if (amount <= 0) return;
        bank ??= Inventory.Instance.AddGold;

        int coins = Mathf.Clamp(amount / 25, 14, 36);
        int share = amount / coins;
        int left = coins;
        // Last coin to land carries the remainder, so the total is exact whatever the arrival order
        System.Action land = () => bank(--left == 0 ? amount - share * (coins - 1) : share);

        if (uiCoinFlight != null && uiCoinFlight(coins, land)) return;
        if (ItemExplosionVFX.Instance != null)
            ItemExplosionVFX.Instance.SpawnExplosion(null, from, coins, coins, onEachArrival: land);
        else
            bank(amount);
    }

    /// <summary>Debug: flag a milestone reached without grinding its counter.</summary>
    public void ForceReachMilestone(StarMilestoneDef milestone)
    {
        if (milestone != null) MarkReached(milestone);
    }

/// <summary>
/// Grant reward for completing a milestone (if reward is defined).
/// Handles unlocking upgrades, features, recipes, etc.
/// </summary>
private void GrantMilestoneReward(StarMilestoneDef milestone)
{
    if (milestone.reward == null)
        return; // No reward defined

    var reward = milestone.reward;

    if (showDebugLogs)
        Debug.Log($"[ProgressionManager] Milestone '{milestone.description}' completed! Granting reward: {reward.rewardType}");

    switch (reward.rewardType)
    {
        case RewardType.None:
            // Just milestone completion, no special reward
            break;

        case RewardType.UnlockUpgrade:
            // Makes upgrade available for purchase (doesn't grant it for free)
            // You'd implement this with an "available upgrades" system
            if (showDebugLogs)
                Debug.Log($"[ProgressionManager] {reward.upgradeToUnlock?.name} is now available for purchase!");
            break;

        case RewardType.GrantUpgrade:
            // Gives upgrade for free (tutorial rewards)
            if (reward.upgradeToUnlock != null)
            {
                ownedUpgrades.Add(reward.upgradeToUnlock);
                OnUpgradePurchased?.Invoke(reward.upgradeToUnlock);
                
                if (showDebugLogs)
                    Debug.Log($"[ProgressionManager] Granted {reward.upgradeToUnlock.name} for free!");
            }
            break;

        case RewardType.UnlockFeature:
            // Enable game feature (handled by listeners to OnUpgradePurchased or custom events)
            if (showDebugLogs)
                Debug.Log($"[ProgressionManager] Feature unlocked: {reward.featureToUnlock}");
            break;

        case RewardType.UnlockRecipe:
            // Enable crafting recipe
            if (reward.recipeToUnlock != null && CraftingManager.Instance != null)
            {
                CraftingManager.Instance.EnableRecipe(reward.recipeToUnlock);
                
                if (showDebugLogs)
                    Debug.Log($"[ProgressionManager] Recipe unlocked: {reward.recipeToUnlock.name}");
            }
            break;

        case RewardType.UnlockLayer:
            // Usually handled by star system, but can override
            break;

        case RewardType.SpawnNPC:
            // Future: spawn special NPCs
            if (showDebugLogs)
                Debug.Log($"[ProgressionManager] NPC spawn triggered (not implemented yet)");
            break;
    }

    // Show notification to player (you'd implement UI for this)
    // NotificationManager.Instance?.ShowReward(reward.rewardMessage, reward.rewardIcon);
}

        

    // ===== SPAWNER REGISTRATION (existing system) =====

    public static void RegisterSpawner(Spawner spawner)
    {
        if (spawner != null && !registeredSpawners.Contains(spawner))
        {
            registeredSpawners.Add(spawner);
        }
    }

    public static void UnregisterSpawner(Spawner spawner)
    {
        registeredSpawners.Remove(spawner);
    }

    // ===== STAR SYSTEM =====

    /// <summary>
    /// Get current star rating (1-5).
    /// </summary>
    public int GetCurrentStars()
    {
        return currentStars;
    }

    /// <summary>
    /// Get number of unlocked dungeon layers (equals maxUnlockedLayer).
    /// </summary>
    public int GetUnlockedLayerCount()
    {
        return maxUnlockedLayer;
    }

    /// <summary>
    /// Award a star and unlock corresponding layer.
    /// Also grants any rewards defined for this star.
    /// Fires OnStarEarned and OnLayerUnlocked events.
    /// </summary>
    private void AwardStar(int star)
    {
        if (star <= currentStars)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[ProgressionManager] Attempted to award {star}★ but already at {currentStars}★");
            return;
        }

        if (star > 5)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[ProgressionManager] Attempted to award {star}★ but max is 5★");
            return;
        }

        currentStars = star;

        // Unlock corresponding layer (0★ = Layer 1, 1★ = Layer 2, etc.)
        int layerToUnlock = star + 1;
        if (layerToUnlock <= 10 && layerToUnlock > maxUnlockedLayer)
        {
            UnlockLayer(layerToUnlock);
        }

        // Grant star rewards
        GrantStarReward(star);

        // Fire events
        OnStarEarned?.Invoke(star);
        GameSignals.RaiseStarEarned(star);

        if (showDebugLogs)
            Debug.Log($"[ProgressionManager] ★ Earned {star}★! Layer {layerToUnlock} unlocked.");
    }

    private void GrantStarReward(int star)
    {
        var reward = starRewards.Find(r => r.starLevel == star);

        if (reward == null)
        {
            if (showDebugLogs)
                Debug.Log($"[ProgressionManager] No reward defined for {star}★");
            return;
        }

        if (showDebugLogs)
            Debug.Log($"[ProgressionManager] Granting {star}★ reward: {reward.rewardTitle}");

        // Unlock upgrades for purchase (add to shop)
        if (reward.upgradesAvailableForPurchase != null)
        {
            foreach (var upgrade in reward.upgradesAvailableForPurchase)
            {
                if (upgrade != null && !availableUpgrades.Contains(upgrade))
                {
                    availableUpgrades.Add(upgrade);

                    if (showDebugLogs)
                        Debug.Log($"[ProgressionManager] Available for purchase: {upgrade.upgradeName}");
                }
            }
        }

        // Grant upgrades for free
        if (reward.upgradesGranted != null)
        {
            foreach (var upgrade in reward.upgradesGranted)
            {
                if (upgrade != null && !ownedUpgrades.Contains(upgrade))
                {
                    ownedUpgrades.Add(upgrade);
                    OnUpgradePurchased?.Invoke(upgrade);

                    if (showDebugLogs)
                        Debug.Log($"[ProgressionManager] Granted for free: {upgrade.upgradeName}");
                }
            }
        }

        // Unlock recipes
        if (reward.recipesUnlocked != null && CraftingManager.Instance != null)
        {
            foreach (var recipe in reward.recipesUnlocked)
            {
                if (recipe != null)
                {
                    CraftingManager.Instance.EnableRecipe(recipe);

                    if (showDebugLogs)
                        Debug.Log($"[ProgressionManager] Unlocked recipe: {recipe.name}");
                }
            }
        }

        // Show reward notification
        // NotificationManager.Instance?.ShowStarReward(reward);
    }

    // ===== MILESTONE SYSTEM =====

    /// <summary>
    /// Get all milestones for a specific star tier.
    /// </summary>
    public List<StarMilestoneDef> GetMilestonesForStar(int star)
    {
        return allMilestones.Where(m => m != null && m.starLevel == star).ToList();
    }

    /// <summary>
    /// Check if a milestone is complete.
    /// </summary>
    public bool IsMilestoneComplete(StarMilestoneDef milestone)
    {
        if (milestone == null) return false;
        if (milestone.milestoneType == MilestoneType.UpgradePurchased)
            return milestone.requiredUpgrade != null && ownedUpgrades.Contains(milestone.requiredUpgrade);
        return GetMilestoneCurrentValue(milestone) >= Mathf.Max(1, milestone.targetValue);
    }

    /// <summary>
    /// Get milestone progress (0.0 to 1.0) for UI display.
    /// </summary>
    public float GetMilestoneProgress(StarMilestoneDef milestone)
    {
        if (milestone == null) return 0f;
        if (milestone.milestoneType == MilestoneType.UpgradePurchased)
            return IsMilestoneComplete(milestone) ? 1f : 0f;
        return Mathf.Clamp01((float)GetMilestoneCurrentValue(milestone) / Mathf.Max(1, milestone.targetValue));
    }

    /// <summary>
    /// Get current counter value for a milestone (for UI display).
    /// </summary>
    public int GetMilestoneCurrentValue(StarMilestoneDef milestone)
    {
        if (milestone == null) return 0;

        return milestone.milestoneType switch
        {
            MilestoneType.GoldEarned => totalGoldEarned,
            MilestoneType.MobsKilled => totalMobsKilled,
            MilestoneType.UnitsHired => totalUnitsHired,
            MilestoneType.AdventurersHired => totalAdventurersHired,
            MilestoneType.PortersHired => totalPortersHired,
            MilestoneType.LootCollected => totalLootCollected,
            MilestoneType.ItemsCrafted => totalItemsCrafted,
            MilestoneType.CraftedItemsSold => totalCraftedItemsSold,
            MilestoneType.CustomersServed => totalCustomersServed,
            MilestoneType.UpgradePurchased => ownedUpgrades.Contains(milestone.requiredUpgrade) ? 1 : 0,
            // Only adventurers promote (rank up), so "units promoted" is the same counter
            MilestoneType.AdventurerRankedUp or MilestoneType.UnitsPromoted => totalAdventurersRankedUp,
            MilestoneType.AllLayersActive => CountStaffedLayers(),
            MilestoneType.UniqueItemsOwned => Inventory.Instance != null ? Inventory.Instance.DistinctItemCount : 0,
            _ => 0
        };
    }

    // Per-layer unit managers (deeper layers start inactive, hence Include); re-found after a scene reload destroys them.
    private MonoBehaviour[] layerManagers;

    /// <summary>Pure query: distinct layers with at least one adventurer or porter on them right now.</summary>
    private int CountStaffedLayers()
    {
        if (layerManagers == null || layerManagers.Length == 0 || layerManagers[0] == null)
            layerManagers = FindObjectsByType<AdventurerManager>(FindObjectsInactive.Include)
                .Concat<MonoBehaviour>(FindObjectsByType<PorterManager>(FindObjectsInactive.Include)).ToArray();

        int mask = 0;
        foreach (var m in layerManagers)
            if (m is IUnitManager um && um.GetTotalCount() > 0) mask |= 1 << um.LayerIndex;

        int layers = 0;
        for (; mask != 0; mask &= mask - 1) layers++;
        return layers;
    }

    // ===== MILESTONE INCREMENT METHODS =====

    public void IncrementGoldEarned(int amount)
    {
        totalGoldEarned += amount;
        milestoneCheckPending = true;

        if (showDebugLogs)
            Debug.Log($"[ProgressionManager] Gold earned: +{amount} (total: {totalGoldEarned})");
    }

    public void IncrementMobsKilled(int count)
    {
        totalMobsKilled += count;
        milestoneCheckPending = true;

        if (showDebugLogs)
            Debug.Log($"[ProgressionManager] Mobs killed: +{count} (total: {totalMobsKilled})");
    }

    public void IncrementUnitsHired(int count)
    {
        totalUnitsHired += count;
        milestoneCheckPending = true;

        if (showDebugLogs)
            Debug.Log($"[ProgressionManager] Units hired: +{count} (total: {totalUnitsHired})");
    }

    public void IncrementAdventurersHired(int count)
    {
        totalAdventurersHired += count;
        milestoneCheckPending = true;

        if (showDebugLogs)
            Debug.Log($"[ProgressionManager] Adventurers hired: +{count} (total: {totalAdventurersHired})");
    }

    public void IncrementPortersHired(int count)
    {
        totalPortersHired += count;
        milestoneCheckPending = true;

        if (showDebugLogs)
            Debug.Log($"[ProgressionManager] Porters hired: +{count} (total: {totalPortersHired})");
    }

    public void IncrementLootCollected(int count)
    {
        totalLootCollected += count;
        milestoneCheckPending = true;

        if (showDebugLogs)
            Debug.Log($"[ProgressionManager] Loot collected: +{count} (total: {totalLootCollected})");
    }

    public void IncrementItemsCrafted(int count)
    {
        totalItemsCrafted += count;
        milestoneCheckPending = true;

        if (showDebugLogs)
            Debug.Log($"[ProgressionManager] Items crafted: +{count} (total: {totalItemsCrafted})");
    }

    public void IncrementCraftedItemsSold(int count)
    {
        totalCraftedItemsSold += count;
        milestoneCheckPending = true;

        if (showDebugLogs)
            Debug.Log($"[ProgressionManager] Crafted items sold: +{count} (total: {totalCraftedItemsSold})");
    }

    public void IncrementCustomersServed(int count)
    {
        totalCustomersServed += count;
        milestoneCheckPending = true;

        if (showDebugLogs)
            Debug.Log($"[ProgressionManager] Customers served: +{count} (total: {totalCustomersServed})");
    }

    private void IncrementAdventurersRankedUp(int amount)
    {
        totalAdventurersRankedUp += amount;
        milestoneCheckPending = true;

        if (showDebugLogs)
            Debug.Log($"[ProgressionManager] Adventurers ranked up: +{amount} (total: {totalAdventurersRankedUp})");
    }

    // ===== UPGRADE SYSTEM =====

    /// <summary>
    /// Check if player can purchase an upgrade (gold + stars).
    /// </summary>
    public bool CanPurchaseUpgrade(GuildUpgradeDef upgrade)
    {
        if (upgrade == null) return false;
        if (ownedUpgrades.Contains(upgrade)) return false; // Already owned
        if (currentStars < upgrade.starRequirement) return false; // Insufficient stars
        if (!Inventory.Instance.CanAfford(upgrade.goldCost)) return false; // Insufficient gold

        return true;
    }

    public bool IsUpgradeAvailable(GuildUpgradeDef upgrade)
    {
        return availableUpgrades.Contains(upgrade) && !ownedUpgrades.Contains(upgrade);
    }

    public List<GuildUpgradeDef> GetAvailableUpgrades()
    {
        return availableUpgrades.Where(u => !ownedUpgrades.Contains(u)).ToList();
    }

    /// <summary>
    /// Purchase an upgrade (deducts gold, adds to owned, checks milestones).
    /// Returns true if successful.
    /// </summary>
    public bool PurchaseUpgrade(GuildUpgradeDef upgrade)
    {
        if (!CanPurchaseUpgrade(upgrade))
        {
            if (showDebugLogs)
                Debug.LogWarning($"[ProgressionManager] Cannot purchase {upgrade.upgradeName} - requirements not met");
            return false;
        }

        // Deduct gold via Inventory
        if (!Inventory.Instance.TrySpendGold(upgrade.goldCost))
        {
            if (showDebugLogs)
                Debug.LogWarning($"[ProgressionManager] Failed to spend gold for {upgrade.upgradeName}");
            return false;
        }

        // Add to owned
        ownedUpgrades.Add(upgrade);

        // Fire event (CraftingManager, etc. can listen)
        OnUpgradePurchased?.Invoke(upgrade);

        // Check if this completes any milestones
        milestoneCheckPending = true;

        if (showDebugLogs)
            Debug.Log($"[ProgressionManager] Purchased: {upgrade.upgradeName} ({upgrade.goldCost}g)");

        return true;
    }

    /// <summary>
    /// Check if an upgrade is owned.
    /// </summary>
    public bool IsUpgradeOwned(GuildUpgradeDef upgrade)
    {
        return upgrade != null && ownedUpgrades.Contains(upgrade);
    }

    /// <summary>
    /// Check if a layer-specific upgrade is owned (e.g., Elevator on Layer 2).
    /// </summary>
    public bool IsUpgradeOwned(UpgradeType type, int layer)
    {
        return ownedUpgrades.Any(u => u.upgradeType == type && u.layerSpecific && u.targetLayer == layer);
    }

    // ===== LAYER SYSTEM (existing, preserved) =====

    public List<ItemDef> GetAvailableItems(ItemCategory category, float maxBudget)
    {
        if (cachedForLayer != maxUnlockedLayer)
        {
            RebuildAvailableItemsCache();
        }

        if (!availableItemsCache.ContainsKey(category))
        {
            if (showDebugLogs)
                Debug.LogWarning($"[ProgressionManager] No items found for category: {category}");
            return new List<ItemDef>();
        }

        var affordable = availableItemsCache[category]
            .Where(item => item.sellPrice <= maxBudget)
            .ToList();

        if (showDebugLogs)
            Debug.Log($"[ProgressionManager] Found {affordable.Count} {category} items within budget {maxBudget}g");

        return affordable;
    }

    public ItemDef GetRandomAvailableItem(ItemCategory category, float maxBudget)
    {
        var available = GetAvailableItems(category, maxBudget);
        
        if (available.Count == 0)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[ProgressionManager] No available items for category {category} within budget {maxBudget}g");
            return null;
        }

        return available[UnityEngine.Random.Range(0, available.Count)];
    }

    public void UnlockLayer(int layer)
    {
        if (layer <= maxUnlockedLayer)
        {
            Debug.LogWarning($"[ProgressionManager] Layer {layer} is already unlocked");
            return;
        }

        maxUnlockedLayer = layer;
        RebuildAvailableItemsCache();
        GameSignals.RaiseLayerUnlocked(layer);

        if (showDebugLogs)
            Debug.Log($"[ProgressionManager] Unlocked layer {layer}! Rebuilding available items cache.");
    }

    private void RebuildAvailableItemsCache()
    {
        availableItemsCache.Clear();

        if (registeredSpawners.Count == 0)
        {
            Debug.LogWarning("[ProgressionManager] No spawners registered! Make sure Spawner.Awake() calls RegisterSpawner()");
            return;
        }

        HashSet<ItemDef> collectedItems = new HashSet<ItemDef>();

        foreach (var spawner in registeredSpawners)
        {
            if (spawner == null) continue;

            if (spawner.layerIndex <= maxUnlockedLayer && spawner.layerIndex > 0)
            {
                foreach (var candidate in spawner.candidates)
                {
                    if (candidate == null) continue;

                    MobDef mobDef = candidate as MobDef;
                    if (mobDef != null)
                    {
                        foreach (var itemDef in mobDef.loot)
                        {
                            if (itemDef != null)
                            {
                                collectedItems.Add(itemDef);
                            }
                        }
                    }
                }
            }
        }

        foreach (var item in collectedItems)
        {
            if (!availableItemsCache.ContainsKey(item.itemCategory))
            {
                availableItemsCache[item.itemCategory] = new List<ItemDef>();
            }

            availableItemsCache[item.itemCategory].Add(item);
        }

        cachedForLayer = maxUnlockedLayer;

        if (showDebugLogs)
        {
            Debug.Log($"[ProgressionManager] Cache rebuilt for layers 1-{maxUnlockedLayer}:");
            Debug.Log($"  Found {registeredSpawners.Count} registered spawners");
            foreach (var kvp in availableItemsCache)
            {
                Debug.Log($"  {kvp.Key}: {kvp.Value.Count} items");
            }
        }
    }

    // ===== SAVE / LOAD =====
    // SO references persist as asset names (renaming an asset orphans it in old saves).

    public ProgressionSave ToSave() => new ProgressionSave
    {
        stars = currentStars,
        maxUnlockedLayer = maxUnlockedLayer,
        goldEarned = totalGoldEarned,
        mobsKilled = totalMobsKilled,
        unitsHired = totalUnitsHired,
        adventurersHired = totalAdventurersHired,
        portersHired = totalPortersHired,
        lootCollected = totalLootCollected,
        itemsCrafted = totalItemsCrafted,
        craftedItemsSold = totalCraftedItemsSold,
        customersServed = totalCustomersServed,
        adventurersRankedUp = totalAdventurersRankedUp,
        craftedItemTypes = craftedItemDefs.Where(d => d != null).Select(d => d.name).ToList(),
        reachedMilestones = reachedMilestones.Where(m => m != null).Select(m => m.name).ToList(),
        claimedMilestones = claimedMilestones.Where(m => m != null).Select(m => m.name).ToList(),
    };

    public List<string> OwnedUpgradeNames() => ownedUpgrades.Where(u => u != null).Select(u => u.name).ToList();

    /// <summary>
    /// Cold-boot restore (SaveManager, before other Starts so activators and UI build from it).
    /// Purchasable upgrades are re-derived from the star rewards already earned; no events fire here.
    /// </summary>
    public void LoadFrom(ProgressionSave p, List<string> ownedUpgradeNames)
    {
        currentStars = p.stars;
        maxUnlockedLayer = p.maxUnlockedLayer;
        totalGoldEarned = p.goldEarned;
        totalMobsKilled = p.mobsKilled;
        totalUnitsHired = p.unitsHired;
        totalAdventurersHired = p.adventurersHired;
        totalPortersHired = p.portersHired;
        totalLootCollected = p.lootCollected;
        totalItemsCrafted = p.itemsCrafted;
        totalCraftedItemsSold = p.craftedItemsSold;
        totalCustomersServed = p.customersServed;
        totalAdventurersRankedUp = p.adventurersRankedUp;

        craftedItemDefs.Clear();
        var items = SaveManager.ItemsByName();
        foreach (var name in p.craftedItemTypes)
            if (items.TryGetValue(name, out var item)) craftedItemDefs.Add(item);
        reachedMilestones = new HashSet<StarMilestoneDef>(allMilestones.Where(m => p.reachedMilestones.Contains(m.name)));
        claimedMilestones = new HashSet<StarMilestoneDef>(allMilestones.Where(m => p.claimedMilestones.Contains(m.name)));
        ownedUpgrades = new HashSet<GuildUpgradeDef>(
            Resources.LoadAll<GuildUpgradeDef>("Guild Upgrades").Where(u => ownedUpgradeNames.Contains(u.name)));

        availableUpgrades.Clear();
        foreach (var reward in starRewards)
            if (reward != null && reward.starLevel <= currentStars && reward.upgradesAvailableForPurchase != null)
                availableUpgrades.UnionWith(reward.upgradesAvailableForPurchase.Where(u => u != null));

        RebuildAvailableItemsCache();
        milestoneCheckPending = true;
    }

    /// <summary>Re-enables recipes unlocked by earned stars and claimed milestones.
    /// Runs after CraftingManager.Start (EnableRecipe needs its inventory ref).</summary>
    public void EnableUnlockedRecipes()
    {
        if (CraftingManager.Instance == null) return;
        foreach (var reward in starRewards)
            if (reward != null && reward.starLevel <= currentStars && reward.recipesUnlocked != null)
                foreach (var recipe in reward.recipesUnlocked)
                    if (recipe != null) CraftingManager.Instance.EnableRecipe(recipe);

        foreach (var m in claimedMilestones)
            if (m.reward != null && m.reward.rewardType == RewardType.UnlockRecipe && m.reward.recipeToUnlock != null)
                CraftingManager.Instance.EnableRecipe(m.reward.recipeToUnlock);
    }

    // ===== DEBUG CONTEXT MENU =====

    [ContextMenu("Debug: Award 2★")]
    private void Debug_Award2Star()
    {
        AwardStar(2);
    }

    [ContextMenu("Debug: Grant 10000 Gold Progress")]
    private void Debug_Grant1000Gold()
    {
        IncrementGoldEarned(10000);
    }

    [ContextMenu("Debug: Complete All 2★ Milestones")]
    private void Debug_Complete2StarMilestones()
    {
        var milestones = GetMilestonesForStar(2);
        foreach (var milestone in milestones)
        {
            switch (milestone.milestoneType)
            {
                case MilestoneType.GoldEarned:
                    totalGoldEarned = milestone.targetValue;
                    break;
                case MilestoneType.MobsKilled:
                    totalMobsKilled = milestone.targetValue;
                    break;
                case MilestoneType.UnitsHired:
                    totalUnitsHired = milestone.targetValue;
                    break;
                case MilestoneType.LootCollected:
                    totalLootCollected = milestone.targetValue;
                    break;
                case MilestoneType.ItemsCrafted:
                    totalItemsCrafted = milestone.targetValue;
                    break;
                case MilestoneType.UpgradePurchased:
                    if (milestone.requiredUpgrade != null && !ownedUpgrades.Contains(milestone.requiredUpgrade))
                    {
                        // Force add upgrade without gold cost for debug
                        ownedUpgrades.Add(milestone.requiredUpgrade);
                        OnUpgradePurchased?.Invoke(milestone.requiredUpgrade);
                    }
                    break;
            }
        }
        CheckMilestones();
        Debug.Log("[ProgressionManager] Completed all 2★ milestones");
    }

    [ContextMenu("Debug: Print Milestone Progress")]
    private void Debug_PrintMilestoneProgress()
    {
        int targetStar = currentStars + 1;
        var milestones = GetMilestonesForStar(targetStar);

        Debug.Log($"=== Milestone Progress ({targetStar}★) ===");
        foreach (var milestone in milestones)
        {
            bool complete = IsMilestoneComplete(milestone);
            float progress = GetMilestoneProgress(milestone);
            int current = GetMilestoneCurrentValue(milestone);
            
            string status = complete ? "✓" : "✗";
            Debug.Log($"  {status} {milestone.description}: {current}/{milestone.targetValue} ({progress * 100:F0}%)");
        }
    }

    [ContextMenu("Debug: Print Available Items")]
    private void Debug_PrintAvailableItems()
    {
        Debug.Log($"=== Available Items (Layers 1-{maxUnlockedLayer}) ===");
        
        foreach (var kvp in availableItemsCache)
        {
            Debug.Log($"\n{kvp.Key} ({kvp.Value.Count} items):");
            foreach (var item in kvp.Value)
            {
                Debug.Log($"  - {item.displayName} ({item.sellPrice}g)");
            }
        }
    }

    [ContextMenu("Debug: Unlock Next Layer")]
    private void Debug_UnlockNextLayer()
    {
        if (maxUnlockedLayer < 10)
        {
            UnlockLayer(maxUnlockedLayer + 1);
            Debug.Log($"[ProgressionManager] Unlocked layer {maxUnlockedLayer}");
        }
        else
        {
            Debug.Log("[ProgressionManager] All layers already unlocked!");
        }
    }

    [ContextMenu("Debug: Reset to Layer 1")]
    private void Debug_ResetProgression()
    {
        maxUnlockedLayer = 1;
        currentStars = 1;
        ownedUpgrades.Clear();
        reachedMilestones.Clear();
        claimedMilestones.Clear();
        totalGoldEarned = 0;
        totalMobsKilled = 0;
        totalUnitsHired = 0;
        totalAdventurersHired = 0;
        totalPortersHired = 0;
        totalLootCollected = 0;
        totalItemsCrafted = 0;
        totalCraftedItemsSold = 0;
        totalCustomersServed = 0;
        RebuildAvailableItemsCache();
        Debug.Log("[ProgressionManager] Reset to 1★, layer 1");
    }

    [ContextMenu("Debug: Print Registered Spawners")]
    private void Debug_PrintSpawners()
    {
        Debug.Log($"=== Registered Spawners ({registeredSpawners.Count}) ===");
        foreach (var spawner in registeredSpawners)
        {
            if (spawner != null)
                Debug.Log($"  - Layer {spawner.layerIndex}: {spawner.name} ({spawner.candidates.Count} candidates)");
        }
    }
}