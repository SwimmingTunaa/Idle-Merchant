using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Inventory page — item grid on the left, item detail on the right.
/// Implements IBookPage; lives inside BookPanelController as tab 2.
/// </summary>
public class InventoryController : MonoBehaviour, IBookPage
{
    [Header("UXML References")]
    [SerializeField] private VisualTreeAsset itemSlotAsset;

    [Header("Grid Settings")]
    [SerializeField] private int baseSlotCount = 15;
    [SerializeField] private int slotsPerRow = 5;

    [Header("For-Sale Stamp Animation")]
    [Tooltip("Dither-erase played on the badge stamp when un-marking an item (frame 0 = full stamp, last = erased).")]
    [SerializeField] private FrameAnim forsaleDitherAnim = new() { frameMs = 40, end = UIFrameAnimator.EndBehaviour.HideAndReset };
    [Tooltip("Big stamp-slam overlay played across the page when marking an item for sale.")]
    [SerializeField] private FrameAnim stampSlamAnim = new() { frameMs = 40, fadeMs = 150, end = UIFrameAnimator.EndBehaviour.HideAndReset };

    private VisualElement itemGrid;
    private ScrollView itemGridScroll;
    private VisualElement detailPanel;

    private Button tabAll;
    private Button tabForSale;
    private Button tabMaterials;
    private Button tabCrafted;
    private Button tabLuxury;

    private Label categoryTitle;
    private Label capacityLabel;
    private Label goldLabel;

    private VisualElement detailIcon;
    private Label detailName;
    private Label detailDescription;
    private VisualElement forsaleBadge;
    private Label forsaleLabel;
    private VisualElement forsaleStamp;
    private VisualElement stampAnim;
    private Label itemValueLabel;
    private Label reserveCountLabel;
    private VisualElement reserveUpButton;
    private VisualElement reserveDownButton;
    private IVisualElementScheduledItem reserveHoldRepeat;
    private const int ReserveHoldInitialDelayMs = 350;
    private const int ReserveHoldIntervalMs = 70;

    private bool filterBarInitialized = false;

    private enum CategoryFilter { All, ForSale, Materials, Crafted, Luxury }
    private CategoryFilter currentCategory = CategoryFilter.All;
    private ItemDef selectedItem = null;
    private Dictionary<ItemDef, VisualElement> itemSlots = new();

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    public string PageTitle => "Inventory";

    void Awake() => hideFlags = HideFlags.HideInInspector;

    // ═════════════════════════════════════════════
    // IBOOK PAGE
    // ═════════════════════════════════════════════

