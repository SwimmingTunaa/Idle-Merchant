using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Guild Upgrade panel with open book layout.
/// Left page: Star progress and milestones
/// Right page: Purchasable upgrades in scrollable grid
/// Subscribes to progression events for real-time updates.
/// </summary>
public class GuildUpgradePanel : BasePanelController
{
    [Header("UXML References")]
    [SerializeField] private VisualTreeAsset upgradeCardTemplate;

    // Left page elements (star progress)
    private VisualElement leftPage;
    private VisualElement starsContainer;
    private VisualElement milestonesContainer;
    private Label currentStarLabel;
    private Label nextStarLabel;

    // Right page elements (upgrades)
    private VisualElement rightPage;
    private ScrollView upgradeScrollView;
    private VisualElement upgradeGrid;

    // Data
    private List<GuildUpgradeDef> allUpgrades = new List<GuildUpgradeDef>();
    private Dictionary<GuildUpgradeDef, VisualElement> upgradeCards = new Dictionary<GuildUpgradeDef, VisualElement>();

    // ═════════════════════════════════════════════
    // LIFECYCLE
    // ═════════════════════════════════════════════

    void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        BuildUI();
    }

    protected override void Start()
    {
        base.Start();

        // Subscribe to progression events
        ProgressionManager.OnStarEarned += OnStarEarned;
        ProgressionManager.OnUpgradePurchased += OnUpgradePurchased;
        GameSignals.GoldChanged += OnGoldChanged;
    }

    protected override void OnDestroy()
    {
        ProgressionManager.OnStarEarned -= OnStarEarned;
        ProgressionManager.OnUpgradePurchased -= OnUpgradePurchased;
        GameSignals.GoldChanged -= OnGoldChanged;

        base.OnDestroy();
    }

    // ═════════════════════════════════════════════
    // UI BUILDING
    // ═════════════════════════════════════════════

    protected override void BuildUI()
    {
        base.BuildUI();

        if (panel == null)
        {
            Debug.LogError("[GuildUpgradePanel] Panel is null after BuildUI!");
            return;
        }

        // Query main book elements
        leftPage = panel.Q<VisualElement>("left-page");
        rightPage = panel.Q<VisualElement>("right-page");

        // Left page (star progress)
        starsContainer = panel.Q<VisualElement>("stars-display");
        currentStarLabel = panel.Q<Label>("current-star-label");
        nextStarLabel = panel.Q<Label>("next-star-label");
        milestonesContainer = panel.Q<VisualElement>("milestones-container");

        // Right page (upgrades)
        upgradeScrollView = panel.Q<ScrollView>("upgrade-scroll");
        upgradeGrid = panel.Q<VisualElement>("upgrade-grid");

        // Close button
        var closeButton = panel.Q<Button>("close-button");
        if (closeButton != null)
            closeButton.clicked += () => UIManager.Instance.ClosePanel(this);

        // Initial visibility
        panel.style.display = DisplayStyle.None;
        panel.style.opacity = 0f;

        if (showDebugLogs)
            Debug.Log("[GuildUpgradePanel] UI built successfully");
    }

    // ═════════════════════════════════════════════
    // PANEL LIFECYCLE OVERRIDES
    // ═════════════════════════════════════════════

    protected override void OnOpenStart()
    {
        LoadUpgrades();
        UpdateLeftPage();
        BuildUpgradeCards();

        if (showDebugLogs)
            Debug.Log("[GuildUpgradePanel] Panel opened");
    }

    // ═════════════════════════════════════════════
    // DATA LOADING
    // ═════════════════════════════════════════════

    private void LoadUpgrades()
    {
        // Load all GuildUpgradeDef assets from Resources/Upgrades/
        var upgrades = Resources.LoadAll<GuildUpgradeDef>("Guild Upgrades");
        allUpgrades = new List<GuildUpgradeDef>(upgrades);

        // Sort by star requirement, then by cost
        allUpgrades.Sort((a, b) =>
        {
            int starCompare = a.starRequirement.CompareTo(b.starRequirement);
            if (starCompare != 0) return starCompare;
            return a.goldCost.CompareTo(b.goldCost);
        });

        if (showDebugLogs)
            Debug.Log($"[GuildUpgradePanel] Loaded {allUpgrades.Count} upgrades");
    }

    // ═════════════════════════════════════════════
    // LEFT PAGE (STAR PROGRESS)
    // ═════════════════════════════════════════════

    private void UpdateLeftPage()
    {
        UpdateStarDisplay();
        UpdateMilestoneList();
    }

    private void UpdateStarDisplay()
    {
        if (starsContainer == null) return;

        int currentStars = ProgressionManager.Instance.GetCurrentStars();

        // Update star visuals (★★★☆☆)
        for (int i = 1; i <= 5; i++)
        {
            var star = starsContainer.Q<VisualElement>($"star-{i}");
            if (star != null)
            {
                star.RemoveFromClassList("star-filled");
                star.RemoveFromClassList("star-empty");

                if (i <= currentStars)
                    star.AddToClassList("star-filled");
                else
                    star.AddToClassList("star-empty");
            }
        }

        // Update labels
        if (currentStarLabel != null)
            currentStarLabel.text = $"Current: {currentStars}★";

        if (nextStarLabel != null)
        {
            if (currentStars >= 5)
                nextStarLabel.text = "Max Stars Achieved!";
            else
                nextStarLabel.text = $"Progress to {currentStars + 1}★:";
        }
    }

    private void UpdateMilestoneList()
    {
        if (milestonesContainer == null) return;

        milestonesContainer.Clear();

        int currentStars = ProgressionManager.Instance.GetCurrentStars();
        if (currentStars >= 5)
        {
            // Max stars reached
            var maxLabel = new Label("All milestones complete!");
            maxLabel.AddToClassList("milestone-complete-label");
            milestonesContainer.Add(maxLabel);
            return;
        }

        int nextStar = currentStars + 1;
        var milestones = ProgressionManager.Instance.GetMilestonesForStar(nextStar);

        foreach (var milestone in milestones)
        {
            var milestoneRow = CreateMilestoneRow(milestone);
            milestonesContainer.Add(milestoneRow);
        }
    }

    private VisualElement CreateMilestoneRow(StarMilestoneDef milestone)
    {
        var row = new VisualElement();
        row.AddToClassList("milestone-row");

        // Display Name (bold title)
        if (!string.IsNullOrEmpty(milestone.displayName))
        {
            var nameLabel = new Label(milestone.displayName);
            nameLabel.AddToClassList("milestone-display-name");
            row.Add(nameLabel);
        }

        // Flavour Text (italic, muted)
        if (!string.IsNullOrEmpty(milestone.flavorText))
        {
            var flavourLabel = new Label($"\"{milestone.flavorText}\"");
            flavourLabel.AddToClassList("milestone-flavour-text");
            row.Add(flavourLabel);
        }

        // Description (technical instruction)
        var descLabel = new Label(milestone.description);
        descLabel.AddToClassList("milestone-description");
        row.Add(descLabel);

        // Progress bar container
        var progressContainer = new VisualElement();
        progressContainer.AddToClassList("milestone-progress-container");

        var progressBar = new VisualElement();
        progressBar.AddToClassList("milestone-progress-bar");

        var progressFill = new VisualElement();
        progressFill.AddToClassList("milestone-progress-fill");
        
        float progress = ProgressionManager.Instance.GetMilestoneProgress(milestone);
        progressFill.style.width = Length.Percent(progress * 100f);

        progressBar.Add(progressFill);
        progressContainer.Add(progressBar);

        // Progress text
        int current = ProgressionManager.Instance.GetMilestoneCurrentValue(milestone);
        int target = milestone.targetValue;
        
        var progressLabel = new Label($"{current}/{target}");
        progressLabel.AddToClassList("milestone-progress-text");
        progressContainer.Add(progressLabel);

        // Checkmark if complete
        if (ProgressionManager.Instance.IsMilestoneComplete(milestone))
        {
            var checkmark = new Label("✓");
            checkmark.AddToClassList("milestone-checkmark");
            progressContainer.Add(checkmark);
        }

        row.Add(progressContainer);

        return row;
    }

    // ═════════════════════════════════════════════
    // RIGHT PAGE (UPGRADE CARDS)
    // ═════════════════════════════════════════════

    private void BuildUpgradeCards()
    {
        if (upgradeGrid == null || upgradeCardTemplate == null)
        {
            Debug.LogError("[GuildUpgradePanel] upgradeGrid or upgradeCardTemplate is null!");
            return;
        }

        upgradeGrid.Clear();
        upgradeCards.Clear();

        foreach (var upgrade in allUpgrades)
        {
            var card = CreateUpgradeCard(upgrade);
            upgradeGrid.Add(card);
            upgradeCards[upgrade] = card;
        }

        if (showDebugLogs)
            Debug.Log($"[GuildUpgradePanel] Created {upgradeCards.Count} upgrade cards");
    }

    private VisualElement CreateUpgradeCard(GuildUpgradeDef upgrade)
    {
        // Clone template
        var card = upgradeCardTemplate.CloneTree().Q<VisualElement>("upgrade-card");

        // Set icon
        var icon = card.Q<VisualElement>("upgrade-icon");
        if (icon != null && upgrade.icon != null)
            icon.style.backgroundImage = new StyleBackground(upgrade.icon);

        // Set text
        card.Q<Label>("upgrade-name").text = upgrade.upgradeName;
        card.Q<Label>("upgrade-description").text = upgrade.description;
        card.Q<Label>("cost-label").text = upgrade.goldCost.ToString();

        // Show star requirement if > 0
        if (upgrade.starRequirement > 0)
        {
            var starReq = card.Q<VisualElement>("star-requirement");
            if (starReq != null)
            {
                starReq.style.display = DisplayStyle.Flex;
                card.Q<Label>("star-requirement-label").text = $"Requires {upgrade.starRequirement}★";
            }
        }

        // Hook purchase button
        var button = card.Q<Button>("purchase-button");
        if (button != null)
        {
            button.clicked += () => OnPurchaseClicked(upgrade);
        }

        // Set initial state
        UpdateCardState(card, upgrade);

        return card;
    }

    private void UpdateCardState(VisualElement card, GuildUpgradeDef upgrade)
    {
        // Clear all state classes
        card.RemoveFromClassList("upgrade-card--locked");
        card.RemoveFromClassList("upgrade-card--unavailable");
        card.RemoveFromClassList("upgrade-card--available");
        card.RemoveFromClassList("upgrade-card--owned");

        var button = card.Q<Button>("purchase-button");

        // Determine state
        if (ProgressionManager.Instance.IsUpgradeOwned(upgrade))
        {
            // OWNED
            card.AddToClassList("upgrade-card--owned");
            if (button != null)
                button.SetEnabled(false);
        }
        else if (ProgressionManager.Instance.GetCurrentStars() < upgrade.starRequirement)
        {
            // LOCKED (insufficient stars)
            card.AddToClassList("upgrade-card--locked");
            if (button != null)
                button.SetEnabled(false);
        }
        else if (!Inventory.Instance.CanAfford(upgrade.goldCost))
        {
            // UNAVAILABLE (insufficient gold)
            card.AddToClassList("upgrade-card--unavailable");
            
            int needed = upgrade.goldCost - Inventory.Instance.Gold;
            var insufficientLabel = card.Q<Label>("insufficient-gold-label");
            if (insufficientLabel != null)
                insufficientLabel.text = $"Need {needed}g";

            if (button != null)
                button.SetEnabled(false);
        }
        else
        {
            // AVAILABLE (can purchase)
            card.AddToClassList("upgrade-card--available");
            if (button != null)
                button.SetEnabled(true);
        }
    }

    // ═════════════════════════════════════════════
    // PURCHASE LOGIC
    // ═════════════════════════════════════════════

    private void OnPurchaseClicked(GuildUpgradeDef upgrade)
    {
        if (ProgressionManager.Instance.PurchaseUpgrade(upgrade))
        {
            if (showDebugLogs)
                Debug.Log($"[GuildUpgradePanel] Purchased: {upgrade.upgradeName}");

            // Card state will update via OnUpgradePurchased event
        }
        else
        {
            if (showDebugLogs)
                Debug.LogWarning($"[GuildUpgradePanel] Failed to purchase: {upgrade.upgradeName}");
        }
    }

    // ═════════════════════════════════════════════
    // EVENT HANDLERS
    // ═════════════════════════════════════════════

    private void OnStarEarned(int newStar)
    {
        if (State != PanelState.Open) return;

        // Update left page (star display + milestones)
        UpdateLeftPage();

        // Update all card states (new upgrades may unlock)
        foreach (var kvp in upgradeCards)
        {
            UpdateCardState(kvp.Value, kvp.Key);
        }

        if (showDebugLogs)
            Debug.Log($"[GuildUpgradePanel] Star earned: {newStar}★");
    }

    private void OnUpgradePurchased(GuildUpgradeDef upgrade)
    {
        if (State != PanelState.Open) return;

        // Update the purchased card
        if (upgradeCards.TryGetValue(upgrade, out var card))
        {
            UpdateCardState(card, upgrade);
        }

        // Update milestones (purchase may complete milestone)
        UpdateMilestoneList();

        if (showDebugLogs)
            Debug.Log($"[GuildUpgradePanel] Upgrade purchased: {upgrade.upgradeName}");
    }

    private void OnGoldChanged(int newTotal)
    {
        if (State != PanelState.Open) return;

        // Update all cards (affordability changed)
        foreach (var kvp in upgradeCards)
        {
            UpdateCardState(kvp.Value, kvp.Key);
        }
    }
}