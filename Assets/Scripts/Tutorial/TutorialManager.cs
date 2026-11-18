using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UIElements;
using UnityEngine;

/// <summary>
/// The tutorial (TUTORIAL_DESIGN.md): world pointers on the 1★ milestone chain (click cues over mobs / drops, the
/// LMB glyph by the hiring sign). Polled twice a second; counts ride <c>SaveData.tutorial</c>, an older save without the
/// chunk has nothing to learn. Scene singleton under System/.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    public const int HintCount = 3;   // kills / pickups the world pointer shows for (same count as the serve / inspect glyphs)
    private const float PollEvery = 0.5f;

    [SerializeField] private bool showDebugLogs;

    /// <summary>Settings toggle: hides every pointer.</summary>
    public bool ShowTips = true;
    public static bool TipsOn => Instance == null || Instance.ShowTips;
    /// <summary>Clicker kills / click pickups so far (saved): the world pointer shows until each reaches HintCount.</summary>
    public int ClickKills, ClickPickups;
    public bool BoardOpened => done.Contains("board");

    private readonly HashSet<string> done = new();   // saved: "board" = the hiring board was opened once
    private float poll;

    private ClickerManager clicker;
    private TextMeshPro pointer;
    private Transform pointerTarget;
    private UnityEngine.UIElements.VisualElement signCue;

    // ═════════════════════════════════════════════
    // LIFECYCLE
    // ═════════════════════════════════════════════

    void Awake() => Instance = this;

    void Start()
    {
        clicker = FindAnyObjectByType<ClickerManager>();
        if (SaveManager.NewGame) PauseMenuController.AfterContinue(ColdOpen);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>Marks a step done (saved). "board" = the hiring board was opened once.</summary>
    public void Mark(string id) => done.Add(id);

    /// <summary>Settings → Reset tips: every pointer comes back.</summary>
    public void ResetAll()
    {
        done.Clear();
        ClickKills = ClickPickups = 0;
    }

    /// <summary>Camera opens on the shop; a new guild's first Continue eases it down to depth 1, where the game starts.</summary>
    private void ColdOpen()
    {
        var cam = CameraScrollController.Instance;
        if (cam != null) cam.JumpTo(DepthPoint(1));
    }

    // ═════════════════════════════════════════════
    // SAVE
    // ═════════════════════════════════════════════

    public TutorialSave ToSave() => new TutorialSave { done = new List<string>(done), clickKills = ClickKills, clickPickups = ClickPickups };

    /// <summary>null = a save from before the tutorial: a player who got this far has nothing to learn from it.</summary>
    public void LoadFrom(TutorialSave s)
    {
        done.Clear();
        if (s == null)
        {
            done.Add("board");
            ClickKills = ClickPickups = HintCount;
            return;
        }
        if (s.done != null) done.UnionWith(s.done);
        ClickKills = s.clickKills;
        ClickPickups = s.clickPickups;
    }

    // ═════════════════════════════════════════════
    // WORLD POINTER
    // ═════════════════════════════════════════════

    public void OnClickKill() { if (ClickKills < HintCount) ClickKills++; }
    public void OnClickPickup() { if (ClickPickups < HintCount) ClickPickups++; }

    private bool PointerWanted => ShowTips && (ClickKills < HintCount || ClickPickups < HintCount);

    /// <summary>Oldest loose drop on depth 1 while pickups are owed, else the mob nearest the camera while kills are.</summary>
    private Transform PickPointerTarget()
    {
        if (ClickPickups < HintCount && LootManager.Instance != null)
            foreach (var l in LootManager.Instance.GetLootOnLayer(1))
                if (l != null && l.gameObject.activeInHierarchy && !l.IsReserved) return l.transform;
        if (ClickKills < HintCount && MobManager.Instance != null)
        {
            Transform best = null; float bestD = float.MaxValue;
            Vector3 eye = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            foreach (var m in MobManager.Instance.GetMobsOnLayer(1))
            {
                if (m == null || !m.IsAlive || m.IsDying) continue;
                float d = (m.transform.position - eye).sqrMagnitude;
                if (d < bestD) { bestD = d; best = m.transform; }
            }
            return best;
        }
        return null;
    }

    void LateUpdate()
    {
        bool blocked = UIManager.Instance != null && UIManager.Instance.IsBlockingWorldInput;
        bool show = pointerTarget != null && pointerTarget.gameObject.activeInHierarchy && !blocked;
        if (show && pointer == null && clicker != null) pointer = clicker.MakeHint("TutorialPointer");
        if (pointer == null) return;
        if (pointer.gameObject.activeSelf != show) pointer.gameObject.SetActive(show);
        if (!show) return;
        float top = pointerTarget.TryGetComponent(out BoxCollider2D col) ? col.offset.y + col.size.y * 0.5f : 1f;
        float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) * 0.15f;
        pointer.transform.position = pointerTarget.TransformPoint(new Vector3(0f, top + clicker.HintOffset + bob, 0f));
    }

    void Update()
    {
        if ((poll += Time.unscaledDeltaTime) < PollEvery) return;
        poll = 0f;
        pointerTarget = PointerWanted ? PickPointerTarget() : null;
        UpdateSignCue();
    }

    // The LMB glyph beside the hiring sign's badge: from "Earn 2,500 gold" reached until the board is opened once.
    private void UpdateSignCue()
    {
        var pm = ProgressionManager.Instance;
        bool want = ShowTips && !BoardOpened && pm != null && pm.GetCurrentStars() == 0 && Reached(pm, "4_Milestone_1Star_FirstSteps");
        if (signCue == null)
        {
            if (!want) return;
            var ui = FindAnyObjectByType<UIManager>();
            var button = ui != null ? ui.GetComponent<UnityEngine.UIElements.UIDocument>()?.rootVisualElement?.Q("hiring-board-button") : null;
            if (button == null) return;
            signCue = new UnityEngine.UIElements.VisualElement { name = "tut-sign-cue", pickingMode = UnityEngine.UIElements.PickingMode.Ignore };
            signCue.AddToClassList("tut-cue");
            var lmb = Resources.Load<Sprite>("Sprite Assets/LMB");
            if (lmb != null) signCue.style.backgroundImage = new UnityEngine.UIElements.StyleBackground(lmb);
            button.Add(signCue);
            int step = 0;
            signCue.schedule.Execute(() => signCue.style.translate = new UnityEngine.UIElements.Translate(0f, -Mathf.Abs(Mathf.Sin(step++ * 0.25f)) * 6f)).Every(50);
        }
        signCue.style.display = want ? UnityEngine.UIElements.DisplayStyle.Flex : UnityEngine.UIElements.DisplayStyle.None;
    }

    // ═════════════════════════════════════════════
    // HELPERS
    // ═════════════════════════════════════════════

    private static bool Reached(ProgressionManager pm, string milestone)
    {
        foreach (var m in pm.GetMilestonesForStar(1)) if (m != null && m.name == milestone) return pm.IsMilestoneReached(m);
        return false;
    }

    private static Vector3 DepthPoint(int depth)
    {
        var m = AdventurerManager.ManagerForLayer(depth);
        return m != null && m.operationArea != null ? m.operationArea.bounds.center : Vector3.zero;
    }
}

public class TutorialSave
{
    public List<string> done = new();
    public int clickKills, clickPickups;
}
