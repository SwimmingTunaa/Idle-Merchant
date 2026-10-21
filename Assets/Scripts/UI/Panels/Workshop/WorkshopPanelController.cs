using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Workshop panel (WorkshopPanel.uxml, instanced in MainUI.uxml) — opened by clicking a station prop in the guild.
/// Stone wall + timber beam, the prop's big hero frames at 3× as the hero (cold frame while unowned), a row of iron-plate
/// slots and a "+ Slot" buy button. Clicking a slot drops a themed "Assign recipe" list of the station's recipes
/// (+ "Clear slot"); a click anywhere else closes it. Mock: scratchpad/crafting-mock/crafting.html (.wpanel).
/// </summary>
public class WorkshopPanelController : BasePanelController
{
    public override string PanelID => "WorkshopPanel";

    private const int TickMs = 100;
    private const float HeroScale = 3f;
    private const float HeroFps = 6f;

    private class SlotView { public StatusFrame frame; public VisualElement plate; }
    private class DropRow { public RecipeDef recipe; public StatusFrame frame; public Label state; }

    private WorkshopProp prop;
    private CraftingStation station;
    private int openSlot = -1;

    private VisualElement card, hero, heroProp, slotRow, drop;
    private Label title, lockedMsg;
    private Button buyButton;
    private Label buyCost;
    private readonly List<SlotView> slotViews = new();
    private readonly List<DropRow> dropRows = new();
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

    /// <summary>Open (or retarget, if already open) for the clicked prop's station.</summary>
    public void OpenFor(WorkshopProp p)
    {
        if (p == null || panel == null) return;
        prop = p;
        station = p.Station;
        if (State == PanelState.Open) Rebuild();
        else UIManager.Instance.OpenPanel(this);
    }

    protected override void BuildUI()
    {
        if (uiDocument == null) return;
        panel = uiDocument.rootVisualElement.Q<VisualElement>("workshop-panel");
        if (panel == null)
        {
            Debug.LogError("[WorkshopPanelController] 'workshop-panel' not found — is WorkshopPanel instanced in MainUI.uxml?");
            return;
        }
        panel.style.display = DisplayStyle.None;
        panel.style.opacity = 0f;
        if (panel.parent != null) panel.parent.pickingMode = PickingMode.Ignore;   // full-screen TemplateContainer

        card = panel.Q("wp-card");
        hero = panel.Q("wp-hero");
        heroProp = panel.Q("wp-hero-prop");
        slotRow = panel.Q("wp-slots");
        drop = panel.Q("wp-drop");
        title = panel.Q<Label>("wp-title");
        lockedMsg = panel.Q<Label>("wp-locked");
        panel.Q<Button>("wp-close").clicked += () => UIManager.Instance.ClosePanel(this);

        // Any press outside the open dropdown (and not on a slot, which toggles it itself) closes it
        panel.RegisterCallback<PointerDownEvent>(e =>
        {
            if (openSlot < 0 || e.target is not VisualElement t) return;
            if (drop.Contains(t)) return;
            foreach (var v in slotViews) if (v.plate.Contains(t)) return;
            CloseDrop();
        }, TrickleDown.TrickleDown);

        tick = panel.schedule.Execute(Tick).Every(TickMs);
        tick.Pause();
    }

    protected override void OnOpenStart()
    {
        GameSignals.OnCraftingConfigChanged += Rebuild;
        ProgressionManager.OnUpgradePurchased += OnUpgradePurchased;
        Rebuild();
        tick.Resume();
    }

    protected override void OnCloseStart()
    {
        GameSignals.OnCraftingConfigChanged -= Rebuild;
        ProgressionManager.OnUpgradePurchased -= OnUpgradePurchased;
        CloseDrop();
        tick.Pause();
    }

    protected override void OnDestroy()
    {
        GameSignals.OnCraftingConfigChanged -= Rebuild;
        ProgressionManager.OnUpgradePurchased -= OnUpgradePurchased;
        base.OnDestroy();
    }

    private void OnUpgradePurchased(GuildUpgradeDef _) => Rebuild();

    // ═════════════════════════════════════════════
    // BUILD
    // ═════════════════════════════════════════════

