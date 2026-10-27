using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Random = UnityEngine.Random;

public enum ContractKind { Deliver, Hunt, Patronage }

/// <summary>One contract — an offer or the active one. Plain data, saved as-is in ContractSave.</summary>
public class Contract
{
    public ContractKind kind;
    public string item;            // Deliver: ItemDef asset name
    public int layer;              // Hunt: layer to cull · Deliver: shallowest layer that drops the item (its tier)
    public int target;             // snapshotted when the offer is rolled — dumping stock can't shrink it
    public int progress;           // counters only, from accept time
    public int gold, seals;
    public bool complete;          // target met / goods handed over → claimable
    [JsonIgnore] public ItemDef itemDef;

    [JsonIgnore] public string Title => kind switch
    {
        ContractKind.Deliver => $"Deliver {target} {(itemDef != null ? itemDef.displayName : item)}",
        ContractKind.Hunt => $"Cull {target} mobs at Depth {layer}",
        _ => $"Serve {target} customers",
    };

    [JsonIgnore] public string Client => kind switch
    {
        ContractKind.Deliver => layer >= 3 ? "The garrison is buying in bulk." : "A villager needs supplies.",
        ContractKind.Hunt => "The town watch wants the tunnels thinned.",
        _ => "The Crown wants to see a busy shop.",
    };
}

/// <summary>
/// Guild Contracts + Guild Seals (CONTRACTS_DESIGN.md, phase 1). Three offers rotate on a timer; the player
/// accepts one. Deliver contracts are fulfilled from stock, Hunt/Patronage count signals from accept time.
/// Claim pays gold through the milestone coin shower (banks on arrival) plus flat seals.
/// Persisted by SaveManager (ToSave / LoadFrom).
/// </summary>
public class ContractManager : PersistentSingleton<ContractManager>
{
    public const int OfferSlots = 3;

    [Header("Offers")]
    [Tooltip("Minutes between offer rotations (unaccepted offers are replaced)")]
    [SerializeField] private float rotationMinutes = 10f;
    [Tooltip("rateTerm = measured rate × a random number of minutes in this range")]
    [SerializeField] private Vector2 rateMinutes = new Vector2(5f, 10f);
    [Tooltip("Deliver stockTerm = this share of current stock (random in range) — the late-game bite")]
    [SerializeField] private Vector2 stockBite = new Vector2(0.3f, 0.4f);
    [Tooltip("Target floors while rates are unmeasured (fresh guild)")]
    [SerializeField] private int minDeliver = 5, minHunt = 10, minPatronage = 5;

    [Header("Rewards")]
    [Tooltip("Deliver gold = units × sellPrice × this premium (goods leave stock)")]
    [SerializeField] private float deliverPremium = 1.6f;
    [Tooltip("Hunt/Patronage gold = this share of the passive gold/min over the minutes the work takes (no stock leaves)")]
    [SerializeField] private float counterGoldShare = 0.5f;
    [Tooltip("Gold/min assumed for counter rewards before the passive rate is measured")]
    [SerializeField] private float minGoldPerMin = 30f;
    [SerializeField] private int commonSeals = 10;
    [Tooltip("Contracts on/from this layer or deeper are tiered: 20 seals, +5 per layer beyond, max 30")]
    [SerializeField] private int tierLayer = 3;

    [Header("Seal Shop")]
    [SerializeField] private int favorCost = 50;
    [SerializeField] private int rushCost = 20;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    private readonly Contract[] offers = new Contract[OfferSlots];
    private float rotationLeft;

    public int Seals { get; private set; }
    public int LifetimeSeals { get; private set; }
    public Contract Active { get; private set; }
    public int AcceptedCount { get; private set; }   // lifetime (guild-hall notice board)
    public float RotationLeft => rotationLeft;
    public int FavorCost => favorCost;
    public int RushCost => rushCost;
    public Contract GetOffer(int slot) => offers[slot];

