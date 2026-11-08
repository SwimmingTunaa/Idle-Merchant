using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Customer card (CustomerPanel.uxml, instanced in MainUI.uxml) — right-click a walk-in (ClickerManager.OnPlayerInspect).
/// Name + epithet + blurb from the agent's CustomerCast.Persona, their want / state / patience live from the agent;
/// hero = the agent's own current sprite so the look keeps animating. Closes itself when the customer leaves the
/// shop (despawned or pooled into a new visit — SpawnSerial changes). Nothing here is saved.
/// </summary>
public class CustomerPanelController : BasePanelController
{
    public static CustomerPanelController Instance { get; private set; }
    public override string PanelID => "CustomerPanel";

    private VisualElement hero, icon, barFill, epithetWrap, purse;
    private Label title, epithet, type, state, want, wantLabel, pays, blurb;
    private IVisualElementScheduledItem tick;
    private CustomerAgent agent;
    private SpriteRenderer agentSprite;
    private int agentSerial;

    void Awake()
    {
        Instance = this;
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
        panel = uiDocument.rootVisualElement.Q<VisualElement>("customer-panel");
        if (panel == null)
        {
            Debug.LogError("[CustomerPanelController] 'customer-panel' not found — is CustomerPanel instanced in MainUI.uxml?");
            return;
        }
        panel.style.display = DisplayStyle.None;
        panel.style.opacity = 0f;
        if (panel.parent != null) panel.parent.pickingMode = PickingMode.Ignore;   // full-screen TemplateContainer

        hero = panel.Q("cu-hero");
        icon = panel.Q("cu-icon");
        barFill = panel.Q("cu-bar-fill");
        epithetWrap = panel.Q("cu-epithet-wrap");
        title = panel.Q<Label>("cu-name");
        epithet = panel.Q<Label>("cu-epithet");
        type = panel.Q<Label>("cu-type-label");
        state = panel.Q<Label>("cu-state");
        want = panel.Q<Label>("cu-want");
        wantLabel = panel.Q<Label>("cu-want-label");
        pays = panel.Q<Label>("cu-pays");
        purse = panel.Q("cu-purse");
        blurb = panel.Q<Label>("cu-blurb");
        panel.Q<Button>("cu-close").clicked += () => UIManager.Instance.ClosePanel(this);

        tick = panel.schedule.Execute(Tick).Every(100);
        tick.Pause();
    }

    /// <summary>Open the card on this customer (re-targets if already open).</summary>
    public void Show(CustomerAgent c)
    {
        if (c == null || c.Def == null) return;
        agent = c;
        agentSerial = c.SpawnSerial;
        agentSprite = c.GetComponentInChildren<SpriteRenderer>();
        var p = c.Persona;
        title.text = p.Name;
        title.style.fontSize = p.Name.Length <= 14 ? 40 : p.Name.Length <= 20 ? 32 : 27;   // the banner body fits ~14 chars of Ithaca at 40px
        type.text = c.Def.displayName;
        epithet.text = p.Epithet;
        epithetWrap.style.display = string.IsNullOrEmpty(p.Epithet) ? DisplayStyle.None : DisplayStyle.Flex;
        string word = PurseWord(c);
        foreach (var w in new[] { "thin", "middling", "fat" }) purse.EnableInClassList("cu-purse--" + w, w == word);
        Tick();
        if (UIManager.Instance.IsPanelOpen(PanelID)) return;
        UIManager.Instance.OpenPanel(this);
    }

    protected override void OnOpenStart() => tick.Resume();
    protected override void OnCloseStart() { tick.Pause(); agent = null; agentSprite = null; }

    protected override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    private void Tick()
    {
        if (agent == null || !agent.gameObject.activeInHierarchy || agent.SpawnSerial != agentSerial || agent.State == CustomerState.Exited)
        {
            if (UIManager.Instance.IsPanelOpen(PanelID)) UIManager.Instance.ClosePanel(this);
            return;
        }

        if (agentSprite != null && agentSprite.sprite != null)
        {
            hero.style.backgroundImage = new StyleBackground(agentSprite.sprite);
            hero.style.scale = new Scale(new Vector2(agentSprite.flipX || agentSprite.transform.lossyScale.x < 0f ? -1f : 1f, 1f));
        }

        var item = agent.desiredItem;
        bool leaving = agent.State == CustomerState.Leaving;
        icon.style.backgroundImage = item != null ? new StyleBackground(item.icon) : StyleKeyword.None;
        if (item != null)
        {
            wantLabel.text = leaving ? "BOUGHT" : "WANTS";
            want.text = $"x{agent.desiredQty} {item.displayName}";
            pays.text = Mathf.FloorToInt(agent.desiredQty * item.sellPrice * agent.PriceMultiplier).ToString("N0");
        }
        else
        {
            wantLabel.text = "WANTS";
            want.text = leaving ? "Nothing, in the end" : "Still looking";
            pays.text = "—";
        }

        state.text = StateLine(agent);
        float p01 = agent.Patience01;
        barFill.style.width = Length.Percent(p01 * 100f);
        barFill.EnableInClassList("cu-bar-fill--low", p01 < 0.25f);
        blurb.text = CustomerCast.Blurb(agent.Persona, agent.Def, item);
    }

    private static string StateLine(CustomerAgent c) => c.State switch
    {
        CustomerState.Entering => "Just walked in",
        CustomerState.Wander or CustomerState.Idle => "Browsing the shelves",
        CustomerState.SeekingQueue => "Heading for the line",
        CustomerState.Queueing => "Waiting in line",
        CustomerState.Buying => "At the counter",
        CustomerState.Leaving => c.walkoutReason switch
        {
            WalkoutReason.NoStock => "Leaving: nothing in stock",
            WalkoutReason.TooExpensive => "Leaving: too dear",
            WalkoutReason.Impatient => "Leaving: ran out of patience",
            _ => "Heading home, happy",
        },
        _ => "",
    };

    // The purse stays a pouch icon, not a number: fat / middling / thin against their own kind's range (CustomerDef.budget)
    private static string PurseWord(CustomerAgent c)
    {
        var range = c.Def.budget;
        float t = range.y > range.x ? Mathf.InverseLerp(range.x, range.y, c.budget) : 0.5f;
        return t < 0.34f ? "thin" : t < 0.67f ? "middling" : "fat";
    }
}
