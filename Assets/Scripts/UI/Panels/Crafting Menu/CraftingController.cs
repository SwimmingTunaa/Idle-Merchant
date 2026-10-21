using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Crafting book page (tab 3). Left: one torn sheet per dungeon layer, recipe tiles (select only) with a
/// StatusFrame, stock slip and a red "on" corner (= pinned in a station slot). Right: inventory-style detail — cap
/// stepper, ingredients, "Station · slot N" line, and the foot stamp plank: slam on = pin to the station's first free
/// slot (a full station shakes the plank instead), dither off = clear its slots (BookStamp).
/// A recipe's layer = the deepest layer any raw ingredient first drops on (crafted ingredients inherit theirs).
/// </summary>
public class CraftingController : MonoBehaviour, IBookPage
{
    [Header("Stamp Animation")]
    [Tooltip("Big Stamp.png slam overlay played over the foot plank when a recipe is switched on.")]
    [SerializeField] private FrameAnim stampSlamAnim = new() { frameMs = 40, fadeMs = 150, end = UIFrameAnimator.EndBehaviour.HideAndReset };
    [Tooltip("Dither-erase (for-sale-stamp-sheet) played on the CRAFTING badge when a recipe is switched off.")]
    [SerializeField] private FrameAnim stampDitherAnim = new() { frameMs = 40, end = UIFrameAnimator.EndBehaviour.HideAndReset };

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    public string PageTitle => "Crafting";

    // Stamp face ≈ 1/3 of the Stamp.png frame; the overlay scales with the plank so the face matches it
    // (the inventory's 593px overlay over its 175px badge is the same ratio)
    private const float SlamPerPlankWidth = 2.8f;
    private const int TickMs = 100;          // sweep repaint cadence
    private const int SafetyRefreshTicks = 10; // full state refresh at least once a second (catches stock changes no signal reports)
    private const int FullMs = 1500;           // "<Station> full" stays on the plank this long
    private static readonly float[] ShakePx = { -6f, 6f, -5f, 5f, -3f, 3f, -1f, 0f }; // 40ms steps

    private class Tile
    {
        public RecipeDef recipe;
        public VisualElement root;
        public StatusFrame frame;
        public Label stock;
    }

    private class IngRow
    {
        public ItemDef item;
        public int need;
        public Label have;
    }

    private readonly List<Tile> tiles = new();
    private readonly List<IngRow> ingRows = new();
    private readonly Dictionary<RecipeDef, int> recipeLayer = new();
    private readonly Dictionary<ItemDef, RecipeDef> recipeFor = new();
    private readonly Dictionary<ItemDef, int> dropLayer = new();
    private static readonly Dictionary<CraftingStation, string> stationNames = new();

    private RecipeDef selected;
    private bool dirty, busy;
    private int ticksSinceRefresh;
    private IVisualElementScheduledItem tick;

    // Elements (UXML instances persist across page shows; callbacks are wired once per root)
    private VisualElement wiredRoot;
    private ScrollView scroll;
    private StatusFrame detailFrame;
    private Label dName, dStation, dValue, dCap, dDesc, dTime, dSlot, stampText;
    private VisualElement ingList, stampSlot, stampBadge, stampBadgeText, slamOverlay;

    void Awake() => hideFlags = HideFlags.HideInInspector;

    // ═════════════════════════════════════════════
    // IBOOK PAGE
    // ═════════════════════════════════════════════

    public void OnPageShown(VisualElement leftPage, VisualElement rightPage)
    {
        if (CraftingManager.Instance == null || leftPage == null || rightPage == null)
        {
            Debug.LogError("[CraftingController] Missing CraftingManager or page containers");
            return;
        }
        if (wiredRoot != rightPage) Wire(leftPage, rightPage);

        GameSignals.OnCraftingConfigChanged += MarkDirty;
        GameSignals.OnCraftStarted += OnRecipeSignal;
        GameSignals.OnProductCrafted += OnStockSignal;
        GameSignals.OnItemAdded += OnStockSignal;
        GameSignals.OnItemSold += OnStockSignal;
        ProgressionManager.OnUpgradePurchased += OnUpgradePurchased;

        busy = false;
        BuildLayerMap();
        BuildList();
        Select(selected != null && recipeLayer.ContainsKey(selected) ? selected : (tiles.Count > 0 ? tiles[0].recipe : null));

        tick ??= rightPage.schedule.Execute(Tick).Every(TickMs);
        tick.Resume();
    }

