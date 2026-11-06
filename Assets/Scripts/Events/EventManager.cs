using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Timed world events: every rollMin–rollMax game seconds one unlocked, off-cooldown EventDef is drawn
/// (weighted) while fewer than maxActive run. Each kind's effect is applied on start and reverted on end;
/// parties end early once every member has left the shop. Signal OnEventsChanged on every start/end;
/// the HUD EventTracker reads Active. Nothing is saved: a reload drops running events and cooldowns
/// (ponytail: add an EventsSave chunk if players reload-farm them).
/// </summary>
public class EventManager : MonoBehaviour
{
    public static EventManager Instance { get; private set; }

    public class ActiveEvent
    {
        public EventDef def;
        public float remaining;
        public readonly List<CustomerAgent> party = new();
        public readonly List<int> serials = new();   // SpawnSerial per member: a pooled instance reused as a new customer no longer counts
        public int partySize;
        /// <summary>Live status line for the HUD card (empty = the def's effectLine).</summary>
        public string note = "";
        /// <summary>Set by RoyalAudience when the lead left unserved: the cooldown is multiplied on End.</summary>
        public bool snubbed;
        /// <summary>Party members still in the shop (0 for timed-only events).</summary>
        public int PartyLeft
        {
            get
            {
                int n = 0;
                for (int i = 0; i < party.Count; i++)
                {
                    var c = party[i];
                    if (c != null && c.SpawnSerial == serials[i] && c.gameObject.activeInHierarchy && c.State != CustomerState.Exited) n++;
                }
                return n;
            }
        }
        public float Progress01 => def.durationSeconds > 0f ? 1f - remaining / def.durationSeconds : 0f;
    }

    [SerializeField] private List<EventDef> defs = new();
    [Tooltip("Game seconds between rolls (random in range); the first roll waits a full interval")]
    [SerializeField] private float rollMin = 240f, rollMax = 360f;
    [SerializeField] private int maxActive = 2;
    [SerializeField] private bool showDebugLogs;

    private readonly List<ActiveEvent> active = new();
    private readonly Dictionary<EventDef, float> cooldownUntil = new();
    private readonly List<EventDef> eligible = new();
    private float nextRollIn;
    private CustomerSpawner spawner;

    public IReadOnlyList<ActiveEvent> Active => active;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        nextRollIn = Random.Range(rollMin, rollMax);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start() { spawner = FindAnyObjectByType<CustomerSpawner>(); }

    void Update()
    {
        float dt = Time.deltaTime;   // scaled: paused / main menu = frozen, like everything else in the world
        bool changed = false;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var e = active[i];
            e.remaining -= dt;
            if (e.remaining <= 0f || (e.partySize > 0 && e.PartyLeft == 0)) { End(e); active.RemoveAt(i); changed = true; }
        }
        if (changed) GameSignals.RaiseEventsChanged();

