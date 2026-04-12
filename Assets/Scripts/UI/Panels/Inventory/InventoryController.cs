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
    private Label statTotal;
    private Label statReserved;
    private Label statForSale;
    private Label statValue;
    private SliderInt reserveSlider;
    private Toggle forsaleToggle;

    private VisualElement emptyDetailMessage;

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

        // Right page queries
        detailPanel = rightPage.Q<VisualElement>("detail-panel");
        emptyDetailMessage = rightPage.Q<VisualElement>("empty-detail-message");
        detailIcon = rightPage.Q<VisualElement>("detail-icon");
        detailName = rightPage.Q<Label>("detail-name");
        detailDescription = rightPage.Q<Label>("detail-description");
        statTotal = rightPage.Q<Label>("stat-total");
        statReserved = rightPage.Q<Label>("stat-reserved");
        statForSale = rightPage.Q<Label>("stat-forsale");
        statValue = rightPage.Q<Label>("stat-value");
        reserveSlider = rightPage.Q<SliderInt>("reserve-slider");
        forsaleToggle = rightPage.Q<Toggle>("forsale-toggle");

        // Unregister first to prevent double-registration if OnPageShown is called twice
        GameSignals.OnItemAdded -= OnInventoryChanged;
        GameSignals.OnItemSold -= OnInventoryChanged;
        GameSignals.OnProductCrafted -= OnInventoryChanged;
        GameSignals.GoldChanged -= OnGoldChanged;

        if (reserveSlider != null) reserveSlider.RegisterValueChangedCallback(OnReserveSliderChanged);
        if (forsaleToggle != null) forsaleToggle.RegisterValueChangedCallback(OnForSaleToggled);

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
        statTotal = null; statReserved = null; statForSale = null; statValue = null;
        reserveSlider = null; forsaleToggle = null;
        emptyDetailMessage = null;
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
        if (tabMaterials!= null) tabMaterials.clicked+= () => OnCategoryTabClicked(CategoryFilter.Materials);
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
        if (detailDescription != null) detailDescription.style.display = DisplayStyle.None;
        if (emptyDetailMessage != null) emptyDetailMessage.style.display = DisplayStyle.Flex;
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
        if (detailDescription != null) detailDescription.style.display = DisplayStyle.Flex;
        if (emptyDetailMessage != null) emptyDetailMessage.style.display = DisplayStyle.None;
        UpdateDetailPanel(item);
    }

    private void UpdateDetailPanel(ItemDef item)
    {
        if (item == null || detailPanel == null) return;

        if (detailIcon != null && item.icon != null)
            detailIcon.style.backgroundImage = new StyleBackground(item.icon);
        if (detailName != null) detailName.text = item.displayName;
        if (detailDescription != null) detailDescription.text = item.description;

        int totalStock = SalesManager.Instance.GetTotalStock(item);
        int reserved = SalesManager.Instance.GetReservedAmount(item);
        int forSale = SalesManager.Instance.GetAvailableForSale(item);

        if (statTotal != null) statTotal.text = $"Total Stock: {totalStock}";
        if (statReserved != null) statReserved.text = $"Reserved: {reserved}";
        if (statForSale != null) statForSale.text = $"For Sale: {forSale}";
        if (statValue != null) statValue.text = $"Unit Value: {item.sellPrice}g";

        if (reserveSlider != null)
        {
            reserveSlider.highValue = totalStock;
            reserveSlider.SetValueWithoutNotify(reserved);
        }
        if (forsaleToggle != null)
            forsaleToggle.SetValueWithoutNotify(SalesManager.Instance.IsMarkedForSale(item));
    }

    // ═════════════════════════════════════════════
    // CALLBACKS
    // ═════════════════════════════════════════════

    private void OnReserveSliderChanged(ChangeEvent<int> evt)
    {
        if (selectedItem == null) return;
        Inventory.Instance.SetReserve(selectedItem, evt.newValue);
        IncrementalUpdateItem(selectedItem);
    }

    private void OnForSaleToggled(ChangeEvent<bool> evt)
    {
        if (selectedItem == null) return;
        SalesManager.Instance.SetMarkedForSale(selectedItem, evt.newValue);

        if (currentCategory == CategoryFilter.ForSale && !evt.newValue)
        {
            RefreshGrid();
            return;
        }

        if (itemSlots.TryGetValue(selectedItem, out var slot))
        {
            UpdateSlotBadges(slot, selectedItem);
            slot.Q<VisualElement>("forsale-sticker")?.SetDisplay(evt.newValue ? DisplayStyle.Flex : DisplayStyle.None);
        }
        UpdateDetailPanel(selectedItem);
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