    public void OnPageHidden()
    {
        GameSignals.OnCraftingConfigChanged -= MarkDirty;
        GameSignals.OnCraftStarted -= OnRecipeSignal;
        GameSignals.OnProductCrafted -= OnStockSignal;
        GameSignals.OnItemAdded -= OnStockSignal;
        GameSignals.OnItemSold -= OnStockSignal;
        ProgressionManager.OnUpgradePurchased -= OnUpgradePurchased;

        tick?.Pause();
        // Cancel any in-flight stamp so the badge isn't left on a half-erased frame
        slamOverlay?.Stop();
        if (slamOverlay != null) slamOverlay.style.display = DisplayStyle.None;
        stampBadge?.Stop();
        if (stampBadge != null && stampDitherAnim.frames != null && stampDitherAnim.frames.Length > 0)
            stampBadge.style.backgroundImage = new StyleBackground(stampDitherAnim.frames[0]);
        if (stampBadgeText != null) stampBadgeText.style.display = DisplayStyle.Flex;
        busy = false;
    }

    private void Wire(VisualElement left, VisualElement right)
    {
        wiredRoot = right;
        scroll = left.Q<ScrollView>("recipe-scroll");
        dName = right.Q<Label>("detail-name");
        dStation = right.Q<Label>("detail-station");
        dValue = right.Q<Label>("detail-value");
        dCap = right.Q<Label>("detail-cap");
        dDesc = right.Q<Label>("detail-desc");
        dTime = right.Q<Label>("detail-time");
        ingList = right.Q<VisualElement>("ingredient-list");
        dSlot = right.Q<Label>("detail-slot-info");
        stampSlot = right.Q<VisualElement>("stamp-slot");
        stampText = right.Q<Label>("stamp-text");
        stampBadge = right.Q<VisualElement>("stamp-badge");
        stampBadgeText = right.Q<VisualElement>("stamp-badge-text");
        // Stampanim lives at page-root level (sibling of the pages) so the stamp hand isn't clipped.
        slamOverlay = (right.parent ?? right).Q<VisualElement>("Stampanim");

        detailFrame = new StatusFrame(120, 4, 96, 44, 48, big: true);
        right.Q<VisualElement>("detail-slot")?.Add(detailFrame);

        // Clickable(delay, interval) = press-and-hold repeat, like the inventory floor stepper
        right.Q<VisualElement>("cap-up")?.AddManipulator(new Clickable(() => StepCap(+1), 350, 70));
        right.Q<VisualElement>("cap-down")?.AddManipulator(new Clickable(() => StepCap(-1), 350, 70));

        stampSlot?.RegisterCallback<ClickEvent>(_ => OnStampClicked());
    }

    // ═════════════════════════════════════════════
    // LAYERS
    // ═════════════════════════════════════════════

    private void BuildLayerMap()
    {
        dropLayer.Clear();
        recipeFor.Clear();
        recipeLayer.Clear();
        foreach (var s in FindObjectsByType<Spawner>(FindObjectsInactive.Include))
            foreach (var c in s.candidates)
                if (c is MobDef mob && mob.loot != null)
                    foreach (var item in mob.loot)
                        if (item != null && (!dropLayer.TryGetValue(item, out int l) || s.layerIndex < l))
                            dropLayer[item] = s.layerIndex;

        var all = CraftingManager.Instance.GetAllRecipes();
        foreach (var r in all) if (r.Output != null) recipeFor[r.Output] = r;
        foreach (var r in all) recipeLayer[r] = LayerOf(r, 0);
    }

    private int LayerOf(RecipeDef r, int depth)
    {
        if (recipeLayer.TryGetValue(r, out int known)) return known;
        int layer = 1;
        if (depth < 8)
            foreach (var ing in r.Ingredients)
                if (ing.Item != null)
                    layer = Mathf.Max(layer, recipeFor.TryGetValue(ing.Item, out var sub) ? LayerOf(sub, depth + 1) : ItemLayer(ing.Item));
        return layer;
    }

