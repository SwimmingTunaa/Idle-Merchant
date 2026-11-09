using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Settings page — category list on the left, that category's controls on the right.
/// Implements IBookPage; lives inside BookPanelController as tab 4 (the book's `settingsPage` field).
/// Audio = SfxManager volumes; Tips = the tutorial's switch (TutorialManager.ShowTips) and a reset.
/// </summary>
public class SettingsPageController : MonoBehaviour, IBookPage
{
    private VisualElement categoryList;
    private Label contentLabel;
    private VisualElement content;
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
        content?.RemoveFromHierarchy();
        content = null;
        categoryList = null;
        contentLabel = null;
    }

    // ═════════════════════════════════════════════
    // CATEGORIES
    // ═════════════════════════════════════════════

    private static readonly string[] Categories = { "Audio", "Tips" };

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
        if (contentLabel == null) return;

        content?.RemoveFromHierarchy();
        content = new VisualElement { style = { marginTop = 12 } };
        contentLabel.parent.Add(content);
        if (category == "Audio") BuildAudio(); else BuildTips();

        if (categoryList == null) return;
        foreach (var child in categoryList.Children())
        {
            if (child is Button btn)
                btn.EnableInClassList("settings-category-button--active", btn.text == category);
        }
    }

    private void BuildAudio()
    {
        contentLabel.text = "Volume";
        var sfx = SfxManager.Instance;
        AddSlider("Master", sfx != null ? sfx.MasterVolume : 0.8f, v => { if (sfx != null) sfx.MasterVolume = v; });
        AddSlider("Effects", sfx != null ? sfx.SfxVolume : 1f, v => { if (sfx != null) sfx.SfxVolume = v; });
        AddSlider("Music", sfx != null ? sfx.MusicVolume : 1f, v => { if (sfx != null) sfx.MusicVolume = v; });
    }

    private void AddSlider(string label, float value, System.Action<float> set)
    {
        var s = new Slider(label, 0f, 1f) { value = value, style = { fontSize = 20, color = Ink, marginBottom = 8 } };
        s.RegisterValueChangedCallback(e => set(e.newValue));
        content.Add(s);
    }

    // Tutorial: pointers, hint lines and first-contact notes; Reset replays them from the start
    private void BuildTips()
    {
        var tm = TutorialManager.Instance;
        contentLabel.text = "Pointers and first-time notes on the progress tracker.";
        var toggle = new Toggle("Show tips") { value = tm == null || tm.ShowTips, style = { fontSize = 20, color = Ink } };
        toggle.RegisterValueChangedCallback(e => { if (tm != null) tm.ShowTips = e.newValue; });
        content.Add(toggle);
        var reset = new Button(() => { tm?.ResetAll(); SfxManager.Instance?.Play(SfxId.UiTick); })
            { text = "Reset tips", style = { alignSelf = Align.FlexStart, marginTop = 10, fontSize = 18 } };
        content.Add(reset);
    }

    private static readonly Color Ink = new Color(0.27f, 0.18f, 0.08f);
}
