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
        body       = root.Q<VisualElement>("mt-body");
        list       = root.Q<VisualElement>("mt-list");

        if (body == null || list == null)
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

        int completed = 0;
        foreach (var m in milestones)
        {
            var row = CreateRow(m);
            list.Add(row);
            rows.Add((m, row));
            if (ProgressionManager.Instance.IsMilestoneComplete(m)) completed++;
        }

        UpdateCountLabel(completed, milestones.Count);
    }

    // Lightweight: updates bar values on existing rows without rebuilding DOM.
    private void RefreshProgress()
    {
        if (ProgressionManager.Instance == null) return;

        int completed = 0;
        foreach (var (def, row) in rows)
        {
            bool  complete = ProgressionManager.Instance.IsMilestoneComplete(def);
            int   current  = ProgressionManager.Instance.GetMilestoneCurrentValue(def);
            float progress = ProgressionManager.Instance.GetMilestoneProgress(def);

            var fill = row.Q<VisualElement>("progress-fill");
            if (fill != null)
            {
                fill.style.width = Length.Percent(Mathf.Clamp01(progress) * 100f);
                fill.EnableInClassList("progress-fill--success", complete);
            }

            var text = row.Q<Label>("progress-label");
            if (text != null) text.text = $"{current}/{def.targetValue}";

            row.Q<Label>("mt-row-description")?.EnableInClassList("mt-row-description--complete", complete);
            row.Q<Label>("mt-row-check")?.EnableInClassList("mt-row-check--visible", complete);

            if (complete) completed++;
        }

        UpdateCountLabel(completed, rows.Count);
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

    private VisualElement CreateRow(StarMilestoneDef def)
    {
        bool  complete = ProgressionManager.Instance.IsMilestoneComplete(def);
        int   current  = ProgressionManager.Instance.GetMilestoneCurrentValue(def);
        float progress = ProgressionManager.Instance.GetMilestoneProgress(def);

        var row = new VisualElement();
        row.AddToClassList("mt-row");

        // Description + checkmark
        var top = new VisualElement();
        top.AddToClassList("mt-row-top");

        var desc = new Label(def.description);
        desc.name = "mt-row-description";
        desc.AddToClassList("mt-row-description");
        desc.EnableInClassList("mt-row-description--complete", complete);
        top.Add(desc);

        var check = new Label("✓");
        check.name = "mt-row-check";
        check.AddToClassList("mt-row-check");
        check.EnableInClassList("mt-row-check--visible", complete);
        top.Add(check);

        row.Add(top);

        // Progress bar — uses ProgressBar component classes
        var track = new VisualElement();
        track.AddToClassList("progress-track");
        track.AddToClassList("mt-progress-row");

        var fill = new VisualElement();
        fill.name = "progress-fill";
        fill.AddToClassList("progress-fill");
        fill.EnableInClassList("progress-fill--success", complete);
        fill.style.width = Length.Percent(Mathf.Clamp01(progress) * 100f);
        track.Add(fill);

        var progressText = new Label($"{current}/{def.targetValue}");
        progressText.name = "progress-label";
        progressText.AddToClassList("progress-label");
        track.Add(progressText);

        row.Add(track);

        return row;
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