    private int ItemLayer(ItemDef item) => dropLayer.TryGetValue(item, out int l) ? Mathf.Max(1, l) : 1;

    // ═════════════════════════════════════════════
    // LEFT: TILES
    // ═════════════════════════════════════════════

    private void BuildList()
    {
        scroll.Clear();
        tiles.Clear();

        var byLayer = new SortedDictionary<int, List<RecipeDef>>();
        foreach (var r in CraftingManager.Instance.GetAllRecipes())
        {
            int l = recipeLayer[r];
            if (!byLayer.TryGetValue(l, out var list)) byLayer[l] = list = new List<RecipeDef>();
            list.Add(r);
        }

        foreach (var kv in byLayer)
        {
            var sheet = new VisualElement();
            sheet.AddToClassList("ct-sheet");
            sheet.Add(Subhead(RosterPanelController.GetLayerName(kv.Key)));
            var grid = new VisualElement();
            grid.AddToClassList("ct-grid");
            foreach (var r in kv.Value) grid.Add(BuildTile(r));
            sheet.Add(grid);
            scroll.Add(sheet);
        }
        dirty = true;
    }

    private static VisualElement Subhead(string text)
    {
        var head = new VisualElement();
        head.AddToClassList("book-subhead");
        var l = new VisualElement(); l.AddToClassList("book-subhead__rule");
        var r = new VisualElement(); r.AddToClassList("book-subhead__rule"); r.AddToClassList("book-subhead__rule--right");
        var label = new Label(text); label.AddToClassList("book-subhead__label");
        head.Add(l); head.Add(label); head.Add(r);
        return head;
    }

    private VisualElement BuildTile(RecipeDef r)
    {
        var t = new Tile { recipe = r, root = new VisualElement() };
        t.root.AddToClassList("ct-tile");

        var ring = new VisualElement { pickingMode = PickingMode.Ignore };
        ring.AddToClassList("ct-sel-ring");
        t.root.Add(ring);

        t.frame = new StatusFrame(60, 4, 44, 33, 36);
        if (r.Output.icon != null) t.frame.Icon.style.backgroundImage = new StyleBackground(r.Output.icon);
        t.root.Add(t.frame);

        var name = new Label(r.Output.displayName) { pickingMode = PickingMode.Ignore };
        name.AddToClassList("ct-name");
        t.root.Add(name);

        var valueRow = new VisualElement { pickingMode = PickingMode.Ignore };
        valueRow.AddToClassList("ct-value-row");
        var coin = new VisualElement { pickingMode = PickingMode.Ignore };
        coin.AddToClassList("craft-coin");
        var value = new Label($"{r.Output.sellPrice:N0}") { pickingMode = PickingMode.Ignore };
        value.AddToClassList("ct-value");
        valueRow.Add(coin); valueRow.Add(value);
        t.root.Add(valueRow);

        t.stock = new Label("0") { pickingMode = PickingMode.Ignore };
        t.stock.AddToClassList("ct-stock");
        t.root.Add(t.stock);

        var onMark = new VisualElement { pickingMode = PickingMode.Ignore };
        onMark.AddToClassList("ct-on-mark");
        t.root.Add(onMark);

        t.root.RegisterCallback<ClickEvent>(_ => Select(r));
        tiles.Add(t);
        return t.root;
    }

    // ═════════════════════════════════════════════
    // RIGHT: DETAIL
    // ═════════════════════════════════════════════

