using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A crafting station standing in the guild interior. Hidden entirely (renderers, colliders, worker) until the
/// station's upgrade is owned, then loops frames 1.. at <see cref="fps"/>; frame 0 (cold) is only a fallback. Clicking it (ClickerManager) opens the Workshop panel for its station,
/// which shows the separate big <see cref="heroFrames"/> / <see cref="heroSideProp"/> art;
/// the world side prop (anvil / shield / cauldron) is a plain child SpriteRenderer;
/// hovering strokes it with the hover outline (rendering-layer bit 2) as the click affordance.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class WorkshopProp : MonoBehaviour
{
    [SerializeField] private CraftingStation station;
    [Tooltip("World (mini, character-scale) frames. 0 = cold (station not owned); 1.. = lit loop.")]
    [SerializeField] private Sprite[] frames;
    [Header("Workshop Panel Hero")]
    [Tooltip("Big detailed frames the Workshop panel shows at 3×. 0 = cold; 1.. = lit loop.")]
    [SerializeField] private Sprite[] heroFrames;
    [Tooltip("Big side prop shown beside the panel hero.")]
    [SerializeField] private Sprite heroSideProp;
    [SerializeField] private float fps = 6f;
    [Tooltip("Rendering-layer bit HoverOutlineFeature strokes (2 = hover/selection, yellow).")]
    [SerializeField] private uint hoverOutlineLayer = 2;
    [Header("Worker")]
    [Tooltip("Station worker; its controller has a 'Working' bool (idle <-> work). Auto-found in children if empty. Hidden while the station isn't owned.")]
    [SerializeField] private Animator worker;

    private static readonly int WorkingHash = Animator.StringToHash("Working");
    private const float WorkerPollInterval = 0.25f;
    private float workerTimer;

    public CraftingStation Station => station;
    public IReadOnlyList<Sprite> HeroFrames => heroFrames;
    public Sprite HeroSideProp => heroSideProp;

    private SpriteRenderer sr;
    private bool lit;
    private float timer;
    private int frame;

    private SpriteRenderer[] renderers;
    private Collider2D[] colliders;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (worker == null) worker = GetComponentInChildren<Animator>(true);
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        colliders = GetComponentsInChildren<Collider2D>(true);
    }

    void Start()
    {
        // SaveManager restores owned upgrades before other Starts; the signals cover purchases and the late load
        ProgressionManager.OnUpgradePurchased += OnUpgradePurchased;
        GameSignals.OnCraftingConfigChanged += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        ProgressionManager.OnUpgradePurchased -= OnUpgradePurchased;
        GameSignals.OnCraftingConfigChanged -= Refresh;
    }

    private void OnUpgradePurchased(GuildUpgradeDef _) => Refresh();

    private void Refresh()
    {
        lit = CraftingManager.Instance != null && CraftingManager.Instance.IsStationOwned(station);
        // Not purchased = not in the guild at all: hide station, side prop and worker, and stop it taking clicks.
        // Renderers/colliders are toggled (not GameObjects) so children you've switched off in the scene stay off.
        foreach (var r in renderers) r.enabled = lit;
        foreach (var c in colliders) c.enabled = lit;
        if (worker != null && worker.gameObject.activeSelf != lit) worker.gameObject.SetActive(lit);
        UpdateWorker();
    }

    // Jobs start and finish on CraftingManager's own cadence with no per-job signal, so poll the cheap running count
    private void UpdateWorker()
    {
        if (worker == null || !worker.isActiveAndEnabled) return;
        worker.SetBool(WorkingHash, lit && CraftingManager.Instance.GetRunningCount(station) > 0);
    }

    void Update()
    {
        if (!lit) return;
        if ((workerTimer += Time.deltaTime) >= WorkerPollInterval) { workerTimer = 0f; UpdateWorker(); }
        if (frames.Length < 2) return;
        timer += Time.deltaTime;
        if (timer < 1f / fps) return;
        timer = 0f;
        frame = frame % (frames.Length - 1) + 1;
        sr.sprite = frames[frame];
    }

    public void SetHover(bool on)
    {
        foreach (var r in GetComponentsInChildren<SpriteRenderer>())
            r.renderingLayerMask = on ? r.renderingLayerMask | hoverOutlineLayer : r.renderingLayerMask & ~hoverOutlineLayer;
    }
}
