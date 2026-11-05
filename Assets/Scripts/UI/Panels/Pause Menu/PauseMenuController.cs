using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Main menu: the guild book's closed cover over the paused, live guild (Pause_Panel.uxml). Opens on cold boot and
/// on Esc mid-game (UIManager.OnCancelPressed, same "Pause_Panel" id as the old pause board). Open = pause, HUD fades,
/// camera eases in, cover slides in from the left; Continue reverses it and resumes time at the end. The first
/// Continue of a session releases the welcome-back panel (<see cref="AfterContinue"/>).
/// </summary>
public class PauseMenuController : BasePanelController
{
    public override string PanelID => "Pause_Panel";

    [Header("Menu")]
    [Tooltip("Camera zoom while the menu is up (1.06 = 6% closer)")]
    [SerializeField] private float menuZoom = 1.06f;

    private const float OpenTime = 0.7f, CloseTime = 1.25f;
    private const int SettingsTab = 4;

    private VisualElement book, shade, mainList, confirmList, continueButton;
    private Label continueSub;
    private VisualElement[] hud;
    private GameManager gameManager;
    private Camera cam;
    private float baseOrtho;
    private AdventurerManager[] advManagers;
    private bool instant;
    // Adventurers and porters are hidden behind the menu (the guild hall stays, workers keep working)
    private readonly HashSet<Renderer> hiddenRenderers = new HashSet<Renderer>();
    private readonly List<Renderer> rendererScratch = new List<Renderer>();

    // Session state: the boot menu shows once per play session (a New Guild reload skips it)
    private static bool continued;
    private static Action afterContinue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { continued = false; afterContinue = null; }

    /// <summary>Runs now if the player is already in the game, else on the first Continue (welcome-back waits for it).</summary>
    public static void AfterContinue(Action action)
    {
        if (continued) action();
        else afterContinue += action;
    }

    // ═════════════════════════════════════════════
    // LIFECYCLE
    // ═════════════════════════════════════════════

    void Awake()
    {
        hideFlags = HideFlags.HideInInspector;
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();
    }

    protected override void Start()
    {
        hasOverlay = false;   // the cover brings its own shade
        BuildUI();
        base.Start();
        gameManager = GameManager.Instance;
        cam = Camera.main;
        advManagers = FindObjectsByType<AdventurerManager>(FindObjectsInactive.Include);

        if (!continued && panel != null)
        {
            instant = true;   // boot: the game appears already behind the cover
            UIManager.Instance.OpenPanel(this);
        }
    }

    protected override void OnDestroy()
    {
        // Safety: restore time if destroyed while paused
        gameManager?.UnpauseGame();
        base.OnDestroy();
    }

    void Update()
    {
        if (State == PanelState.Closed || State == PanelState.Closing) return;
        RefreshContinueLine();
        // Every frame, not once per unit: the boot menu opens before the save restore spawns the roster, and a
        // unit's sprite parts are built after it spawns
        HideUnits(FindObjectsByType<AdventurerAgent>());
        HideUnits(FindObjectsByType<PorterAgent>());
    }

    // forceRenderingOff, not enabled: unit code toggles its own renderers / markers every frame (PromoteIndicator),
    // and nothing else touches this flag. Inactive children included so a marker that turns on later stays hidden.
    private void HideUnits<T>(T[] units) where T : Component
    {
        foreach (var u in units) Hide(u);
    }

    private void Hide(Component unit)
    {
        unit.GetComponentsInChildren(true, rendererScratch);
        foreach (var r in rendererScratch)
            if (!r.forceRenderingOff) { r.forceRenderingOff = true; hiddenRenderers.Add(r); }
    }

    private void ShowUnits()
    {
        foreach (var r in hiddenRenderers)
            if (r != null) r.forceRenderingOff = false;
        hiddenRenderers.Clear();
    }

    // ═════════════════════════════════════════════
    // OPEN / CLOSE
    // ═════════════════════════════════════════════

    public override bool Open()
    {
        if (State != PanelState.Closed || panel == null)
            return false;

        State = PanelState.Opening;
        gameManager.PauseGame();
        ShowConfirm(false);
        continueButton.style.display = HasGuild ? DisplayStyle.Flex : DisplayStyle.None;
        RefreshContinueLine();
        StartCoroutine(OpenAnimation());
        return true;
    }