    private void Select(RecipeDef r)
    {
        selected = r;
        foreach (var t in tiles) t.root.EnableInClassList("ct-tile--sel", t.recipe == r);
        if (r == null) return;

        detailFrame.Icon.style.backgroundImage = r.Output.icon != null ? new StyleBackground(r.Output.icon) : null;
        dName.text = r.Output.displayName;
        dStation.text = StationName(r.Station);
        dValue.text = $"{r.Output.sellPrice:N0}";
        dDesc.text = r.Output.description;
        dTime.text = $"{r.CraftSeconds:0.#}s";

        ingList.Clear();
        ingRows.Clear();
        foreach (var ing in r.Ingredients)
        {
            if (ing.Item == null) continue;
            var row = new VisualElement();
            row.AddToClassList("craft-ing");
            if (ingRows.Count > 0) row.AddToClassList("craft-ing--stacked");
            var icon = new VisualElement();
            icon.AddToClassList("craft-ing__icon");
            if (ing.Item.icon != null) icon.style.backgroundImage = new StyleBackground(ing.Item.icon);
            var name = new Label(ing.Item.displayName);
            name.AddToClassList("craft-ing__name");
            var have = new Label();
            have.AddToClassList("craft-ing__have");
            row.Add(icon); row.Add(name); row.Add(have);
            ingList.Add(row);
            ingRows.Add(new IngRow { item = ing.Item, need = ing.Qty, have = have });
        }
        Refresh();
    }

    // ═════════════════════════════════════════════
    // STATE
    // ═════════════════════════════════════════════

    private static StatusFrame.State StateOf(RecipeDef r)
    {
        var cm = CraftingManager.Instance;
        if (!cm.IsStationOwned(r.Station)) return StatusFrame.State.Locked;
        if (cm.IsCrafting(r)) return StatusFrame.State.Crafting;
        if (!cm.IsRecipeEnabled(r)) return StatusFrame.State.Idle; // not in a slot
        // Not GetBlockReason: it reports "Station busy" before cap/materials, which would hide both
        if (Inventory.Instance.Get(r.Output.itemCategory, r.Output) >= cm.GetCap(r)) return StatusFrame.State.Capped;
        return cm.CanCraft(r) ? StatusFrame.State.Idle : StatusFrame.State.Blocked;
    }

    private void Tick()
    {
        if (dirty || ++ticksSinceRefresh >= SafetyRefreshTicks) { Refresh(); return; }
        // Between refreshes only the sweeps move
        var cm = CraftingManager.Instance;
        foreach (var t in tiles)
            if (cm.IsCrafting(t.recipe)) t.frame.SetState(StatusFrame.State.Crafting, cm.GetCraftingProgress(t.recipe));
        if (selected != null && cm.IsCrafting(selected))
            detailFrame.SetState(StatusFrame.State.Crafting, cm.GetCraftingProgress(selected));
    }

    private void Refresh()
    {
        dirty = false;
        ticksSinceRefresh = 0;
        var cm = CraftingManager.Instance;
        var inv = Inventory.Instance;
        if (cm == null || inv == null) return;

        foreach (var t in tiles)
        {
            var st = StateOf(t.recipe);
            t.frame.SetState(st, cm.GetCraftingProgress(t.recipe));
            int stock = inv.Get(t.recipe.Output.itemCategory, t.recipe.Output);
            bool capped = st == StatusFrame.State.Capped;
            t.stock.text = capped ? $"{stock} / {cm.GetCap(t.recipe)}" : $"{stock}";
            t.stock.EnableInClassList("ct-stock--capped", capped);
            t.root.EnableInClassList("ct-tile--locked", st == StatusFrame.State.Locked);
            t.root.EnableInClassList("ct-tile--on", cm.IsRecipeEnabled(t.recipe));
        }

        if (selected == null) return;
        var state = StateOf(selected);
        detailFrame.SetState(state, cm.GetCraftingProgress(selected));
        dCap.text = $"{cm.GetCap(selected)}";

        // "have" = usable stock (above the item's keep floor), matching what CanCraft checks
        foreach (var row in ingRows)
        {
            int usable = Mathf.Max(0, inv.Get(row.item.itemCategory, row.item) - inv.GetFloor(row.item));
            row.have.text = $"{usable}/{row.need}";
            row.have.EnableInClassList("craft-ing__have--short", usable < row.need);
        }

        dSlot.text = SlotLine(cm, selected);

        if (!busy) RefreshStamp(state == StatusFrame.State.Locked, cm.IsRecipeEnabled(selected));
    }

