using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.Rendering;

/// <summary>
/// Pause menu controller with bulletin board aesthetic.
/// Inherits from BasePanelController, overrides for pause/unpause and unscaled time animations.
/// Accessed via ESC key through UIManager.
/// </summary>
public class PauseMenuController : BasePanelController
{
    [Header("Scene Management")]
    [Tooltip("Name of main menu scene (hookup later when scene exists)")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    // IPanelController implementation
    public override string PanelID => "PauseMenu";
   // public override VisualElement RootElement => panel;

    // UI Elements
    private Button resumeButton;
    private Button settingsButton;
    private Button saveButton;
    private Button loadButton;
    private Button mainMenuButton;
    private Button quitButton;

    private GameManager gameManager;

    // ═════════════════════════════════════════════
    // LIFECYCLE
    // ═════════════════════════════════════════════

    void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();
        
        BuildUI();
    }

    protected override void Start()
    {
        base.Start();
        gameManager = GameManager.Instance;
    }

    protected override void OnDestroy()
    {
        // Safety: restore time if destroyed while paused
        gameManager.UnpauseGame();
        
        base.OnDestroy();
    }

    // ═════════════════════════════════════════════
    // OVERRIDE OPEN/CLOSE FOR PAUSE LOGIC
    // ═════════════════════════════════════════════

    public override bool Open()
    {
        if (State != PanelState.Closed)
            return false;

        State = PanelState.Opening;
        gameManager.PauseGame();
        OnOpenStart();
        StartCoroutine(OpenAnimation());
        return true;
    }

    public override bool Close()
    {
        if (State != PanelState.Open)
            return false;

        State = PanelState.Closing;
        gameManager.UnpauseGame();
        OnCloseStart();
        StartCoroutine(CloseAnimation());
        return true;
    }

    // ═════════════════════════════════════════════
    // OVERRIDE FOR UNSCALED TIME
    // ═════════════════════════════════════════════

    protected override float GetDeltaTime() => Time.unscaledDeltaTime;

    // ═════════════════════════════════════════════
    // OVERRIDE ANIMATIONS FOR SCALE EFFECT
    // ═════════════════════════════════════════════

    protected override IEnumerator OpenAnimation()
    {
        if (RootElement == null)
        {
            State = PanelState.Open;
            InvokeOnOpenComplete();
            yield break;
        }

        // Show elements
        panel.style.display = DisplayStyle.Flex;
        if (hasOverlay && overlayElement != null)
            overlayElement.style.display = DisplayStyle.Flex;

        // Fade + scale up
        float elapsed = 0f;
        while (elapsed < openCloseDuration)
        {
            elapsed += GetDeltaTime();
            float t = Mathf.Clamp01(elapsed / openCloseDuration);

            panel.style.opacity = t;
            float scale = Mathf.Lerp(0.9f, 1f, t);
            panel.style.scale = new Scale(new Vector3(scale, scale, 1f));

            if (hasOverlay && overlayElement != null)
                overlayElement.style.opacity = t * overlayColor.a;

            yield return null;
        }

        panel.style.opacity = 1f;
        panel.style.scale = new Scale(Vector3.one);
        if (hasOverlay && overlayElement != null)
            overlayElement.style.opacity = overlayColor.a;

        State = PanelState.Open;
        InvokeOnOpenComplete();

        if (showDebugLogs)
            Debug.Log("[PauseMenu] Open animation complete");
    }

    protected override IEnumerator CloseAnimation()
    {
        if (RootElement == null)
        {
            State = PanelState.Closed;
            InvokeOnCloseComplete();
            yield break;
        }

        // Fade + scale down
        float elapsed = 0f;
        while (elapsed < openCloseDuration)
        {
            elapsed += GetDeltaTime();
            float t = 1f - Mathf.Clamp01(elapsed / openCloseDuration);

            panel.style.opacity = t;
            float scale = Mathf.Lerp(1f, 0.9f, 1f - t);
            panel.style.scale = new Scale(new Vector3(scale, scale, 1f));

            if (hasOverlay && overlayElement != null)
                overlayElement.style.opacity = t * overlayColor.a;

            yield return null;
        }

        // Hide elements
        panel.style.opacity = 0f;
        panel.style.display = DisplayStyle.None;
        panel.style.scale = new Scale(new Vector3(0.9f, 0.9f, 1f));

        if (hasOverlay && overlayElement != null)
        {
            overlayElement.style.opacity = 0f;
            overlayElement.style.display = DisplayStyle.None;
        }

        State = PanelState.Closed;
        InvokeOnCloseComplete();

        if (showDebugLogs)
            Debug.Log("[PauseMenu] Close animation complete");
    }

    // ═════════════════════════════════════════════
    // UI SETUP
    // ═════════════════════════════════════════════

    protected override void BuildUI()
    {
        base.BuildUI();  

        // Query buttons
        resumeButton = panel.Q<Button>("resume-button");
        settingsButton = panel.Q<Button>("settings-button");
        saveButton = panel.Q<Button>("save-button");
        loadButton = panel.Q<Button>("load-button");
        mainMenuButton = panel.Q<Button>("mainmenu-button");
        quitButton = panel.Q<Button>("quit-button");

        // Hook up callbacks
        resumeButton.clicked += OnResumeClicked;
        settingsButton.clicked += OnSettingsClicked;
        saveButton.clicked += OnSaveClicked;
        loadButton.clicked += OnLoadClicked;
        mainMenuButton.clicked += OnMainMenuClicked;
        quitButton.clicked += OnQuitClicked;

        // Disable future buttons
        settingsButton.SetEnabled(false);
        saveButton.SetEnabled(false);
        loadButton.SetEnabled(false);

        // Initial visibility
        panel.style.display = DisplayStyle.None;
        panel.style.opacity = 0f;
    }

    // ═════════════════════════════════════════════
    // BUTTON CALLBACKS
    // ═════════════════════════════════════════════

    private void OnResumeClicked()
    {
        UIManager.Instance.ClosePanel(this);
    }

    private void OnSettingsClicked()
    {
        // Future: Open settings panel on top of pause menu
        // UIManager.Instance.OpenPanel("SettingsPanel");
        Debug.Log("[PauseMenu] Settings coming soon");
    }

    private void OnSaveClicked()
    {
        // TODO: Hook up to save system when implemented
        Debug.Log("[PauseMenu] Save game - not yet implemented");
    }

    private void OnLoadClicked()
    {
        // TODO: Hook up to save system when implemented
        Debug.Log("[PauseMenu] Load game - not yet implemented");
    }

    private void OnMainMenuClicked()
    {
        // Unpause before scene transition
        gameManager.UnpauseGame();

        // Check if scene exists before loading
        if (SceneManager.GetSceneByName(mainMenuSceneName).IsValid())
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
        else
        {
            Debug.LogWarning($"[PauseMenu] Main menu scene '{mainMenuSceneName}' not found. Create scene or update mainMenuSceneName field.");
        }
    }

    private void OnQuitClicked()
    {
        // Unpause before quitting
       gameManager.UnpauseGame();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif

        if (showDebugLogs)
            Debug.Log("[PauseMenu] Quit game");
    }
}