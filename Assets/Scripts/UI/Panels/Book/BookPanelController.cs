using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Hosts the unified Book UI — a 1200x900 open-book overlay with 5 bookmark tabs.
/// Tabs: 0=Roster, 1=Guild, 2=Inventory, 3=Crafting, 4=Settings.
/// All page content is embedded in BookPanel.uxml as merged page-N instances.
/// SwitchToTab shows/hides the active page TemplateContainer and passes
/// its left-content / right-content children to the IBookPage controller.
/// </summary>
public class BookPanelController : BasePanelController
{
    [Header("Book Pages")]
    [SerializeField] private MonoBehaviour rosterPage;
    [SerializeField] private MonoBehaviour guildPage;
    [SerializeField] private MonoBehaviour inventoryPage;
    [SerializeField] private MonoBehaviour craftingPage;
    [SerializeField] private MonoBehaviour settingsPage;

    public override string PanelID => "BookPanel";

    // Cached UI elements
    private readonly Button[] tabButtons = new Button[5];
    private readonly VisualElement[] tabBadges = new VisualElement[5];
    private readonly VisualElement[] pageContainers = new VisualElement[5];

    private VisualElement inventoryFilterBar;

    private MonoBehaviour[] _pages;
    private int lastActiveTab = 0;

    // ═════════════════════════════════════════════
    // LIFECYCLE
    // ═════════════════════════════════════════════

    void Awake()
    {
        hideFlags = HideFlags.HideInInspector;
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        _pages = new MonoBehaviour[] { rosterPage, guildPage, inventoryPage, craftingPage, settingsPage };
    }

    protected override void Start()
    {
        BuildUI();
        base.Start(); // registers "BookPanel" with UIManager
        WireHUDButtons();
        WireBadgeSignals();
    }

    protected override void OnDestroy()
    {
        GameSignals.OnAdventurerPromoted -= OnAdventurerPromotedForBadge;
        base.OnDestroy();
    }

    // ═════════════════════════════════════════════
    // UI SETUP
    // ═════════════════════════════════════════════

    protected override void BuildUI()
    {
        if (uiDocument == null) return;

        panel = uiDocument.rootVisualElement.Q<VisualElement>("book-root");
        if (panel == null)
        {
            Debug.LogError("[BookPanel] 'book-root' not found in UIDocument. Ensure BookPanel is in MainUI.uxml.");
            return;
        }
        // Leave TemplateContainer hidden (display:none in UXML); Open() will show it
        panel.style.display = DisplayStyle.Flex;
        panel.style.opacity = 1f;

        // Cache per-tab page containers (TemplateContainers) and hide all of them
        for (int i = 0; i < 5; i++)
        {
            pageContainers[i] = panel.Q<VisualElement>($"page-{i}");
            if (pageContainers[i] != null) pageContainers[i].style.display = DisplayStyle.None;
        }

        // Wire tab buttons and collect badge refs
        string[] tabNames = { "tab-roster", "tab-guild", "tab-inventory", "tab-crafting", "tab-settings" };
        for (int i = 0; i < 5; i++)
        {
            int idx = i;
            tabButtons[i] = panel.Q<Button>(tabNames[i]);
            if (tabButtons[i] == null) continue;

            tabButtons[i].clicked += () => SwitchToTab(idx);
            tabBadges[i] = tabButtons[i].Q<VisualElement>("tab-badge");
        }

        // Inventory filter bar — lives in book-root, shown only when tab 2 is active
        inventoryFilterBar = panel.Q<VisualElement>("inventory-filter-bar");
        if (inventoryFilterBar != null)
        {
            inventoryFilterBar.style.display = DisplayStyle.None;
            (_pages[2] as InventoryController)?.SetFilterBar(inventoryFilterBar);
        }
    }