    public void OnPageShown(VisualElement leftPage, VisualElement rightPage)
    {
        // Left page queries (filter tabs come from SetFilterBar — they live outside the page template)
        itemGrid = leftPage.Q<VisualElement>("item-grid");
        itemGridScroll = leftPage.Q<ScrollView>("item-grid-scroll");
        categoryTitle = leftPage.Q<Label>("category-title");
        capacityLabel = leftPage.Q<Label>("capacity-label");
        goldLabel = leftPage.Q<Label>("gold-label");

        // Right page queries — rightPage IS the detail panel root
        detailPanel = rightPage;
        detailIcon = rightPage.Q<VisualElement>("detail-icon");
        detailName = rightPage.Q<Label>("detail-name");
        detailDescription = rightPage.Q<Label>("detail-description");
        forsaleBadge = rightPage.Q<VisualElement>("forsale-badge");
        forsaleLabel = rightPage.Q<Label>("forsale-label");
        forsaleStamp = rightPage.Q<VisualElement>("forsale-stamp");
        // Stampanim lives at page-root level (sibling of right-content) so it overlays the whole page.
        stampAnim = (rightPage.parent ?? rightPage).Q<VisualElement>("Stampanim");
        itemValueLabel = rightPage.Q<Label>("item-value");
        reserveCountLabel = rightPage.Q<Label>("reserve-count");
        reserveUpButton = rightPage.Q<VisualElement>("reserve-up");
        reserveDownButton = rightPage.Q<VisualElement>("reserve-down");

        if (reserveUpButton != null)
        {
            reserveUpButton.RegisterCallback<PointerDownEvent>(OnReserveUpPointerDown);
            reserveUpButton.RegisterCallback<PointerUpEvent>(OnReservePointerRelease);
            reserveUpButton.RegisterCallback<PointerLeaveEvent>(OnReservePointerRelease);
        }
        if (reserveDownButton != null)
        {
            reserveDownButton.RegisterCallback<PointerDownEvent>(OnReserveDownPointerDown);
            reserveDownButton.RegisterCallback<PointerUpEvent>(OnReservePointerRelease);
            reserveDownButton.RegisterCallback<PointerLeaveEvent>(OnReservePointerRelease);
        }

        if (stampAnim != null) stampAnim.style.display = DisplayStyle.None;

        // Both the "Not For Sale" badge and the "FOR SALE" stamp toggle the state.
        // The handler routes by current state: not-for-sale → slam-in, for-sale → dither-out.
        if (forsaleBadge != null)
            forsaleBadge.RegisterCallback<ClickEvent>(OnForSaleBadgeClicked);
        if (forsaleStamp != null)
            forsaleStamp.RegisterCallback<ClickEvent>(OnForSaleBadgeClicked);

        // Unregister first to prevent double-registration if OnPageShown is called twice
        GameSignals.OnItemAdded -= OnInventoryChanged;
        GameSignals.OnItemSold -= OnInventoryChanged;
        GameSignals.OnProductCrafted -= OnInventoryChanged;
        GameSignals.GoldChanged -= OnGoldChanged;

        GameSignals.OnItemAdded += OnInventoryChanged;
        GameSignals.OnItemSold += OnInventoryChanged;
        GameSignals.OnProductCrafted += OnInventoryChanged;
        GameSignals.GoldChanged += OnGoldChanged;

        UpdateCategoryTabStates();
        UpdateHeaderLabels();
        RefreshGrid();
    }

    public void OnPageHidden()
    {
        GameSignals.OnItemAdded -= OnInventoryChanged;
        GameSignals.OnItemSold -= OnInventoryChanged;
        GameSignals.OnProductCrafted -= OnInventoryChanged;
        GameSignals.GoldChanged -= OnGoldChanged;

        itemGrid = null; itemGridScroll = null; detailPanel = null;
        // Note: tabAll/tabMaterials/tabCrafted/tabLuxury/tabForSale are intentionally kept —
        // they reference the persistent inventory-filter-bar in book-root (set via SetFilterBar).
        categoryTitle = null; capacityLabel = null; goldLabel = null;
        detailIcon = null; detailName = null; detailDescription = null;
        if (forsaleBadge != null) forsaleBadge.UnregisterCallback<ClickEvent>(OnForSaleBadgeClicked);
        if (forsaleStamp != null) forsaleStamp.UnregisterCallback<ClickEvent>(OnForSaleBadgeClicked);
        if (reserveUpButton != null)
        {
            reserveUpButton.UnregisterCallback<PointerDownEvent>(OnReserveUpPointerDown);
            reserveUpButton.UnregisterCallback<PointerUpEvent>(OnReservePointerRelease);
            reserveUpButton.UnregisterCallback<PointerLeaveEvent>(OnReservePointerRelease);
        }
        if (reserveDownButton != null)
        {
            reserveDownButton.UnregisterCallback<PointerDownEvent>(OnReserveDownPointerDown);
            reserveDownButton.UnregisterCallback<PointerUpEvent>(OnReservePointerRelease);
            reserveDownButton.UnregisterCallback<PointerLeaveEvent>(OnReservePointerRelease);
        }
        StopReserveHold();
        forsaleStamp?.Stop();
        stampAnim?.Stop();
        forsaleBadge = null; forsaleLabel = null; forsaleStamp = null; stampAnim = null; itemValueLabel = null; reserveCountLabel = null;
        reserveUpButton = null; reserveDownButton = null;
        itemSlots.Clear();
        selectedItem = null;
    }

    // ═════════════════════════════════════════════
    // FILTER BAR (set once from BookPanelController)
    // ═════════════════════════════════════════════