    private void Rebuild()
    {
        var cm = CraftingManager.Instance;
        if (cm == null || prop == null) return;
        CloseDrop();
        bool owned = cm.IsStationOwned(station);
        title.text = CraftingController.StationName(station);

        SizeHero();
        hero.style.backgroundImage = new StyleBackground(prop.HeroFrames[0]);

        slotRow.Clear();
        slotViews.Clear();
        buyButton = null;
        slotRow.style.display = owned ? DisplayStyle.Flex : DisplayStyle.None;
        lockedMsg.style.display = owned ? DisplayStyle.None : DisplayStyle.Flex;
        lockedMsg.text = $"Buy the {CraftingController.StationName(station)} in the Guild book to start crafting";
        if (owned)
        {
            var slots = cm.GetSlots(station);
            for (int i = 0; i < slots.Count; i++) slotRow.Add(BuildPlate(i, slots[i]));
            slotRow.Add(BuildBuy());
        }
        Tick();
    }

    // The hero is the prop's big hero sprite at 3× (bottom-left), the side prop beside it — sized from the sprites
    private void SizeHero()
    {
        var f = prop.HeroFrames[0].rect;
        hero.style.width = f.width * HeroScale;
        hero.style.height = f.height * HeroScale;
        var side = prop.HeroSideProp;
        heroProp.style.display = side != null ? DisplayStyle.Flex : DisplayStyle.None;
        hero.style.marginRight = (side != null ? side.rect.width * HeroScale + 4 : 0) + 18;
        if (side == null) return;
        heroProp.style.backgroundImage = new StyleBackground(side);
        heroProp.style.width = side.rect.width * HeroScale;
        heroProp.style.height = side.rect.height * HeroScale;
        heroProp.style.left = f.width * HeroScale + 4;
    }

    private VisualElement BuildPlate(int index, RecipeDef r)
    {
        var plate = new VisualElement();
        plate.AddToClassList("wp-slot");
        var frame = new StatusFrame(48, 4, 34, 24, 26);
        frame.AddToClassList("wp-frame");
        if (r == null)
        {
            plate.AddToClassList("wp-slot--empty");
            frame.AddToClassList("wp-frame--empty");
            frame.Icon.Add(new Label("+") { pickingMode = PickingMode.Ignore });
        }
        else if (r.Output.icon != null) frame.Icon.style.backgroundImage = new StyleBackground(r.Output.icon);
        plate.Add(frame);
        var name = new Label(r != null ? r.Output.displayName : "Empty slot") { pickingMode = PickingMode.Ignore };
        name.AddToClassList("wp-slot__name");
        plate.Add(name);
        plate.RegisterCallback<ClickEvent>(_ => ToggleDrop(index));
        slotViews.Add(new SlotView { frame = frame, plate = plate });
        return plate;
    }

    private VisualElement BuildBuy()
    {
        var holder = new VisualElement();
        holder.AddToClassList("wp-buy");
        buyButton = new Button(() =>
        {
            if (!CraftingManager.Instance.BuyStationSlot(station))
                Debug.Log($"[WorkshopPanelController] Can't afford a {station} slot");
        });
        buyButton.AddToClassList("btn-success");
        buyButton.AddToClassList("wp-buy__btn");
        buyButton.Add(new Label("+ Slot") { pickingMode = PickingMode.Ignore });
        var coin = new VisualElement { pickingMode = PickingMode.Ignore };
        coin.AddToClassList("btn-coin");
        coin.AddToClassList("wp-buy__coin");
        buyButton.Add(coin);
        buyCost = new Label { pickingMode = PickingMode.Ignore };
        buyButton.Add(buyCost);
        holder.Add(buyButton);
        return holder;
    }

    // ═════════════════════════════════════════════
    // DROPDOWN
    // ═════════════════════════════════════════════

    private void ToggleDrop(int index)
    {
        bool reopen = openSlot != index;
        CloseDrop();
        if (!reopen) return;
        openSlot = index;
        var cm = CraftingManager.Instance;
        var slots = cm.GetSlots(station);
        var current = slots[index];

        drop.Clear();
        dropRows.Clear();
        var head = new Label("ASSIGN RECIPE");
        head.AddToClassList("wp-drop__head");
        drop.Add(head);

        foreach (var r in cm.GetAllRecipes())
        {
            if (r.Station != station) continue;
            var row = Row(r.Output.icon, r.Output.displayName, out var frame, out var state);
            row.EnableInClassList("wp-it--cur", r == current);
            var rr = r;
            row.RegisterCallback<ClickEvent>(_ => Assign(rr));
            drop.Add(row);
            dropRows.Add(new DropRow { recipe = r, frame = frame, state = state });
        }
        if (current != null)
        {
            var clear = Row(null, "Clear slot", out var frame, out _);
            clear.AddToClassList("wp-it--rm");
            frame.AddToClassList("wp-frame--empty");
            clear.RegisterCallback<ClickEvent>(_ => Assign(null));
            drop.Add(clear);
        }

        // Hang it under the clicked plate (card-local; the card doesn't clip, so it may overhang the bottom).
        // A plate built this frame (or while the panel is still hidden) has no layout yet — place it once it has one.
        var plate = slotViews[index].plate;
        void Place()
        {
            var pos = plate.ChangeCoordinatesTo(card, Vector2.zero);
            drop.style.left = pos.x - 6;
            drop.style.top = pos.y + plate.layout.height + 6;
            drop.style.minWidth = Mathf.Max(300f, plate.layout.width + 12);
        }
        if (plate.layout.height > 0) Place();
        else
        {
            EventCallback<GeometryChangedEvent> once = null;
            once = _ => { plate.UnregisterCallback(once); Place(); };
            plate.RegisterCallback(once);
        }
        drop.style.display = DisplayStyle.Flex;
        plate.AddToClassList("wp-slot--open");
        RefreshDrop();
    }