    void Start()
    {
        // Fresh guild; a save's chunk replaces these a frame later (SaveManager.ApplyLate)
        Rotate();
    }

    void OnEnable()
    {
        GameSignals.OnEntityDeath += HandleEntityDeath;
        GameSignals.OnCustomerServed += HandleCustomerServed;
    }

    void OnDisable()
    {
        GameSignals.OnEntityDeath -= HandleEntityDeath;
        GameSignals.OnCustomerServed -= HandleCustomerServed;
    }

    void Update()
    {
        if ((rotationLeft -= Time.deltaTime) <= 0f) Rotate();
    }

    // ===== OFFERS =====

    /// <summary>Replace every unaccepted offer (and empty slot) and restart the rotation timer.</summary>
    [ContextMenu("Debug: Rotate Offers Now")]
    public void Rotate()
    {
        var used = new HashSet<string>();   // no two offers with the same ask (item / hunt layer / patronage)
        for (int i = 0; i < OfferSlots; i++)
        {
            Contract c = null;
            for (int tries = 0; tries < 6; tries++)
                if (used.Add(Key(c = Roll(used)))) break;
            offers[i] = c;
        }
        rotationLeft = rotationMinutes * 60f;
        if (showDebugLogs)
            Debug.Log($"[ContractManager] New offers: {string.Join(" | ", offers.Select(o => $"{o.Title} → {o.gold}g +{o.seals} seals"))}");
    }

    private static string Key(Contract c) =>
        c.kind == ContractKind.Deliver ? "D:" + c.item : c.kind == ContractKind.Hunt ? "H:" + c.layer : "P";

    private Contract Roll(HashSet<string> used)
    {
        int maxLayer = ProgressionManager.Instance != null ? ProgressionManager.Instance.maxUnlockedLayer : 1;
        float minutes = Random.Range(rateMinutes.x, rateMinutes.y);
        float r = Random.value;
        if (r < 0.5f)
        {
            var c = RollDeliver(maxLayer, minutes, used);
            if (c != null) return c;
        }
        return r < 0.75f ? RollHunt(maxLayer, minutes) : RollPatronage(minutes);
    }

    // target = max(rateTerm, stockTerm, floor). rateTerm = item's share of the drop tables × measured loot/min
    // × minutes; stockTerm = 30–40% of current stock. Snapshotted here, at offer time.
    private Contract RollDeliver(int maxLayer, float minutes, HashSet<string> used)
    {
        var weights = SaveManager.DropWeights(1, maxLayer);
        var pool = weights.Keys.Where(i => !used.Contains("D:" + i.name)).ToList();
        if (pool.Count == 0 || Inventory.Instance == null) return null;

        var item = pool[Random.Range(0, pool.Count)];
        float lootPerMin = SaveManager.Instance != null ? SaveManager.Instance.LootPerMin : 0f;
        float itemPerMin = lootPerMin * weights[item] / weights.Values.Sum();
        int stock = Inventory.Instance.Get(item.itemCategory, item);
        int target = Mathf.Max(minDeliver,
            Mathf.CeilToInt(itemPerMin * minutes),
            Mathf.CeilToInt(stock * Random.Range(stockBite.x, stockBite.y)));

        int layer = 1;
        while (layer < maxLayer && !SaveManager.DropWeights(layer, layer).ContainsKey(item)) layer++;

        return new Contract
        {
            kind = ContractKind.Deliver, item = item.name, itemDef = item, layer = layer, target = target,
            gold = Mathf.RoundToInt(target * item.sellPrice * deliverPremium), seals = SealsFor(layer),
        };
    }

