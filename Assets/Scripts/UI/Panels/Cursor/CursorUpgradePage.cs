using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Cursor page — cursor stats on the left, click upgrades on the right.
/// Implements IBookPage; lives inside BookPanelController as tab 5.
/// Cards are cloned from CursorUpgradeCard.uxml and bound from a small
/// in-code upgrade list (formulas live on ClickerManager).
/// </summary>
public class CursorUpgradePage : MonoBehaviour, IBookPage
{
    [Tooltip("Card template (CursorUpgradeCard.uxml): icon + name/stat column + full-width buy button or lock pill.")]
    [SerializeField] private VisualTreeAsset cardTemplate;

    [Header("Upgrade Icons")]
    [SerializeField] private Sprite iconClickDamage;
    [SerializeField] private Sprite iconPickupRadius;
    [SerializeField] private Sprite iconDamageAOE;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    public string PageTitle => "Cursor";

    private ClickerManager clicker;

    // Left page
    private Label statDamage;
    private Label statRadius;
    private Label statAOE;

    // Per-card bindings, re-run on every refresh
    private readonly List<System.Action> cardRefreshers = new List<System.Action>();

    void Awake() => hideFlags = HideFlags.HideInInspector;

    // ═════════════════════════════════════════════
    // IBOOK PAGE
    // ═════════════════════════════════════════════

    public void OnPageShown(VisualElement leftPage, VisualElement rightPage)
    {
        clicker = FindAnyObjectByType<ClickerManager>();

        statDamage = leftPage.Q<Label>("stat-damage");
        statRadius = leftPage.Q<Label>("stat-radius");
        statAOE = leftPage.Q<Label>("stat-aoe");

        BuildCards(rightPage.Q<VisualElement>("cursor-upgrade-list"));

        GameSignals.GoldChanged += OnGoldChanged;
        GameSignals.OnClickUpgradePurchased += OnUpgradePurchased;
        ProgressionManager.OnStarEarned += OnStarEarned;

        Refresh();

        if (showDebugLogs) Debug.Log("[CursorUpgradePage] Page shown");
    }

    public void OnPageHidden()
    {
        GameSignals.GoldChanged -= OnGoldChanged;
        GameSignals.OnClickUpgradePurchased -= OnUpgradePurchased;
        ProgressionManager.OnStarEarned -= OnStarEarned;

        statDamage = null;
        statRadius = null;
        statAOE = null;
        cardRefreshers.Clear();
    }

    private void OnGoldChanged(int _) => Refresh();
    private void OnUpgradePurchased(int _) => Refresh();
    private void OnStarEarned(int _) => Refresh();

    // ═════════════════════════════════════════════
    // CARDS
    // ═════════════════════════════════════════════

    private void BuildCards(VisualElement list)
    {
        if (list == null || cardTemplate == null) return;
        list.Clear();
        cardRefreshers.Clear();

        // Click Damage — live
        AddBuyableCard(list, "Click Damage", iconClickDamage,
            stat: () => $"{clicker.EffectiveClickDamage:0.#} ➜ <color=#376C2D>{clicker.NextClickDamage:0.#} dmg</color>",
            cost: () => clicker.ClickUpgradeCost,
            maxed: () => false,
            buy: () => clicker.TryBuyClickUpgrade());

        // Pickup Radius — live
        AddBuyableCard(list, "Pickup Radius", iconPickupRadius,
            stat: () => clicker.PickupRadiusMaxed
                ? $"{clicker.LootPickupRadius:0.0} radius (max)"
                : $"{clicker.LootPickupRadius:0.0} ➜ <color=#376C2D>{clicker.NextLootPickupRadius:0.0} radius</color>",
            cost: () => clicker.RadiusUpgradeCost,
            maxed: () => clicker.PickupRadiusMaxed,
            buy: () => clicker.TryBuyRadiusUpgrade());

        // Damage AOE — star gated: lock pill until unlocked, then a live buy card
        var aoe = NewCard(list, "Damage AOE", iconDamageAOE);
        aoe.Q<Label>("card-substat").text = "clicks damage nearby mobs";
        aoe.Q<Label>("card-lock-label").text = $"Unlocks at {clicker.AOEStarRequirement}";
        var aoeRoot = aoe.Q<VisualElement>("card-root");
        var aoeStat = aoe.Q<Label>("card-stat");
        var aoePill = aoe.Q<VisualElement>("card-lockpill");
        var aoeBuy = aoe.Q<Button>("card-buy");
        var aoeBuyLabel = aoe.Q<Label>("card-buy-label");
        var aoeCoin = aoe.Q<VisualElement>("card-buy-coin");
        aoeBuy.clicked += () => clicker.TryBuyAOEUpgrade();

        cardRefreshers.Add(() =>
        {
            bool unlocked = clicker.AOEUnlocked;
            aoeRoot.EnableInClassList("cursor-card--locked", !unlocked);
            aoePill.style.display = unlocked ? DisplayStyle.None : DisplayStyle.Flex;
            aoeBuy.style.display = unlocked ? DisplayStyle.Flex : DisplayStyle.None;
            aoeStat.style.display = unlocked ? DisplayStyle.Flex : DisplayStyle.None;
            if (!unlocked) return;

            if (clicker.AOEMaxed)
            {
                aoeStat.text = $"{clicker.EffectiveDamageRadius:0.0} radius (max)";
                aoeBuyLabel.text = "Maxed";
                aoeCoin.style.display = DisplayStyle.None;
                aoeBuy.SetEnabled(false);
                return;
            }
            aoeStat.text = clicker.AOELevel == 0
                ? $"single ➜ <color=#376C2D>{clicker.NextDamageRadius:0.0} radius</color>"
                : $"{clicker.EffectiveDamageRadius:0.0} ➜ <color=#376C2D>{clicker.NextDamageRadius:0.0} radius</color>";
            aoeCoin.style.display = DisplayStyle.Flex;
            int c = clicker.AOEUpgradeCost;
            aoeBuyLabel.text = AnimatedNumberUITK.Abbrev(c);
            aoeBuy.SetEnabled(Inventory.Instance != null && Inventory.Instance.Gold >= c);
        });
    }

