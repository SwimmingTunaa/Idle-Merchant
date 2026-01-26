using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class InventoryController : BasePanelController
{
    [Header("UXML References")]
    [SerializeField] private VisualTreeAsset itemSlotAsset;

    [Header("Grid Settings")]
    [SerializeField] private int baseSlotCount = 15;
    [SerializeField] private int slotsPerRow = 5;

    public override string PanelID => "InventoryPanel";

    private VisualElement itemGrid;
    private ScrollView itemGridScroll;
    private VisualElement detailPanel;
    
    private Button tabAll;
    private Button tabForSale;
    private Button tabMaterials;
    private Button tabCrafted;
    private Button tabLuxury;
    private Button closeButton;
    
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

    private enum CategoryFilter { All, ForSale, Materials, Crafted, Luxury }
    private CategoryFilter currentCategory = CategoryFilter.All;
    private ItemDef selectedItem = null;
    private Dictionary<ItemDef, VisualElement> itemSlots = new();

    void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();
        
        BuildUI();
    }

    void Update()
    {
        if (State == PanelState.Open)
        {
            UpdateHeaderLabels();
            
            if (selectedItem != null && detailPanel != null && detailPanel.style.display == DisplayStyle.Flex)
            {
                UpdateDetailPanel(selectedItem);
            }
        }
    }

    protected override void Start()
    {
        base.Start();
        
        GameSignals.OnItemAdded += OnInventoryChanged;
        GameSignals.OnItemSold += OnInventoryChanged;
        GameSignals.OnProductCrafted += OnInventoryChanged;
        GameSignals.GoldChanged += OnGoldChanged;
    }

    protected override void OnDestroy()
    {
        GameSignals.OnItemAdded -= OnInventoryChanged;
        GameSignals.OnItemSold -= OnInventoryChanged;
        GameSignals.OnProductCrafted -= OnInventoryChanged;
        GameSignals.GoldChanged -= OnGoldChanged;
        
        base.OnDestroy();
    }

    protected override void OnOpenStart()
    {
        UpdateHeaderLabels();
        RefreshGrid();
        if (showDebugLogs)
            Debug.Log("[InventoryController] Panel opened");
    }

    protected override void BuildUI()
    {
        base.BuildUI();

        if (panel == null)
        {
            Debug.LogError("[InventoryController] Panel is null after BuildUI!");
            return;
        }

        tabAll = panel.Q<Button>("tab-all");
        tabForSale = panel.Q<Button>("tab-forsale");
        tabMaterials = panel.Q<Button>("tab-materials");
        tabCrafted = panel.Q<Button>("tab-crafted");
        tabLuxury = panel.Q<Button>("tab-luxury");
        closeButton = panel.Q<Button>("close-button");

        itemGrid = panel.Q<VisualElement>("item-grid");
        itemGridScroll = panel.Q<ScrollView>("item-grid-scroll");

        categoryTitle = panel.Q<Label>("category-title");
        capacityLabel = panel.Q<Label>("capacity-label");
        goldLabel = panel.Q<Label>("gold-label");

        detailPanel = panel.Q<VisualElement>("detail-panel");
        detailIcon = panel.Q<VisualElement>("detail-icon");
        detailName = panel.Q<Label>("detail-name");
        detailDescription = panel.Q<Label>("detail-description");
        statTotal = panel.Q<Label>("stat-total");
        statReserved = panel.Q<Label>("stat-reserved");
        statForSale = panel.Q<Label>("stat-forsale");
        statValue = panel.Q<Label>("stat-value");
        reserveSlider = panel.Q<SliderInt>("reserve-slider");
        forsaleToggle = panel.Q<Toggle>("forsale-toggle");

        if (tabAll != null) tabAll.clicked += () => OnCategoryTabClicked(CategoryFilter.All);
        if (tabForSale != null) tabForSale.clicked += () => OnCategoryTabClicked(CategoryFilter.ForSale);
        if (tabMaterials != null) tabMaterials.clicked += () => OnCategoryTabClicked(CategoryFilter.Materials);
        if (tabCrafted != null) tabCrafted.clicked += () => OnCategoryTabClicked(CategoryFilter.Crafted);
        if (tabLuxury != null) tabLuxury.clicked += () => OnCategoryTabClicked(CategoryFilter.Luxury);
        if (closeButton != null) closeButton.clicked += () => Close();

        if (reserveSlider != null) reserveSlider.RegisterValueChangedCallback(OnReserveSliderChanged);
        if (forsaleToggle != null) forsaleToggle.RegisterValueChangedCallback(OnForSaleToggled);

        panel.style.opacity = 0f;
        
        UpdateCategoryTabStates();
    }

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
        if (tabAll != null) tabAll.RemoveFromClassList("category-tab-selected");
        if (tabForSale != null) tabForSale.RemoveFromClassList("category-tab-selected");
        if (tabMaterials != null) tabMaterials.RemoveFromClassList("category-tab-selected");
        if (tabCrafted != null) tabCrafted.RemoveFromClassList("category-tab-selected");
        if (tabLuxury != null) tabLuxury.RemoveFromClassList("category-tab-selected");

        switch (currentCategory)
        {
            case CategoryFilter.All:
                if (tabAll != null) tabAll.AddToClassList("category-tab-selected");
                break;
            case CategoryFilter.ForSale:
                if (tabForSale != null) tabForSale.AddToClassList("category-tab-selected");
                break;
            case CategoryFilter.Materials:
                if (tabMaterials != null) tabMaterials.AddToClassList("category-tab-selected");
                break;
            case CategoryFilter.Crafted:
                if (tabCrafted != null) tabCrafted.AddToClassList("category-tab-selected");
                break;
            case CategoryFilter.Luxury:
                if (tabLuxury != null) tabLuxury.AddToClassList("category-tab-selected");
                break;
        }
    }

    private void RefreshGrid()
    {
        if (itemGrid == null) return;

        itemGrid.Clear();
        itemSlots.Clear();

        var allItems = GetFilteredItems();
        int itemCount = allItems.Count;

        foreach (var item in allItems)
        {
            CreateItemSlot(item);
        }

        int totalSlots = CalculateTotalSlots(itemCount);
        int emptySlots = totalSlots - itemCount;

        for (int i = 0; i < emptySlots; i++)
        {
            CreateEmptySlot();
        }
    }

    private int CalculateTotalSlots(int itemCount)
    {
        if (itemCount < baseSlotCount)
        {
            return baseSlotCount;
        }

        int extraRowsNeeded = Mathf.CeilToInt((itemCount - baseSlotCount) / (float)slotsPerRow);
        return baseSlotCount + ((extraRowsNeeded + 1) * slotsPerRow);
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
                CategoryFilter.Materials => row.category == ItemCategory.Common,
                CategoryFilter.Crafted => row.category == ItemCategory.Crafted,
                CategoryFilter.Luxury => row.category == ItemCategory.Luxury,
                _ => false
            };

            if (matches)
                items.Add(row.item);
        }

        if (currentCategory == CategoryFilter.ForSale)
        {
            items = items.OrderByDescending(item => SalesManager.Instance.GetAvailableForSale(item)).ToList();
        }

        return items;
    }

    private void CreateItemSlot(ItemDef item)
    {
        if (itemSlotAsset == null)
        {
            Debug.LogError("[InventoryController] itemSlotAsset is null!");
            return;
        }

        var slot = itemSlotAsset.CloneTree().Q<VisualElement>("item-slot");
        if (slot == null)
        {
            Debug.LogError("[InventoryController] Failed to find 'item-slot' in cloned tree!");
            return;
        }
        
        var icon = slot.Q<VisualElement>("slot-icon");
        if (icon != null && item.icon != null)
            icon.style.backgroundImage = new StyleBackground(item.icon);

        int quantity = Inventory.Instance.Get(item.itemCategory, item);
        var quantityLabel = slot.Q<Label>("slot-quantity");
        if (quantityLabel != null)
            quantityLabel.text = $"x{quantity}";

        UpdateSlotBadges(slot, item);

        var forsaleSticker = slot.Q<VisualElement>("forsale-sticker");
        if (forsaleSticker != null && SalesManager.Instance.IsMarkedForSale(item))
        {
            forsaleSticker.style.display = DisplayStyle.Flex;
        }

        if (currentCategory == CategoryFilter.ForSale)
        {
            int available = SalesManager.Instance.GetAvailableForSale(item);
            if (available <= 0)
            {
                slot.style.opacity = 0.5f;
            }
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

        var quantityLabel = slot.Q<Label>("slot-quantity");
        if (quantityLabel != null)
            quantityLabel.style.display = DisplayStyle.None;

        var reservedBadge = slot.Q<Label>("slot-reserved");
        if (reservedBadge != null)
            reservedBadge.style.display = DisplayStyle.None;

        var forSaleBadge = slot.Q<Label>("slot-forsale");
        if (forSaleBadge != null)
            forSaleBadge.style.display = DisplayStyle.None;

        var forsaleSticker = slot.Q<VisualElement>("forsale-sticker");
        if (forsaleSticker != null)
            forsaleSticker.style.display = DisplayStyle.None;

        itemGrid.Add(slot);
    }

    private void UpdateSlotBadges(VisualElement slot, ItemDef item)
    {
        if (slot == null || item == null) return;

        int reserved = SalesManager.Instance.GetReservedAmount(item);
        int forSale = SalesManager.Instance.GetAvailableForSale(item);

        var reservedBadge = slot.Q<Label>("slot-reserved");
        var forSaleBadge = slot.Q<Label>("slot-forsale");

        if (reservedBadge != null)
        {
            if (reserved > 0)
            {
                reservedBadge.style.display = DisplayStyle.Flex;
                reservedBadge.text = $"📦 {reserved}";
            }
            else
            {
                reservedBadge.style.display = DisplayStyle.None;
            }
        }

        if (forSaleBadge != null)
        {
            if (forSale > 0)
            {
                forSaleBadge.style.display = DisplayStyle.Flex;
                forSaleBadge.text = $"💰 {forSale}";
            }
            else
            {
                forSaleBadge.style.display = DisplayStyle.None;
            }
        }
    }

    private void OnItemSlotClicked(ItemDef item)
    {
        selectedItem = item;
        ShowDetailPanel(item);
        
        foreach (var kvp in itemSlots)
        {
            if (kvp.Key == item)
                kvp.Value.AddToClassList("item-slot-selected");
            else
                kvp.Value.RemoveFromClassList("item-slot-selected");
        }
    }

    private void ShowDetailPanel(ItemDef item)
    {
        if (detailPanel != null)
        {
            detailPanel.style.display = DisplayStyle.Flex;
            UpdateDetailPanel(item);
        }
    }

    private void UpdateDetailPanel(ItemDef item)
    {
        if (item == null || detailPanel == null) return;

        if (detailIcon != null && item.icon != null)
            detailIcon.style.backgroundImage = new StyleBackground(item.icon);

        if (detailName != null)
            detailName.text = item.displayName;
        
        if (detailDescription != null)
            detailDescription.text = item.description;

        int totalStock = SalesManager.Instance.GetTotalStock(item);
        int reserved = SalesManager.Instance.GetReservedAmount(item);
        int forSale = SalesManager.Instance.GetAvailableForSale(item);

        if (statTotal != null)
            statTotal.text = $"Total Stock: {totalStock}";
        
        if (statReserved != null)
            statReserved.text = $"Reserved: {reserved}";
        
        if (statForSale != null)
            statForSale.text = $"For Sale: {forSale}";
        
        if (statValue != null)
            statValue.text = $"Unit Value: {item.sellPrice}g";

        if (reserveSlider != null)
        {
            reserveSlider.highValue = totalStock;
            reserveSlider.SetValueWithoutNotify(reserved);
        }

        if (forsaleToggle != null)
            forsaleToggle.SetValueWithoutNotify(SalesManager.Instance.IsMarkedForSale(item));
    }

    private void OnReserveSliderChanged(ChangeEvent<int> evt)
    {
        if (selectedItem == null) return;

        CraftingManager.Instance.SetReserve(selectedItem, evt.newValue);
        
        if (itemSlots.TryGetValue(selectedItem, out var slot))
            UpdateSlotBadges(slot, selectedItem);

        if (showDebugLogs)
            Debug.Log($"[InventoryController] Reserve for {selectedItem.displayName} set to {evt.newValue}");
    }

    private void OnForSaleToggled(ChangeEvent<bool> evt)
    {
        if (selectedItem == null) return;

        SalesManager.Instance.SetMarkedForSale(selectedItem, evt.newValue);
        
        if (currentCategory == CategoryFilter.ForSale && !evt.newValue)
        {
            RefreshGrid();
            if (detailPanel != null)
                detailPanel.style.display = DisplayStyle.None;
            selectedItem = null;
            return;
        }

        if (itemSlots.TryGetValue(selectedItem, out var slot))
        {
            UpdateSlotBadges(slot, selectedItem);
            
            var forsaleSticker = slot.Q<VisualElement>("forsale-sticker");
            if (forsaleSticker != null)
                forsaleSticker.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (showDebugLogs)
            Debug.Log($"[InventoryController] {selectedItem.displayName} {(evt.newValue ? "marked for" : "unmarked from")} sale");
    }

    private void UpdateHeaderLabels()
    {
        if (goldLabel != null)
            goldLabel.text = $"💰 {Inventory.Instance.Gold}";

        if (capacityLabel != null)
        {
            int totalItems = Inventory.Instance.SnapshotAll().Sum(r => r.qty);
            capacityLabel.text = $"Items: {totalItems}";
        }
    }

    private void OnGoldChanged(int newTotal)
    {
        if (State == PanelState.Open && goldLabel != null)
            goldLabel.text = $"💰 {newTotal}";
    }

    private void OnInventoryChanged(ResourceStack stack)
    {
        if (State == PanelState.Open)
        {
            RefreshGrid();
            
            if (selectedItem == stack.itemDef)
                UpdateDetailPanel(selectedItem);
        }
    }
}