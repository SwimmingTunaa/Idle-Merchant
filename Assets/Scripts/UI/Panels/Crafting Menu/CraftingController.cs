using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Crafting panel controller - displays recipes, material reserves, and crafting progress.
/// Inherits from BasePanelController for lifecycle and animation management.
/// </summary>
public class CraftingController : BasePanelController
{
    [Header("UXML References")]
    [SerializeField] private VisualTreeAsset recipeCardAsset;

    //public override VisualElement RootElement => panel;

    // UI Elements
    private ScrollView recipeScroll;
    private DropdownField filterDropdown;
    private DropdownField sortDropdown;
    private Button closeButton;
    private Label activeCraftsLabel;
    private Label statusLabel;

    // State
    private List<RecipeCardData> recipeCards = new List<RecipeCardData>();

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

    // ═════════════════════════════════════════════
    // LIFECYCLE
    // ═════════════════════════════════════════════

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
            UpdateCraftingProgress();
            UpdateFooter();
        }
    }

    // ═════════════════════════════════════════════
    // LIFECYCLE HOOKS (Override from base)
    // ═════════════════════════════════════════════

    protected override void OnOpenStart()
    {
        PopulateRecipes();
        
        if (showDebugLogs)
            Debug.Log("[CraftingController] Panel opened, data populated");
    }

    protected override void OnCloseStart()
    {
        if (showDebugLogs)
            Debug.Log("[CraftingController] Panel closing");
    }

    // ═════════════════════════════════════════════
    // UI SETUP
    // ═════════════════════════════════════════════

    protected override void BuildUI()
    {
        base.BuildUI();

        // Query elements
        recipeScroll = panel.Q<ScrollView>("recipe-scroll");
        filterDropdown = panel.Q<DropdownField>("filter-dropdown");
        sortDropdown = panel.Q<DropdownField>("sort-dropdown");
        closeButton = panel.Q<Button>("close-button");
        activeCraftsLabel = panel.Q<Label>("active-crafts-label");
        statusLabel = panel.Q<Label>("status-label");

        // Setup dropdowns
        filterDropdown.choices = new List<string> { "All", "Can Craft", "Missing Materials", "Enabled" };
        filterDropdown.value = "All";
        filterDropdown.RegisterValueChangedCallback(OnFilterChanged);

        sortDropdown.choices = new List<string> { "Unlock Order", "Craft Time (Fast→Slow)", "Output Value (High→Low)" };
        sortDropdown.value = "Unlock Order";
        sortDropdown.RegisterValueChangedCallback(OnSortChanged);

        // Button callbacks
        closeButton.clicked += () => Close();

        // Subscribe to events
        GameSignals.OnProductCrafted += OnProductCrafted;

        // Initial visibility
        panel.style.display = DisplayStyle.None;
        panel.style.opacity = 0f;
    }

    protected override void OnDestroy()
    {
        GameSignals.OnProductCrafted -= OnProductCrafted;
        base.OnDestroy();
    }

    // ═════════════════════════════════════════════
    // RECIPE LIST
    // ═════════════════════════════════════════════

    private void PopulateRecipes()
    {
        recipeScroll.Clear();
        recipeCards.Clear();

        if (CraftingManager.Instance == null)
        {
            Debug.LogError("[CraftingController] CraftingManager.Instance is null");
            return;
        }

        List<RecipeDef> recipes = CraftingManager.Instance.GetAllRecipes();

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

        cardData.cardElement.EnableInClassList("cannot-craft", !canCraft && !isCrafting);
        cardData.cardElement.EnableInClassList("crafting", isCrafting);

        cardData.progressContainer.style.display = isCrafting ? DisplayStyle.Flex : DisplayStyle.None;
        cardData.craftTimeLabel.style.display = isCrafting ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private void UpdateCraftingProgress()
    {
        foreach (var cardData in recipeCards)
        {
            if (CraftingManager.Instance.IsCrafting(cardData.recipe))
            {
                float progress = CraftingManager.Instance.GetCraftingProgress(cardData.recipe);
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

    // ═════════════════════════════════════════════
    // FILTERING & SORTING
    // ═════════════════════════════════════════════

    private void ApplyFilterAndSort()
    {
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

        switch (currentSort)
        {
            case SortMode.CraftTime:
                visibleCards = visibleCards.OrderBy(c => c.recipe.CraftSeconds).ToList();
                break;
            case SortMode.OutputValue:
                visibleCards = visibleCards.OrderByDescending(c => c.recipe.Output.sellPrice).ToList();
                break;
        }

        foreach (var cardData in recipeCards)
        {
            cardData.cardElement.style.display = visibleCards.Contains(cardData) ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    // ═════════════════════════════════════════════
    // CALLBACKS
    // ═════════════════════════════════════════════

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

    private void OnProductCrafted(ResourceStack stack)
    {
        RefreshRecipeList();

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

    private void UpdateFooter()
    {
        int activeCrafts = CraftingManager.Instance.GetActiveCraftCount();
        activeCraftsLabel.text = $"Active Crafts: {activeCrafts}";

        int enabledCount = CraftingManager.Instance.GetEnabledRecipes().Count;
        statusLabel.text = enabledCount > 0 ? $"{enabledCount} recipes enabled" : "No recipes enabled";
    }
}