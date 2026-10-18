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
    [SerializeField] private Sprite iconAutoClick;
    [SerializeField] private Sprite iconAutoCollect;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    public string PageTitle => "Cursor";

    private ClickerManager clicker;

    // Left page
    private Label statDamage;
    private Label statRadius;
    private Label statAOE;
    private Label statAutoClick;
    private Label statAutoCollect;

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
        statAutoClick = leftPage.Q<Label>("stat-autoclick");
        statAutoCollect = leftPage.Q<Label>("stat-autoloot");

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
        statAutoClick = null;
        statAutoCollect = null;
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

        // Star-gated cards: lock pill until unlocked, then a live buy card
        AddBuyableCard(list, "Damage AOE", iconDamageAOE,
            stat: () => clicker.AOEMaxed ? $"{clicker.EffectiveDamageRadius:0.0} radius (max)"
                : clicker.AOELevel == 0 ? $"single ➜ <color=#376C2D>{clicker.NextDamageRadius:0.0} radius</color>"
                : $"{clicker.EffectiveDamageRadius:0.0} ➜ <color=#376C2D>{clicker.NextDamageRadius:0.0} radius</color>",
            cost: () => clicker.AOEUpgradeCost,
            maxed: () => clicker.AOEMaxed,
            buy: () => clicker.TryBuyAOEUpgrade(),
            substat: "clicks damage nearby mobs",
            starRequirement: clicker.AOEStarRequirement,
            unlocked: () => clicker.AOEUnlocked);

        AddBuyableCard(list, "Auto Click", iconAutoClick,
            stat: () => IntervalStat(clicker.AutoClickLevel, clicker.AutoClickMaxed, clicker.AutoClickInterval, clicker.NextAutoClickInterval),
            cost: () => clicker.AutoClickUpgradeCost,
            maxed: () => clicker.AutoClickMaxed,
            buy: () => clicker.TryBuyAutoClickUpgrade(),
            substat: "auto-clicks a random mob",
            starRequirement: clicker.AutoClickStarRequirement,
            unlocked: () => clicker.AutoClickUnlocked);

        AddBuyableCard(list, "Auto Collect", iconAutoCollect,
            stat: () => IntervalStat(clicker.AutoCollectLevel, clicker.AutoCollectMaxed, clicker.AutoCollectInterval, clicker.NextAutoCollectInterval),
            cost: () => clicker.AutoCollectUpgradeCost,
            maxed: () => clicker.AutoCollectMaxed,
            buy: () => clicker.TryBuyAutoCollectUpgrade(),
            substat: "auto-collects nearby loot",
            starRequirement: clicker.AutoCollectStarRequirement,
            unlocked: () => clicker.AutoCollectUnlocked);
    }

    /// <summary>Automation ladder stat line: "off ➜ every 3.0s", "3.0s ➜ 2.9s", "every 1.0s (max)".</summary>
    private static string IntervalStat(int level, bool maxed, float interval, float next) =>
        maxed ? $"every {interval:0.0}s (max)"
        : level == 0 ? $"off ➜ <color=#376C2D>every {next:0.0}s</color>"
        : $"{interval:0.0}s ➜ <color=#376C2D>{next:0.0}s</color>";

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

    /// <summary>Card with a stat line and a buy button. Pass unlocked (+ starRequirement) for a star-gated
    /// card that shows a lock pill until the gate opens; substat null hides the substat line.</summary>
    private void AddBuyableCard(VisualElement list, string name, Sprite icon,
        System.Func<string> stat, System.Func<int> cost, System.Func<bool> maxed, System.Func<bool> buy,
        string substat = null, int starRequirement = 0, System.Func<bool> unlocked = null)
    {
        var card = NewCard(list, name, icon);
        var substatLabel = card.Q<Label>("card-substat");
        if (substat != null) substatLabel.text = substat;
        else substatLabel.style.display = DisplayStyle.None;
        card.Q<Label>("card-lock-label").text = $"Unlocks at {starRequirement}";

        var root = card.Q<VisualElement>("card-root");
        var pill = card.Q<VisualElement>("card-lockpill");
        var statLabel = card.Q<Label>("card-stat");
        var buyButton = card.Q<Button>("card-buy");
        var buyLabel = card.Q<Label>("card-buy-label");
        var coin = card.Q<VisualElement>("card-buy-coin");

        buyButton.clicked += () => { SfxManager.Instance?.Play(SfxId.UiTick); buy(); };

        cardRefreshers.Add(() =>
        {
            bool open = unlocked == null || unlocked();
            root.EnableInClassList("cursor-card--locked", !open);
            pill.style.display = open ? DisplayStyle.None : DisplayStyle.Flex;
            buyButton.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            statLabel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!open) return;

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
        SetGatedStat(statAOE, clicker.AOEUnlocked, clicker.AOELevel, clicker.EffectiveDamageRadius.ToString("0.0"));
        SetGatedStat(statAutoClick, clicker.AutoClickUnlocked, clicker.AutoClickLevel, $"{clicker.AutoClickInterval:0.0}s");
        SetGatedStat(statAutoCollect, clicker.AutoCollectUnlocked, clicker.AutoCollectLevel, $"{clicker.AutoCollectInterval:0.0}s");

        foreach (var refresh in cardRefreshers) refresh();
    }

    /// <summary>Left-page stat for a star-gated ladder: "locked" / "off" (muted) or the live value.</summary>
    private static void SetGatedStat(Label label, bool unlocked, int level, string value)
    {
        if (label == null) return;
        label.text = !unlocked ? "locked" : level == 0 ? "off" : value;
        label.EnableInClassList("cursor-stat-value--muted", !unlocked || level == 0);
    }
}
