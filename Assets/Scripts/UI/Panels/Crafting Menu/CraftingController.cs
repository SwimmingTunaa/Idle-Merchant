using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Crafting page — recipe list on the left, active crafts status on the right.
/// Implements IBookPage; lives inside BookPanelController as tab 3.
/// </summary>
public class CraftingController : MonoBehaviour, IBookPage
{
    [Header("UXML References")]
    [SerializeField] private VisualTreeAsset recipeCardAsset;

    // UI Elements
    private ScrollView recipeScroll;
    private DropdownField filterDropdown;
    private DropdownField sortDropdown;
    private Label activeCraftsLabel;
    private Label statusLabel;

    // State
    private List<RecipeCardData> recipeCards = new List<RecipeCardData>();
    private bool isShown = false;

    private enum FilterMode { All, CanCraft, MissingMaterials, Enabled }
    private enum SortMode { UnlockOrder, CraftTime, OutputValue }

    private FilterMode currentFilter = FilterMode.All;
    private SortMode currentSort = SortMode.UnlockOrder;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

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

    public string PageTitle => "Crafting";

    void Awake() => hideFlags = HideFlags.HideInInspector;

    // ═════════════════════════════════════════════
    // UNITY LIFECYCLE
    // ═════════════════════════════════════════════

    void Update()
    {
        if (!isShown) return;
        UpdateCraftingProgress();
        UpdateFooter();
    }

    // ═════════════════════════════════════════════
    // IBOOK PAGE
    // ═════════════════════════════════════════════

    public void OnPageShown(VisualElement leftPage, VisualElement rightPage)
    {
        recipeScroll = leftPage.Q<ScrollView>("recipe-scroll");
        filterDropdown = leftPage.Q<DropdownField>("filter-dropdown");
        sortDropdown = leftPage.Q<DropdownField>("sort-dropdown");
        activeCraftsLabel = rightPage.Q<Label>("active-crafts-label");
        statusLabel = rightPage.Q<Label>("status-label");

        filterDropdown.choices = new List<string> { "All", "Can Craft", "Missing Materials", "Enabled" };
        filterDropdown.value = "All";
        filterDropdown.RegisterValueChangedCallback(OnFilterChanged);

        sortDropdown.choices = new List<string> { "Unlock Order", "Craft Time (Fast→Slow)", "Output Value (High→Low)" };
        sortDropdown.value = "Unlock Order";
        sortDropdown.RegisterValueChangedCallback(OnSortChanged);

        GameSignals.OnProductCrafted += OnProductCrafted;

        isShown = true;
        PopulateRecipes();
    }

    public void OnPageHidden()
    {
        GameSignals.OnProductCrafted -= OnProductCrafted;

        isShown = false;
        recipeScroll = null;
        filterDropdown = null;
        sortDropdown = null;
        activeCraftsLabel = null;
        statusLabel = null;
        recipeCards.Clear();
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

        foreach (var recipe in CraftingManager.Instance.GetAllRecipes())
            CreateRecipeCard(recipe);

        ApplyFilterAndSort();
    }

    private void CreateRecipeCard(RecipeDef recipe)
    {
        VisualElement card = recipeCardAsset.CloneTree().Q<VisualElement>("recipe-card");

        var outputIcon = card.Q<VisualElement>("output-icon");
        var recipeName = card.Q<Label>("recipe-name");
        var outputQuantity = card.Q<Label>("output-quantity");

        if (recipe.Output.icon != null)
            outputIcon.style.backgroundImage = new StyleBackground(recipe.Output.icon);
        recipeName.text = recipe.Output.displayName;
        outputQuantity.text = $"x{recipe.OutputQty}";

        var ingredientsContainer = card.Q<VisualElement>("ingredients-container");
        foreach (var ingredient in recipe.Ingredients)
            CreateIngredientSlot(ingredientsContainer, ingredient);

        var craftTimeLabel = card.Q<Label>("craft-time");
        craftTimeLabel.text = $"{recipe.CraftSeconds:F1}s";

        var progressContainer = card.Q<VisualElement>("progress-container");
        var progressBar = card.Q<VisualElement>("progress-bar");

        var enableToggle = card.Q<Toggle>("enable-toggle");
        enableToggle.value = CraftingManager.Instance.IsRecipeEnabled(recipe);
        enableToggle.RegisterValueChangedCallback(evt => OnRecipeToggled(recipe, evt.newValue));

        var cardData = new RecipeCardData
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
        var slot = new VisualElement();
        slot.AddToClassList("ingredient-slot");

        var icon = new VisualElement();
        icon.AddToClassList("ingredient-icon");
        if (ingredient.Item.icon != null)
            icon.style.backgroundImage = new StyleBackground(ingredient.Item.icon);

        var quantity = new Label($"x{ingredient.Qty}");
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
            UpdateRecipeCardState(cardData);
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
            cardData.cardElement.style.display = visibleCards.Contains(cardData) ? DisplayStyle.Flex : DisplayStyle.None;
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
    }

    private void OnProductCrafted(ResourceStack stack)
    {
        RefreshRecipeList();
        var cardData = recipeCards.FirstOrDefault(c => c.recipe.Output == stack.itemDef);
        if (cardData != null)
        {
            cardData.cardElement.AddToClassList("flash-complete");
            cardData.cardElement.schedule.Execute(() =>
                cardData.cardElement.RemoveFromClassList("flash-complete")).StartingIn(300);
        }
    }

    private void UpdateFooter()
    {
        if (activeCraftsLabel != null)
            activeCraftsLabel.text = $"Active Crafts: {CraftingManager.Instance.GetActiveCraftCount()}";
        if (statusLabel != null)
        {
            int enabledCount = CraftingManager.Instance.GetEnabledRecipes().Count;
            statusLabel.text = enabledCount > 0 ? $"{enabledCount} recipes enabled" : "No recipes enabled";
        }
    }
}