    private Contract RollHunt(int maxLayer, float minutes)
    {
        // Only layers someone is working (clicks can always cull layer 1)
        var staffed = FindObjectsByType<AdventurerManager>(FindObjectsInactive.Include)
            .Where(m => m.LayerIndex <= maxLayer && m.GetTotalCount() > 0).Select(m => m.LayerIndex).ToList();
        int layer = staffed.Count > 0 ? staffed[Random.Range(0, staffed.Count)] : 1;
        float rate = SaveManager.Instance != null ? SaveManager.Instance.KillsPerMin(layer) : 0f;
        int target = Mathf.Max(minHunt, Mathf.CeilToInt(rate * minutes));
        return new Contract
        {
            kind = ContractKind.Hunt, layer = layer, target = target,
            gold = CounterGold(target, rate, minutes), seals = SealsFor(layer),
        };
    }

    private Contract RollPatronage(float minutes)
    {
        float rate = SaveManager.Instance != null ? SaveManager.Instance.ServedPerMin : 0f;
        int target = Mathf.Max(minPatronage, Mathf.CeilToInt(rate * minutes));
        return new Contract
        {
            kind = ContractKind.Patronage, target = target,
            gold = CounterGold(target, rate, minutes), seals = commonSeals,
        };
    }

    // Counters cost no stock, so they pay a share of the passive income over the time the work takes
    // (target / rate, capped at 30 min so a floor-sized target on a trickle rate can't balloon).
    private int CounterGold(int target, float rate, float minutes)
    {
        float workMinutes = rate > 0f ? Mathf.Min(target / rate, 30f) : minutes;
        float goldPerMin = Mathf.Max(minGoldPerMin, SaveManager.Instance != null ? SaveManager.Instance.GoldPerMin : 0f);
        return Mathf.Max(1, Mathf.RoundToInt(goldPerMin * workMinutes * counterGoldShare));
    }

    // Flat by tier, never by size (seals must not inherit gold's inflation)
    private int SealsFor(int layer) =>
        layer >= tierLayer ? Mathf.Clamp(20 + 5 * (layer - tierLayer), 20, 30) : commonSeals;

    // ===== ACTIVE CONTRACT =====

    public bool Accept(int slot)
    {
        if (Active != null || slot < 0 || slot >= OfferSlots || offers[slot] == null) return false;
        Active = offers[slot];
        offers[slot] = null;   // refilled on the next rotation
        AcceptedCount++;
        Active.progress = 0;
        Active.complete = false;
        GameSignals.RaiseContractAccepted(Active);
        if (showDebugLogs) Debug.Log($"[ContractManager] Accepted: {Active.Title}");
        return true;
    }

    /// <summary>Progress for display: stock on hand (capped) for Deliver, the counter otherwise. Pure.</summary>
    public int ProgressOf(Contract c)
    {
        if (c == null) return 0;
        if (c.complete) return c.target;
        if (c.kind != ContractKind.Deliver) return c.progress;
        return c.itemDef != null && Inventory.Instance != null
            ? Mathf.Min(c.target, Inventory.Instance.Get(c.itemDef.itemCategory, c.itemDef)) : 0;
    }

    public bool CanFulfil => Active != null && !Active.complete && Active.kind == ContractKind.Deliver
                             && ProgressOf(Active) >= Active.target;

    /// <summary>Hand the goods over from stock — exactly `target` units leave the inventory.</summary>
    public bool Fulfil()
    {
        if (!CanFulfil) return false;
        var inv = Inventory.Instance;
        if (!inv.TryRemove(inv.GetInventoryType(Active.itemDef.itemCategory), Active.itemDef, Active.target)) return false;
        inv.InventoryDebugUi();
        Complete();
        return true;
    }

    private void Complete()
    {
        Active.complete = true;
        Active.progress = Active.target;
        GameSignals.RaiseContractCompleted(Active);
        if (showDebugLogs) Debug.Log($"[ContractManager] Completed: {Active.Title}");
    }