    public override bool Close()
    {
        if (State != PanelState.Open)
            return false;

        State = PanelState.Closing;
        ShowUnits();   // the guild fills back up as the cover leaves
        StartCoroutine(CloseAnimation());   // time resumes when the camera has settled
        return true;
    }

    // Settings opens the book over the cover; the cover hides until the book closes
    public override void OnLoseFocus() => panel.style.display = DisplayStyle.None;
    public override void OnFocus() => panel.style.display = DisplayStyle.Flex;

    protected override float GetDeltaTime() => Time.unscaledDeltaTime;

    protected override IEnumerator OpenAnimation()
    {
        if (panel.parent != null) panel.parent.style.display = DisplayStyle.Flex;
        panel.style.display = DisplayStyle.Flex;
        panel.style.opacity = 1f;
        if (cam != null && baseOrtho <= 0f) baseOrtho = cam.orthographicSize;

        if (!instant)
            for (float e = 0f; e < OpenTime; e += GetDeltaTime())
            {
                Pose(Seg(e, 0.15f, 0.55f), Seg(e, 0f, OpenTime), Seg(e, 0f, 0.3f));
                yield return null;
            }
        instant = false;
        Pose(1f, 1f, 1f);

        State = PanelState.Open;
        InvokeOnOpenComplete();
    }

    protected override IEnumerator CloseAnimation()
    {
        // book slides off → camera eases into the game view → HUD fades in → time resumes
        for (float e = 0f; e < CloseTime; e += GetDeltaTime())
        {
            Pose(1f - Seg(e, 0f, 0.55f), 1f - Seg(e, 0.25f, 0.9f), 1f - Seg(e, 0.9f, 0.35f));
            yield return null;
        }
        Pose(0f, 0f, 0f);
        panel.style.display = DisplayStyle.None;
        if (panel.parent != null) panel.parent.style.display = DisplayStyle.None;

        State = PanelState.Closed;
        gameManager.UnpauseGame();
        InvokeOnCloseComplete();

        if (!continued)
        {
            continued = true;
            var pending = afterContinue;
            afterContinue = null;
            pending?.Invoke();
        }
    }

