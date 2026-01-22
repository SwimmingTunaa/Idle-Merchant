using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class CraftingController : MonoBehaviour, IPanelController
{
    [Header("UXML References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VisualTreeAsset craftingPanelAsset;
    [SerializeField] private VisualTreeAsset recipeCardAsset;

    [Header("Panel Settings")]
    [SerializeField] private bool blocksWorldInput = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // IPanelController implementation
    public string PanelID => "CraftingPanel";
    public VisualElement RootElement => craftingPanel;
    public PanelState State { get; private set; } = PanelState.Closed;
    public bool BlocksWorldInput => blocksWorldInput;
    public bool IsModal => true;

    public event Action<IPanelController> OnOpenComplete;
    public event Action<IPanelController> OnCloseComplete;

    // UI Elements
    private VisualElement root;
    private VisualElement craftingPanel;
    private ScrollView recipeScroll;
    private ScrollView reserveScroll;
    private DropdownField filterDropdown;
    private DropdownField sortDropdown;
    private Button closeButton;
    private Label activeCraftsLabel;
    private Label statusLabel;

    // State
    private List<RecipeCardData> recipeCards = new List<RecipeCardData>();
    private Dictionary<ItemDef, VisualElement> reserveItems = new Dictionary<ItemDef, VisualElement>();

    private enum FilterMode { All, CanCraft, MissingMaterials, Enabled }
    private enum SortMode { UnlockOrder, CraftTime, OutputValue }

    private FilterMode currentFilter = FilterMode.All;
    private SortMode currentSort = SortMode.UnlockOrder;

    private class RecipeCardData
    {
        public RecipeDef recipe;
        public VisualElement cardElement;
        public VisualElement ingredientsContainer;
        public VisualElement progressContainer;
        public VisualElement progressBar;
        public Label craftTimeLabel;
        public Toggle enableToggle;
    }

    void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();
    }

    void Start()
    {
        BuildUI();
        SubscribeToEvents();
    }

    void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    void Update()
    {
        if (State == PanelState.Open)
        {
            UpdateCraftingProgress();
            UpdateFooter();
        }
    }

    // ===== UI SETUP =====

    private void BuildUI()
    {
        root = uiDocument.rootVisualElement;

        craftingPanel = craftingPanelAsset.CloneTree().Q<VisualElement>("crafting-panel");
        root.Add(craftingPanel);

        // Query elements
        recipeScroll = craftingPanel.Q<ScrollView>("recipe-scroll");
        reserveScroll = craftingPanel.Q<ScrollView>("reserve-scroll");
        filterDropdown = craftingPanel.Q<DropdownField>("filter-dropdown");
        sortDropdown = craftingPanel.Q<DropdownField>("sort-dropdown");
        closeButton = craftingPanel.Q<Button>("close-button");
        activeCraftsLabel = craftingPanel.Q<Label>("active-crafts-label");
        statusLabel = craftingPanel.Q<Label>("status-label");

        // Setup dropdowns
        filterDropdown.choices = new List<string> { "All", "Can Craft", "Missing Materials", "Enabled" };
        filterDropdown.value = "All";
        filterDropdown.RegisterValueChangedCallback(OnFilterChanged);

        sortDropdown.choices = new List<string> { "Unlock Order", "Craft Time (Fast→Slow)", "Output Value (High→Low)" };
        sortDropdown.value = "Unlock Order";
        sortDropdown.RegisterValueChangedCallback(OnSortChanged);

        // Button callbacks
        closeButton.clicked += OnCloseClicked;

        // Initial visibility
        craftingPanel.style.display = DisplayStyle.None;
    }

    private void SubscribeToEvents()
    {
        GameSignals.OnProductCrafted += OnProductCrafted;
    }

    private void UnsubscribeFromEvents()
    {
        GameSignals.OnProductCrafted -= OnProductCrafted;
    }

    // ===== IPANELCONTROLLER IMPLEMENTATION =====

    public bool Open()
    {
        if (State != PanelState.Closed)
            return false;

        State = PanelState.Opening;
        craftingPanel.style.display = DisplayStyle.Flex;

        PopulateRecipes();
        PopulateReserves();

        State = PanelState.Open;
        OnOpenComplete?.Invoke(this);

        if (showDebugLogs)
            Debug.Log("[CraftingController] Panel opened");

        return true;
    }

    public bool Close()
    {
        if (State != PanelState.Open)
            return false;

        State = PanelState.Closing;
        craftingPanel.style.display = DisplayStyle.None;

        State = PanelState.Closed;
        OnCloseComplete?.Invoke(this);

        if (showDebugLogs)
            Debug.Log("[CraftingController] Panel closed");

        return true;
    }

    public void OnFocus()
    {
        // Refresh data when panel gains focus
        if (State == PanelState.Open)
        {
            RefreshRecipeList();
            RefreshReserves();
        }
    }

    public void OnLoseFocus()
    {
        // Nothing needed
    }

    public bool CanClose()
    {
        // Always allow closing crafting panel
        return true;
    }

    // ===== RECIPE LIST =====

    private void PopulateRecipes()
    {
        recipeScroll.Clear();
        recipeCards.Clear();

        if (CraftingManager.Instance == null)
        {
            Debug.LogError("[CraftingController] CraftingManager.Instance is null");
            return;
        }

        // Get all recipes (assuming you expose this in CraftingManager)
        // For now, we'll need to add a public getter in CraftingManager
        // This is a placeholder - you'll need to expose craftingRecipes
        List<RecipeDef> recipes = GetAllRecipes();

        foreach (var recipe in recipes)
        {
            CreateRecipeCard(recipe);
        }

        ApplyFilterAndSort();
    }

    private void CreateRecipeCard(RecipeDef recipe)
    {
        VisualElement card = recipeCardAsset.CloneTree().Q<VisualElement>("recipe-card");

        // Setup output
        VisualElement outputIcon = card.Q<VisualElement>("output-icon");
        Label recipeName = card.Q<Label>("recipe-name");
        Label outputQuantity = card.Q<Label>("output-quantity");

        if (recipe.Output.icon != null)
            outputIcon.style.backgroundImage = new StyleBackground(recipe.Output.icon);

        recipeName.text = recipe.Output.displayName;
        outputQuantity.text = $"x{recipe.OutputQty}";

        // Setup ingredients
        VisualElement ingredientsContainer = card.Q<VisualElement>("ingredients-container");
        foreach (var ingredient in recipe.Ingredients)
        {
            CreateIngredientSlot(ingredientsContainer, ingredient);
        }

        // Setup controls
        Label craftTimeLabel = card.Q<Label>("craft-time");
        craftTimeLabel.text = $"{recipe.CraftSeconds:F1}s";

        VisualElement progressContainer = card.Q<VisualElement>("progress-container");
        VisualElement progressBar = card.Q<VisualElement>("progress-bar");

        Toggle enableToggle = card.Q<Toggle>("enable-toggle");
        enableToggle.value = CraftingManager.Instance.IsRecipeEnabled(recipe);
        enableToggle.RegisterValueChangedCallback(evt => OnRecipeToggled(recipe, evt.newValue));

        // Store card data
        RecipeCardData cardData = new RecipeCardData
        {
            recipe = recipe,
            cardElement = card,
            ingredientsContainer = ingredientsContainer,
            progressContainer = progressContainer,
            progressBar = progressBar,
            craftTimeLabel = craftTimeLabel,
            enableToggle = enableToggle
        };

        recipeCards.Add(cardData);
        recipeScroll.Add(card);

        UpdateRecipeCardState(cardData);
    }

    private void CreateIngredientSlot(VisualElement container, RecipeDef.Ingredient ingredient)
    {
        VisualElement slot = new VisualElement();
        slot.AddToClassList("ingredient-slot");

        VisualElement icon = new VisualElement();
        icon.AddToClassList("ingredient-icon");
        if (ingredient.Item.icon != null)
            icon.style.backgroundImage = new StyleBackground(ingredient.Item.icon);

        Label quantity = new Label($"x{ingredient.Qty}");
        quantity.AddToClassList("ingredient-quantity");

        slot.Add(icon);
        slot.Add(quantity);
        container.Add(slot);
    }

    private void UpdateRecipeCardState(RecipeCardData cardData)
    {
        bool canCraft = CraftingManager.Instance.CanCraft(cardData.recipe);
        bool isCrafting = CraftingManager.Instance.IsCrafting(cardData.recipe);

        // Update card styling
        cardData.cardElement.EnableInClassList("cannot-craft", !canCraft && !isCrafting);
        cardData.cardElement.EnableInClassList("crafting", isCrafting);

        // Show/hide progress bar
        cardData.progressContainer.style.display = isCrafting ? DisplayStyle.Flex : DisplayStyle.None;
        cardData.craftTimeLabel.style.display = isCrafting ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private void UpdateCraftingProgress()
    {
        foreach (var cardData in recipeCards)
        {
            if (CraftingManager.Instance.IsCrafting(cardData.recipe))
            {
                // Get progress (you'll need to expose this in CraftingManager)
                float progress = GetCraftingProgress(cardData.recipe);
                cardData.progressBar.style.width = Length.Percent(progress * 100f);
            }
        }
    }

    private void RefreshRecipeList()
    {
        foreach (var cardData in recipeCards)
        {
            UpdateRecipeCardState(cardData);
        }
    }

    // ===== MATERIAL RESERVES =====

    private void PopulateReserves()
    {
        reserveScroll.Clear();
        reserveItems.Clear();

        // Get all unique materials from recipes
        HashSet<ItemDef> materials = new HashSet<ItemDef>();
        foreach (var cardData in recipeCards)
        {
            foreach (var ingredient in cardData.recipe.Ingredients)
            {
                materials.Add(ingredient.Item);
            }
        }

        foreach (var material in materials)
        {
            CreateReserveItem(material);
        }
    }

    private void CreateReserveItem(ItemDef material)
    {
        VisualElement item = new VisualElement();
        item.AddToClassList("reserve-item");

        // Icon
        VisualElement icon = new VisualElement();
        icon.AddToClassList("reserve-item-icon");
        if (material.icon != null)
            icon.style.backgroundImage = new StyleBackground(material.icon);

        // Name
        Label nameLabel = new Label(material.displayName);
        nameLabel.AddToClassList("reserve-item-name");

        // Stock count
        Label stockLabel = new Label();
        stockLabel.AddToClassList("reserve-item-stock");
        UpdateStockLabel(stockLabel, material);

        // Slider
        SliderInt slider = new SliderInt(0, 100);
        slider.AddToClassList("reserve-slider");
        slider.value = CraftingManager.Instance.GetReserve(material);
        slider.RegisterValueChangedCallback(evt => OnReserveChanged(material, evt.newValue));

        item.Add(icon);
        item.Add(nameLabel);
        item.Add(stockLabel);
        item.Add(slider);

        reserveItems[material] = item;
        reserveScroll.Add(item);
    }

    private void UpdateStockLabel(Label label, ItemDef material)
    {
        int stock = Inventory.Instance.Get(material.itemCategory, material);
        int reserve = CraftingManager.Instance.GetReserve(material);
        label.text = $"Stock: {stock} (Reserve: {reserve})";
    }

    private void RefreshReserves()
    {
        foreach (var kvp in reserveItems)
        {
            Label stockLabel = kvp.Value.Q<Label>(className: "reserve-item-stock");
            if (stockLabel != null)
                UpdateStockLabel(stockLabel, kvp.Key);
        }
    }

    // ===== FILTERING & SORTING =====

    private void ApplyFilterAndSort()
    {
        // Filter
        List<RecipeCardData> visibleCards = recipeCards;

        switch (currentFilter)
        {
            case FilterMode.CanCraft:
                visibleCards = recipeCards.Where(c => CraftingManager.Instance.CanCraft(c.recipe)).ToList();
                break;
            case FilterMode.MissingMaterials:
                visibleCards = recipeCards.Where(c => !CraftingManager.Instance.CanCraft(c.recipe)).ToList();
                break;
            case FilterMode.Enabled:
                visibleCards = recipeCards.Where(c => CraftingManager.Instance.IsRecipeEnabled(c.recipe)).ToList();
                break;
        }

        // Sort
        switch (currentSort)
        {
            case SortMode.CraftTime:
                visibleCards = visibleCards.OrderBy(c => c.recipe.CraftSeconds).ToList();
                break;
            case SortMode.OutputValue:
                visibleCards = visibleCards.OrderByDescending(c => c.recipe.Output.sellPrice).ToList();
                break;
        }

        // Update visibility
        foreach (var cardData in recipeCards)
        {
            cardData.cardElement.style.display = visibleCards.Contains(cardData) ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    // ===== CALLBACKS =====

    private void OnFilterChanged(ChangeEvent<string> evt)
    {
        currentFilter = evt.newValue switch
        {
            "Can Craft" => FilterMode.CanCraft,
            "Missing Materials" => FilterMode.MissingMaterials,
            "Enabled" => FilterMode.Enabled,
            _ => FilterMode.All
        };

        ApplyFilterAndSort();
    }

    private void OnSortChanged(ChangeEvent<string> evt)
    {
        currentSort = evt.newValue switch
        {
            "Craft Time (Fast→Slow)" => SortMode.CraftTime,
            "Output Value (High→Low)" => SortMode.OutputValue,
            _ => SortMode.UnlockOrder
        };

        ApplyFilterAndSort();
    }

    private void OnRecipeToggled(RecipeDef recipe, bool enabled)
    {
        if (enabled)
            CraftingManager.Instance.EnableRecipe(recipe);
        else
            CraftingManager.Instance.DisableRecipe(recipe);

        if (showDebugLogs)
            Debug.Log($"[CraftingController] Recipe {recipe.Output.displayName} {(enabled ? "enabled" : "disabled")}");
    }

    private void OnReserveChanged(ItemDef material, int value)
    {
        CraftingManager.Instance.SetReserve(material, value);

        if (showDebugLogs)
            Debug.Log($"[CraftingController] Reserve for {material.displayName} set to {value}");
    }

    private void OnProductCrafted(ResourceStack stack)
    {
        RefreshRecipeList();
        RefreshReserves();

        // Flash completed recipe
        var cardData = recipeCards.FirstOrDefault(c => c.recipe.Output == stack.itemDef);
        if (cardData != null)
        {
            cardData.cardElement.AddToClassList("flash-complete");
            cardData.cardElement.schedule.Execute(() =>
            {
                cardData.cardElement.RemoveFromClassList("flash-complete");
            }).StartingIn(300);
        }
    }

    private void OnCloseClicked()
    {
        Close();
    }

    private void UpdateFooter()
    {
        int activeCrafts = CraftingManager.Instance.GetActiveCraftCount();
        activeCraftsLabel.text = $"Active Crafts: {activeCrafts}";

        int enabledCount = CraftingManager.Instance.GetEnabledRecipes().Count;
        statusLabel.text = enabledCount > 0 ? $"{enabledCount} recipes enabled" : "No recipes enabled";
    }

    // ===== HELPER METHODS =====

    private List<RecipeDef> GetAllRecipes()
    {
        if (CraftingManager.Instance == null)
            return new List<RecipeDef>();
        
        return CraftingManager.Instance.GetAllRecipes();
    }

    private float GetCraftingProgress(RecipeDef recipe)
    {
        if (CraftingManager.Instance == null)
            return 0f;
        
        return CraftingManager.Instance.GetCraftingProgress(recipe);
    }
}