    /// <summary>
    /// Called once by BookPanelController after BuildUI.
    /// The filter bar lives in book-root (outside the page template) so it
    /// persists across page switches — we only wire click handlers once.
    /// </summary>
    public void SetFilterBar(VisualElement filterBar)
    {
        if (filterBar == null || filterBarInitialized) return;

        tabAll      = filterBar.Q<Button>("tab-all");
        tabForSale  = filterBar.Q<Button>("tab-forsale");
        tabMaterials = filterBar.Q<Button>("tab-materials");
        tabCrafted  = filterBar.Q<Button>("tab-crafted");
        tabLuxury   = filterBar.Q<Button>("tab-luxury");

        if (tabAll      != null) tabAll.clicked      += () => OnCategoryTabClicked(CategoryFilter.All);
        if (tabForSale  != null) tabForSale.clicked  += () => OnCategoryTabClicked(CategoryFilter.ForSale);
        if (tabMaterials != null) tabMaterials.clicked += () => OnCategoryTabClicked(CategoryFilter.Materials);
        if (tabCrafted  != null) tabCrafted.clicked  += () => OnCategoryTabClicked(CategoryFilter.Crafted);
        if (tabLuxury   != null) tabLuxury.clicked   += () => OnCategoryTabClicked(CategoryFilter.Luxury);

        filterBarInitialized = true;
    }

    // ═════════════════════════════════════════════
    // CATEGORY TABS
    // ═════════════════════════════════════════════

    private void OnCategoryTabClicked(CategoryFilter category)
    {
        currentCategory = category;
        UpdateCategoryTabStates();
        RefreshGrid();
        if (categoryTitle != null)
        {
            categoryTitle.text = category switch
            {
                CategoryFilter.All => "All Items",
                CategoryFilter.ForSale => "For Sale",
                CategoryFilter.Materials => "Materials",
                CategoryFilter.Crafted => "Crafted Goods",
                CategoryFilter.Luxury => "Luxury Items",
                _ => "Inventory"
            };
        }
    }

    private void UpdateCategoryTabStates()
    {
        tabAll?.RemoveFromClassList("category-tab-selected");
        tabForSale?.RemoveFromClassList("category-tab-selected");
        tabMaterials?.RemoveFromClassList("category-tab-selected");
        tabCrafted?.RemoveFromClassList("category-tab-selected");
        tabLuxury?.RemoveFromClassList("category-tab-selected");

        switch (currentCategory)
        {
            case CategoryFilter.All:      tabAll?.AddToClassList("category-tab-selected"); break;
            case CategoryFilter.ForSale:  tabForSale?.AddToClassList("category-tab-selected"); break;
            case CategoryFilter.Materials: tabMaterials?.AddToClassList("category-tab-selected"); break;
            case CategoryFilter.Crafted:  tabCrafted?.AddToClassList("category-tab-selected"); break;
            case CategoryFilter.Luxury:   tabLuxury?.AddToClassList("category-tab-selected"); break;
        }
    }

    // ═════════════════════════════════════════════
    // GRID
    // ═════════════════════════════════════════════

    private void RefreshGrid()
    {
        if (itemGrid == null) return;
        itemGrid.Clear();
        itemSlots.Clear();

        var allItems = GetFilteredItems();
        foreach (var item in allItems)
            CreateItemSlot(item);

        int totalSlots = CalculateTotalSlots(allItems.Count);
        for (int i = 0; i < totalSlots - allItems.Count; i++)
            CreateEmptySlot();

        if (allItems.Count > 0)
            SelectFirstItem(allItems[0]);
        else
            ShowEmptyDetailPanel();
    }

    private void IncrementalUpdateItem(ItemDef item)
    {
        if (!itemSlots.TryGetValue(item, out var slot)) return;

        int quantity = Inventory.Instance.Get(item.itemCategory, item);
        var quantityLabel = slot.Q<Label>("slot-quantity");
        if (quantityLabel != null) quantityLabel.text = $"x{quantity}";

        UpdateSlotBadges(slot, item);

        if (currentCategory == CategoryFilter.ForSale)
        {
            int available = SalesManager.Instance.GetAvailableForSale(item);
            slot.style.opacity = available > 0 ? 1f : 0.5f;
        }

        if (selectedItem == item) UpdateDetailPanel(item);
    }

