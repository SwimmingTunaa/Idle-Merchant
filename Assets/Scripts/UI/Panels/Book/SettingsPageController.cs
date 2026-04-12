using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Settings page — category list on the left, placeholder content on the right.
/// Implements IBookPage; lives inside BookPanelController as tab 4.
/// </summary>
public class SettingsPageController : MonoBehaviour, IBookPage
{
    private VisualElement categoryList;
    private Label contentLabel;
    private string selectedCategory = "Audio";

    public string PageTitle => "Settings";

    void Awake() => hideFlags = HideFlags.HideInInspector;

    // ═════════════════════════════════════════════
    // IBOOK PAGE
    // ═════════════════════════════════════════════

    public void OnPageShown(VisualElement leftPage, VisualElement rightPage)
    {
        categoryList = leftPage.Q<VisualElement>("category-list");
        contentLabel = rightPage.Q<Label>("settings-content-label");

        BuildCategoryList();
        ShowCategory(selectedCategory);
    }

    public void OnPageHidden()
    {
        categoryList = null;
        contentLabel = null;
    }

    // ═════════════════════════════════════════════
    // CATEGORIES
    // ═════════════════════════════════════════════

    private static readonly string[] Categories = { "Audio", "Video", "Controls", "Key Bindings" };

    private void BuildCategoryList()
    {
        if (categoryList == null) return;
        categoryList.Clear();

        foreach (var category in Categories)
        {
            var btn = new Button();
            btn.text = category;
            btn.AddToClassList("settings-category-button");
            string captured = category;
            btn.clicked += () => ShowCategory(captured);
            categoryList.Add(btn);
        }
    }

    private void ShowCategory(string category)
    {
        selectedCategory = category;

        if (contentLabel != null)
            contentLabel.text = $"{category} settings coming soon.";

        if (categoryList == null) return;
        foreach (var child in categoryList.Children())
        {
            if (child is Button btn)
                btn.EnableInClassList("settings-category-button--active", btn.text == category);
        }
    }
}