        nextRollIn -= dt;
        if (nextRollIn <= 0f)
        {
            nextRollIn = Random.Range(rollMin, rollMax);
            Roll();
        }
    }

    private void Roll()
    {
        if (active.Count >= maxActive) return;
        int stars = ProgressionManager.Instance != null ? ProgressionManager.Instance.GetCurrentStars() : 0;
        eligible.Clear();
        float total = 0f;
        foreach (var d in defs)
        {
            if (d == null || stars < d.unlockStar || IsActive(d)) continue;
            if (d.kind == EventKind.PirateRaid && !ShopOnScreen) continue;   // a raid you can't see is just a tax
            if (cooldownUntil.TryGetValue(d, out float until) && Time.time < until) continue;
            eligible.Add(d); total += Mathf.Max(0.001f, d.weight);
        }
        if (eligible.Count == 0) return;
        float r = Random.Range(0f, total);
        foreach (var d in eligible) { r -= Mathf.Max(0.001f, d.weight); if (r <= 0f) { Fire(d); return; } }
        Fire(eligible[^1]);
    }

    /// <summary>Camera is up at the guild (the shop band), not down a depth.</summary>
    private static bool ShopOnScreen => Camera.main != null && Camera.main.transform.position.y > -6f;

    public bool IsActive(EventDef def) { foreach (var e in active) if (e.def == def) return true; return false; }

    /// <summary>Start an event now (the roll, or the Game Debugger).</summary>
    public bool Fire(EventDef def)
    {
        if (def == null || IsActive(def) || spawner == null) return false;
        var e = new ActiveEvent { def = def, remaining = def.durationSeconds };
        switch (def.kind)
        {
            case EventKind.RushHour:
                spawner.RateMultiplier *= def.rateMultiplier;
                spawner.RecalculateSpawnRate();
                break;
            case EventKind.AdventurerParty:
                if (def.partyCustomer == null) return false;
                spawner.SpawnParty(def.partyCustomer, def.partySize, e.party);
                e.partySize = e.party.Count;
                if (e.partySize == 0) return false;
                Announce(e.party[0], $"{def.displayName}!");
                break;
            case EventKind.RoyalVisit:
                spawner.SpawnRoyalVisit(e.party);
                e.partySize = e.party.Count;
                if (e.partySize == 0) return false;
                e.party[0].gameObject.AddComponent<RoyalAudience>().Begin(e.party[0], def, e);
                break;
            case EventKind.Delegation:
            case EventKind.PirateRaid:
                if (def.partyMembers != null)
                    foreach (var m in def.partyMembers) if (m != null) spawner.SpawnParty(m, 1, e.party);
                e.partySize = e.party.Count;
                if (e.partySize == 0) return false;
                if (def.kind == EventKind.PirateRaid)
                    foreach (var c in e.party) c.gameObject.AddComponent<PirateRaider>().Begin(c, def.stealInterval, def.carryMax, def.stealPercent);
                else
                    e.party[0].gameObject.AddComponent<RoyalAudience>().Begin(e.party[0], def, e);
                Announce(e.party[0], def.kind == EventKind.PirateRaid ? "Pirates!" : $"{def.displayName}!");
                break;
        }
        foreach (var c in e.party) e.serials.Add(c.SpawnSerial);
        active.Add(e);
        if (showDebugLogs) Debug.Log($"[EventManager] {def.name} started ({def.durationSeconds}s)");
        GameSignals.RaiseEventsChanged();
        return true;
    }

    private void End(ActiveEvent e)
    {
        if (e.def.kind == EventKind.RushHour && spawner != null)
        {
            spawner.RateMultiplier /= e.def.rateMultiplier;
            spawner.RecalculateSpawnRate();
        }
        cooldownUntil[e.def] = Time.time + e.def.cooldownSeconds * (e.snubbed ? e.def.snubCooldownMultiplier : 1f);
        if (showDebugLogs) Debug.Log($"[EventManager] {e.def.name} ended");
    }

    public static void Announce(Component at, string text)
    {
        if (at != null && DamageNumberManager.Instance != null)
            DamageNumberManager.Instance.ShowText(text, at.transform, new Color(1f, 0.84f, 0.3f));
    }

    /// <summary>Where the event is happening (HUD card click → camera). Every current kind is at the shop counter.</summary>
    public Vector3 FocusPoint(ActiveEvent e)
    {
        if (e.def.kind == EventKind.PirateRaid && LootPile.Instance != null) return LootPile.Instance.transform.position;
        var q = QueueController.Instance;
        return q != null && q.counterPoint != null ? q.counterPoint.position : transform.position;
    }

    /// <summary>The first party member still in the shop (the lead while they're around), or null for timed-only events.</summary>
    public CustomerAgent Lead(ActiveEvent e)
    {
        for (int i = 0; i < e.party.Count; i++)
        {
            var c = e.party[i];
            if (c != null && c.SpawnSerial == e.serials[i] && c.gameObject.activeInHierarchy && c.State != CustomerState.Exited) return c;
        }
        return null;
    }

    /// <summary>HUD card click: follow the event's lead customer (manual scroll releases it), else ease to its spot.</summary>
    public void FocusOn(ActiveEvent e)
    {
        var cam = CameraScrollController.Instance; if (cam == null) return;
        var lead = Lead(e);
        if (lead != null) cam.LockOnto(lead.transform); else cam.JumpTo(FocusPoint(e));
    }
}