    private void SelectFirstItem(ItemDef item)
    {
        selectedItem = item;
        ShowDetailPanel(item);
        if (itemSlots.TryGetValue(item, out var slot))
            slot.AddToClassList("item-slot-selected");
    }

    private void ShowEmptyDetailPanel()
    {
        selectedItem = null;
        if (detailIcon != null) detailIcon.style.backgroundImage = null;
        if (detailName != null) detailName.text = "Select an item";
        if (detailDescription != null) detailDescription.text = "Select an item to view details.";
        if (forsaleLabel != null) { forsaleLabel.text = "Not For Sale"; forsaleLabel.style.display = DisplayStyle.Flex; }
        if (forsaleStamp != null) forsaleStamp.style.display = DisplayStyle.None;
        if (itemValueLabel != null) itemValueLabel.text = "—";
        if (reserveCountLabel != null) reserveCountLabel.text = "0";
        foreach (var kvp in itemSlots) kvp.Value.RemoveFromClassList("item-slot-selected");
    }

    private int CalculateTotalSlots(int itemCount)
    {
        if (itemCount < baseSlotCount) return baseSlotCount;
        int extraRows = Mathf.CeilToInt((itemCount - baseSlotCount) / (float)slotsPerRow);
        return baseSlotCount + ((extraRows + 1) * slotsPerRow);
    }

    private List<ItemDef> GetFilteredItems()
    {
        var snapshot = Inventory.Instance.SnapshotAll();
        var items = new List<ItemDef>();
        foreach (var row in snapshot)
        {
            if (row.qty <= 0) continue;
            bool matches = currentCategory switch
            {
                CategoryFilter.All => true,
                CategoryFilter.ForSale => SalesManager.Instance.IsMarkedForSale(row.item),
                CategoryFilter.Materials => row.category == ItemCategory.Basic,
                CategoryFilter.Crafted => row.category == ItemCategory.Advanced,
                CategoryFilter.Luxury => row.category == ItemCategory.Premium,
                _ => false
            };
            if (matches) items.Add(row.item);
        }

        if (currentCategory == CategoryFilter.ForSale)
            items = items.OrderByDescending(item => SalesManager.Instance.GetAvailableForSale(item)).ToList();

        return items;
    }

    private void CreateItemSlot(ItemDef item)
    {
        if (itemSlotAsset == null) return;
        var slot = itemSlotAsset.CloneTree().Q<VisualElement>("item-slot");
        if (slot == null) return;

        var icon = slot.Q<VisualElement>("slot-icon");
        if (icon != null && item.icon != null)
            icon.style.backgroundImage = new StyleBackground(item.icon);

        int quantity = Inventory.Instance.Get(item.itemCategory, item);
        var quantityLabel = slot.Q<Label>("slot-quantity");
        if (quantityLabel != null) quantityLabel.text = $"x{quantity}";

        UpdateSlotBadges(slot, item);

        var forsaleSticker = slot.Q<VisualElement>("forsale-sticker");
        if (forsaleSticker != null && SalesManager.Instance.IsMarkedForSale(item))
            forsaleSticker.style.display = DisplayStyle.Flex;

        if (currentCategory == CategoryFilter.ForSale)
        {
            int available = SalesManager.Instance.GetAvailableForSale(item);
            if (available <= 0) slot.style.opacity = 0.5f;
        }

        slot.RegisterCallback<ClickEvent>(evt => OnItemSlotClicked(item));
        itemGrid.Add(slot);
        itemSlots[item] = slot;
    }

    private void CreateEmptySlot()
    {
        if (itemSlotAsset == null) return;
        var slot = itemSlotAsset.CloneTree().Q<VisualElement>("item-slot");
        if (slot == null) return;
        slot.AddToClassList("item-slot-empty");
        slot.Q<Label>("slot-quantity")?.SetDisplay(DisplayStyle.None);
        slot.Q<Label>("slot-reserved")?.SetDisplay(DisplayStyle.None);
        slot.Q<Label>("slot-forsale")?.SetDisplay(DisplayStyle.None);
        slot.Q<VisualElement>("forsale-sticker")?.SetDisplay(DisplayStyle.None);
        itemGrid.Add(slot);
    }

