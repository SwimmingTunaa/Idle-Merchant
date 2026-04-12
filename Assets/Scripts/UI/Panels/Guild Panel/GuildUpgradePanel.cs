using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Guild page — star progress on the left, purchasable upgrades on the right.
/// Implements IBookPage; lives inside BookPanelController as tab 1.
/// </summary>
public class GuildUpgradePanel : MonoBehaviour, IBookPage
{
    [Header("UXML References")]
    [SerializeField] private VisualTreeAsset upgradeCardTemplate;
    [SerializeField] private VisualTreeAsset milestoneCardTemplate;

    // Left page elements
    private VisualElement starsContainer;
    private VisualElement milestonesContainer;
    private Label currentStarLabel;
    private Label nextStarLabel;

    // Right page elements
    private ScrollView upgradeScrollView;
    private VisualElement upgradeGrid;

    // Data
    private List<GuildUpgradeDef> allUpgrades = new List<GuildUpgradeDef>();
    private Dictionary<GuildUpgradeDef, VisualElement> upgradeCards = new Dictionary<GuildUpgradeDef, VisualElement>();
    private MilestoneProgressEvents progressEvents;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    public string PageTitle => "Guild";

    void Awake() => hideFlags = HideFlags.HideInInspector;

    // ═════════════════════════════════════════════
    // IBOOK PAGE
    // ═════════════════════════════════════════════

    public void OnPageShown(VisualElement leftPage, VisualElement rightPage)
    {
        // Query left page
        starsContainer = leftPage.Q<VisualElement>("stars-display");
        currentStarLabel = leftPage.Q<Label>("current-star-label");
        nextStarLabel = leftPage.Q<Label>("next-star-label");
        milestonesContainer = leftPage.Q<VisualElement>("milestones-container");

        // Query right page
        upgradeScrollView = rightPage.Q<ScrollView>("upgrade-scroll");
        upgradeGrid = rightPage.Q<VisualElement>("upgrade-grid");

        // Subscribe signals
        progressEvents = new MilestoneProgressEvents(
            onProgressChanged: RefreshMilestoneProgress,
            onFullRebuild: UpdateLeftPage
        );
        progressEvents.Subscribe();
        GameSignals.GoldChanged += OnGoldChanged;

        // Populate
        LoadUpgrades();
        UpdateLeftPage();
        BuildUpgradeCards();

        if (showDebugLogs)
            Debug.Log("[GuildUpgradePanel] Page shown");
    }

    public void OnPageHidden()
    {
        progressEvents?.Unsubscribe();
        GameSignals.GoldChanged -= OnGoldChanged;

        starsContainer = null;
        milestonesContainer = null;
        currentStarLabel = null;
        nextStarLabel = null;
        upgradeScrollView = null;
        upgradeGrid = null;
        upgradeCards.Clear();
    }

    // ═════════════════════════════════════════════
    // DATA LOADING
    // ═════════════════════════════════════════════

    private void LoadUpgrades()
    {
        var upgrades = Resources.LoadAll<GuildUpgradeDef>("Guild Upgrades");
        allUpgrades = new List<GuildUpgradeDef>(upgrades);
        allUpgrades.Sort((a, b) =>
        {
            int starCompare = a.starRequirement.CompareTo(b.starRequirement);
            return starCompare != 0 ? starCompare : a.goldCost.CompareTo(b.goldCost);
        });
    }

    // ═════════════════════════════════════════════
    // LEFT PAGE
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
        for (int i = 1; i <= 5; i++)
        {
            var star = starsContainer.Q<VisualElement>($"star-{i}");
            if (star != null)
            {
                star.EnableInClassList("star-filled", i <= currentStars);
                star.EnableInClassList("star-empty", i > currentStars);
            }
        }

        if (currentStarLabel != null)
            currentStarLabel.text = $"Current: {currentStars}★";

