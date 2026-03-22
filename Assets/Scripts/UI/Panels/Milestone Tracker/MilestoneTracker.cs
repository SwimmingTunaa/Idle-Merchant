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
    private MilestoneProgressEvents progressEvents;

    // ═════════════════════════════════════════════
    // LIFECYCLE
    // ═════════════════════════════════════════════

    void Start()
    {
        if (!QueryElements()) return;

        progressEvents = new MilestoneProgressEvents(RefreshProgress, Refresh);
        progressEvents.Subscribe();

        Refresh();

        isExpanded = startExpanded;
        ApplyExpandedState();

        refreshCoroutine = StartCoroutine(LiveRefreshLoop());
    }

    void OnDestroy()
    {
        progressEvents?.Unsubscribe();

        if (refreshCoroutine != null)
            StopCoroutine(refreshCoroutine);
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

        var toggleButton = root.Q<Button>("mt-toggle-button");

        if (list == null || body == null || toggleButton == null)
        {
            Debug.LogError("[MilestoneTracker] Required elements not found. Check UXML is added to the UIDocument.");
            return false;
        }

        toggleButton.clicked += Toggle;

        if (showDebugLogs)
            Debug.Log("[MilestoneTracker] Elements queried successfully");

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

            var fill = row.Q<VisualElement>("mt-progress-fill");
            if (fill != null)
            {
                fill.style.width = Length.Percent(Mathf.Clamp01(progress) * 100f);
                fill.EnableInClassList("mt-progress-fill--complete", complete);
            }

            var text = row.Q<Label>("mt-progress-text");
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

        // Progress bar — track is the container, fill + label both sit inside it
        var track = new VisualElement();
        track.AddToClassList("mt-progress-track");
        track.AddToClassList("mt-progress-row");

        var fill = new VisualElement();
        fill.name = "mt-progress-fill";
        fill.AddToClassList("mt-progress-fill");
        fill.EnableInClassList("mt-progress-fill--complete", complete);
        fill.style.width = Length.Percent(Mathf.Clamp01(progress) * 100f);
        track.Add(fill);

        var progressText = new Label($"{current}/{def.targetValue}");
        progressText.name = "mt-progress-text";
        progressText.AddToClassList("mt-progress-text");
        track.Add(progressText);

        row.Add(track);

        return row;
    }

    // ═════════════════════════════════════════════
    // ACCORDION
    // ═════════════════════════════════════════════

    private void Toggle()
    {
        isExpanded = !isExpanded;
        ApplyExpandedState();
    }

    private void ApplyExpandedState()
    {
        if (body == null || chevron == null) return;

        body.EnableInClassList("mt-body--collapsed", !isExpanded);
        chevron.EnableInClassList("mt-chevron--collapsed", !isExpanded);

        if (showDebugLogs)
            Debug.Log($"[MilestoneTracker] {(isExpanded ? "Expanded" : "Collapsed")}");
    }
}
