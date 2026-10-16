using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Always-visible HUD milestone tracker.
// Expects milestone-tracker elements to already exist in the UIDocument
// (added manually via UXML or UI Builder). Just queries and drives them.
public class MilestoneTracker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UIDocument uiDocument;
    [Tooltip("Row template (MilestoneCard.uxml): torn-paper card with progress, reward and claim button.")]
    [SerializeField] private VisualTreeAsset cardTemplate;

    [Header("Settings")]
    [Tooltip("Seconds between live progress refreshes")]
    [SerializeField] private float refreshInterval = 1f;
    [SerializeField] private bool startExpanded = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // Cached element refs
    private Label starLabel;
    private Label countLabel;
    private Label chevron;
    private Label claimBadge;
    private VisualElement body;
    private VisualElement list;

    // State
    private bool isExpanded;
    private List<(StarMilestoneDef def, VisualElement row)> rows = new();
    private Coroutine refreshCoroutine;
    private Coroutine accordionCoroutine;
    private MilestoneProgressEvents progressEvents;

    // ═════════════════════════════════════════════
    // LIFECYCLE
    // ═════════════════════════════════════════════

    void OnEnable()
    {
        if (uiDocument == null) return;
        var btn = uiDocument.rootVisualElement?.Q<Button>("mt-toggle-button");
        if (btn == null) return;
        btn.clicked += Toggle;
        Debug.Log("[MilestoneTracker] clicked registered");
    }

    void OnDisable()
    {
        if (uiDocument == null) return;
        var btn = uiDocument.rootVisualElement?.Q<Button>("mt-toggle-button");
        if (btn != null) btn.clicked -= Toggle;
    }

    void Start()
    {
        if (!QueryElements()) return;

        progressEvents = new MilestoneProgressEvents(RefreshProgress, Refresh);
        progressEvents.Subscribe();

        Refresh();

        isExpanded = startExpanded;
        InitAccordion();

        refreshCoroutine = StartCoroutine(LiveRefreshLoop());
    }

    void OnDestroy()
    {
        progressEvents?.Unsubscribe();

        if (refreshCoroutine != null)
            StopCoroutine(refreshCoroutine);
    }

    private void OnTogglePressed(PointerDownEvent evt)
    {
        Debug.Log("[MilestoneTracker] PointerDown received");
        Toggle();
    }

    // ═════════════════════════════════════════════
    // ELEMENT QUERY
    // ═════════════════════════════════════════════

    private bool QueryElements()
    {
        if (uiDocument == null)
        {
            Debug.LogError("[MilestoneTracker] UIDocument not assigned!");
            return false;
        }

        var root = uiDocument.rootVisualElement;

        starLabel  = root.Q<Label>("mt-star-label");
        countLabel = root.Q<Label>("mt-count-label");
        chevron    = root.Q<Label>("mt-chevron");
        claimBadge = root.Q<Label>("mt-claim-badge");
        body       = root.Q<VisualElement>("mt-body");
        list       = root.Q<VisualElement>("mt-list");

        if (body == null || list == null || cardTemplate == null)
        {
            Debug.LogError("[MilestoneTracker] Required elements not found.");
            return false;
        }

        return true;
    }

    // ═════════════════════════════════════════════
    // REFRESH
    // ═════════════════════════════════════════════

    // Full rebuild: recreates all rows from current milestone state.
    private void Refresh()
    {
        if (list == null || ProgressionManager.Instance == null) return;

        list.Clear();
        rows.Clear();

        int currentStars = ProgressionManager.Instance.GetCurrentStars();
        int nextStar     = currentStars + 1;

        if (starLabel != null)
            starLabel.text = currentStars >= 5 ? "Max Stars!" : $"{nextStar}★ Progress";

        var milestones = currentStars >= 5
            ? new List<StarMilestoneDef>()
            : ProgressionManager.Instance.GetMilestonesForStar(nextStar);

        foreach (var m in milestones)
        {
            var row = CreateRow(m);
            list.Add(row);
            rows.Add((m, row));
        }

        RefreshProgress();
    }

    // Lightweight: updates bar values + claim state on existing rows without rebuilding DOM.
    private void RefreshProgress()
    {
        var pm = ProgressionManager.Instance;
        if (pm == null) return;

        int claimed = 0;
        foreach (var (def, row) in rows)
        {
            bool isClaimed = pm.IsMilestoneClaimed(def);
            bool reached   = pm.IsMilestoneReached(def);
            int  current   = reached ? def.targetValue : pm.GetMilestoneCurrentValue(def);
            float progress = reached ? 1f : pm.GetMilestoneProgress(def);

            row.EnableInClassList("mt-card--claimable", reached && !isClaimed);
            row.EnableInClassList("mt-card--claimed", isClaimed);
            row.Q<VisualElement>("progress-fill").style.width = Length.Percent(Mathf.Clamp01(progress) * 100f);
            row.Q<Label>("progress-label").text =
                $"{AnimatedNumberUITK.Abbrev(current)} / {AnimatedNumberUITK.Abbrev(def.targetValue)}";
            if (isClaimed && def.goldReward > 0)
                row.Q<Label>("card-reward-label").text = $"Reward: {AnimatedNumberUITK.Abbrev(def.goldReward)} - collected";

            if (isClaimed) claimed++;
        }

        UpdateCountLabel(claimed, rows.Count);

        int claimable = pm.ClaimableCount;
        if (claimBadge != null) claimBadge.text = claimable.ToString();
        UIManager.SetBoardBadge(claimBadge, claimable > 0, claimable > 0);
    }

    private void UpdateCountLabel(int completed, int total)
    {
        if (countLabel != null)
            countLabel.text = $"{completed}/{total}";
    }

    private IEnumerator LiveRefreshLoop()
    {
        var wait = new WaitForSeconds(refreshInterval);
        while (true)
        {
            yield return wait;
            RefreshProgress();
        }
    }

    // ═════════════════════════════════════════════
    // ROW CREATION
    // ═════════════════════════════════════════════

    // Clones the card; state classes/values are applied by RefreshProgress.
    private VisualElement CreateRow(StarMilestoneDef def)
    {
        var row = cardTemplate.CloneTree().Q<VisualElement>("card-root");

        row.Q<Label>("card-name").text = def.displayName;
        row.Q<Label>("card-desc").text = def.description;
        if (def.icon != null)
            row.Q<VisualElement>("card-icon").style.backgroundImage = new StyleBackground(def.icon);

        string reward = AnimatedNumberUITK.Abbrev(def.goldReward);
        row.Q<Label>("card-reward-label").text = $"Reward: {reward}";
        if (def.goldReward <= 0)   // inline only for "off" — USS hides it while claimable
            row.Q<VisualElement>("card-reward").style.display = DisplayStyle.None;

        var claimButton = row.Q<Button>("card-claim");
        row.Q<Label>("card-claim-amount").text = def.goldReward > 0 ? $"+{reward}" : "";
        claimButton.clicked += () =>
        {
            claimButton.SetEnabled(false);   // no double-claims; the row flips to claimed on the signal
            ProgressionManager.Instance?.TryClaimMilestone(def, BurstOrigin(row));
        };

        return row;
    }

    // Panel point → world point (z=0) just LEFT of the card: world sprites draw under UI Toolkit,
    // so a burst at the button itself would fly hidden behind the tracker column.
    private static Vector3 BurstOrigin(VisualElement element)
    {
        Camera cam = Camera.main;
        if (cam == null || element.panel == null) return Vector3.zero;

        Vector2 center = new Vector2(element.worldBound.xMin - 80f, element.worldBound.center.y);
        Vector2 panelSize = element.panel.visualTree.layout.size;
        var screen = new Vector3(
            center.x / panelSize.x * Screen.width,
            (1f - center.y / panelSize.y) * Screen.height,
            -cam.transform.position.z);
        return cam.ScreenToWorldPoint(screen);
    }

    // ═════════════════════════════════════════════
    // ACCORDION
    // ═════════════════════════════════════════════

    private void InitAccordion()
    {
        body.RegisterCallback<GeometryChangedEvent>(OnBodyGeometryReady);
    }

    private void OnBodyGeometryReady(GeometryChangedEvent evt)
    {
        if (evt.newRect.height <= 0f) return;
        body.UnregisterCallback<GeometryChangedEvent>(OnBodyGeometryReady);

        if (!isExpanded)
            SnapCollapsed();
    }

    private void SnapCollapsed()
    {
        body.style.display = DisplayStyle.None;
        body.style.opacity = 0f;
        chevron.EnableInClassList("mt-chevron--collapsed", true);
    }

    private void Toggle()
    {
        isExpanded = !isExpanded;

        Debug.Log($"[MilestoneTracker] Toggle → isExpanded={isExpanded}");

        if (accordionCoroutine != null)
            StopCoroutine(accordionCoroutine);

        accordionCoroutine = StartCoroutine(AnimateAccordion(isExpanded));
        chevron.EnableInClassList("mt-chevron--collapsed", !isExpanded);
    }

    private IEnumerator AnimateAccordion(bool expand)
    {
        if (expand)
        {
            // Show first so we can animate opacity
            body.style.display = DisplayStyle.Flex;
            body.style.opacity = 0f;

            float duration = 0.2f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                body.style.opacity = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                yield return null;
            }
            body.style.opacity = 1f;
        }
        else
        {
            float duration = 0.15f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                body.style.opacity = Mathf.SmoothStep(1f, 0f, elapsed / duration);
                yield return null;
            }
            body.style.opacity = 0f;
            body.style.display = DisplayStyle.None;
        }
    }
}