        if (nextStarLabel != null)
            nextStarLabel.text = currentStars >= 5 ? "Max Stars Achieved!" : $"Progress to {currentStars + 1}★:";
    }

    private void UpdateMilestoneList()
    {
        if (milestonesContainer == null) return;
        milestonesContainer.Clear();

        int currentStars = ProgressionManager.Instance.GetCurrentStars();
        if (currentStars >= 5)
        {
            var maxLabel = new Label("All milestones complete!");
            maxLabel.AddToClassList("milestone-complete-label");
            milestonesContainer.Add(maxLabel);
            return;
        }

        int nextStar = currentStars + 1;
        var milestones = ProgressionManager.Instance.GetMilestonesForStar(nextStar);
        foreach (var milestone in milestones)
            milestonesContainer.Add(CreateMilestoneRow(milestone));
    }

    private VisualElement CreateMilestoneRow(StarMilestoneDef milestone)
    {
        var card = milestoneCardTemplate.CloneTree();
        card.userData = milestone;

        var nameLabel = card.Q<Label>("milestone-name");
        if (nameLabel != null)
            nameLabel.text = !string.IsNullOrEmpty(milestone.displayName) ? milestone.displayName : milestone.description;

        var flavourLabel = card.Q<Label>("milestone-flavour");
        if (flavourLabel != null)
        {
            bool hasFlavour = !string.IsNullOrEmpty(milestone.flavorText);
            flavourLabel.style.display = hasFlavour ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasFlavour) flavourLabel.text = milestone.flavorText;
        }

        var descLabel = card.Q<Label>("milestone-description");
        if (descLabel != null) descLabel.text = milestone.description;

        int current = ProgressionManager.Instance.GetMilestoneCurrentValue(milestone);
        int target = milestone.targetValue;
        float progress = ProgressionManager.Instance.GetMilestoneProgress(milestone);
        bool complete = ProgressionManager.Instance.IsMilestoneComplete(milestone);

        var fill = card.Q<VisualElement>("progress-fill");
        if (fill != null)
        {
            fill.style.width = Length.Percent(Mathf.Clamp01(progress) * 100f);
            fill.EnableInClassList("progress-fill--success", complete);
        }

        var progressText = card.Q<Label>("progress-label");
        if (progressText != null) progressText.text = $"{current}/{target}";

        if (complete)
        {
            card.AddToClassList("milestone-card--complete");
            card.Q<Label>("milestone-checkmark")?.AddToClassList("milestone-checkmark--visible");
        }

        return card;
    }

    private void RefreshMilestoneProgress()
    {
        if (milestonesContainer == null) return;
        foreach (var card in milestonesContainer.Children())
        {
            var def = card.userData as StarMilestoneDef;
            if (def == null) continue;

            bool complete = ProgressionManager.Instance.IsMilestoneComplete(def);
            int current = ProgressionManager.Instance.GetMilestoneCurrentValue(def);
            float progress = ProgressionManager.Instance.GetMilestoneProgress(def);

            var fill = card.Q<VisualElement>("progress-fill");
            if (fill != null)
            {
                fill.style.width = Length.Percent(Mathf.Clamp01(progress) * 100f);
                fill.EnableInClassList("progress-fill--success", complete);
            }

            var progressLabel = card.Q<Label>("progress-label");
            if (progressLabel != null) progressLabel.text = $"{current}/{def.targetValue}";
            card.Q<Label>("milestone-checkmark")?.EnableInClassList("milestone-checkmark--visible", complete);
            card.EnableInClassList("milestone-card--complete", complete);
        }
    }

    // ═════════════════════════════════════════════
    // RIGHT PAGE
    // ═════════════════════════════════════════════

    private void BuildUpgradeCards()
    {
        if (upgradeGrid == null || upgradeCardTemplate == null) return;
        upgradeGrid.Clear();
        upgradeCards.Clear();

        foreach (var upgrade in allUpgrades)
        {
            var card = CreateUpgradeCard(upgrade);
            upgradeGrid.Add(card);
            upgradeCards[upgrade] = card;
        }
    }

    private VisualElement CreateUpgradeCard(GuildUpgradeDef upgrade)
    {
        var card = upgradeCardTemplate.CloneTree();

        var icon = card.Q<VisualElement>("upgrade-icon");
        if (icon != null && upgrade.icon != null)
            icon.style.backgroundImage = new StyleBackground(upgrade.icon);

        card.Q<Label>("upgrade-name").text = upgrade.upgradeName;
        card.Q<Label>("upgrade-description").text = upgrade.description;
        card.Q<Label>("cost-label").text = upgrade.goldCost.ToString();

        if (upgrade.starRequirement > 0)
        {
            var starReq = card.Q<VisualElement>("star-requirement");
            if (starReq != null)
            {
                starReq.style.display = DisplayStyle.Flex;
                card.Q<Label>("star-requirement-label").text = $"Requires {upgrade.starRequirement}★";
            }
        }

        var button = card.Q<Button>("purchase-button");
        if (button != null)
            button.clicked += () => OnPurchaseClicked(upgrade);

        UpdateCardState(card, upgrade);
        return card;
    }

    private void UpdateCardState(VisualElement card, GuildUpgradeDef upgrade)
    {
        card.RemoveFromClassList("upgrade-card--locked");
        card.RemoveFromClassList("upgrade-card--unavailable");
        card.RemoveFromClassList("upgrade-card--available");
        card.RemoveFromClassList("upgrade-card--owned");

        var button = card.Q<Button>("purchase-button");

        if (ProgressionManager.Instance.IsUpgradeOwned(upgrade))
        {
            card.AddToClassList("upgrade-card--owned");
            button?.SetEnabled(false);
        }
        else if (ProgressionManager.Instance.GetCurrentStars() < upgrade.starRequirement)
        {
            card.AddToClassList("upgrade-card--locked");
            button?.SetEnabled(false);
        }
        else if (!Inventory.Instance.CanAfford(upgrade.goldCost))
        {
            card.AddToClassList("upgrade-card--unavailable");
            int needed = upgrade.goldCost - Inventory.Instance.Gold;
            var insufficientLabel = card.Q<Label>("insufficient-gold-label");
            if (insufficientLabel != null) insufficientLabel.text = $"Need {needed}g";
            button?.SetEnabled(false);
        }
        else
        {
            card.AddToClassList("upgrade-card--available");
            button?.SetEnabled(true);
        }
    }

    private void OnPurchaseClicked(GuildUpgradeDef upgrade)
    {
        ProgressionManager.Instance.PurchaseUpgrade(upgrade);
    }

    // ═════════════════════════════════════════════
    // SIGNALS
    // ═════════════════════════════════════════════

    private void OnGoldChanged(int newTotal)
    {
        foreach (var kvp in upgradeCards)
            UpdateCardState(kvp.Value, kvp.Key);
    }
}
