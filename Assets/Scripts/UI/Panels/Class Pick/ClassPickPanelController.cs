using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// "Choose a Class" modal (ClassPickPanel.uxml, instanced in MainUI.uxml) — opened by the Roster's Promote on a Novice
/// (paid Wood V → Bronze) or for a pending free path pick. Four cards (Warrior, Guardian, Rogue, Mage) show the promoting
/// adventurer in that class's tier def; pick one, then confirm. X or a backdrop click cancels.
/// Mock: scratchpad/classmock/index.html.
/// </summary>
public class ClassPickPanelController : BasePanelController
{
    public override string PanelID => "ClassPickPanel";

    private const float FigureHeight = 112f;          // target px; drawn at the largest whole px-per-art-pixel that fits
    private const float TexelsPerArtPixel = 12.5f;    // CharacterSpriteGenerator captures at 100 px/unit, character art is 8 PPU

    // PLACEHOLDER outfits (shirt + sleeves, pants) until the hand-drawn class sprites land on the tier defs —
    // then drop the colours and call CharacterSpriteGenerator.GenerateSprite(def, indices).
    private static readonly (ClassLine line, Color32 shirt, Color32 pants)[] Lines =
    {
        (ClassLine.Warrior,  new Color32(0xB4, 0x46, 0x3C, 0xFF), new Color32(0x6E, 0x5A, 0x46, 0xFF)),
        (ClassLine.Guardian, new Color32(0x9A, 0xA6, 0xB4, 0xFF), new Color32(0x5A, 0x60, 0x70, 0xFF)),
        (ClassLine.Rogue,    new Color32(0x4E, 0x3C, 0x5C, 0xFF), new Color32(0x2E, 0x2A, 0x34, 0xFF)),
        (ClassLine.Mage,     new Color32(0x4C, 0x78, 0xC8, 0xFF), new Color32(0x34, 0x4C, 0x8C, 0xFF)),
    };

    private AdventurerAgent agent;
    private Action onPicked;
    private bool free;
    private int tier;
    private ClassLine picked;
    private bool goldDirty;

    private VisualElement cards, confirmCoin;
    private Label sub, confirmLabel, confirmCost;
    private Button confirm;
    private readonly List<(ClassLine line, VisualElement card)> cardViews = new();

    void Awake()
    {
        if (uiDocument == null) uiDocument = GetComponentInParent<UIDocument>();
    }

    protected override void Start()
    {
        BuildUI();
        base.Start();
    }

    /// <summary>Open for this adventurer's class pick; <paramref name="picked"/> runs after a successful Promote / ChoosePath.</summary>
    public void OpenFor(AdventurerAgent a, Action picked)
    {
        if (a == null || panel == null || ClassCatalog.Instance == null) return;
        agent = a;
        onPicked = picked;
        if (State == PanelState.Open) Rebuild();
        else UIManager.Instance.OpenPanel(this);
    }

    protected override void BuildUI()
    {
        if (uiDocument == null) return;
        panel = uiDocument.rootVisualElement.Q<VisualElement>("class-pick-panel");
        if (panel == null)
        {
            Debug.LogError("[ClassPickPanelController] 'class-pick-panel' not found — is ClassPickPanel instanced in MainUI.uxml?");
            return;
        }
        panel.style.display = DisplayStyle.None;
        panel.style.opacity = 0f;
        if (panel.parent != null) panel.parent.pickingMode = PickingMode.Ignore;   // full-screen TemplateContainer

        cards = panel.Q("cp-cards");
        sub = panel.Q<Label>("cp-sub");
        confirm = panel.Q<Button>("cp-confirm");
        confirmLabel = panel.Q<Label>("cp-confirm-label");
        confirmCoin = panel.Q("cp-confirm-coin");
        confirmCost = panel.Q<Label>("cp-confirm-cost");
        confirm.clicked += OnConfirm;
        panel.Q<Button>("cp-close").clicked += () => UIManager.Instance.ClosePanel(this);
        panel.RegisterCallback<ClickEvent>(e => { if (e.target == panel) UIManager.Instance.ClosePanel(this); });   // backdrop
    }

    protected override void OnOpenStart()
    {
        GameSignals.GoldChanged += OnGoldChanged;
        Rebuild();
    }

    protected override void OnCloseStart()
    {
        GameSignals.GoldChanged -= OnGoldChanged;
    }

    protected override void OnDestroy()
    {
        GameSignals.GoldChanged -= OnGoldChanged;
        base.OnDestroy();
    }

    // Gold ticks often — re-check the confirm button's affordability once per frame
    private void OnGoldChanged(int _) => goldDirty = true;

