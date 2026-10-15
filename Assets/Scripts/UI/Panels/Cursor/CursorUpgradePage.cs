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

    [Header("Upgrade Icons")]
    [SerializeField] private Sprite iconClickDamage;
    [SerializeField] private Sprite iconPickupRadius;
    [SerializeField] private Sprite iconDamageAOE;

    [Tooltip("Guild star (Star_active) shown in star-gated lock pills.")]
    [SerializeField] private Sprite starIcon;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    public string PageTitle => "Cursor";

    private ClickerManager clicker;

    // Left page
    private Label statDamage;
    private Label statRadius;

    // Right page — damage card live elements
    private Label dmgStatLabel;
    private Button dmgBuyButton;
    private Label dmgBuyLabel;

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
        dmgStatLabel = null;
        dmgBuyButton = null;
        dmgBuyLabel = null;
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
        var dmgCard = NewCard("Click Damage", locked: false, icon: iconClickDamage);
        dmgStatLabel = AddLabel(dmgCard.Q<VisualElement>("card-info"), "cursor-card-stat");

        dmgBuyButton = new Button(OnBuyDamageClicked);
        dmgBuyButton.AddToClassList("btn-success");
        dmgBuyButton.AddToClassList("cursor-card-buy");
        dmgBuyLabel = new Label();
        dmgBuyLabel.AddToClassList("btn-label");
        dmgBuyButton.Add(dmgBuyLabel);
        var coin = new VisualElement();
        coin.AddToClassList("btn-coin");    // trailing gold icon, same as the wallet/hire cost
        dmgBuyButton.Add(coin);
        dmgCard.Add(dmgBuyButton);
        list.Add(dmgCard);

        // Pickup Radius — locked preview (no upgrade path built yet)
        var radiusCard = NewCard("Pickup Radius", locked: true, icon: iconPickupRadius);
        AddLabel(radiusCard.Q<VisualElement>("card-info"), "cursor-card-substat").text = "loot vacuum area on click";
        radiusCard.Add(LockPill("Coming soon"));
        list.Add(radiusCard);

        // Damage AOE — locked preview, star gated per GDD mouse upgrades
        var aoeCard = NewCard("Damage AOE", locked: true, icon: iconDamageAOE);
        AddLabel(aoeCard.Q<VisualElement>("card-info"), "cursor-card-substat").text = "clicks damage nearby mobs";
        aoeCard.Add(LockPill("Unlocks at 2", withStar: true));
        list.Add(aoeCard);
    }

    private VisualElement NewCard(string name, bool locked, Sprite icon = null)
    {
        var card = new VisualElement();
        card.AddToClassList("cursor-card");
        if (locked) card.AddToClassList("cursor-card--locked");

        if (cardFrame != null)
        {
            // card-torn.png: 8px slices keep the periodic torn top + baked shadow intact
            card.style.backgroundImage = new StyleBackground(cardFrame);
            card.style.unitySliceLeft = 8;
            card.style.unitySliceTop = 8;
            card.style.unitySliceRight = 8;
            card.style.unitySliceBottom = 8;
            card.style.unitySliceScale = 3f;
        }

        // Row 1: icon placeholder beside name + level block
        var header = new VisualElement { name = "card-header" };
        header.AddToClassList("cursor-card-header");

        var iconSlot = new VisualElement { name = "card-icon" };
        iconSlot.AddToClassList("cursor-card-icon");
        if (icon != null)
        {
            iconSlot.style.backgroundImage = new StyleBackground(icon);
            iconSlot.style.borderLeftWidth = 0;   // placeholder border only while empty
            iconSlot.style.borderRightWidth = 0;
            iconSlot.style.borderTopWidth = 0;
            iconSlot.style.borderBottomWidth = 0;
        }
        header.Add(iconSlot);

        var info = new VisualElement { name = "card-info" };
        info.AddToClassList("cursor-card-info");

        var nameLabel = new Label(name);
        nameLabel.AddToClassList("cursor-card-name");
        info.Add(nameLabel);

        header.Add(info);
        card.Add(header);
        return card;
    }

    private static Label AddLabel(VisualElement parent, string className)
    {
        var label = new Label();
        label.AddToClassList(className);
        parent.Add(label);
        return label;
    }

    private VisualElement LockPill(string text, bool withStar = false)
    {
        var pill = new VisualElement();
        pill.AddToClassList("btn-disabled");
        pill.AddToClassList("cursor-card-lockpill");

        var label = new Label(text);
        label.AddToClassList("btn-label");
        pill.Add(label);

        if (withStar && starIcon != null)
        {
            var star = new VisualElement();
            star.AddToClassList("lockpill-star");
            star.style.backgroundImage = new StyleBackground(starIcon);
            pill.Add(star);
        }
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

        if (dmgStatLabel != null)
            // next value in success green (UITK rich text; colour = --color-text-success)
            dmgStatLabel.text = $"{clicker.EffectiveClickDamage:0.#} ➜ <color=#376C2D>{clicker.NextClickDamage:0.#} dmg</color>";

        if (dmgBuyButton != null)
        {
            int cost = clicker.ClickUpgradeCost;
            if (dmgBuyLabel != null) dmgBuyLabel.text = AnimatedNumberUITK.Abbrev(cost);
            bool affordable = Inventory.Instance != null && Inventory.Instance.Gold >= cost;
            dmgBuyButton.SetEnabled(affordable);
        }
    }
}
