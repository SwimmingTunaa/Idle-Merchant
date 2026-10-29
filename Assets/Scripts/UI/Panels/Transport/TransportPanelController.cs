using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Transport modal (TransportPanel.uxml, instanced in MainUI.uxml after DecoratePanel) — opened by clicking a dungeon
/// ladder (<see cref="TransportPoint"/>, ClickerManager priority 5). Decorate-panel look; this depth's Elevator then its
/// Teleporter (layerSpecific, targetLayer = depth), each Owned / locked / Buy n gold.
/// </summary>
public class TransportPanelController : BasePanelController
{
    public override string PanelID => "TransportPanel";

    private TransportPoint point;
    private VisualElement rows;
    private Label title, sub;

    void Awake()
    {
        if (uiDocument == null) uiDocument = GetComponentInParent<UIDocument>();
    }

    protected override void Start()
    {
        BuildUI();
        base.Start();
    }

    /// <summary>Open (or retarget, if already open) for the clicked ladder.</summary>
    public void OpenFor(TransportPoint tp)
    {
        if (tp == null || panel == null || ProgressionManager.Instance == null) return;
        point = tp;
        if (State == PanelState.Open) Rebuild();
        else UIManager.Instance.OpenPanel(this);
    }

    protected override void BuildUI()
    {
        if (uiDocument == null) return;
        panel = uiDocument.rootVisualElement.Q<VisualElement>("transport-panel");
        if (panel == null)
        {
            Debug.LogError("[TransportPanelController] 'transport-panel' not found — is TransportPanel instanced in MainUI.uxml?");
            return;
        }
        panel.style.display = DisplayStyle.None;
        panel.style.opacity = 0f;
        if (panel.parent != null) panel.parent.pickingMode = PickingMode.Ignore;   // full-screen TemplateContainer

        rows = panel.Q("tp-rows");
        title = panel.Q<Label>("tp-title");
        sub = panel.Q<Label>("tp-sub");
        panel.Q<Button>("tp-close").clicked += () => UIManager.Instance.ClosePanel(this);
        panel.RegisterCallback<ClickEvent>(e => { if (e.target == panel) UIManager.Instance.ClosePanel(this); });   // backdrop
    }

    protected override void OnOpenStart()
    {
        Subscribe(false);
        Subscribe(true);
        Rebuild();
    }

    protected override void OnCloseStart() => Subscribe(false);

    protected override void OnDestroy()
    {
        Subscribe(false);
        base.OnDestroy();
    }

    private void Subscribe(bool on)
    {
        if (on) { GameSignals.GoldChanged += OnGoldChanged; ProgressionManager.OnUpgradePurchased += OnUpgradePurchased; }
        else { GameSignals.GoldChanged -= OnGoldChanged; ProgressionManager.OnUpgradePurchased -= OnUpgradePurchased; }
    }

    private void OnGoldChanged(int _) => Rebuild();
    private void OnUpgradePurchased(GuildUpgradeDef _) => Rebuild();

    // ponytail: rebuilds every row on each gold tick (6 rows); diff the button states if gold ticks get hot
    private void Rebuild()
    {
        if (point == null) return;
        var pm = ProgressionManager.Instance;
        bool teleport = point.transportType == TransportType.Teleporter;
        bool lift = point.transportType == TransportType.Elevator;
        title.text = $"Depth {point.sourceLayer} {(teleport ? "teleport pad" : lift ? "lift" : "ladder")}";
        sub.text = teleport ? "Porters blink to the floor above."
                 : lift ? "Porters ride this lift in a second."
                 : "Porters climb this ladder at walking pace.";

        rows.Clear();
        var all = Resources.LoadAll<GuildUpgradeDef>("Guild Upgrades");
        // Only this depth's elevator then teleporter — the Guild book lists every depth's
        foreach (var type in new[] { UpgradeType.Elevator, UpgradeType.Teleporter })
        {
            var u = all.FirstOrDefault(d => d.upgradeType == type && d.layerSpecific && d.targetLayer == point.sourceLayer);
            if (u != null) rows.Add(BuildRow(u));
        }
    }

    // Same states as the Guild card (GuildUpgradePanel.UpdateCardState): Owned / Requires N stars / Needs <prev> / Buy
    private VisualElement BuildRow(GuildUpgradeDef u)
    {
        var pm = ProgressionManager.Instance;
        var row = new VisualElement();
        row.AddToClassList("dp-row");

        var name = new Label(u.upgradeName) { pickingMode = PickingMode.Ignore };
        name.AddToClassList("dp-row__name");
        row.Add(name);
        if (u.materials.Count > 0 && !pm.IsUpgradeOwned(u)) row.Add(GuildUpgradePanel.MaterialRow(u.materials));

        bool owned = pm.IsUpgradeOwned(u);
        bool starLocked = !owned && pm.GetCurrentStars() < u.starRequirement;
        var prev = u.requiredUpgrade;
        bool prevLocked = !owned && !starLocked && prev != null && !pm.IsUpgradeOwned(prev);
        bool buyable = !owned && !starLocked && !prevLocked;

        var button = new Button(() => OnBuy(u));
        button.AddToClassList("dp-btn");
        button.AddToClassList(buyable ? "btn-success" : "btn-secondary");
        button.Add(new Label(owned ? "Owned"
                           : starLocked ? $"Requires {u.starRequirement} stars"
                           : prevLocked ? $"Needs {prev.upgradeName}"
                           : $"Buy {AnimatedNumberUITK.Abbrev(u.goldCost)}") { pickingMode = PickingMode.Ignore });
        if (buyable)
        {
            var coin = new VisualElement { pickingMode = PickingMode.Ignore };
            coin.AddToClassList("btn-coin");
            coin.AddToClassList("tp-btn__coin");
            button.Add(coin);
        }
        button.SetEnabled(buyable && Inventory.Instance != null && pm.CanPurchaseUpgrade(u));
        row.Add(button);
        return row;
    }

    private void OnBuy(GuildUpgradeDef u)
    {
        SfxManager.Instance?.Play(SfxId.UiTick);
        ProgressionManager.Instance.PurchaseUpgrade(u);
        Rebuild();
    }
}
