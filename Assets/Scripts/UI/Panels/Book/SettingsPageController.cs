using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Settings page — category list on the left, that category's controls on the right.
/// Implements IBookPage; lives inside BookPanelController as tab 4 (the book's `settingsPage` field).
/// Audio = SfxManager volumes (Master / Effects / Music, each a labelled slider + percent readout; the Effects
/// slider ticks as it moves so the level is audible); Tips = the pointer switch (TutorialManager.ShowTips) and a reset.
/// Styled by the .settings-* rules in BookPanel.uss.
/// </summary>
public class SettingsPageController : MonoBehaviour, IBookPage
{
    private VisualElement categoryList;
    private VisualElement rightPage;
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
        this.rightPage = rightPage;

        BuildCategoryList();
        ShowCategory(selectedCategory);
    }

    public void OnPageHidden()
    {
        content?.RemoveFromHierarchy();
        content = null;
        categoryList = null;
        rightPage = null;
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
            var btn = new Button { text = category };
            btn.AddToClassList("settings-category-button");
            string captured = category;
            btn.clicked += () => { SfxManager.Instance?.Play(SfxId.UiTick); ShowCategory(captured); };
            categoryList.Add(btn);
        }
    }

    private void ShowCategory(string category)
    {
        selectedCategory = category;
        if (rightPage == null) return;

        content?.RemoveFromHierarchy();
        content = new VisualElement();
        content.AddToClassList("settings-content");
        rightPage.Add(content);
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
        var sfx = SfxManager.Instance;
        AddSlider("Master",  sfx != null ? sfx.MasterVolume : 0.8f, v => { if (sfx != null) sfx.MasterVolume = v; });
        AddSlider("Effects", sfx != null ? sfx.SfxVolume    : 1f,   v => { if (sfx != null) { sfx.SfxVolume = v; sfx.Play(SfxId.UiTick); } });
        AddSlider("Music",   sfx != null ? sfx.MusicVolume  : 1f,   v => { if (sfx != null) sfx.MusicVolume = v; });
    }

    // label · slider · "80%" on one row; the readout follows the knob
    private void AddSlider(string label, float value, System.Action<float> set)
    {
        var row = new VisualElement();
        row.AddToClassList("settings-row");
        var name = new Label(label);
        name.AddToClassList("settings-row-label");
        var s = new Slider(0f, 1f) { value = value };
        s.AddToClassList("settings-slider");
        var pct = new Label(Percent(value));
        pct.AddToClassList("settings-row-value");
        s.RegisterValueChangedCallback(e => { set(e.newValue); pct.text = Percent(e.newValue); });
        row.Add(name); row.Add(s); row.Add(pct);
        content.Add(row);
    }

    private static string Percent(float v) => $"{Mathf.RoundToInt(v * 100f)}%";

    // Tutorial pointers; Reset brings them back
    private void BuildTips()
    {
        var tm = TutorialManager.Instance;
        var toggle = new Toggle("Show tips") { value = tm == null || tm.ShowTips };
        toggle.AddToClassList("settings-toggle");
        toggle.RegisterValueChangedCallback(e => { if (tm != null) tm.ShowTips = e.newValue; SfxManager.Instance?.Play(SfxId.UiTick); });
        content.Add(toggle);
        var reset = new Button(() => { tm?.ResetAll(); SfxManager.Instance?.Play(SfxId.UiTick); }) { text = "Reset tips" };
        reset.AddToClassList("settings-category-button");
        reset.AddToClassList("settings-reset-button");
        content.Add(reset);
    }
}
