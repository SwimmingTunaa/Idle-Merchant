using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Caravan offer panel (CaravanPanel.uxml, instanced in MainUI.uxml) — opened by clicking the wagon at the guild door.
/// Shows the driver's line, the lot weighed against the seals, the manifest (lot, holding + floor, pay, departure fuse); Accept / Decline go through CaravanManager,
/// and the wagon leaving (accept, decline, timeout) closes the panel via OnCaravanChanged.
/// </summary>
public class CaravanPanelController : BasePanelController
{
    public override string PanelID => "CaravanPanel";

    private VisualElement hero, icon, panIcon, scale, fuseFill, fuseSpark;
    private Label want, seals, leaves, error, say, hold, panQty, panSeals;
    private IVisualElementScheduledItem tick;

    // the driver's patter, one line per visit ({item} = the lot's name, {n} = the seals)
    private static readonly string[] Patter =
    {
        "Fine {item}, that. {n} seals for the lot?",
        "Road's long and the wagon's light. Fill it with {item}?",
        "The guild up north pays in seals. Your {item} for {n} of them.",
        "Weighed it myself. Fair's fair: {n} seals.",
        "I'll take the {item} off your hands before the damp gets it.",
        "Can't eat seals, but you can't carry that much {item} either.",
        "Last stop before the pass. {item}, if you're selling.",
        "A handshake and {n} seals. Quickest trade you'll make today.",
    };

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
        panIcon = panel.Q("cv-pan-icon");
        scale = panel.Q("cv-scale");
        fuseFill = panel.Q("cv-fuse-fill");
        fuseSpark = panel.Q("cv-fuse-spark");
        want = panel.Q<Label>("cv-want");
        seals = panel.Q<Label>("cv-seals");
        leaves = panel.Q<Label>("cv-leaves");
        error = panel.Q<Label>("cv-error");
        say = panel.Q<Label>("cv-say");
        hold = panel.Q<Label>("cv-hold");
        panQty = panel.Q<Label>("cv-pan-qty");
        panSeals = panel.Q<Label>("cv-pan-seals");
        panel.Q<Button>("cv-close").clicked += () => UIManager.Instance.ClosePanel(this);
        var accept = panel.Q<Button>("cv-accept");
        accept.clicked += () =>
        {
            if (CaravanManager.Instance != null && !CaravanManager.Instance.Accept()) error.text = "Not enough stock";
        };
        accept.RegisterCallback<PointerEnterEvent>(_ => scale.AddToClassList("cv-scale--tip"));
        accept.RegisterCallback<PointerLeaveEvent>(_ => scale.RemoveFromClassList("cv-scale--tip"));
        panel.Q<Button>("cv-decline").clicked += () => CaravanManager.Instance?.Leave();

        tick = panel.schedule.Execute(Tick).Every(250);
        tick.Pause();
    }

    protected override void OnOpenStart()
    {
        GameSignals.OnCaravanChanged += Refresh;
        TutorialManager.Flag("caravan");
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
        panIcon.style.backgroundImage = new StyleBackground(c.Item.icon);
        want.text = $"×{c.Qty} {c.Item.displayName}";
        panQty.text = $"×{c.Qty}";
        seals.text = $"{CaravanManager.SealReward} Guild Seals";
        panSeals.text = $"×{CaravanManager.SealReward}";
        var inv = Inventory.Instance;
        hold.text = inv != null ? $"you hold {inv.Get(c.Item.itemCategory, c.Item)} · keep {inv.GetFloor(c.Item)}" : "";
        // same line for the whole visit, a different one next time
        int pick = Mathf.Abs((c.Item.name.GetHashCode() * 31 + c.Qty) % Patter.Length);
        say.text = Patter[pick].Replace("{item}", c.Item.displayName).Replace("{n}", CaravanManager.SealReward.ToString());
        Tick();
    }

    private void Tick()
    {
        var c = CaravanManager.Instance;
        if (c == null || !c.Present) return;
        int s = Mathf.CeilToInt(c.LeaveIn);
        leaves.text = $"{s / 60}:{s % 60:00}";
        float f = Mathf.Clamp01(c.LeaveIn / CaravanManager.StayFor) * 100f;
        fuseFill.style.width = Length.Percent(f);
        fuseSpark.style.left = Length.Percent(f);
    }
}
