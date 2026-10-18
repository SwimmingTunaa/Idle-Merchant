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
    private MilestoneTracker coinShower;   // HUD tracker owns the UI coin flight to the gold counter

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
        coinShower = FindAnyObjectByType<MilestoneTracker>();
        progressEvents = new MilestoneProgressEvents(
            onProgressChanged: RefreshMilestoneProgress,
            onFullRebuild: OnProgressionChanged
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
        coinShower = null;
    }

    // Star earned / upgrade bought: rebuild the milestone tier and re-gate every upgrade card.
    private void OnProgressionChanged()
    {
        UpdateLeftPage();
        OnGoldChanged(0);
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
        foreach (var milestone in ProgressionManager.Instance.GetMilestonesForStar(nextStar))
            milestonesContainer.Add(CreateMilestoneRow(milestone));
        RefreshMilestoneProgress();
    }

    // Same card + claim flow as the HUD tracker (MilestoneTracker.BindCard/RefreshCard); claims fly coins
    // through the tracker's UI shower, else ProgressionManager's world-burst/direct fallback banks the gold.
    private VisualElement CreateMilestoneRow(StarMilestoneDef milestone)
    {
        var card = milestoneCardTemplate.CloneTree();
        card.userData = milestone;
        var row = card.Q<VisualElement>("card-root");

        MilestoneTracker.BindCard(row, milestone,
            (source, count, land) => coinShower != null && coinShower.LaunchCoins(source, count, land));

        var flavour = row.Q<Label>("card-flavour");
        bool hasFlavour = !string.IsNullOrEmpty(milestone.flavorText);
        flavour.style.display = hasFlavour ? DisplayStyle.Flex : DisplayStyle.None;
        if (hasFlavour) flavour.text = milestone.flavorText;

        return card;
    }

    private void RefreshMilestoneProgress()
    {
        if (milestonesContainer == null) return;
        var pm = ProgressionManager.Instance;
        int claimed = 0, total = 0;
        foreach (var card in milestonesContainer.Children())
        {
            if (card.userData is not StarMilestoneDef def) continue;
            MilestoneTracker.RefreshCard(card.Q<VisualElement>("card-root"), def);
            total++;
            if (pm.IsMilestoneClaimed(def)) claimed++;
        }

        int stars = pm.GetCurrentStars();
        if (nextStarLabel != null && stars < 5)
            nextStarLabel.text = $"Progress to {stars + 1}★ - {claimed}/{total} claimed";
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
        card.style.width = Length.Percent(48.5f);   // the grid sizes the TemplateContainer, not card-root

        if (upgrade.icon != null)
            card.Q<VisualElement>("upgrade-icon").style.backgroundImage = new StyleBackground(upgrade.icon);

        card.Q<Label>("upgrade-name").text = upgrade.upgradeName;
        card.Q<Label>("upgrade-description").text = upgrade.description;
        card.Q<Label>("purchase-label").text = AnimatedNumberUITK.Abbrev(upgrade.goldCost);
        card.Q<Label>("lock-label").text = $"Requires {upgrade.starRequirement}";
        card.Q<Button>("purchase-button").clicked += () => OnPurchaseClicked(upgrade);

        UpdateCardState(card, upgrade);
        return card;
    }

    // Exactly one bottom slot: Owned pill / "Requires N★" lock pill / buy button (disabled while unaffordable).
    private void UpdateCardState(VisualElement card, GuildUpgradeDef upgrade)
    {
        bool owned = ProgressionManager.Instance.IsUpgradeOwned(upgrade);
        bool locked = !owned && ProgressionManager.Instance.GetCurrentStars() < upgrade.starRequirement;

        card.Q<VisualElement>("card-root").EnableInClassList("guild-card--locked", locked);
        card.Q<VisualElement>("owned-pill").style.display = owned ? DisplayStyle.Flex : DisplayStyle.None;
        card.Q<VisualElement>("lock-pill").style.display = locked ? DisplayStyle.Flex : DisplayStyle.None;

        var button = card.Q<Button>("purchase-button");
        button.style.display = owned || locked ? DisplayStyle.None : DisplayStyle.Flex;
        button.SetEnabled(Inventory.Instance != null && Inventory.Instance.CanAfford(upgrade.goldCost));
    }

    private void OnPurchaseClicked(GuildUpgradeDef upgrade)
    {
        SfxManager.Instance?.Play(SfxId.UiTick);
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
