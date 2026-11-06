using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Royal audience panel (AudiencePanel.uxml, instanced in MainUI.uxml): the waiting royal's portrait, what they
/// fancy, up to three offers from stock (click one = RoyalAudience.Present), the audience countdown, Decline.
/// Closes itself when the royal stops waiting (presented, snubbed, gone).
/// </summary>
public class AudiencePanelController : BasePanelController
{
    public override string PanelID => "AudiencePanel";

    private VisualElement hero, offers;
    private Label title, taste, leaves, error;
    private IVisualElementScheduledItem tick;
    private RoyalAudience audience;

    void Awake()
    {
        hideFlags = HideFlags.HideInInspector;
        if (uiDocument == null) uiDocument = GetComponentInParent<UIDocument>();
    }

    protected override void Start()
    {
        BuildUI();
        base.Start();
    }

    protected override void BuildUI()
    {
        if (uiDocument == null) return;
        panel = uiDocument.rootVisualElement.Q<VisualElement>("audience-panel");
        if (panel == null) { Debug.LogError("[AudiencePanelController] 'audience-panel' not found — is AudiencePanel instanced in MainUI.uxml?"); return; }
        panel.style.display = DisplayStyle.None;
        panel.style.opacity = 0f;
        if (panel.parent != null) panel.parent.pickingMode = PickingMode.Ignore;

        hero = panel.Q("au-hero");
        offers = panel.Q("au-offers");
        title = panel.Q<Label>("au-title");
        taste = panel.Q<Label>("au-taste");
        leaves = panel.Q<Label>("au-leaves");
        error = panel.Q<Label>("au-error");
        panel.Q<Button>("au-close").clicked += () => UIManager.Instance.ClosePanel(this);
        panel.Q<Button>("au-decline").clicked += () => UIManager.Instance.ClosePanel(this);
        tick = panel.schedule.Execute(Tick).Every(250);
        tick.Pause();
    }

    public void OpenFor(RoyalAudience a)
    {
        audience = a;
        UIManager.Instance.OpenPanel(this);
    }

    protected override void OnOpenStart()
    {
        error.text = "";
        Refresh();
        tick.Resume();
    }

    protected override void OnCloseStart() => tick.Pause();

    private void Refresh()
    {
        if (audience == null || !audience.Waiting) { UIManager.Instance.ClosePanel(this); return; }
        var portrait = audience.Portrait;
        hero.style.backgroundImage = portrait != null ? new StyleBackground(portrait) : StyleKeyword.None;
        title.text = $"Audience: {audience.Title}";
        offers.Clear();
        var list = audience.BuildOffers();
        RoyalAudience.Offer? fancied = null; foreach (var o in list) if (o.fancied) fancied = o;
        taste.text = fancied.HasValue ? $"{audience.Title} fancies the {fancied.Value.item.displayName}."
                   : list.Count > 0 ? $"Nothing {audience.Title} fancies is for sale — they'd settle for:" : "Nothing is for sale.";
        foreach (var o in list)
        {
            var offer = o;
            var row = new Button(); row.AddToClassList("au-offer"); row.EnableInClassList("au-offer--fancied", o.fancied);
            var icon = new VisualElement(); icon.AddToClassList("au-offer-icon");
            if (o.item.icon != null) icon.style.backgroundImage = new StyleBackground(o.item.icon);
            row.Add(icon);
            var name = new Label(o.item.displayName); name.AddToClassList("au-offer-name"); row.Add(name);
            var price = new Label($"{o.Gold:N0} gold  (×{o.multiplier:0.#})"); price.AddToClassList("au-offer-price"); row.Add(price);
            if (o.fancied) { var tag = new Label("fancied"); tag.AddToClassList("au-offer-tag"); row.Add(tag); }
            row.clicked += () =>
            {
                if (audience != null && audience.Present(offer)) UIManager.Instance.ClosePanel(this);
                else error.text = "Not in stock any more";
            };
            offers.Add(row);
        }
        Tick();
    }

    private void Tick()
    {
        if (audience == null || !audience.Waiting) { UIManager.Instance.ClosePanel(this); return; }
        int s = Mathf.CeilToInt(audience.WaitLeft);
        leaves.text = $"Leaves in {s / 60}:{s % 60:00}";
    }
}
