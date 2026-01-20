using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Pause menu controller with bulletin board aesthetic.
/// Manages time scaling, button interactions, and scene transitions.
/// Accessed via ESC key through UIManager.
/// </summary>
public class PauseMenuController : MonoBehaviour, IPanelController
{
    [Header("UXML")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VisualTreeAsset pausePanelAsset;

    [Header("Scene Management")]
    [Tooltip("Name of main menu scene (hookup later when scene exists)")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Animation")]
    [SerializeField] private float openCloseDuration = 0.3f;
    


    // IPanelController
    public string PanelID => "PauseMenu";
    public VisualElement RootElement => pausePanel;
    public PanelState State { get; private set; } = PanelState.Closed;
    public bool BlocksWorldInput => true;
    public bool IsModal => true;

    public event System.Action<IPanelController> OnOpenComplete;
    public event System.Action<IPanelController> OnCloseComplete;

    private VisualElement pausePanel;
    private Button resumeButton;
    private Button settingsButton;
    private Button saveButton;
    private Button loadButton;
    private Button mainMenuButton;
    private Button quitButton;

    private float previousTimeScale = 1f;
    private bool didPauseTime = false;

    // ─────────────────────────────────────────────
    // LIFECYCLE
    // ─────────────────────────────────────────────

    private void Awake()
    {
        BuildUI();
    }

    private void Start()
    {
        UIManager.Instance.RegisterPanel(this);
        pausePanel.style.display = DisplayStyle.None;
        State = PanelState.Closed;
    }

    private void OnDestroy()
    {
        UIManager.Instance.UnregisterPanel(this);
        
        // Safety: restore time if destroyed while paused
        if (didPauseTime)
        {
            Time.timeScale = previousTimeScale;
            didPauseTime = false;
        }
    }


    // ─────────────────────────────────────────────
    // UI SETUP
    // ─────────────────────────────────────────────

    private void BuildUI()
    {
        var root = uiDocument.rootVisualElement;
        
        // Add directly to root for full-screen overlay
        pausePanel = pausePanelAsset.CloneTree().Q<VisualElement>("pause-panel");
        root.Add(pausePanel);

        // Query buttons
        resumeButton = pausePanel.Q<Button>("resume-button");
        settingsButton = pausePanel.Q<Button>("settings-button");
        saveButton = pausePanel.Q<Button>("save-button");
        loadButton = pausePanel.Q<Button>("load-button");
        mainMenuButton = pausePanel.Q<Button>("mainmenu-button");
        quitButton = pausePanel.Q<Button>("quit-button");

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
    }

    // ─────────────────────────────────────────────
    // IPANELCONTROLLER IMPLEMENTATION
    // ─────────────────────────────────────────────

    public bool Open()
    {
        if (State != PanelState.Closed)
            return false;

        State = PanelState.Opening;
        PauseGame();
        StartCoroutine(OpenAnimation());
        return true;
    }

    public bool Close()
    {
        if (State != PanelState.Open)
            return false;

        State = PanelState.Closing;
        UnpauseGame();
        StartCoroutine(CloseAnimation());
        return true;
    }

    public void OnFocus()
    {
        // Could highlight resume button here
    }

    public void OnLoseFocus()
    {
        // Another panel opened on top (like settings submenu)
    }

    public bool CanClose()
    {
        // Always closeable
        return true;
    }

    // ─────────────────────────────────────────────
    // TIME MANAGEMENT
    // ─────────────────────────────────────────────

    private void PauseGame()
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        didPauseTime = true;
    }

    private void UnpauseGame()
    {
        if (didPauseTime)
        {
            Time.timeScale = previousTimeScale;
            didPauseTime = false;
        }
    }

    // ─────────────────────────────────────────────
    // ANIMATIONS (using unscaledDeltaTime for pause compatibility)
    // ─────────────────────────────────────────────

    private IEnumerator OpenAnimation()
    {
        pausePanel.style.display = DisplayStyle.Flex;
        
        float elapsed = 0f;
        while (elapsed < openCloseDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / openCloseDuration);
            
            // Fade + scale up
            pausePanel.style.opacity = t;
            float scale = Mathf.Lerp(0.9f, 1f, t);
            pausePanel.style.scale = new Scale(new Vector3(scale, scale, 1f));
            
            yield return null;
        }

        pausePanel.style.opacity = 1f;
        pausePanel.style.scale = new Scale(Vector3.one);
        
        State = PanelState.Open;
        OnOpenComplete?.Invoke(this);
    }

    private IEnumerator CloseAnimation()
    {
        float elapsed = 0f;
        
        while (elapsed < openCloseDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / openCloseDuration);
            
            // Fade + scale down
            pausePanel.style.opacity = 1f - t;
            float scale = Mathf.Lerp(1f, 0.9f, t);
            pausePanel.style.scale = new Scale(new Vector3(scale, scale, 1f));
            
            yield return null;
        }

        pausePanel.style.opacity = 0f;
        pausePanel.style.display = DisplayStyle.None;
        
        State = PanelState.Closed;
        OnCloseComplete?.Invoke(this);
    }

    // ─────────────────────────────────────────────
    // BUTTON CALLBACKS
    // ─────────────────────────────────────────────

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
        if (didPauseTime)
        {
            Time.timeScale = previousTimeScale;
            didPauseTime = false;
        }

        // Check if scene exists before loading
        if (SceneManager.GetSceneByName(mainMenuSceneName).IsValid() || 
            System.Array.Exists(GetSceneNames(), scene => scene == mainMenuSceneName))
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
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

    // ─────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────

    private string[] GetSceneNames()
    {
        int sceneCount = SceneManager.sceneCountInBuildSettings;
        string[] scenes = new string[sceneCount];
        for (int i = 0; i < sceneCount; i++)
        {
            scenes[i] = System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
        }
        return scenes;
    }

    // ─────────────────────────────────────────────
    // PUBLIC API (for external triggers)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Enable/disable save button when save system is ready
    /// </summary>
    public void SetSaveEnabled(bool enabled)
    {
        if (saveButton != null)
        {
            saveButton.SetEnabled(enabled);
            saveButton.text = enabled ? "Save Game" : "Save Game (Coming Soon)";
        }
    }

    /// <summary>
    /// Enable/disable load button when save system is ready
    /// </summary>
    public void SetLoadEnabled(bool enabled)
    {
        if (loadButton != null)
        {
            loadButton.SetEnabled(enabled);
            loadButton.text = enabled ? "Load Game" : "Load Game (Coming Soon)";
        }
    }

    /// <summary>
    /// Enable/disable settings button when settings panel is ready
    /// </summary>
    public void SetSettingsEnabled(bool enabled)
    {
        if (settingsButton != null)
        {
            settingsButton.SetEnabled(enabled);
            settingsButton.text = enabled ? "Settings" : "Settings (Coming Soon)";
        }
    }
}