    private void UpdateSlotBadges(VisualElement slot, ItemDef item)
    {
        if (slot == null || item == null) return;
        int reserved = SalesManager.Instance.GetReservedAmount(item);
        int forSale = SalesManager.Instance.GetAvailableForSale(item);

        var reservedBadge = slot.Q<Label>("slot-reserved");
        if (reservedBadge != null)
        {
            reservedBadge.style.display = reserved > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (reserved > 0) reservedBadge.text = $"Held: {reserved}";
        }

        var forSaleBadge = slot.Q<Label>("slot-forsale");
        if (forSaleBadge != null)
        {
            forSaleBadge.style.display = forSale > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (forSale > 0) forSaleBadge.text = $"Sale: {forSale}";
        }
    }

    private void OnItemSlotClicked(ItemDef item)
    {
        selectedItem = item;
        ShowDetailPanel(item);
        foreach (var kvp in itemSlots)
            kvp.Value.EnableInClassList("item-slot-selected", kvp.Key == item);
    }

    private void ShowDetailPanel(ItemDef item)
    {
        if (detailPanel == null) return;
        UpdateDetailPanel(item);
    }

    private void UpdateDetailPanel(ItemDef item)
    {
        if (item == null || detailPanel == null) return;

        if (detailIcon != null)
            detailIcon.style.backgroundImage = item.icon != null ? new StyleBackground(item.icon) : null;
        if (detailName != null) detailName.text = item.displayName;
        if (detailDescription != null) detailDescription.text = item.description;

        int reserved = SalesManager.Instance.GetReservedAmount(item);
        bool isForSale = SalesManager.Instance.IsMarkedForSale(item);

        if (forsaleLabel != null) forsaleLabel.style.display = isForSale ? DisplayStyle.None : DisplayStyle.Flex;
        if (forsaleStamp != null) forsaleStamp.style.display = isForSale ? DisplayStyle.Flex : DisplayStyle.None;
        if (itemValueLabel != null) itemValueLabel.text = $"{item.sellPrice}";
        if (reserveCountLabel != null) reserveCountLabel.text = $"{reserved}";
    }

    // ═════════════════════════════════════════════
    // CALLBACKS
    // ═════════════════════════════════════════════

    private void OnForSaleBadgeClicked(ClickEvent evt)
    {
        if (selectedItem == null) return;

        bool newState = !SalesManager.Instance.IsMarkedForSale(selectedItem);
        SalesManager.Instance.SetMarkedForSale(selectedItem, newState);

        var item = selectedItem;

        // Update the slot's sticker badge immediately
        if (itemSlots.TryGetValue(item, out var slot))
        {
            UpdateSlotBadges(slot, item);
            slot.Q<VisualElement>("forsale-sticker")?.SetDisplay(newState ? DisplayStyle.Flex : DisplayStyle.None);
        }

        if (newState)
        {
            // Marking ON: play the big stamp-slam overlay, then settle into the for-sale state
            PlayStampSlamIn(() =>
            {
                if (selectedItem == item) UpdateDetailPanel(item);
            });
        }
        else
        {
            // Marking OFF: play dither-out animation on the stamp, then update panel
            PlayStampDitherOut(() =>
            {
                if (currentCategory == CategoryFilter.ForSale)
                    RefreshGrid();
                else if (selectedItem == item)
                    UpdateDetailPanel(item);
            });
        }
    }

    // Reserve stepper — supports hold-to-repeat. PointerDown fires StepReserve once,
    // then after a short delay starts auto-repeating until pointer is released or leaves.
    private void OnReserveUpPointerDown(PointerDownEvent evt) => StartReserveHold(+1);
    private void OnReserveDownPointerDown(PointerDownEvent evt) => StartReserveHold(-1);
    private void OnReservePointerRelease(EventBase evt) => StopReserveHold();

    private void StartReserveHold(int delta)
    {
        StopReserveHold();
        StepReserve(delta); // immediate first step on press
        // Then after the initial delay, auto-repeat. Use detailPanel as a stable schedule host.
        var host = detailPanel ?? reserveUpButton;
        if (host == null) return;
        reserveHoldRepeat = host.schedule
            .Execute(() => StepReserve(delta))
            .StartingIn(ReserveHoldInitialDelayMs)
            .Every(ReserveHoldIntervalMs);
    }

