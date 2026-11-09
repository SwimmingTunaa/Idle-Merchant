using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Timed world events: every rollMin–rollMax game seconds one unlocked, off-cooldown EventDef is drawn
/// (weighted) while fewer than maxActive run. Each kind's effect is applied on start and reverted on end;
/// parties end early once every member has left the shop. Signal OnEventsChanged on every start/end;
/// the HUD EventTracker reads Active. Saved as SaveData.events: timed effects (Rush Hour, notices) keep
/// their clock across a reload; party events can't (customers aren't saved) and go on cooldown as if ended,
/// and what pirates held at save goes back on the pile at load.
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
        /// <summary>AdventurerParty waves still to arrive (EventDef.waves − 1 at Fire) and the clock to the next.</summary>
        public int wavesLeft;
        public float nextWaveIn;
        /// <summary>Live status line for the HUD card (empty = the def's effectLine).</summary>
        public string note = "";
        /// <summary>Set by RoyalAudience when the lead left unserved: the cooldown is multiplied on End.</summary>
        public bool snubbed;
        /// <summary>Notices: the audience (Advertise) or the item (FeaturedItem) the player chose at post time.</summary>
        public CustomerDef customer;
        public ItemDef item;
        public Sprite Icon => item != null ? item.icon : customer != null && customer.portrait != null ? customer.portrait : def.icon;
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
    [SerializeField] private float rollMin = 150f, rollMax = 240f;
    [SerializeField] private int maxActive = 2;
    [SerializeField] private bool showDebugLogs;

    private readonly List<ActiveEvent> active = new();
    private readonly Dictionary<EventDef, float> cooldownUntil = new();
    private readonly List<EventDef> eligible = new();
    private float nextRollIn;
    private CustomerSpawner spawner;

    public IReadOnlyList<ActiveEvent> Active => active;
    /// <summary>Player-posted notices (Resources/Events/Ads, playerOnly): posted from the Contracts page, never rolled.</summary>
    public IReadOnlyList<EventDef> Ads => ads;
    private readonly List<EventDef> ads = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        nextRollIn = Random.Range(rollMin, rollMax);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        spawner = FindAnyObjectByType<CustomerSpawner>();
        foreach (var d in Resources.LoadAll<EventDef>("Events/Ads")) if (d != null && d.playerOnly) ads.Add(d);
    }

    void Update()
    {
        float dt = Time.deltaTime;   // scaled: paused / main menu = frozen, like everything else in the world
        bool changed = false;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var e = active[i];
            e.remaining -= dt;
            if (e.wavesLeft > 0 && (e.nextWaveIn -= dt) <= 0f) SpawnWave(e);
            if (e.remaining <= 0f || (e.partySize > 0 && e.wavesLeft == 0 && e.PartyLeft == 0)) { End(e); active.RemoveAt(i); changed = true; }
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
        int natural = 0;
        foreach (var e in active) if (!e.def.playerOnly && !e.def.outsideCap) natural++;   // posted notices and raids never crowd out the dice
        bool capFull = natural >= maxActive;   // only outsideCap defs (pirates) may still roll
        int stars = ProgressionManager.Instance != null ? ProgressionManager.Instance.GetCurrentStars() : 0;
        eligible.Clear();
        float total = 0f;
        foreach (var d in defs)
        {
            if (d == null || d.playerOnly || stars < d.unlockStar || (IsActive(d) && !d.stackable) || (capFull && !d.outsideCap)) continue;
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

    /// <summary>A Rush Hour is on: spawn rate × the def's multiplier (Fire/End), every walk-in decisive
    /// (CustomerAgent.Init) and the counter serves at RushServeScale (CounterService); coin border on the HUD (EventTracker).</summary>
    public bool RushHour { get { foreach (var e in active) if (e.def != null && e.def.kind == EventKind.RushHour) return true; return false; } }
    public const float RushServeScale = 0.5f;

    public ActiveEvent ActiveOf(EventDef def) { foreach (var e in active) if (e.def == def) return e; return null; }
    public float CooldownLeft(EventDef def) => cooldownUntil.TryGetValue(def, out float until) ? Mathf.Max(0f, until - Time.time) : 0f;

    /// <summary>Gold to post a notice: goldCostMinutes of passive income, at least minGoldCost.</summary>
    public int CostOf(EventDef def)
    {
        float goldPerMin = Mathf.Max(30f, SaveManager.Instance != null ? SaveManager.Instance.GoldPerMin : 0f);
        return Mathf.Max(def.minGoldCost, Mathf.RoundToInt(goldPerMin * def.goldCostMinutes));
    }

    /// <summary>Contracts page: pay for and start a notice (one of each at a time, cooldown after).</summary>
    public bool TryPost(EventDef def, CustomerDef customer = null, ItemDef item = null)
    {
        if (def == null || !def.playerOnly || IsActive(def) || CooldownLeft(def) > 0f) return false;
        if (def.kind == EventKind.Advertise && customer == null) return false;
        if (def.kind == EventKind.FeaturedItem && item == null) return false;
        if (Inventory.Instance == null || !Inventory.Instance.TrySpendGold(CostOf(def))) return false;
        return Fire(def, customer, item);
    }

    /// <summary>Start an event now (the roll, the Game Debugger, or a posted notice).</summary>
    public bool Fire(EventDef def, CustomerDef customer = null, ItemDef item = null)
    {
        if (def == null || (IsActive(def) && !def.stackable) || spawner == null) return false;
        var e = new ActiveEvent { def = def, remaining = def.durationSeconds, customer = customer, item = item };
        switch (def.kind)
        {
            case EventKind.Advertise:
                if (customer == null) return false;
                spawner.BoostWeight(customer, def.rateMultiplier);
                e.note = $"{customer.displayName} ×{def.rateMultiplier:0.#}";
                break;
            case EventKind.FeaturedItem:
                if (item == null || SalesManager.Instance == null) return false;
                SalesManager.Instance.Featured = item;
                SalesManager.Instance.FeaturedChance = def.featuredChance;
                e.note = $"Sale on {item.displayName}";
                break;
            case EventKind.RushHour:
                spawner.RateMultiplier *= def.rateMultiplier;
                spawner.RecalculateSpawnRate();
                break;
            case EventKind.AdventurerParty:
                if (def.partyCustomer == null) return false;
                spawner.SpawnParty(def.partyCustomer, def.partySize, e.party);
                e.partySize = e.party.Count;
                if (e.partySize == 0) return false;
                e.wavesLeft = Mathf.Max(0, def.waves - 1);
                e.nextWaveIn = def.waveGapSeconds;
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
        if (def.kind != EventKind.PirateRaid) foreach (var c in e.party) EventMarker.Attach(c, def.icon);   // pennant over each member
        active.Add(e);
        if (showDebugLogs) Debug.Log($"[EventManager] {def.name} started ({def.durationSeconds}s)");
        GameSignals.RaiseEventsChanged();
        return true;
    }

    /// <summary>AdventurerParty with waves: the next party walks in, joining the event's roster (serials + pennants).</summary>
    private void SpawnWave(ActiveEvent e)
    {
        e.wavesLeft--;
        e.nextWaveIn = e.def.waveGapSeconds;
        if (spawner == null || e.def.partyCustomer == null) return;
        int before = e.party.Count;
        spawner.SpawnParty(e.def.partyCustomer, e.def.partySize, e.party);
        for (int i = before; i < e.party.Count; i++)
        {
            e.serials.Add(e.party[i].SpawnSerial);
            EventMarker.Attach(e.party[i], e.def.icon);
        }
        e.partySize = e.party.Count;
        if (e.party.Count > before) Announce(e.party[before], "Another party!");
    }

    private void End(ActiveEvent e)
    {
        for (int i = 0; i < e.party.Count; i++)
            if (e.party[i] != null && e.party[i].SpawnSerial == e.serials[i]) EventMarker.Remove(e.party[i]);
        if (e.def.kind == EventKind.RushHour && spawner != null)
        {
            spawner.RateMultiplier /= e.def.rateMultiplier;
            spawner.RecalculateSpawnRate();
        }
        if (e.def.kind == EventKind.Advertise && spawner != null) spawner.BoostWeight(e.customer, 1f / e.def.rateMultiplier);
        if (e.def.kind == EventKind.FeaturedItem && SalesManager.Instance != null && SalesManager.Instance.Featured == e.item) SalesManager.Instance.Featured = null;
        cooldownUntil[e.def] = Time.time + e.def.cooldownSeconds * (e.snubbed ? e.def.snubCooldownMultiplier : 1f);
        if (showDebugLogs) Debug.Log($"[EventManager] {e.def.name} ended");
    }

    // ===== SAVE =====
    public EventsSave ToSave()
    {
        var s = new EventsSave { nextRollIn = nextRollIn };
        foreach (var kv in cooldownUntil) { float left = kv.Value - Time.time; if (left > 0f) s.cooldowns[kv.Key.name] = left; }
        foreach (var e in active)
        {
            if (e.partySize > 0)
            {
                s.cooldowns[e.def.name] = e.def.cooldownSeconds;   // dropped on load: treat as ended now
                for (int i = 0; i < e.party.Count; i++)   // pirates vanish with the raid: what they hold goes back at load
                {
                    var c = e.party[i];
                    if (c == null || c.SpawnSerial != e.serials[i] || !c.gameObject.activeInHierarchy || !c.TryGetComponent<PirateRaider>(out var pr)) continue;
                    foreach (var (item, qty) in pr.CarriedStacks) s.stolen[item.name] = (s.stolen.TryGetValue(item.name, out int have) ? have : 0) + qty;
                }
            }
            else s.active.Add(new EventsSave.Entry { def = e.def.name, remaining = e.remaining, customer = e.customer != null ? e.customer.name : null, item = e.item != null ? e.item.name : null });
        }
        return s;
    }

    /// <summary>Null chunk (older save) keeps the defaults; unknown names are skipped.</summary>
    public void LoadFrom(EventsSave s, Dictionary<string, ItemDef> items)
    {
        if (s == null) return;
        var byName = new Dictionary<string, EventDef>();
        foreach (var d in defs) if (d != null) byName[d.name] = d;
        foreach (var d in ads) byName[d.name] = d;
        var customers = new Dictionary<string, CustomerDef>();
        if (spawner != null) foreach (var c in spawner.AdvertisableDefs()) customers[c.name] = c;
        if (s.nextRollIn > 0f) nextRollIn = s.nextRollIn;
        var inv = Inventory.Instance;
        if (inv != null && s.stolen != null)
            foreach (var kv in s.stolen) if (kv.Value > 0 && items.TryGetValue(kv.Key, out var it)) inv.Add(inv.GetInventoryType(it.itemCategory), new ResourceStack(it, kv.Value, 0));
        foreach (var kv in s.cooldowns) if (byName.TryGetValue(kv.Key, out var d)) cooldownUntil[d] = Time.time + kv.Value;
        foreach (var a in s.active)
        {
            if (!byName.TryGetValue(a.def, out var d) || a.remaining <= 0f) continue;
            customers.TryGetValue(a.customer ?? "", out var c);
            items.TryGetValue(a.item ?? "", out var it);
            if (Fire(d, c, it)) ActiveOf(d).remaining = a.remaining;
        }
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