    void Update()
    {
        if (!goldDirty) return;
        goldDirty = false;
        if (State == PanelState.Open || State == PanelState.Opening) RefreshConfirm();
    }

    // ═════════════════════════════════════════════
    // BUILD
    // ═════════════════════════════════════════════

    private void Rebuild()
    {
        free = agent.NeedsPathPick;
        tier = free ? agent.CurrentRank : agent.CurrentRank + 1;
        string name = !string.IsNullOrEmpty(agent.Identity?.firstName) ? agent.Identity.firstName : agent.gameObject.name;
        sub.text = free
            ? $"A free pick for a {AdventurerRank.Name(tier)} veteran"
            : $"{name} is ready for their first promotion";

        picked = ClassLine.Novice;
        cards.Clear();
        cardViews.Clear();
        foreach (var (line, shirt, pants) in Lines)
        {
            var def = ClassCatalog.Instance.Get(line, tier);
            if (def == null) continue;
            var card = BuildCard(def, shirt, pants);
            card.RegisterCallback<ClickEvent>(_ => Select(line));
            cards.Add(card);
            cardViews.Add((line, card));
        }
        RefreshConfirm();
    }

    private VisualElement BuildCard(AdventurerDef def, Color shirt, Color pants)
    {
        var card = new VisualElement();
        card.AddToClassList("cp-class");
        card.Add(Child("cp-class__ring"));

        var stage = Child("cp-class__stage");
        var figure = Child("cp-class__figure");
        if (agent.appearanceManager != null && def.useModularCharacter)
        {
            // This adventurer's own layers + skin/hair, in the class's tier def with the placeholder outfit tint
            var sprite = CharacterSpriteGenerator.GenerateSprite(def, agent.appearanceManager.GetCurrentIndices(), shirt, pants);
            if (sprite != null)
            {
                int k = Mathf.Max(1, Mathf.FloorToInt(FigureHeight * TexelsPerArtPixel / sprite.rect.height));
                figure.style.width = sprite.rect.width * k / TexelsPerArtPixel;
                figure.style.height = sprite.rect.height * k / TexelsPerArtPixel;
                figure.style.backgroundImage = new StyleBackground(sprite);
            }
        }
        stage.Add(figure);
        card.Add(stage);

        var name = new Label(def.displayName) { pickingMode = PickingMode.Ignore };
        name.AddToClassList("cp-class__name");
        card.Add(name);
        var role = new Label(def.description) { pickingMode = PickingMode.Ignore };
        role.AddToClassList("cp-class__role");
        card.Add(role);

        var slip = Child("cp-class__slip");
        var icon = Child("cp-class__icon");
        if (def.classIcon != null) icon.style.backgroundImage = new StyleBackground(def.classIcon);
        slip.Add(icon);
        card.Add(slip);   // last, so it draws over the chip edge
        return card;
    }

    private static VisualElement Child(string cls)
    {
        var e = new VisualElement { pickingMode = PickingMode.Ignore };
        e.AddToClassList(cls);
        return e;
    }

    // ═════════════════════════════════════════════
    // PICK + CONFIRM
    // ═════════════════════════════════════════════

    private void Select(ClassLine line)
    {
        picked = line;
        foreach (var (l, card) in cardViews) card.EnableInClassList("cp-class--sel", l == line);
        RefreshConfirm();
    }

    private void RefreshConfirm()
    {
        if (agent == null) return;
        var def = picked != ClassLine.Novice ? ClassCatalog.Instance.Get(picked, tier) : null;
        bool any = def != null;
        confirm.EnableInClassList("btn-disabled", !any);
        confirm.EnableInClassList("btn-success", any);
        confirmLabel.text = !any ? "Pick a class"
            : free ? $"Become a {def.displayName} · Free"
            : $"Promote to {def.displayName}";
        var paid = any && !free ? DisplayStyle.Flex : DisplayStyle.None;
        confirmCoin.style.display = paid;
        confirmCost.style.display = paid;
        confirmCost.text = $" {agent.PromoteCost:N0}";
        confirm.SetEnabled(any && (free || (Inventory.Instance != null && Inventory.Instance.CanAfford(agent.PromoteCost))));
    }

    private void OnConfirm()
    {
        if (agent == null || picked == ClassLine.Novice) return;
        bool done = agent.NeedsPathPick ? agent.ChoosePath(picked) : agent.Promote(picked);
        if (!done) return;
        var cb = onPicked;
        UIManager.Instance.ClosePanel(this);
        cb?.Invoke();
    }
}