    private VisualElement Row(Sprite icon, string text, out StatusFrame frame, out Label state)
    {
        var row = new VisualElement();
        row.AddToClassList("wp-it");
        frame = new StatusFrame(32, 3, 24, 16, 18);
        frame.AddToClassList("wp-frame");
        if (icon != null) frame.Icon.style.backgroundImage = new StyleBackground(icon);
        row.Add(frame);
        var name = new Label(text) { pickingMode = PickingMode.Ignore };
        name.AddToClassList("wp-it__name");
        row.Add(name);
        state = new Label { pickingMode = PickingMode.Ignore };
        state.AddToClassList("wp-it__state");
        row.Add(state);
        return row;
    }

    private void Assign(RecipeDef r)
    {
        int slot = openSlot;
        CloseDrop();
        if (slot >= 0) CraftingManager.Instance.AssignSlot(station, slot, r); // raises config changed → Rebuild
    }

    private void CloseDrop()
    {
        if (openSlot >= 0 && openSlot < slotViews.Count) slotViews[openSlot].plate.RemoveFromClassList("wp-slot--open");
        openSlot = -1;
        dropRows.Clear();
        if (drop != null) drop.style.display = DisplayStyle.None;
    }

    // ═════════════════════════════════════════════
    // TICK — hero frames, slot sweeps, dropdown states, buy affordability
    // ═════════════════════════════════════════════

    private void Tick()
    {
        var cm = CraftingManager.Instance;
        if (cm == null || prop == null) return;
        var frames = prop.HeroFrames;
        if (cm.IsStationOwned(station) && frames.Count > 1)
            hero.style.backgroundImage = new StyleBackground(frames[1 + (int)(Time.unscaledTime * HeroFps) % (frames.Count - 1)]);

        for (int i = 0; i < slotViews.Count; i++)
        {
            var (st, p) = (cm.GetSlotState(station, i), cm.GetSlotProgress(station, i));
            slotViews[i].frame.SetState(st switch
            {
                CraftingManager.SlotState.Crafting => StatusFrame.State.Crafting,
                CraftingManager.SlotState.AtCap => StatusFrame.State.Capped,
                CraftingManager.SlotState.MissingMaterials => StatusFrame.State.Blocked,
                _ => StatusFrame.State.Idle,
            }, p);
        }

        if (buyButton != null && Inventory.Instance != null)
        {
            int cost = cm.GetSlotCost(station);
            buyCost.text = $"{cost:N0}";
            buyButton.SetEnabled(Inventory.Instance.Gold >= cost);
        }
        RefreshDrop();
    }

    // Per recipe: crafting / at cap / short on materials / ready, prefixed "also in slot N · " when it fills another slot
    private void RefreshDrop()
    {
        if (openSlot < 0) return;
        var cm = CraftingManager.Instance;
        var inv = Inventory.Instance;
        var slots = cm.GetSlots(station);
        foreach (var row in dropRows)
        {
            var r = row.recipe;
            StatusFrame.State fs;
            string text;
            if (cm.IsCrafting(r)) { fs = StatusFrame.State.Crafting; text = "crafting"; }
            else if (inv.Get(r.Output.itemCategory, r.Output) >= cm.GetCap(r)) { fs = StatusFrame.State.Capped; text = "at cap"; }
            else if (!cm.CanCraft(r)) { fs = StatusFrame.State.Blocked; text = "short on materials"; }
            else { fs = StatusFrame.State.Idle; text = "ready"; }
            row.frame.SetState(fs, cm.GetCraftingProgress(r));

            int other = -1;
            for (int i = 0; i < slots.Count && other < 0; i++) if (i != openSlot && slots[i] == r) other = i;
            row.state.text = other >= 0 ? $"also in slot {other + 1} · {text}" : text;
        }
    }

}