    /// <summary>Clone the template into the list and bind the shared bits (icon, name).</summary>
    private VisualElement NewCard(VisualElement list, string name, Sprite icon)
    {
        var card = cardTemplate.CloneTree();
        card.style.width = Length.Percent(48.5f);   // the grid sizes the TemplateContainer, not card-root
        card.Q<Label>("card-name").text = name;

        var iconSlot = card.Q<VisualElement>("card-icon");
        if (icon != null)
        {
            iconSlot.style.backgroundImage = new StyleBackground(icon);
            iconSlot.style.borderLeftWidth = 0;
            iconSlot.style.borderRightWidth = 0;
            iconSlot.style.borderTopWidth = 0;
            iconSlot.style.borderBottomWidth = 0;
        }

        list.Add(card);
        return card;
    }

    private void AddBuyableCard(VisualElement list, string name, Sprite icon,
        System.Func<string> stat, System.Func<int> cost, System.Func<bool> maxed, System.Func<bool> buy)
    {
        var card = NewCard(list, name, icon);
        card.Q<Label>("card-substat").style.display = DisplayStyle.None;
        card.Q<VisualElement>("card-lockpill").style.display = DisplayStyle.None;

        var statLabel = card.Q<Label>("card-stat");
        var buyButton = card.Q<Button>("card-buy");
        var buyLabel = card.Q<Label>("card-buy-label");
        var coin = card.Q<VisualElement>("card-buy-coin");

        buyButton.clicked += () => buy();

        cardRefreshers.Add(() =>
        {
            statLabel.text = stat();
            if (maxed())
            {
                buyLabel.text = "Maxed";
                coin.style.display = DisplayStyle.None;
                buyButton.SetEnabled(false);
                return;
            }
            coin.style.display = DisplayStyle.Flex;
            int c = cost();
            buyLabel.text = AnimatedNumberUITK.Abbrev(c);
            buyButton.SetEnabled(Inventory.Instance != null && Inventory.Instance.Gold >= c);
        });
    }

    // ═════════════════════════════════════════════
    // REFRESH
    // ═════════════════════════════════════════════

    private void Refresh()
    {
        if (clicker == null) return;

        if (statDamage != null) statDamage.text = clicker.EffectiveClickDamage.ToString("0.#");
        if (statRadius != null) statRadius.text = clicker.LootPickupRadius.ToString("0.0");
        if (statAOE != null)
        {
            statAOE.text = !clicker.AOEUnlocked ? "locked"
                : clicker.AOELevel == 0 ? "off"
                : clicker.EffectiveDamageRadius.ToString("0.0");
            statAOE.EnableInClassList("cursor-stat-value--muted", !clicker.AOEUnlocked || clicker.AOELevel == 0);
        }

        foreach (var refresh in cardRefreshers) refresh();
    }
}