    private void WireHUDButtons()
    {
        if (uiDocument == null) return;
        var root = uiDocument.rootVisualElement;

        root.Q<Button>("units-button")?.RegisterCallback<ClickEvent>(_ => OpenToTab(0));
        root.Q<Button>("guild-button")?.RegisterCallback<ClickEvent>(_ => OpenToTab(1));
        root.Q<Button>("inventory-button")?.RegisterCallback<ClickEvent>(_ => OpenToTab(2));
        root.Q<Button>("crafting-button")?.RegisterCallback<ClickEvent>(_ => OpenToTab(3));
        root.Q<Button>("settings-button")?.RegisterCallback<ClickEvent>(_ => OpenToTab(4));
    }

    private void WireBadgeSignals()
    {
        GameSignals.OnAdventurerPromoted += OnAdventurerPromotedForBadge;
    }

    private void OnAdventurerPromotedForBadge(EntityBase entity, string oldRole, string newRole)
    {
        if (State != PanelState.Open || lastActiveTab != 0)
            SetTabBadge(0, true);
    }

    // ═════════════════════════════════════════════
    // PANEL LIFECYCLE OVERRIDES
    // ═════════════════════════════════════════════

    // Open/Close toggle the active page instance — book-root stays visible always
    public override bool Open()
    {
        if (State == PanelState.Opening || State == PanelState.Open) return false;
        State = PanelState.Opening;
        if (panel.parent != null) panel.parent.style.display = DisplayStyle.Flex;
        SwitchToTab(lastActiveTab);
        State = PanelState.Open;
        InvokeOnOpenComplete();
        return true;
    }

    public override bool Close()
    {
        if (State != PanelState.Open) return false;
        State = PanelState.Closing;
        (_pages[lastActiveTab] as IBookPage)?.OnPageHidden();
        if (pageContainers[lastActiveTab] != null) pageContainers[lastActiveTab].style.display = DisplayStyle.None;
        if (inventoryFilterBar != null) inventoryFilterBar.style.display = DisplayStyle.None;
        if (panel.parent != null) panel.parent.style.display = DisplayStyle.None;
        State = PanelState.Closed;
        InvokeOnCloseComplete();
        return true;
    }

    protected override void OnOpenStart() { }

    protected override void OnCloseStart() { }

    // ═════════════════════════════════════════════
    // TAB MANAGEMENT
    // ═════════════════════════════════════════════

    public void OpenToTab(int index)
    {
        if (index < 0 || index >= 5) return;

        if (State == PanelState.Closed)
        {
            lastActiveTab = index;
            UIManager.Instance.OpenPanel(this);
        }
        else if (State == PanelState.Open)
        {
            // Same tab pressed again — toggle close
            if (lastActiveTab == index)
                UIManager.Instance.ClosePanel(this);
            else
                SwitchToTab(index);
        }
    }

    private void SwitchToTab(int index)
    {
        if (index < 0 || index >= 5) return;

        // Hide previous page
        (_pages[lastActiveTab] as IBookPage)?.OnPageHidden();
        if (pageContainers[lastActiveTab] != null) pageContainers[lastActiveTab].style.display = DisplayStyle.None;

        lastActiveTab = index;

        // Show new page container
        if (pageContainers[index] != null) pageContainers[index].style.display = DisplayStyle.Flex;

        // Update tab active class
        for (int i = 0; i < 5; i++)
            tabButtons[i]?.EnableInClassList("book-tab--active", i == index);

        // Show inventory filter bar only when on inventory tab
        if (inventoryFilterBar != null)
            inventoryFilterBar.style.display = index == 2 ? DisplayStyle.Flex : DisplayStyle.None;

        // Clear badge on newly opened tab
        SetTabBadge(index, false);

        // Notify page — pass left-content and right-content from within the merged page
        var leftContent  = pageContainers[index]?.Q<VisualElement>("left-content");
        var rightContent = pageContainers[index]?.Q<VisualElement>("right-content");
        (_pages[index] as IBookPage)?.OnPageShown(leftContent, rightContent);
    }

    public void SetTabBadge(int tabIndex, bool visible)
    {
        if (tabIndex < 0 || tabIndex >= tabBadges.Length) return;
        tabBadges[tabIndex]?.EnableInClassList("tab-badge--visible", visible);
    }
}