    private static float Seg(float e, float start, float duration) =>
        Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((e - start) / duration));

    /// <summary>1 = menu fully up: cover in place, camera zoomed, HUD hidden.</summary>
    private void Pose(float cover, float zoom, float hudHidden)
    {
        book.style.translate = new Translate(Length.Percent(-140f * (1f - cover)), 0f);
        book.style.rotate = new Rotate(Angle.Degrees(-6f * (1f - cover)));
        shade.style.opacity = cover;
        if (cam != null && baseOrtho > 0f)
            cam.orthographicSize = baseOrtho / Mathf.Lerp(1f, menuZoom, zoom);
        foreach (var el in hud)
            if (el != null) el.style.opacity = 1f - hudHidden;
    }

    // ═════════════════════════════════════════════
    // UI SETUP
    // ═════════════════════════════════════════════

    protected override void BuildUI()
    {
        if (uiDocument == null) return;
        var root = uiDocument.rootVisualElement;
        panel = root.Q<VisualElement>("pause-panel");
        if (panel == null)
        {
            Debug.LogError("[PauseMenuController] 'pause-panel' not found in UIDocument. Ensure it is present in MainUI.uxml.");
            return;
        }

        book = panel.Q("menu-book");
        shade = panel.Q("menu-shade");
        shade.generateVisualContent += DrawShade;
        mainList = panel.Q("menu-main");
        confirmList = panel.Q("menu-confirm");
        continueButton = panel.Q("continue-button");
        continueSub = panel.Q<Label>("continue-sub");
        hud = new[] { root.Q("TopNavigation"), root.Q("MilestoneTracker") };

        panel.Q<Button>("continue-button").clicked += Continue;
        panel.Q<Button>("newguild-button").clicked += OnNewGuildClicked;
        panel.Q<Button>("settings-button").clicked += () => FindAnyObjectByType<BookPanelController>()?.OpenToTab(SettingsTab);
        panel.Q<Button>("quit-button").clicked += () => GameManager.Instance.QuitGame();   // quit saves (SaveManager.OnApplicationQuit)
        panel.Q<Button>("confirm-yes").clicked += StartNewGuild;
        panel.Q<Button>("confirm-no").clicked += () => ShowConfirm(false);

        // Initial visibility — hide TC so it doesn't intercept events when closed
        panel.style.display = DisplayStyle.None;
        if (panel.parent != null) panel.parent.style.display = DisplayStyle.None;
    }

    // Warm dark wash on the left (cover in shadow, guild lit on the right): vertex-coloured quads, alpha .75 → .4 → 0
    private static readonly (float x, float a)[] ShadeStops = { (0f, 0.75f), (0.4f, 0.4f), (0.62f, 0f) };

    private static void DrawShade(MeshGenerationContext ctx)
    {
        var r = ctx.visualElement.contentRect;
        int quads = ShadeStops.Length - 1;
        var mesh = ctx.Allocate(quads * 4, quads * 6);
        for (int i = 0; i < quads; i++)
        {
            float x0 = r.width * ShadeStops[i].x, x1 = r.width * ShadeStops[i + 1].x;
            Color32 c0 = new Color(0.1f, 0.055f, 0.03f, ShadeStops[i].a), c1 = new Color(0.1f, 0.055f, 0.03f, ShadeStops[i + 1].a);
            mesh.SetNextVertex(new Vertex { position = new Vector3(x0, 0f, Vertex.nearZ), tint = c0 });
            mesh.SetNextVertex(new Vertex { position = new Vector3(x1, 0f, Vertex.nearZ), tint = c1 });
            mesh.SetNextVertex(new Vertex { position = new Vector3(x1, r.height, Vertex.nearZ), tint = c1 });
            mesh.SetNextVertex(new Vertex { position = new Vector3(x0, r.height, Vertex.nearZ), tint = c0 });
            ushort b = (ushort)(i * 4);
            mesh.SetNextIndex(b); mesh.SetNextIndex((ushort)(b + 1)); mesh.SetNextIndex((ushort)(b + 2));
            mesh.SetNextIndex(b); mesh.SetNextIndex((ushort)(b + 2)); mesh.SetNextIndex((ushort)(b + 3));
        }
    }

    // ═════════════════════════════════════════════
    // ACTIONS
    // ═════════════════════════════════════════════

    // A guild exists once this session has played or a save is on disk (first launch: only New Guild)
    private static bool HasGuild => continued || File.Exists(SaveManager.SavePath);

    private void Continue() => UIManager.Instance.ClosePanel(this);

    private void OnNewGuildClicked()
    {
        if (HasGuild) ShowConfirm(true);
        else Continue();   // first launch: the fresh guild is already behind the cover
    }

    private void ShowConfirm(bool on)
    {
        mainList.style.display = on ? DisplayStyle.None : DisplayStyle.Flex;
        confirmList.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void RefreshContinueLine()
    {
        int units = 0;
        foreach (var m in advManagers)
            if (m != null) units += m.GetTotalCount();
        int gold = Inventory.Instance != null ? Inventory.Instance.Gold : 0;
        continueSub.text = $"{AnimatedNumberUITK.Abbrev(gold)} gold · {units} adventurer{(units == 1 ? "" : "s")}";
    }

    /// <summary>
    /// Wipes the save and reboots the scene from scratch: everything in DontDestroyOnLoad (the singletons and the
    /// pools holding the old guild's units) is destroyed so the reload's own copies take over (SaveManager's boot
    /// flag stays set → no load → a fresh guild).
    /// </summary>
    private static void StartNewGuild()
    {
        SaveManager.WipeSaveFiles();
        DecorSlot.ResetStatics();
        continued = true;      // the new guild starts straight in, no boot menu
        afterContinue = null;  // the old guild's welcome-back award goes with it
        Time.timeScale = 1f;

        var host = new GameObject("[New Guild]");
        DontDestroyOnLoad(host);
        host.AddComponent<Reboot>();
    }

    /// <summary>Two frames: the persistent objects are destroyed and the old scene switched off (nothing runs without
    /// them), then the scene loads once the Destroys have landed — a reload in the same frame met the dying
    /// Instances in PersistentSingleton.Awake and the new copies destroyed themselves.</summary>
    private class Reboot : MonoBehaviour
    {
        IEnumerator Start()
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var go in gameObject.scene.GetRootGameObjects())   // the DontDestroyOnLoad scene
                if (go != gameObject) Destroy(go);
            foreach (var go in scene.GetRootGameObjects())
                go.SetActive(false);
            yield return null;
            SceneManager.LoadScene(scene.buildIndex);
            Destroy(gameObject);
        }
    }

}
