using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Cursor page — cursor stats on the left, click upgrades on the right.
/// Implements IBookPage; lives inside BookPanelController as tab 5.
/// Cards are built in code (no template asset) — only Click Damage is buyable;
/// Pickup Radius and Damage AOE show as locked previews until those systems exist.
/// </summary>
public class CursorUpgradePage : MonoBehaviour, IBookPage
{
    [Tooltip("9-slice pixel frame for upgrade cards (box-outline sprite). Set from code because " +
             "the USS importer silently dropped the url() reference for this sheet.")]
    [SerializeField] private Sprite cardFrame;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    public string PageTitle => "Cursor";

    private ClickerManager clicker;

    // Left page
    private Label statDamage;
    private Label statRadius;

    // Right page — damage card live elements
    private Label dmgLevelLabel;
    private Label dmgStatLabel;
    private Button dmgBuyButton;

    void Awake() => hideFlags = HideFlags.HideInInspector;

    // ═════════════════════════════════════════════
    // IBOOK PAGE
    // ═════════════════════════════════════════════

    public void OnPageShown(VisualElement leftPage, VisualElement rightPage)
    {
        clicker = FindAnyObjectByType<ClickerManager>();

        statDamage = leftPage.Q<Label>("stat-damage");
        statRadius = leftPage.Q<Label>("stat-radius");

        BuildCards(rightPage.Q<VisualElement>("cursor-upgrade-list"));

        GameSignals.GoldChanged += OnGoldChanged;
        GameSignals.OnClickUpgradePurchased += OnUpgradePurchased;

        Refresh();

        if (showDebugLogs) Debug.Log("[CursorUpgradePage] Page shown");
    }

    public void OnPageHidden()
    {
        GameSignals.GoldChanged -= OnGoldChanged;
        GameSignals.OnClickUpgradePurchased -= OnUpgradePurchased;

        statDamage = null;
        statRadius = null;
        dmgLevelLabel = null;
        dmgStatLabel = null;
        dmgBuyButton = null;
    }

    private void OnGoldChanged(int _) => Refresh();
    private void OnUpgradePurchased(int _) => Refresh();

    // ═════════════════════════════════════════════
    // CARDS
    // ═════════════════════════════════════════════

    private void BuildCards(VisualElement list)
    {
        if (list == null) return;
        list.Clear();

        // Click Damage — live
        var dmgCard = NewCard("Click Damage", locked: false);
        var info = dmgCard.Q<VisualElement>("card-info");
        dmgLevelLabel = AddLabel(info, "cursor-card-level");
        dmgStatLabel = AddLabel(info, "cursor-card-stat");

        dmgBuyButton = new Button(OnBuyDamageClicked);
        dmgBuyButton.AddToClassList("btn-success");
        dmgBuyButton.AddToClassList("cursor-card-buy");
        dmgCard.Add(dmgBuyButton);
        list.Add(dmgCard);

        // Pickup Radius — locked preview (no upgrade path built yet)
        var radiusCard = NewCard("Pickup Radius", locked: true);
        AddLabel(radiusCard.Q<VisualElement>("card-info"), "cursor-card-substat").text = "loot vacuum area on click";
        radiusCard.Add(LockPill("Coming soon"));
        list.Add(radiusCard);

        // Damage AOE — locked preview, star gated per GDD mouse upgrades
        var aoeCard = NewCard("Damage AOE", locked: true);
        AddLabel(aoeCard.Q<VisualElement>("card-info"), "cursor-card-substat").text = "clicks damage nearby mobs";
        aoeCard.Add(LockPill("Unlocks at 2★"));
        list.Add(aoeCard);
    }

    private VisualElement NewCard(string name, bool locked)
    {
        var card = new VisualElement();
        card.AddToClassList("cursor-card");
        if (locked) card.AddToClassList("cursor-card--locked");

        if (cardFrame != null)
        {
            card.style.backgroundImage = new StyleBackground(cardFrame);
            card.style.unitySliceLeft = 1;
            card.style.unitySliceTop = 1;
            card.style.unitySliceRight = 1;
            card.style.unitySliceBottom = 1;
            card.style.unitySliceScale = 5f;
        }

        var info = new VisualElement { name = "card-info" };
        info.AddToClassList("cursor-card-info");

        var nameLabel = new Label(name);
        nameLabel.AddToClassList("cursor-card-name");
        info.Add(nameLabel);

        card.Add(info);
        return card;
    }

    private static Label AddLabel(VisualElement parent, string className)
    {
        var label = new Label();
        label.AddToClassList(className);
        parent.Add(label);
        return label;
    }

    private static Label LockPill(string text)
    {
        var pill = new Label(text);
        pill.AddToClassList("btn-disabled");
        pill.AddToClassList("cursor-card-lockpill");
        return pill;
    }

    private void OnBuyDamageClicked()
    {
        if (clicker == null) return;
        clicker.TryBuyClickUpgrade();
        // Refresh happens via OnClickUpgradePurchased / GoldChanged
    }

    // ═════════════════════════════════════════════
    // REFRESH
    // ═════════════════════════════════════════════

    private void Refresh()
    {
        if (clicker == null) return;

        if (statDamage != null) statDamage.text = clicker.EffectiveClickDamage.ToString("0.#");
        if (statRadius != null) statRadius.text = clicker.LootPickupRadius.ToString("0.0");

        if (dmgLevelLabel != null) dmgLevelLabel.text = $"Level {clicker.ClickDamageLevel}";
        if (dmgStatLabel != null)
            dmgStatLabel.text = $"{clicker.EffectiveClickDamage:0.#} ➜ {clicker.NextClickDamage:0.#} dmg";

        if (dmgBuyButton != null)
        {
            int cost = clicker.ClickUpgradeCost;
            dmgBuyButton.text = $"Upgrade  {AnimatedNumberUITK.Abbrev(cost)}g";
            bool affordable = Inventory.Instance != null && Inventory.Instance.Gold >= cost;
            dmgBuyButton.SetEnabled(affordable);
        }
    }
}
