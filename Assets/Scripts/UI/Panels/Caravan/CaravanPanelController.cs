using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Caravan offer panel (CaravanPanel.uxml, instanced in MainUI.uxml) — opened by clicking the wagon at the guild door.
/// Shows the wanted lot, the seal reward and the leave countdown; Accept / Decline go through CaravanManager,
/// and the wagon leaving (accept, decline, timeout) closes the panel via OnCaravanChanged.
/// </summary>
public class CaravanPanelController : BasePanelController
{
    public override string PanelID => "CaravanPanel";

    private VisualElement hero, icon;
    private Label want, seals, leaves, error;
    private IVisualElementScheduledItem tick;

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
        panel = uiDocument.rootVisualElement.Q<VisualElement>("caravan-panel");
        if (panel == null)
        {
            Debug.LogError("[CaravanPanelController] 'caravan-panel' not found — is CaravanPanel instanced in MainUI.uxml?");
            return;
        }
        panel.style.display = DisplayStyle.None;
        panel.style.opacity = 0f;
        if (panel.parent != null) panel.parent.pickingMode = PickingMode.Ignore;   // full-screen TemplateContainer

        hero = panel.Q("cv-hero");
        icon = panel.Q("cv-icon");
        want = panel.Q<Label>("cv-want");
        seals = panel.Q<Label>("cv-seals");
        leaves = panel.Q<Label>("cv-leaves");
        error = panel.Q<Label>("cv-error");
        panel.Q<Button>("cv-close").clicked += () => UIManager.Instance.ClosePanel(this);
        panel.Q<Button>("cv-accept").clicked += () =>
        {
            if (CaravanManager.Instance != null && !CaravanManager.Instance.Accept()) error.text = "Not enough stock";
        };
        panel.Q<Button>("cv-decline").clicked += () => CaravanManager.Instance?.Leave();

        tick = panel.schedule.Execute(Tick).Every(250);
        tick.Pause();
    }

    protected override void OnOpenStart()
    {
        GameSignals.OnCaravanChanged += Refresh;
        error.text = "";
        Refresh();
        tick.Resume();
    }

    protected override void OnCloseStart()
    {
        GameSignals.OnCaravanChanged -= Refresh;
        tick.Pause();
    }

    protected override void OnDestroy()
    {
        GameSignals.OnCaravanChanged -= Refresh;
        base.OnDestroy();
    }

    private void Refresh()
    {
        var c = CaravanManager.Instance;
        if (c == null || !c.Present)
        {
            UIManager.Instance.ClosePanel(this);
            return;
        }
        hero.style.backgroundImage = new StyleBackground(c.Parked);
        icon.style.backgroundImage = new StyleBackground(c.Item.icon);
        want.text = $"x{c.Qty} {c.Item.displayName}";
        seals.text = $"for {CaravanManager.SealReward} Guild Seals";
        Tick();
    }

    private void Tick()
    {
        var c = CaravanManager.Instance;
        if (c == null || !c.Present) return;
        int s = Mathf.CeilToInt(c.LeaveIn);
        leaves.text = $"Leaves in {s / 60}:{s % 60:00}";
    }
}