    // "Forging Station · slot 1" / "· slots 1, 2" when stacked / "· not in a slot"
    private static string SlotLine(CraftingManager cm, RecipeDef r)
    {
        var slots = cm.GetSlots(r.Station);
        var held = new List<int>();
        for (int i = 0; i < slots.Count; i++) if (slots[i] == r) held.Add(i + 1);
        string where = held.Count == 0 ? "not in a slot" : held.Count == 1 ? $"slot {held[0]}" : $"slots {string.Join(", ", held)}";
        return $"{StationName(r.Station)} · {where}";
    }

    private void RefreshStamp(bool locked, bool on)
    {
        stampSlot.RemoveFromClassList("craft-stampslot--full");
        stampSlot.EnableInClassList("craft-stampslot--locked", locked);
        stampText.text = locked ? $"Needs {StationName(selected.Station)}" : "Craft";
        stampText.style.visibility = on && !locked ? Visibility.Hidden : Visibility.Visible;
        stampBadge.style.display = on && !locked ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // ═════════════════════════════════════════════
    // INPUT
    // ═════════════════════════════════════════════

    private void StepCap(int delta)
    {
        if (selected != null) CraftingManager.Instance.SetCap(selected, CraftingManager.Instance.GetCap(selected) + delta);
    }

    // The foot plank is the switch: off → slam the CRAFTING stamp on; on → dither it off.
    private void OnStampClicked()
    {
        var cm = CraftingManager.Instance;
        var r = selected;
        if (busy || r == null || !cm.IsStationOwned(r.Station)) return;
        busy = true;

        if (!cm.IsRecipeEnabled(r))
        {
            if (!cm.HasFreeSlot(r.Station)) { ShowStationFull(r); return; }
            BookStamp.AimAt(slamOverlay, stampSlot, stampSlot.resolvedStyle.width * SlamPerPlankWidth);
            BookStamp.SlamIn(stampSlamAnim, slamOverlay,
                onImpact: () =>
                {
                    bool pinned = cm.PinToFirstFreeSlot(r);
                    if (selected == r) RefreshStamp(false, pinned);
                },
                onComplete: () => { busy = false; dirty = true; });
        }
        else
        {
            stampText.style.visibility = Visibility.Visible; // "Craft" reveals as the stamp erases
            BookStamp.DitherOut(stampDitherAnim, stampBadge, stampBadgeText, () =>
            {
                busy = false;
                cm.ClearRecipeSlots(r);
                Refresh();
            });
        }
        if (showDebugLogs) Debug.Log($"[CraftingController] Stamp toggled on {r.name}");
    }

    // The stamp doesn't land: the plank jitters sideways and reads "<Station> full" for FullMs.
    private void ShowStationFull(RecipeDef r)
    {
        stampText.text = $"{StationName(r.Station)} full";
        stampSlot.AddToClassList("craft-stampslot--full");
        int step = 0;
        stampSlot.schedule.Execute(() => stampSlot.style.translate = new Translate(ShakePx[Mathf.Min(step++, ShakePx.Length - 1)], 0))
            .Every(40).Until(() => step >= ShakePx.Length);
        stampSlot.schedule.Execute(() => { busy = false; dirty = true; }).StartingIn(FullMs);
        if (showDebugLogs) Debug.Log($"[CraftingController] {r.Station} full — stamp refused for {r.name}");
    }

    // ═════════════════════════════════════════════
    // SIGNALS
    // ═════════════════════════════════════════════

    private void MarkDirty() => dirty = true;
    private void OnRecipeSignal(RecipeDef _) => dirty = true;
    private void OnStockSignal(ResourceStack _) => dirty = true;
    private void OnUpgradePurchased(GuildUpgradeDef _) => dirty = true;

    // Station display name comes from its GuildUpgradeDef (asset names don't match: "Potion Station" shows "Smithy").
    public static string StationName(CraftingStation station)
    {
        if (stationNames.TryGetValue(station, out var n)) return n;
        var type = CraftingManager.StationUpgradeType(station);
        n = station.ToString();
        foreach (var u in Resources.LoadAll<GuildUpgradeDef>("Guild Upgrades"))
            if (u.upgradeType == type) { n = u.upgradeName; break; }
        return stationNames[station] = n;
    }
}
