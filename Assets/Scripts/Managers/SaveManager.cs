using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Single local save slot (see SAVE_DESIGN.md). Asks each manager for its chunk — no per-component
/// ISaveable. Collects on the main thread, serializes + writes atomically on a background task.
/// Loads once per cold boot: progression/clicker/settings before other Starts, then (next frame)
/// stock, recipes and the roster, then raises the usual changed-signals so the UI refreshes.
/// </summary>
[DefaultExecutionOrder(-100)] // Start before activators/UI so they build from the loaded state
public class SaveManager : PersistentSingleton<SaveManager>
{
    public const int CurrentVersion = 1;
    private const float AutosaveInterval = 30f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // Cold-boot flag: scene reloads within a session never re-load. Reset per play session even with
    // domain reload disabled.
    private static bool bootLoadDone;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetBootFlag() => bootLoadDone = false;

    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
        Formatting = Formatting.Indented,
        Converters = { new StringEnumConverter() },
    };

    public static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

    private SaveData pending;      // read in Awake, applied in Start + next frame
    private bool restoring;        // half-restored state is never written
    private bool savingDisabled;   // unreadable/future-version file on disk (never overwrite it), or wiped this session
    private Task writeTask = Task.CompletedTask;
    private float autosaveTimer;
    private double playTime;

    // ===== Offline earnings (§3) =====
    public const float OfflineEfficiency = 0.5f;
    public static readonly TimeSpan OfflineCap = TimeSpan.FromHours(8), WelcomeThreshold = TimeSpan.FromMinutes(2);
    private const float RateWindow = 600f;   // seconds of game time the rolling rates average over (~10 min)
    private const float RateTick = 10f;

    // Bias-corrected EMA: rateWeight ramps 0→1 so a fresh session reads its real rate, not one dragged toward 0.
    private float rateTimer, tickGold, tickLoot, emaGold, emaLoot, rateWeight;
    // lastPlayedUtc is saved as "now − uncollected away time": while the welcome-back award is unclaimed it stays
    // pinned behind the away period (live play excluded), so quitting on the panel re-offers it next boot.
    private TimeSpan pendingAway;
    private int goldInFlight;   // collected offline gold still flying to the counter — saved as banked

    public float GoldPerMin => rateWeight > 0f ? emaGold / rateWeight : 0f;
    public float LootPerMin => rateWeight > 0f ? emaLoot / rateWeight : 0f;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;
        GameSignals.OnItemSold += OnItemSold;
        if (bootLoadDone) return;
        bootLoadDone = true;
        pending = ReadSave(out savingDisabled);
    }

    void OnDestroy()
    {
        if (Instance == this) GameSignals.OnItemSold -= OnItemSold;
    }

    void Start()
    {
        if (pending == null) return;
        playTime = pending.meta.totalPlayTime;
        emaGold = pending.meta.goldPerMin;
        emaLoot = pending.meta.lootPerMin;
        rateWeight = emaGold + emaLoot > 0f ? 1f : 0f;
        ApplyEarly(pending);
        StartCoroutine(ApplyLate(pending));
        pending = null;
    }

    void Update()
    {
        TickRates(Time.deltaTime);
        playTime += Time.unscaledDeltaTime;
        if ((autosaveTimer += Time.unscaledDeltaTime) < AutosaveInterval) return;
        autosaveTimer = 0f;
        Save();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) Save();
    }

    void OnApplicationQuit()
    {
        Save();
        writeTask.Wait();
    }

    // ===== SAVE =====

    /// <summary>Collects state now; the write runs on a background task, one at a time, in order.</summary>
    public void Save()
    {
        if (Instance != this || savingDisabled || restoring) return;
        var data = Collect();
        if (data == null) return;

        string path = SavePath;
        writeTask = writeTask.ContinueWith(_ => Write(data, path), TaskScheduler.Default);
        if (showDebugLogs)
            Debug.Log($"[SaveManager] Saving: {data.economy.gold}g, {data.economy.stock.Count} item types, {data.roster.Count} units");
    }

    /// <summary>Stop all writes for this session (wipe, debugger reload). Next cold boot starts normally.</summary>
    public void SuspendSaving()
    {
        savingDisabled = true;
        writeTask.Wait();
    }

    /// <summary>Deletes save.json + .bak (+ stray .tmp). The running session stops saving so quit can't rewrite it.</summary>
    public static void WipeSaveFiles()
    {
        if (Instance != null) Instance.SuspendSaving();
        foreach (var f in new[] { SavePath, SavePath + ".bak", SavePath + ".tmp" })
            if (File.Exists(f)) File.Delete(f);
        Debug.Log("[SaveManager] Save wiped — next cold boot is a fresh start");
    }

    private SaveData Collect()
    {
        var inv = Inventory.Instance;
        var pm = ProgressionManager.Instance;
        var advManagers = FindObjectsByType<AdventurerManager>(FindObjectsInactive.Include);
        var porterManagers = FindObjectsByType<PorterManager>(FindObjectsInactive.Include);
        // Not a gameplay scene (e.g. main menu): saving here would write an empty roster over the real one
        if (inv == null || pm == null || advManagers.Length + porterManagers.Length == 0) return null;

        var d = new SaveData();
        d.meta.version = CurrentVersion;
        d.meta.lastPlayedUtc = DateTime.UtcNow - pendingAway;
        d.meta.totalPlayTime = playTime;
        d.meta.goldPerMin = GoldPerMin;
        d.meta.lootPerMin = LootPerMin;

        d.economy.gold = inv.Gold + goldInFlight;
        foreach (var row in inv.SnapshotAll()) Bank(d, row.item, row.qty);
        // §4: loot on the ground and in porters' arms banks into the saved stock — items are never dropped
        foreach (var loot in FindObjectsByType<Loot>()) Bank(d, loot.GetItemDef(), loot.GetQuantity());

        d.progression = pm.ToSave();
        d.guild.ownedUpgrades = pm.OwnedUpgradeNames();

        var clicker = FindAnyObjectByType<ClickerManager>();
        if (clicker != null)
        {
            d.clicker.clickDamage = clicker.ClickDamageLevel;
            d.clicker.pickupRadius = clicker.PickupRadiusLevel;
            d.clicker.damageAOE = clicker.AOELevel;
            d.clicker.autoClick = clicker.AutoClickLevel;
            d.clicker.autoCollect = clicker.AutoCollectLevel;
        }

        foreach (var m in advManagers)
            foreach (var a in m.GetAllUnits())
                if (a != null) d.roster.Add(ToUnitSave(a, HireRole.Adventurer, m.LayerIndex));
        foreach (var m in porterManagers)
            foreach (var p in m.GetAllUnits())
            {
                if (p == null) continue;
                d.roster.Add(ToUnitSave(p, HireRole.Porter, m.LayerIndex));
                foreach (var s in p.CarriedLoot) Bank(d, s.itemDef, s.qty);
            }

        if (GameManager.Instance != null) d.settings.gameSpeed = GameManager.Instance.GameSpeed;
        var sfx = SfxManager.Instance;
        if (sfx != null)
        {
            d.settings.masterVolume = sfx.MasterVolume;
            d.settings.sfxVolume = sfx.SfxVolume;
            d.settings.musicVolume = sfx.MusicVolume;
        }
        return d;
    }

    private static void Bank(SaveData d, ItemDef item, int qty)
    {
        if (item == null || qty <= 0) return;
        d.economy.stock.TryGetValue(item.name, out int q);
        d.economy.stock[item.name] = q + qty;
    }

    private static UnitSave ToUnitSave(EntityBase u, HireRole role, int layer)
    {
        var ic = u.GetComponent<IdentityComponent>();
        var s = new UnitSave
        {
            role = role,
            def = u.def != null ? u.def.name : null,
            layer = layer,
            identity = ic != null && ic.Identity != null ? ic.Identity : u.Identity,
            traits = u.traitComponent != null ? u.traitComponent.GetTraitsForSave() : Array.Empty<TraitInstance>(),
            appearance = u.appearanceManager != null ? u.appearanceManager.GetCurrentIndices() : default,
        };
        if (u is AdventurerAgent a)
        {
            s.rank = a.CurrentRank;
            s.level = a.CurrentLevel;
            s.xp = a.CurrentXP;
        }
        return s;
    }

    // Background thread: no Unity API besides Debug.Log. Atomic: tmp → Replace, previous kept as .bak.
    private static void Write(SaveData data, string path)
    {
        try
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(data, JsonSettings));
            if (File.Exists(path)) File.Replace(tmp, path, path + ".bak");
            else File.Move(tmp, path);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Save FAILED — previous save left intact: {e}");
        }
    }

    // ===== LOAD =====

    private static SaveData ReadSave(out bool lockSaves)
    {
        lockSaves = false;
        string path = SavePath, bak = path + ".bak";
        if (!File.Exists(path) && !File.Exists(bak)) return null; // new game

        var data = TryRead(path);
        if (data == null)
        {
            data = TryRead(bak);
            if (data != null)
                Debug.LogError($"[SaveManager] save.json UNREADABLE — loaded the backup ({bak}). Progress since the last good save may be missing.");
        }
        if (data == null)
        {
            lockSaves = true;
            Debug.LogError($"[SaveManager] save.json AND save.json.bak UNREADABLE — starting fresh WITHOUT saving so the files can be recovered from {Path.GetDirectoryName(path)}");
            return null;
        }
        if (data.meta.version > CurrentVersion)
        {
            lockSaves = true;
            Debug.LogError($"[SaveManager] Save is version {data.meta.version}, this build knows {CurrentVersion}. Refusing to load or overwrite it.");
            return null;
        }
        // ponytail: v1 is the only version; migrations become a switch on data.meta.version right here
        return data;
    }

    private static SaveData TryRead(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var d = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(path), JsonSettings);
            if (d?.meta == null) throw new JsonException("empty file or missing meta");
            return d;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] CORRUPT SAVE {path}: {e.Message}");
            return null;
        }
    }

    // Before any other Start: activators, tracker, Guild page and Cursor page build from this state.
    private static void ApplyEarly(SaveData d)
    {
        if (ProgressionManager.Instance != null)
            ProgressionManager.Instance.LoadFrom(d.progression, d.guild.ownedUpgrades);

        var clicker = FindAnyObjectByType<ClickerManager>();
        if (clicker != null)
            clicker.RestoreLevels(d.clicker.clickDamage, d.clicker.pickupRadius, d.clicker.damageAOE,
                d.clicker.autoClick, d.clicker.autoCollect);

        if (GameManager.Instance != null) GameManager.Instance.SetTimeScale(d.settings.gameSpeed);
        var sfx = SfxManager.Instance;
        if (sfx != null)
        {
            sfx.MasterVolume = d.settings.masterVolume;
            sfx.SfxVolume = d.settings.sfxVolume;
            sfx.MusicVolume = d.settings.musicVolume;
        }
    }

    // One frame later: every Start has run, so saved layers are active, CraftingManager has its inventory
    // and OnItemAdded listeners (SalesManager auto-mark, LootPile, Inventory UI) are subscribed.
    private IEnumerator ApplyLate(SaveData d)
    {
        restoring = true;
        yield return null;

        var inv = Inventory.Instance;
        var items = ItemsByName();
        inv.SetGoldFromSave(d.economy.gold);
        foreach (var kv in d.economy.stock)
        {
            if (items.TryGetValue(kv.Key, out var item))
                inv.Add(inv.GetInventoryType(item.itemCategory), new ResourceStack(item, kv.Value, 0));
            else
                Debug.LogError($"[SaveManager] Unknown item '{kv.Key}' x{kv.Value} in save (asset renamed?) — not restored");
        }

        var pm = ProgressionManager.Instance;
        if (pm != null) pm.EnableUnlockedRecipes();

        SpawnRoster(d.roster);

        // Same signals live play raises, so camera bounds, hire pools and the Cursor page catch up
        if (pm != null)
            for (int layer = 2; layer <= pm.maxUnlockedLayer; layer++) GameSignals.RaiseLayerUnlocked(layer);
        var clicker = FindAnyObjectByType<ClickerManager>();
        if (clicker != null) GameSignals.RaiseClickUpgradePurchased(clicker.ClickDamageLevel);

        restoring = false;
        Debug.Log($"[SaveManager] Loaded save: {d.economy.gold}g, {d.economy.stock.Count} item types, " +
                  $"{d.roster.Count} units, {d.progression.stars}★ (saved {d.meta.lastPlayedUtc:u})");
        OfferOfflineEarnings(d);
    }

    // ===== OFFLINE EARNINGS =====

    /// <summary>Porter deposits are the loot pipeline that keeps running "while away" (click pickups don't count).</summary>
    public void RecordPorterDelivery(int qty) => tickLoot += qty;

    // Customer sales only (SalesManager + CounterService) — never click gold, milestone claims, hiring refunds or debug.
    private void OnItemSold(ResourceStack sale) => tickGold += sale.sellValue;

    // Game time, so pause doesn't dilute the rate and a 3x session doesn't claim 3x offline (offline runs at 1x).
    private void TickRates(float dt)
    {
        // Rates freeze while an award is unclaimed: quitting on the panel re-derives the exact same award
        if (pendingAway > TimeSpan.Zero) { rateTimer = tickGold = tickLoot = 0f; return; }
        if ((rateTimer += dt) < RateTick) return;
        // §3 honesty guard: only a working guild (adventurers AND porters on the books) measures passive income
        bool passive = HasUnits<AdventurerManager>() && HasUnits<PorterManager>();
        float a = Mathf.Min(1f, rateTimer / RateWindow), perMin = 60f / rateTimer;
        emaGold += a * ((passive ? tickGold * perMin : 0f) - emaGold);
        emaLoot += a * ((passive ? tickLoot * perMin : 0f) - emaLoot);
        rateWeight += a * (1f - rateWeight);
        rateTimer = tickGold = tickLoot = 0f;
    }

    private static bool HasUnits<T>() where T : MonoBehaviour, IUnitManager =>
        FindObjectsByType<T>(FindObjectsInactive.Include).Any(m => m.GetTotalCount() > 0);

    private void OfferOfflineEarnings(SaveData d)
    {
        var away = DateTime.UtcNow - d.meta.lastPlayedUtc.ToUniversalTime();
        if (away < TimeSpan.Zero) away = TimeSpan.Zero;   // clock went backwards
        if (away > OfflineCap) away = OfflineCap;
        if (away <= WelcomeThreshold) return;

        int maxLayer = ProgressionManager.Instance != null ? ProgressionManager.Instance.maxUnlockedLayer : 1;
        var (gold, items) = ComputeOfflineAward(d.meta.goldPerMin, d.meta.lootPerMin, away.TotalMinutes, maxLayer);
        if (gold <= 0 && items.Count == 0) return;

        pendingAway = away;   // nothing banks, and lastPlayedUtc stays behind the away period, until Collect
        Debug.Log($"[SaveManager] Offline {away.TotalMinutes:F1} min @ {d.meta.goldPerMin:F1}g/min, {d.meta.lootPerMin:F2} loot/min " +
                  $"→ {gold}g, {items.Sum(i => i.qty)} items ({string.Join(", ", items.Select(i => $"{i.item.name} x{i.qty}"))})");
        if (!WelcomeBackPanel.Show(away, gold, items, source => CollectOffline(gold, items, source)))
            CollectOffline(gold, items, null);   // no panel in this HUD — bank directly, never drop it
    }

    private void CollectOffline(int gold, List<(ItemDef item, int qty)> items, VisualElement source)
    {
        pendingAway = TimeSpan.Zero;   // lastPlayedUtc advances from the next save on
        var inv = Inventory.Instance;
        foreach (var (item, qty) in items)
            inv.Add(inv.GetInventoryType(item.itemCategory), new ResourceStack(item, qty, 0));

        var cam = Camera.main;
        Vector3 from = cam != null ? cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0f)) : Vector3.zero;
        from.z = 0f;
        if (LootPile.Instance != null)
            for (int i = 0; i < Mathf.Min(6, items.Count * 2); i++) LootPile.Instance.FlyIn(items[i % items.Count].item, from);

        // Gold banks per coin on landing; until then it's saved as banked, so a quit mid-shower loses nothing
        goldInFlight = gold;
        var tracker = FindAnyObjectByType<MilestoneTracker>();
        ProgressionManager.PayMilestoneGold(gold, from,
            source == null || tracker == null ? null : (count, land) => tracker.LaunchCoins(source, count, land),
            amount => { goldInFlight -= amount; inv.AddGold(amount); });
        Debug.Log($"[SaveManager] Collected offline earnings: {gold}g, {items.Sum(i => i.qty)} items");
    }

    /// <summary>gold = goldPerMin × min × 50%; items = lootPerMin × min × 50%, split across what the unlocked
    /// layers' mobs drop (spawn weight × drop chance × avg stack), largest remainder so it's deterministic —
    /// quitting on the welcome panel re-derives the same split.</summary>
    public static (int gold, List<(ItemDef item, int qty)> items) ComputeOfflineAward(
        float goldPerMin, float lootPerMin, double awayMinutes, int maxLayer)
    {
        int gold = (int)(goldPerMin * awayMinutes * OfflineEfficiency);
        int count = (int)(lootPerMin * awayMinutes * OfflineEfficiency);
        var items = new List<(ItemDef item, int qty)>();

        // ponytail: every spawner weighs the same regardless of its spawn rate/maxAlive; weight by those if layers diverge
        var weights = new Dictionary<ItemDef, float>();
        foreach (var s in FindObjectsByType<Spawner>(FindObjectsInactive.Include))
        {
            if (s.layerIndex < 1 || s.layerIndex > maxLayer) continue;
            float total = s.candidates.Where(c => c != null).Sum(c => Mathf.Max(0.001f, c.spawnWeight));
            foreach (var c in s.candidates)
            {
                if (!(c is MobDef mob) || mob.loot == null) continue;
                float perKill = Mathf.Max(0.001f, mob.spawnWeight) / total * (mob.lootDropAmount.x + mob.lootDropAmount.y) * 0.5f;
                foreach (var item in mob.loot)
                {
                    if (item == null) continue;
                    weights.TryGetValue(item, out float w);
                    weights[item] = w + perKill * item.chance;
                }
            }
        }

        float sum = weights.Values.Sum();
        if (count <= 0 || sum <= 0f) return (gold, items);
        var shares = weights.Select(kv => (item: kv.Key, exact: count * kv.Value / sum))
            .OrderByDescending(x => x.exact - Math.Floor(x.exact)).ThenBy(x => x.item.name).ToList();
        int remainder = count - shares.Sum(x => (int)x.exact);
        for (int i = 0; i < shares.Count; i++)
        {
            int qty = (int)shares[i].exact + (i < remainder ? 1 : 0);
            if (qty > 0) items.Add((shares[i].item, qty));
        }
        items.Sort((x, y) => y.qty != x.qty ? y.qty.CompareTo(x.qty) : string.CompareOrdinal(x.item.name, y.item.name));
        return (gold, items);
    }

    /// <summary>Debug: save now, then rewrite the file's lastPlayedUtc to `minutes` ago. Saving stops for this
    /// session so the quit-save can't undo it — restart play mode to load it.</summary>
    public static void SimulateAway(double minutes)
    {
        if (Instance != null)
        {
            Instance.Save();
            Instance.SuspendSaving();
        }
        var data = TryRead(SavePath);
        if (data == null) { Debug.LogError("[SaveManager] SimulateAway: no readable save"); return; }
        data.meta.lastPlayedUtc = DateTime.UtcNow - TimeSpan.FromMinutes(minutes);
        Write(data, SavePath);
        Debug.Log($"[SaveManager] lastPlayedUtc set {minutes} min ago (rates in file: {data.meta.goldPerMin:F1}g/min, {data.meta.lootPerMin:F2} loot/min)");
    }

    /// <summary>ItemDef by asset name. Items live in Resources/Items and in Prefabs/Loot (mob loot tables), so
    /// this loads the Resources ones and then takes every loaded ItemDef.</summary>
    // ponytail: non-Resources items resolve only while the scene references them (spawner mob loot);
    // move all ItemDefs under Resources or into a registry asset if one ever stops resolving.
    public static Dictionary<string, ItemDef> ItemsByName()
    {
        Resources.LoadAll<ItemDef>("Items");
        var items = new Dictionary<string, ItemDef>();
        foreach (var i in Resources.FindObjectsOfTypeAll<ItemDef>()) items[i.name] = i;
        return items;
    }

    private static void SpawnRoster(List<UnitSave> roster)
    {
        var defs = Resources.FindObjectsOfTypeAll<EntityDef>();
        var advManagers = FindObjectsByType<AdventurerManager>(FindObjectsInactive.Include);
        var porterManagers = FindObjectsByType<PorterManager>(FindObjectsInactive.Include);

        foreach (var u in roster)
        {
            var candidate = new HiringCandidate
            {
                entityDef = defs.FirstOrDefault(x => x.name == u.def),
                identity = u.identity,
                traits = u.traits,
                appearanceIndices = u.appearance,
            };

            EntityBase spawned = null;
            if (candidate.entityDef != null && u.role == HireRole.Adventurer)
            {
                var m = advManagers.FirstOrDefault(x => x.LayerIndex == u.layer);
                var a = m != null ? m.SpawnFromData(candidate) : null;
                if (a != null) a.RestoreProgress(u.rank, u.level, u.xp);
                spawned = a;
            }
            else if (candidate.entityDef != null)
            {
                var m = porterManagers.FirstOrDefault(x => x.LayerIndex == u.layer);
                spawned = m != null ? m.SpawnFromData(candidate) : null;
            }

            if (spawned == null)
                Debug.LogError($"[SaveManager] Could not respawn {u.role} '{u.identity?.DisplayName}' ({u.def}) on layer {u.layer}");
        }
    }
}

