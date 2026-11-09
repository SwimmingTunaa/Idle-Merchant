using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The tutorial (TUTORIAL_DESIGN.md). Part one: pointers and one-line hints on the 1★ milestone chain. Part two:
/// first-contact notes, one at a time, pinned atop the milestone tracker. Steps are a table in code (polled twice a
/// second); done ids ride <c>SaveData.tutorial</c>, an older save without the chunk marks everything done.
/// Scene singleton under System/.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    public const int HintCount = 3;   // kills / pickups the world pointer shows for (same count as the serve / inspect glyphs)
    private const float PollEvery = 0.5f;

    private class Step
    {
        public string id, title, copy, showMeLabel = "Show me";
        public Func<bool> trigger, done;
        public Action showMe, onEnd;
    }

    public class Hint { public string copy; public Action showMe; }

    [SerializeField] private bool showDebugLogs;

    /// <summary>Settings toggle: hides every pointer, hint line and note. Show me buttons on 1★ cards stay.</summary>
    public bool ShowTips = true;
    public static bool TipsOn => Instance == null || Instance.ShowTips;
    /// <summary>Clicker kills / click pickups so far (saved): the world pointer shows until each reaches HintCount.</summary>
    public int ClickKills, ClickPickups;
    public bool BoardOpened => done.Contains("board");

    private readonly HashSet<string> done = new();
    private readonly HashSet<string> flags = new();   // session facts (not saved): "tab6", "moved", "promoted" …
    private readonly List<Step> steps = new();
    private readonly Dictionary<string, Hint> hints = new();
    private Step current;
    private float poll;

    private MilestoneTracker tracker;
    private ClickerManager clicker;
    private TextMeshPro pointer;
    private Transform pointerTarget;
    private UnityEngine.UIElements.VisualElement signCue;

    // ═════════════════════════════════════════════
    // LIFECYCLE
    // ═════════════════════════════════════════════

    void Awake()
    {
        Instance = this;
        BuildTable();
    }

    void Start()
    {
        tracker = FindAnyObjectByType<MilestoneTracker>();
        clicker = FindAnyObjectByType<ClickerManager>();
        GameSignals.OnAdventurerPromoted += OnPromoted;
        if (SaveManager.NewGame) PauseMenuController.AfterContinue(ColdOpen);
    }

    void OnDestroy()
    {
        GameSignals.OnAdventurerPromoted -= OnPromoted;
        if (Instance == this) Instance = null;
    }

    private void OnPromoted(EntityBase _, string __, string ___) => Flag("promoted");

    /// <summary>A session fact a done-condition reads ("tab6", "moved", "workshop" …). Null-safe one-liner for callers.</summary>
    public static void Flag(string flag) { if (Instance != null) Instance.flags.Add(flag); }
    private bool Has(string flag) => flags.Contains(flag);

    /// <summary>Marks a step done (saved). "board" = the hiring board was opened once.</summary>
    public void Mark(string id) => done.Add(id);

    /// <summary>Settings → Reset tips: every note and pointer comes back.</summary>
    public void ResetAll()
    {
        done.Clear();
        flags.Clear();
        ClickKills = ClickPickups = 0;
        EndCurrent(false);
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
            foreach (var st in steps) done.Add(st.id);
            done.Add("board");
            ClickKills = ClickPickups = HintCount;
            return;
        }
        if (s.done != null) done.UnionWith(s.done);
        ClickKills = s.clickKills;
        ClickPickups = s.clickPickups;
    }

    // ═════════════════════════════════════════════
    // PART ONE — 1★ card hints + world pointer
    // ═════════════════════════════════════════════

    /// <summary>The one-line hint (and Show me) for a 1★ milestone, by asset name; null for the rest.</summary>
    public Hint HintFor(string milestoneName) => hints.TryGetValue(milestoneName, out var h) ? h : null;

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

    // ═════════════════════════════════════════════
    // PART TWO — notes
    // ═════════════════════════════════════════════

    void Update()
    {
        if ((poll += Time.unscaledDeltaTime) < PollEvery) return;
        poll = 0f;

        pointerTarget = PointerWanted ? PickPointerTarget() : null;
        UpdateSignCue();

        if (current != null)
        {
            if (!ShowTips) { EndCurrent(false); return; }
            if (Safe(current.done)) EndCurrent(true);
            return;
        }
        if (!ShowTips || tracker == null) return;
        foreach (var s in steps)
        {
            if (done.Contains(s.id) || !Safe(s.trigger)) continue;
            if (Safe(s.done)) { done.Add(s.id); continue; }   // learned it before the note arrived
            Show(s);
            return;
        }
    }

    private static bool Safe(Func<bool> f)
    {
        try { return f != null && f(); }
        catch (Exception e) { Debug.LogException(e); return false; }
    }

    private void Show(Step s)
    {
        current = s;
        if (showDebugLogs) Debug.Log($"[TutorialManager] note {s.id}");
        tracker.ShowNote(s.title, s.copy, s.showMe == null ? null : s.showMeLabel, s.showMe, () => EndCurrent(true));
    }

    private void EndCurrent(bool markDone)
    {
        if (current == null) return;
        if (markDone) done.Add(current.id);
        try { current.onEnd?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
        current = null;
        tracker?.HideNote();
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
    // THE TABLE
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

    private static void Tab(int i) => FindAnyObjectByType<BookPanelController>()?.OpenToTab(i);
    private static void Jump(Vector3 p) => CameraScrollController.Instance?.JumpTo(p);
    private static void Lock(Transform t) { if (t != null) CameraScrollController.Instance?.LockOnto(t); }
    private static void Board(int tab) => FindAnyObjectByType<HireController>()?.OpenTab(tab);
    private static int BookTab { get { var b = FindAnyObjectByType<BookPanelController>(); return b != null ? b.CurrentTab : -1; } }
    private static int Stars => ProgressionManager.Instance != null ? ProgressionManager.Instance.GetCurrentStars() : 0;

    private static void Roster(AdventurerAgent a)
    {
        Tab(0);
        if (a != null) FindAnyObjectByType<RosterPanelController>()?.FocusAgent(a);
    }

    private static IEnumerable<AdventurerAgent> Heroes()
    {
        foreach (var m in FindObjectsByType<AdventurerManager>(FindObjectsSortMode.None))
            foreach (var a in m.GetAllUnits()) if (a != null) yield return a;
    }

    private static AdventurerAgent FirstHero(Func<AdventurerAgent, bool> pick)
    {
        foreach (var a in Heroes()) if (pick(a)) return a;
        return null;
    }

    private static EventManager.ActiveEvent FirstEvent(Func<EventDef, bool> pick)
    {
        var em = EventManager.Instance; if (em == null) return null;
        foreach (var e in em.Active) if (e.def != null && pick(e.def)) return e;
        return null;
    }

    private static bool AnyStation()
    {
        var cm = CraftingManager.Instance; if (cm == null) return false;
        foreach (CraftingStation s in Enum.GetValues(typeof(CraftingStation))) if (cm.IsStationOwned(s)) return true;
        return false;
    }

    private static bool AnyPinned(Func<RecipeDef, bool> pick = null)
    {
        var cm = CraftingManager.Instance; if (cm == null) return false;
        foreach (CraftingStation s in Enum.GetValues(typeof(CraftingStation)))
            foreach (var r in cm.GetSlots(s)) if (r != null && (pick == null || pick(r))) return true;
        return false;
    }

    private static WorkshopProp OwnedProp()
    {
        var cm = CraftingManager.Instance;
        foreach (var p in FindObjectsByType<WorkshopProp>(FindObjectsSortMode.None))
            if (cm != null && cm.IsStationOwned(p.Station)) return p;
        return null;
    }

    private void BuildTable()
    {
        // ── Part one: the 1★ chain, one line per card (shown on the first unreached card) ──
        hints["1_Milestone_1Star_DungeonCurious"] = new Hint { copy = "Click the creatures. You hit as hard as any hero, for now.", showMe = () => Jump(DepthPoint(1)) };
        hints["2_Milestone_1Star_LootForDays"] = new Hint { copy = "Click the drops. Everything you pick up goes on sale upstairs.", showMe = () => Jump(DepthPoint(1)) };
        hints["3_Milestone_1Star_FirstSale"] = new Hint { copy = "Your loot is in the shop. Customers buy it on their own; click one at the counter to hurry it along.",
            showMe = () => { var q = QueueController.Instance; if (q != null && q.counterPoint != null) Jump(q.counterPoint.position); } };
        hints["4_Milestone_1Star_FirstSteps"] = new Hint { copy = "Keep clicking downstairs, keep serving upstairs. Hires cost about a thousand." };
        hints["5_Milestone_1Star_BraveSoulAcquired"] = new Hint { copy = "The sign up top is the hiring board. Pull the tack to see the next poster.", showMe = () => Board(0) };
        hints["6_Milestone_1Star_HeavyLifting"] = new Hint { copy = "Porters carry loot up so you don't have to click it.", showMe = () => Board(1) };

        // ── Part two: first-contact notes, in priority order ──
        steps.Add(new Step { id = "seals", title = "Guild Seals", copy = "Stars pay Guild Seals. Spend them on the Contracts page.",
            trigger = () => Stars >= 1 && !StarFanfare.Playing, done = () => Has("tab6"), showMe = () => Tab(6) });
        steps.Add(new Step { id = "contracts", title = "Contracts", copy = "The guild is being asked for things. Take one; it never expires.",
            trigger = () => { var cm = ContractManager.Instance; if (cm == null) return false; for (int i = 0; i < ContractManager.OfferSlots; i++) if (cm.GetOffer(i) != null) return true; return false; },
            done = () => ContractManager.Instance != null && ContractManager.Instance.Active != null, showMe = () => Tab(6) });
        steps.Add(new Step { id = "promote", title = "Promotion", copy = "Someone's ready to rank up. Open the Roster and promote them.",
            trigger = () => FirstHero(a => a.CanPromote && !a.NeedsPathPick) != null, done = () => Has("promoted"),
            showMe = () => Roster(FirstHero(a => a.CanPromote)) });
        steps.Add(new Step { id = "path", title = "Choose a path", copy = "A Novice picks a class on their first promotion. No wrong answer.",
            trigger = () => FirstHero(a => a.NeedsPathPick) != null, done = () => FirstHero(a => a.NeedsPathPick) == null,
            showMe = () => Roster(FirstHero(a => a.NeedsPathPick)) });
        steps.Add(new Step { id = "workshop", title = "Workshops", copy = "Your new workshop is in the hall. Click it and pin a recipe to a slot.",
            trigger = AnyStation, done = () => Has("workshop"),
            showMe = () => { var p = OwnedProp(); if (p != null) { Jump(p.transform.position); p.SetHover(true); } },
            onEnd = () => { foreach (var p in FindObjectsByType<WorkshopProp>(FindObjectsSortMode.None)) p.SetHover(false); } });
        steps.Add(new Step { id = "recipes", title = "Recipes", copy = "Recipes live in the book. Stations are bought on the Guild page.",
            trigger = () => AnyPinned() && !AnyStation(), done = () => Has("tab1"), showMe = () => Tab(3) });
        steps.Add(new Step { id = "depth2", title = "A new depth", copy = "A new depth. Your units stay put; drag a portrait in the Roster, or carry them with the mouse.",
            trigger = () => ProgressionManager.Instance != null && ProgressionManager.Instance.maxUnlockedLayer >= 2, done = () => Has("moved"),
            showMe = () => Jump(DepthPoint(2)) });
        steps.Add(new Step { id = "fit", title = "Rank fit", copy = "The face over a hero says how the depth fits them. Hover it.",
            trigger = () => FirstHero(Misfit) != null, done = () => Has("fitface"), showMe = () => Lock(FirstHero(Misfit)?.transform) });
        steps.Add(new Step { id = "caravan", title = "The caravan", copy = "The caravan buys surplus for Seals. It leaves in two minutes.",
            trigger = () => CaravanManager.Instance != null && CaravanManager.Instance.Present,
            done = () => Has("caravan") || CaravanManager.Instance == null || !CaravanManager.Instance.Present,
            showMe = () => Lock(CaravanManager.Instance?.transform) });
        steps.Add(new Step { id = "events", title = "Events", copy = "Things happen at the shop. The card on the left follows them.",
            trigger = () => FirstEvent(d => !d.playerOnly) != null, done = () => Has("evcard"),
            showMe = () => { var e = FirstEvent(d => !d.playerOnly); if (e != null) EventManager.Instance.FocusOn(e); } });
        steps.Add(new Step { id = "pirates", title = "Pirates!", copy = "Pirates. Click them before they reach the door.",
            trigger = () => FirstEvent(d => d.kind == EventKind.PirateRaid) != null,
            done = () => Has("piratekilled") || FirstEvent(d => d.kind == EventKind.PirateRaid) == null,
            showMe = () => { var e = FirstEvent(d => d.kind == EventKind.PirateRaid); if (e != null) Jump(EventManager.Instance.FocusPoint(e)); } });
        steps.Add(new Step { id = "royal", title = "Royal audience", copy = "Royalty waits at the dais. Click them to make an offer.",
            trigger = () => FirstEvent(Royal) != null, done = () => Has("audience") || FirstEvent(Royal) == null,
            showMe = () => { var e = FirstEvent(Royal); if (e != null) Lock(EventManager.Instance.Lead(e)?.transform); } });
        steps.Add(new Step { id = "notices", title = "Notices", copy = "Post a notice to pull a crowd or push an item.",
            trigger = () => BookTab == 6 && Stars >= 2 && Inventory.Instance != null && Inventory.Instance.Gold >= 500,
            done = () => FirstEvent(d => d.playerOnly) != null, showMe = () => Tab(6) });
        steps.Add(new Step { id = "floors", title = "Keep floors", copy = "Crafting is eating your stock. Set a KEEP on the Inventory page to hold some back.",
            trigger = () => AnyStation() && AnyPinned(r => CraftingManager.Instance.GetBlockReason(r) == "Missing materials"),
            done = () => Inventory.Instance != null && Inventory.Instance.FloorsToSave().Count > 0, showMe = () => Tab(2) });
        steps.Add(new Step { id = "shop", title = "Shop upgrades", copy = "Shop upgrades are at the bottom. Quick Counter first; the line is the bottleneck.",
            trigger = () => { var pm = ProgressionManager.Instance; var qc = Resources.Load<GuildUpgradeDef>("Guild Upgrades/Quick Counter I");
                return BookTab == 1 && Stars >= 1 && pm != null && qc != null && pm.CanPurchaseUpgrade(qc); },
            done = () => { var pm = ProgressionManager.Instance; return pm != null && (pm.HasUpgrade(UpgradeType.QuickCounter) || pm.HasUpgrade(UpgradeType.QueueRoom) || pm.HasUpgrade(UpgradeType.Shelves)); },
            showMe = () => Tab(1) });
    }

    private static bool Misfit(AdventurerAgent a) { var m = RankFit.MoodOf(a.Fit); return m == RankFit.Mood.Outmatched || m == RankFit.Mood.Slacking; }
    private static bool Royal(EventDef d) => d.kind == EventKind.RoyalVisit || d.kind == EventKind.Delegation;
}

public class TutorialSave
{
    public List<string> done = new();
    public int clickKills, clickPickups;
}