    /// <summary>Pay a completed contract: gold through the coin-shower chain (saved as banked while it flies,
    /// banks per coin on arrival — never dropped) + its seals. uiCoinFlight as in TryClaimMilestone.</summary>
    public bool Claim(Vector3 coinOrigin, Func<int, Action, bool> uiCoinFlight = null)
    {
        if (Active == null || !Active.complete) return false;
        var c = Active;
        Active = null;
        ProgressionManager.PayMilestoneGold(c.gold, coinOrigin, uiCoinFlight,
            SaveManager.Instance != null ? SaveManager.Instance.InFlightBank(c.gold) : null);
        GrantSeals(c.seals);
        if (showDebugLogs) Debug.Log($"[ContractManager] Claimed: {c.Title} → {c.gold}g +{c.seals} seals");
        return true;
    }

    private void HandleEntityDeath(GameObject dead)
    {
        if (Active == null || Active.complete || Active.kind != ContractKind.Hunt || dead == null) return;
        if (!dead.TryGetComponent<MobAgent>(out var mob) || mob.layerIndex != Active.layer) return;
        if (++Active.progress >= Active.target) Complete();
    }

    private void HandleCustomerServed()
    {
        if (Active == null || Active.complete || Active.kind != ContractKind.Patronage) return;
        if (++Active.progress >= Active.target) Complete();
    }

    // ===== SEALS =====

    public void GrantSeals(int amount)
    {
        if (amount <= 0) return;
        Seals += amount;
        LifetimeSeals += amount;
        GameSignals.RaiseSealsChanged(Seals);
        if (showDebugLogs) Debug.Log($"[ContractManager] +{amount} seals (balance {Seals})");
    }

    public bool TrySpendSeals(int amount)
    {
        if (amount < 0 || Seals < amount) return false;
        Seals -= amount;
        GameSignals.RaiseSealsChanged(Seals);
        return true;
    }

    /// <summary>Seal shop: a guaranteed high-rank adventurer candidate joins the hiring pool.</summary>
    public bool BuyRecruitmentFavor()
    {
        var hire = FindAnyObjectByType<HireController>();
        if (hire == null || !hire.CanGrantFavor || !TrySpendSeals(favorCost)) return false;
        hire.GrantRecruitmentFavor();
        return true;
    }

    /// <summary>Seal shop: every hiring candidate pool restocks now.</summary>
    public bool BuyRushOrder()
    {
        var hire = FindAnyObjectByType<HireController>();
        if (hire == null || !TrySpendSeals(rushCost)) return false;
        hire.RefreshAllPools();
        return true;
    }

    [ContextMenu("Debug: +100 Seals")]
    private void DebugGrantSeals() => GrantSeals(100);

    // ===== SAVE / LOAD =====

    public ContractSave ToSave() => new ContractSave
    {
        seals = Seals,
        lifetimeSeals = LifetimeSeals,
        rotationLeft = rotationLeft,
        offers = offers.ToList(),
        active = Active,
        accepted = AcceptedCount,
    };

    /// <summary>Cold-boot restore (SaveManager.ApplyLate, after stock). Re-raises the usual signals for the UI.</summary>
    public void LoadFrom(ContractSave s, Dictionary<string, ItemDef> items)
    {
        Seals = s.seals;
        LifetimeSeals = s.lifetimeSeals;
        Active = Resolve(s.active, items);
        AcceptedCount = Mathf.Max(s.accepted, Active != null ? 1 : 0);   // saves before the counter: an active one counts
        if (s.offers.Count == 0)
            Rotate();   // save predates contracts
        else
        {
            for (int i = 0; i < OfferSlots; i++) offers[i] = i < s.offers.Count ? Resolve(s.offers[i], items) : null;
            rotationLeft = s.rotationLeft;
        }
        GameSignals.RaiseSealsChanged(Seals);
        if (Active != null) GameSignals.RaiseContractAccepted(Active);
    }

    private static Contract Resolve(Contract c, Dictionary<string, ItemDef> items)
    {
        if (c == null || c.kind != ContractKind.Deliver) return c;
        if (c.item != null && items.TryGetValue(c.item, out c.itemDef)) return c;
        Debug.LogError($"[ContractManager] Unknown item '{c.item}' in saved contract (asset renamed?) — dropped");
        return null;
    }
}