// ===== SAVE DTO (Newtonsoft; missing fields keep these defaults) =====

public class SaveData
{
    public MetaSave meta = new MetaSave();
    public EconomySave economy = new EconomySave();
    public ProgressionSave progression = new ProgressionSave();
    public ClickerSave clicker = new ClickerSave();
    public GuildSave guild = new GuildSave();
    public List<UnitSave> roster = new List<UnitSave>();
    public SettingsSave settings = new SettingsSave();
}

public class MetaSave
{
    public int version;
    public DateTime lastPlayedUtc;
    public double totalPlayTime;   // seconds, all sessions
    public float goldPerMin;       // phase 2 (offline earnings)
    public float lootPerMin;       // phase 2
}

public class EconomySave
{
    public int gold;
    public Dictionary<string, int> stock = new Dictionary<string, int>(); // ItemDef asset name → qty
}

public class ProgressionSave
{
    public int stars = 1;
    public int maxUnlockedLayer = 1;
    public int goldEarned, mobsKilled, unitsHired, adventurersHired, portersHired, lootCollected,
               itemsCrafted, craftedItemsSold, customersServed, adventurersRankedUp;
    public List<string> craftedItemTypes = new List<string>();   // ItemDef names (CraftedItemsSold tracking)
    public List<string> reachedMilestones = new List<string>();  // StarMilestoneDef names
    public List<string> claimedMilestones = new List<string>();
}

public class ClickerSave
{
    public int clickDamage, pickupRadius, damageAOE, autoClick, autoCollect;
}

public class GuildSave
{
    public List<string> ownedUpgrades = new List<string>();      // GuildUpgradeDef names
}

public class UnitSave
{
    public HireRole role;
    public string def;             // EntityDef asset name
    public int layer;
    public Identity identity;
    public TraitInstance[] traits;
    public CharacterAppearanceIndices appearance;
    public int rank = 1, level = 1; // adventurers only
    public float xp;
}

public class SettingsSave
{
    public float gameSpeed = 1f;
    public float masterVolume = 0.8f, sfxVolume = 1f, musicVolume = 1f;   // SfxManager volumes
}