    private void StopReserveHold()
    {
        reserveHoldRepeat?.Pause();
        reserveHoldRepeat = null;
    }

    private void StepReserve(int delta)
    {
        if (selectedItem == null) return;
        int totalStock = SalesManager.Instance.GetTotalStock(selectedItem);
        int current = Inventory.Instance.GetReserve(selectedItem);
        int next = Mathf.Clamp(current + delta, 0, totalStock);
        if (next == current) return; // already at bound — no work
        Inventory.Instance.SetReserve(selectedItem, next);
        // Refresh the slot badge + detail labels for the changed item
        IncrementalUpdateItem(selectedItem);
    }

    // Big stamp-slam overlay (Stampanim) — plays the Stamp sheet across the right page when
    // an item is marked for sale. The persistent badge stamp is revealed at the impact frame
    // (midway through the slam) rather than at the end, so it lands in sync with the slam.
    private void PlayStampSlamIn(System.Action onComplete)
    {
        // Hide the small badge label/stamp during the slam so only the dramatic overlay shows
        if (forsaleLabel != null) forsaleLabel.style.display = DisplayStyle.None;
        if (forsaleStamp != null) forsaleStamp.style.display = DisplayStyle.None;

        if (stampAnim == null || stampSlamAnim?.frames == null || stampSlamAnim.frames.Length == 0)
        {
            // No overlay — just reveal the badge stamp now
            if (forsaleStamp != null) forsaleStamp.style.display = DisplayStyle.Flex;
            onComplete?.Invoke();
            return;
        }

        int impactFrame = stampSlamAnim.frames.Length / 2; // midway = the "slam lands" moment

        stampAnim.style.display = DisplayStyle.Flex;
        stampSlamAnim.Play(
            stampAnim,
            onComplete: onComplete,
            onFrame: idx =>
            {
                // Reveal the persistent badge stamp the instant the slam reaches impact
                if (idx == impactFrame && forsaleStamp != null)
                    forsaleStamp.style.display = DisplayStyle.Flex;
            });
    }

    // Pixel-art dither-erase: swap backgroundImage through the for-sale-stamp-sheet frames.
    // Frame 0 is the full stamp, the last frame is fully erased. The overlay "FOR SALE"
    // text is hidden during the animation since the dither frames have the text baked in,
    // and "Not For Sale" is shown underneath so it reveals as the stamp erases.
    private void PlayStampDitherOut(System.Action onComplete)
    {
        if (forsaleStamp == null) { onComplete?.Invoke(); return; }

        var stampText = forsaleStamp.Q<Label>("forsale-stamp-text");
        if (stampText != null) stampText.style.display = DisplayStyle.None;
        if (forsaleLabel != null) forsaleLabel.style.display = DisplayStyle.Flex;

        forsaleDitherAnim.Play(
            forsaleStamp,
            onComplete: () =>
            {
                // Restore the text so the next time the stamp is shown (re-marked for sale),
                // the label appears overlaid on frame 0 correctly.
                if (stampText != null) stampText.style.display = DisplayStyle.Flex;
                onComplete?.Invoke();
            });
    }

    private void UpdateHeaderLabels()
    {
        if (goldLabel != null)
            goldLabel.text = $"{Inventory.Instance.Gold}";
        if (capacityLabel != null)
        {
            int totalItems = Inventory.Instance.SnapshotAll().Sum(r => r.qty);
            capacityLabel.text = $"Items: {totalItems}";
        }
    }

    private void OnGoldChanged(int newTotal)
    {
        if (goldLabel != null) goldLabel.text = $"{newTotal}g";
    }

    private void OnInventoryChanged(ResourceStack stack)
    {
        if (stack.itemDef != null && itemSlots.ContainsKey(stack.itemDef))
        {
            IncrementalUpdateItem(stack.itemDef);
            UpdateHeaderLabels();
        }
        else
        {
            RefreshGrid();
            UpdateHeaderLabels();
        }
    }
}

// Extension helpers to reduce verbosity
internal static class VisualElementDisplayExtensions
{
    internal static void SetDisplay(this VisualElement ve, DisplayStyle style)
    {
        if (ve != null) ve.style.display = style;
    }
}
