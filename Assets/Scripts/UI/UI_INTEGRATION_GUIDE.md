# UI Manager System - Integration Guide

## Overview
This system provides centralized UI management with ESC navigation, input blocking, and animation state handling.

## Architecture

```
UIManager (singleton)
├── Manages panel lifecycle (open/close/focus)
├── Modal stack for ESC navigation
├── Request queue for edge case handling
└── World input blocking state

IPanelController (interface)
├── Panel state machine
├── Animation callbacks
└── Focus management

BasePanelController (abstract class)
└── Common implementation, inherit for quick setup

HirePanelController (concrete)
└── Your hire panel, fully integrated
```

## Setup Steps

### 1. Create Input Action Asset
1. Create new Input Action Asset: `Assets/Input/UIInputActions.inputactions`
2. Add action map: "UI"
3. Add action: "Cancel"
   - Binding: Keyboard/Escape
   - Binding: Gamepad/B Button
   - Action Type: Button

### 2. Setup UIManager GameObject
1. Create empty GameObject in scene: "[UIManager]"
2. Add `UIManager` component
3. Assign `UI/Cancel` action to `cancelAction` field
4. Configure `inputCooldown` (default 0.2s is good)

### 3. Migrate Your HireController
1. **BACKUP YOUR EXISTING HireController.cs**
2. Replace with `HirePanelController.cs`
3. Update GameObject component name
4. All serialized fields should auto-migrate
5. Test that hiring still works

### 4. Add WorldInputBlocker to Clickables
Add to any world object that should ignore clicks when UI is open:
- Loot objects
- Units (adventurers, porters, mobs)
- Buildings
- Interactive objects

Example:
```csharp
// Your existing loot script
public class Loot : MonoBehaviour
{
    void OnMouseDown()
    {
        // This will never fire if UIManager.IsBlockingWorldInput == true
        // because WorldInputBlocker disables the collider
    }
}
```

## Creating New Panels

### Method A: Use BasePanelController (Simple Panels)
```csharp
public class MyPanel : BasePanelController
{
    [SerializeField] private UIDocument uiDoc;
    [SerializeField] private VisualTreeAsset panelAsset;
    
    private VisualElement panel;
    
    public override string PanelID => "MyPanel";
    public override VisualElement RootElement => panel;
    
    void Awake()
    {
        panel = panelAsset.CloneTree();
        uiDoc.rootVisualElement.Add(panel);
        panel.style.display = DisplayStyle.None;
    }
    
    // Optional lifecycle hooks
    protected override void OnOpenStart()
    {
        // Called before open animation
    }
    
    protected override void OnCloseStart()
    {
        // Called before close animation
    }
}
```

### Method B: Implement IPanelController Directly (Complex Panels)
See `HirePanelController.cs` for full example with custom animations.

## Usage Examples

### Opening/Closing Panels
```csharp
// From any script
UIManager.Instance.OpenPanel("HirePanel");
UIManager.Instance.ClosePanel("HirePanel");

// Or by reference
UIManager.Instance.OpenPanel(myPanelController);

// Check if open
bool isOpen = UIManager.Instance.IsPanelOpen("HirePanel");
```

### Button Callbacks
```csharp
shopButton.clicked += () => UIManager.Instance.OpenPanel("HirePanel");
closeButton.clicked += () => UIManager.Instance.ClosePanel("HirePanel");
```

### ESC Key Behavior
**Automatic:**
- UIManager listens to UI/Cancel action
- Closes top modal panel on stack
- Respects `CanClose()` veto
- Applies cooldown to prevent spam

**No code needed** - just press ESC!

### World Input Blocking
```csharp
// Check from game code
if (UIManager.Instance.IsBlockingWorldInput)
{
    // Don't process world clicks
    return;
}

// Or use WorldInputBlocker component (recommended)
```

## Edge Case Handling

### 1. ESC During Animation
**Problem:** Player hits ESC while panel is opening
**Solution:** Close request queued, executes when state becomes Open

### 2. Open Panel While Closing
**Problem:** Code tries to open panel that's still closing
**Solution:** Open request queued, executes when state becomes Closed

### 3. Hire Animation Blocks Close
**Problem:** Player hits ESC during hire animation
**Solution:** `HirePanelController.CanClose()` returns false during animation

