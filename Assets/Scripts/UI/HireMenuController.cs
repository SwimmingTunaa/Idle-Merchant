using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Hooks up your hire menu UI to your existing managers.
/// Populates the ScrollView with layer sections and unit buttons.
/// Entire card is clickable (no separate button element).
/// Shows animated, abbreviated coin count via AnimatedNumberUITK.
/// </summary>
public class HireMenuController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VisualTreeAsset hireButtonTemplate;
    
    [Header("Data Sources")]
    [SerializeField] private List<AdventurerManager> adventurerManagers = new List<AdventurerManager>();
    [SerializeField] private List<PorterManager> porterManagers = new List<PorterManager>();
    
    [Header("Biome Names (Layer 1-10)")]
    [SerializeField] private string[] biomeNames = new string[]
    {
        "Slime Caverns",
        "Goblin Mines", 
        "Darkwood Thicket",
        "Flooded Crypt",
        "Crystal Gardens",
        "Ancient Library",
        "Forge Depths",
        "Frozen Wastes",
        "Shadow Realm",
        "Dragon's Hoard"
    };
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    
    private VisualElement root;
    private ScrollView scrollView;
    private Button openMenuButton;
    private VisualElement hirePanel;
    private Label goldLabel;

    // Animated coin counter (UI Toolkit)
    private AnimatedNumberUITK animatedGold;
    
    // Track all buttons for updates
    private readonly List<ButtonData> allButtons = new List<ButtonData>();
    
    void OnEnable()
    {
        root = uiDocument ? uiDocument.rootVisualElement : null;
        if (root == null)
        {
            Debug.LogError("[HireMenuController] UIDocument or rootVisualElement missing!");
            return;
        }

        // Find UI elements
        scrollView     = root.Q<ScrollView>("ScrollView");
        openMenuButton = root.Q<Button>("ShopButton");
        hirePanel      = root.Q<VisualElement>("hire-panel");
        goldLabel      = root.Q<Label>("CoinLabel");
        
        if (scrollView == null)
        {
            Debug.LogError("[HireMenuController] ScrollView not found in Main.uxml!");
            return;
        }
        
        if (hireButtonTemplate == null)
        {
            Debug.LogError("[HireMenuController] HireButton template not assigned!");
            return;
        }
        
        // Hook up menu button
        if (openMenuButton != null)
            openMenuButton.clicked += ToggleHirePanel;
        
        // Start with panel hidden
        if (hirePanel != null)
            hirePanel.style.display = DisplayStyle.None;
    }
    
    void Start()
    {
        if (Inventory.Instance == null)
        {
            Debug.LogError("[HireMenuController] Inventory.Instance is null!");
            return;
        }
        
        BuildHireMenu();

        // --- Animated coin setup ---
        // Attach/find the animator on the same GameObject.
        animatedGold = GetComponent<AnimatedNumberUITK>();
        if (animatedGold == null)
            animatedGold = gameObject.AddComponent<AnimatedNumberUITK>();

        // Point it at the same UIDocument and label; abbreviations on by default.
        // animatedGold.abbreviate = true;
        if (animatedGold && animatedGold.enabled)
        {
            // If the component didn't auto-resolve, give it hints:
            if (animatedGold.GetType().GetField("uiDocument", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public) != null)
            {
                // Best-effort: if the script exposes fields, try to set them.
                // (Safe even if fields are private due to reflection check.)
            }
            // Initialize to current value immediately
            animatedGold.SetInstant(Inventory.Instance.Gold);
        }

        // Subscribe to gold changes
        GameEvents.GoldChanged += OnGoldChanged;
    }
    
    void OnDestroy()
    {
        GameEvents.GoldChanged -= OnGoldChanged;
        if (openMenuButton != null)
            openMenuButton.clicked -= ToggleHirePanel;
    }
    
    private void OnGoldChanged(int newGold)
    {
        // Animate coin label; fallback to plain text if animator missing.
        if (animatedGold != null)
            animatedGold.SetTargetValue(newGold);
        else if (goldLabel != null)
            goldLabel.text = newGold.ToString("N0");
        
        // Only update buttons if panel is visible
        if (hirePanel != null && hirePanel.style.display == DisplayStyle.Flex)
            UpdateAllButtons();
    }
    
    // ===== PANEL MANAGEMENT =====
    
    private void ToggleHirePanel()
    {
        if (hirePanel == null) return;
        
        bool isVisible = hirePanel.style.display == DisplayStyle.Flex;
        
        if (isVisible)
        {
            hirePanel.style.display = DisplayStyle.None;
        }
        else
        {
            hirePanel.style.display = DisplayStyle.Flex;

            // Ensure the displayed value is current when opening
            if (animatedGold != null)
                animatedGold.SetInstant(Inventory.Instance.Gold);
            else if (goldLabel != null)
                goldLabel.text = Inventory.Instance.Gold.ToString("N0");

            UpdateAllButtons();
        }
    }
    
    // ===== MENU BUILDING =====
    
    private void BuildHireMenu()
    {
        scrollView.Clear();
        allButtons.Clear();
        
        var allManagers = new List<(int layer, IUnitManager manager, bool isPorter)>();
        
        foreach (var manager in adventurerManagers)
            allManagers.Add((manager.layerIndex, manager, false));
        
        foreach (var manager in porterManagers)
            allManagers.Add((manager.layerIndex, manager, true));
        
        var layerGroups = allManagers
            .GroupBy(m => m.layer)
            .OrderBy(g => g.Key);
        
        foreach (var layerGroup in layerGroups)
        {
            int layer = layerGroup.Key;
            var layerSection = CreateLayerSection(layer);
            
            foreach (var (_, manager, isPorter) in layerGroup)
                AddButtonsForManager(layerSection, manager, isPorter);
            
            scrollView.Add(layerSection);
        }
        
        if (showDebugLogs)
            Debug.Log($"[HireMenuController] Built {allButtons.Count} buttons across {layerGroups.Count()} layers");
    }
    
    private VisualElement CreateLayerSection(int layer)
    {
        var section = new VisualElement();
        section.style.marginBottom = 20;
        
        string biomeName = layer <= biomeNames.Length ? biomeNames[layer - 1] : "Unknown";
        var header = new Label($"Layer {layer} - {biomeName}");
        header.AddToClassList("levelHeader");
        
        section.Add(header);
        return section;
    }
    
    private void AddButtonsForManager(VisualElement section, IUnitManager manager, bool isPorter)
    {
        var unitLimits = GetUnitLimits(manager);
        foreach (var limit in unitLimits)
        {
            if (limit.unitDef == null) continue;
            var button = CreateHireButton(limit.unitDef, manager, isPorter);
            section.Add(button);
        }
    }
    
    private List<UnitTypeLimit> GetUnitLimits(IUnitManager manager)
    {
        if (manager is AdventurerManager advManager) return advManager.unitLimits;
        if (manager is PorterManager portManager)   return portManager.unitLimits;
        return new List<UnitTypeLimit>();
    }
    
    private VisualElement CreateHireButton(EntityDef unitDef, IUnitManager manager, bool isPorter)
    {
        var buttonElement = hireButtonTemplate.CloneTree();
        
        // Get elements from your UXML
        var hireContainer = buttonElement.Q<VisualElement>("hire-container");
        var iconElement   = buttonElement.Q<VisualElement>("unit-icon");
        var nameLabel     = buttonElement.Q<Label>("unit-name");
        var descLabel     = buttonElement.Q<Label>("unit-description");
        var costLabel     = buttonElement.Q<Label>("cost");
        
        // Make entire card clickable
        if (hireContainer != null)
        {
            hireContainer.RegisterCallback<ClickEvent>(evt => OnHireClicked(unitDef, manager, buttonElement));
            
            // Hover effects
            hireContainer.RegisterCallback<MouseEnterEvent>(evt => {
                if (hireContainer.enabledSelf)
                    hireContainer.style.scale = new Scale(new Vector3(1.02f, 1.02f, 1f));
            });
            
            hireContainer.RegisterCallback<MouseLeaveEvent>(evt => {
                hireContainer.style.scale = new Scale(Vector3.one);
            });
        }
        
        // Populate data
        if (nameLabel != null)
        {
            int currentCount = manager.GetUnitCount(unitDef);
            int maxCount = manager.GetUnitLimit(unitDef);
            nameLabel.text = $"{unitDef.displayName} {currentCount}/{maxCount}";
        }
        
        if (descLabel != null)
            descLabel.text = unitDef.description;
        
        if (costLabel != null)
            costLabel.text = unitDef.hireCost.ToString();
        
        // Set icon
        if (iconElement != null && unitDef.icon != null)
        {
            iconElement.style.backgroundImage = new StyleBackground(unitDef.icon);
            if (showDebugLogs) Debug.Log($"[HireMenuController] Set icon for {unitDef.displayName}");
        }
        else if (showDebugLogs)
        {
            Debug.LogWarning($"[HireMenuController] No icon for {unitDef.displayName}. Element: {iconElement != null}, Sprite: {unitDef.icon != null}");
        }
        
        // Store for updates
        var buttonData = new ButtonData
        {
            unitDef = unitDef,
            manager = manager,
            buttonElement = buttonElement,
            hireContainer = hireContainer,
            costLabel = costLabel,
            descLabel = descLabel,
            nameLabel = nameLabel
        };
        allButtons.Add(buttonData);
        
        UpdateButton(buttonData);
        return buttonElement;
    }
    
    // ===== INTERACTION =====
    
    private void OnHireClicked(EntityDef unitDef, IUnitManager manager, VisualElement buttonElement)
    {
        bool success = manager.HireUnit(unitDef);
        
        if (success)
        {
            // Update only this button
            var buttonData = allButtons.Find(b => b.unitDef == unitDef && b.manager == manager);
            if (buttonData != null)
                UpdateButton(buttonData);

            // GoldChanged event will animate the label;
            // this is a safe local refresh if needed.
            if (animatedGold != null)
                animatedGold.SetTargetValue(Inventory.Instance.Gold);

            if (showDebugLogs)
                Debug.Log($"[HireMenuController] Hired {unitDef.displayName}");
        }
        else
        {
            if (showDebugLogs)
                Debug.LogWarning($"[HireMenuController] Failed to hire {unitDef.displayName}");
        }
    }
    
    // ===== UPDATES =====
    
    private void UpdateAllButtons()
    {
        foreach (var buttonData in allButtons)
            UpdateButton(buttonData);
    }
    
    private void UpdateButton(ButtonData data)
    {
        if (data.unitDef == null || data.manager == null) return;
        if (Inventory.Instance == null) return;
        
        int currentCount = data.manager.GetUnitCount(data.unitDef);
        int maxCount     = data.manager.GetUnitLimit(data.unitDef);
        int currentGold  = Inventory.Instance.Gold;
        int cost         = data.unitDef.hireCost;
        
        bool canAfford = currentGold >= cost;
        bool hasSpace  = currentCount < maxCount;
        bool canHire   = canAfford && hasSpace;
        
        // Update labels
        if (data.nameLabel != null)
            data.nameLabel.text = $"{data.unitDef.displayName} {currentCount}/{maxCount}";
        
        if (data.costLabel != null)
            data.costLabel.text = cost.ToString();
        
        // Visual feedback on entire card
        if (data.hireContainer != null)
        {
            if (!canAfford)
            {
                data.hireContainer.style.backgroundColor = new Color(1f, 0.7f, 0.7f);
                data.hireContainer.SetEnabled(false);
            }
            else if (!hasSpace)
            {
                data.hireContainer.style.backgroundColor = new Color(0.7f, 0.7f, 0.7f);
                data.hireContainer.SetEnabled(false);
            }
            else
            {
                data.hireContainer.style.backgroundColor = new Color(0.91f, 0.81f, 0.69f);
                data.hireContainer.SetEnabled(true);
            }
        }
    }
    
    // ===== DATA =====
    
    private class ButtonData
    {
        public EntityDef unitDef;
        public IUnitManager manager;
        public VisualElement buttonElement;
        public VisualElement hireContainer;
        public Label costLabel;
        public Label descLabel;
        public Label nameLabel;
    }
    
    // ===== DEBUG =====
    
    [ContextMenu("Rebuild Menu")]
    private void RebuildMenu()
    {
        BuildHireMenu();
    }
}