### 4. Multiple ESC Presses
**Problem:** Player mashes ESC
**Solution:** Input cooldown (default 0.2s) prevents rapid closes

### 5. Panel Stack Corruption
**Problem:** Panel removed from scene while on stack
**Solution:** `UnregisterPanel()` cleans up stack automatically

## Panel Settings

### Modal vs Persistent
**Modal (default):**
- Goes on navigation stack
- ESC closes it
- Can block world input
- Examples: Hire menu, pause menu, settings

**Persistent:**
```csharp
protected override void Start()
{
    isModal = false; // Don't participate in ESC navigation
    base.Start();
}
```
- Doesn't go on stack
- ESC ignores it
- Always visible
- Examples: HUD, minimap, health bars

### Block World Input
```csharp
blocksWorldInput = true;  // Modal panels
blocksWorldInput = false; // Non-blocking panels
```

## Animation Customization

### Override Open/Close Animations
```csharp
protected override IEnumerator OpenAnimation()
{
    RootElement.style.display = DisplayStyle.Flex;
    
    // Custom animation here
    float elapsed = 0f;
    while (elapsed < openCloseDuration)
    {
        elapsed += Time.deltaTime;
        // Scale animation
        float scale = Mathf.Lerp(0.5f, 1f, elapsed / openCloseDuration);
        RootElement.style.scale = new Scale(Vector3.one * scale);
        yield return null;
    }
    
    State = PanelState.Open;
    OnOpenComplete?.Invoke(this);
}
```

## Common Patterns

### Settings Submenu
```csharp
// From pause menu
settingsButton.clicked += () => 
    UIManager.Instance.OpenPanel("SettingsPanel");

// Settings panel opens ON TOP of pause menu
// ESC from settings returns to pause menu
// ESC from pause menu closes both
```

### Confirmation Dialog
```csharp
public class ConfirmDialog : BasePanelController
{
    public override bool CanClose()
    {
        // Force user to click Yes or No
        return false;
    }
    
    void OnYesClicked()
    {
        // Do action
        UIManager.Instance.ClosePanel(this);
    }
}
```

### Tutorial Panel (Un-closeable)
```csharp
public class TutorialPanel : BasePanelController
{
    private bool completed = false;
    
    public override bool CanClose()
    {
        return completed;
    }
    
    void OnCompleteTutorial()
    {
        completed = true;
        UIManager.Instance.ClosePanel(this);
    }
}
```

## Performance Notes

### Memory
- Panels stay in memory when closed (display:none)
- Only instantiated once at startup
- No per-frame allocation in UIManager

### Stack Operations
- Push/Pop: O(1)
- Panel lookup: O(1) via Dictionary

### Input Blocking
- WorldInputBlocker: O(1) per frame per component
- Cheap collider enable/disable
- No raycasting overhead

## Migration Checklist

- [ ] Create Input Action Asset with UI/Cancel
- [ ] Add UIManager to scene
- [ ] Backup existing HireController.cs
- [ ] Replace with HirePanelController.cs
- [ ] Test hire panel opens/closes
- [ ] Test ESC closes hire panel
- [ ] Add WorldInputBlocker to clickable objects
- [ ] Test world input blocks when panel open
- [ ] Create additional panels as needed

## Troubleshooting

**Panel doesn't close with ESC:**
- Check `IsModal = true`
- Verify Input Action is assigned
- Check `CanClose()` returns true

**World input still works when panel open:**
- Add `WorldInputBlocker` to object
- Verify `BlocksWorldInput = true` on panel

**Panel won't open:**
- Check `RegisterPanel()` called in Start()
- Verify panel ID is unique
- Check State is Closed

**Animation doesn't play:**
- Verify `openCloseDuration > 0`
- Check coroutines are starting
- Override animation methods if needed

## Next Steps

1. Test with your existing hire panel
2. Create pause menu using example
3. Add WorldInputBlocker to world objects
4. Create additional panels as needed
5. Consider navigation history for back-button (future enhancement)

## Future Enhancements (Optional)

- Navigation history for back-button
- Panel priority system
- Panel groups (close all in group)
- Save/restore panel state
- Panel transition effects library
