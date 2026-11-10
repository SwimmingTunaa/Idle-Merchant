using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

// Customer-only spawner: filters candidate customer defs by guild-star unlock before the weighted roll.
// This lets you keep archetype weights static (on each CustomerDef.spawnWeight) while gating by progression.
public class CustomerSpawner : Spawner
{
    [Header("Archetype Unlocks (guild stars)")]
    [SerializeField] private int adventurerUnlockStar = 2;
    [SerializeField] private int nobleUnlockStar = 3;

    [Header("Spawn Config")]
    [SerializeField] private int maxCommoner = 999;
    [SerializeField] private int maxAdventurer = 5;
    [SerializeField] private int maxNoble = 3;
    [Tooltip("Royals in the shop at once; a visit (lead + party) fills up to this.")]
    [SerializeField] private int maxRoyalty = 4;

    [Header("Royal Visits")]
    [Tooltip("A picked royal brings company: chance the visit is a pair, else solo; the rest of the time the whole family comes.")]
    [SerializeField] private float royalPairChance = 0.5f;
    [SerializeField] private float royalSoloChance = 0.3f;

    [Header("Spawn Rate")]
    [Tooltip("Base spawns/min with no stars, no items for sale, and 1 layer.")]
    [SerializeField] private float baseSpawnsPerMinute = 5f;
    [Tooltip("Additional spawns/min per guild star earned.")]
    [SerializeField] private float spawnRatePerStar = 2f;
    [Tooltip("Cheap walk-ins (budget.y ≤ cheapBudget: Villager, Herbalist) have their spawn weight divided by (stars − 1) from 3★ (×0.5 at 3★, ×0.33 at 4★, ×0.25 at 5★), so the counter's ~20 serves a minute go to adventurers and nobles as the guild grows.")]
    [SerializeField] private int cheapBudget = 1000;
    [Tooltip("Additional spawns/min per item marked for sale.")]
    [SerializeField] private float spawnRatePerSaleItem = 0.5f;
    [Tooltip("Additional spawns/min per active dungeon layer.")]
    [SerializeField] private float spawnRatePerLayer = 1f;
    [Tooltip("Spawn-rate bonus per owned Shelves rank (UpgradeType.Shelves): displayed goods draw walk-ins. 0.15 = +15% / +30% / +45%.")]
    [SerializeField] private float spawnRatePerShelfRank = 0.15f;
    [Tooltip("Minimum spawns/min regardless of other factors.")]
    [SerializeField] private float minSpawnsPerMinute = 1f;
    [Tooltip("Maximum spawns/min cap.")]
    [SerializeField] private float maxSpawnsPerMinute = 60f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    /// <summary>Spawn-rate factor applied by events (Rush Hour); 1 = normal. Call RecalculateSpawnRate after changing.</summary>
    public float RateMultiplier { get; set; } = 1f;

    // Advertise events: per-def spawn-weight multipliers (× into spawnWeight at the roll; never an override, so the crowd stays mixed)
    private readonly Dictionary<CustomerDef, float> weightBoost = new();
    public void BoostWeight(CustomerDef def, float mult)
    {
        if (def == null || mult <= 0f) return;
        float m = (weightBoost.TryGetValue(def, out float cur) ? cur : 1f) * mult;
        if (Mathf.Abs(m - 1f) < 0.001f) weightBoost.Remove(def); else weightBoost[def] = m;
    }
    private float Boost(EntityDef def) => def is CustomerDef c ? (weightBoost.TryGetValue(c, out float m) ? m : 1f) * WordBoost(c) * StarScale(c) : 1f;

    // Word of mouth: selling to a def's customers draws more of them. Each sale adds wordPerSale (capped at 1), the whole
    // thing fades to 0 over wordDecaySeconds; weight × (1 + word × (wordMaxBoost − 1)). A share shift, never an override:
    // ×2 on Bulk Traders at 3★ is 17% → 28% of walk-ins. Self-limiting — once the stock they want is gone they walk out and the word fades.
    [Header("Word of mouth")]
    [Tooltip("Word of mouth gained per sale to a def's customers (0–1 scale).")]
    [SerializeField] private float wordPerSale = 0.15f;
    [Tooltip("Spawn-weight multiplier at full word of mouth.")]
    [SerializeField] private float wordMaxBoost = 2f;
    [Tooltip("Seconds for full word of mouth to fade to nothing.")]
    [SerializeField] private float wordDecaySeconds = 180f;
    private readonly Dictionary<CustomerDef, (float word, float at)> wordOfMouth = new();
    public static CustomerSpawner Instance { get; private set; }

    public void RecordSale(CustomerDef def)
    {
        if (def == null) return;
        wordOfMouth[def] = (Mathf.Min(1f, WordOf(def) + wordPerSale), Time.time);
    }
    /// <summary>Current word of mouth for a def, 0–1 (decayed lazily, nothing to tick).</summary>
    public float WordOf(CustomerDef def) =>
        wordOfMouth.TryGetValue(def, out var w) ? Mathf.Max(0f, w.word - (Time.time - w.at) / Mathf.Max(1f, wordDecaySeconds)) : 0f;
    private float WordBoost(CustomerDef def) => 1f + WordOf(def) * (wordMaxBoost - 1f);

    // Cheap commoners thin out as stars rise (the queue is the bottleneck from 3★; a 400-gold sale costs the same serve as a 4,000-gold one)
    private static float StarScale(CustomerDef c, int cheapBudget)
    {
        if (c.customerArcheType != CustomerArcheType.Commoner || c.budget.y > cheapBudget) return 1f;
        int stars = ProgressionManager.Instance != null ? ProgressionManager.Instance.GetCurrentStars() : 0;
        return 1f / Mathf.Max(1, stars - 1);
    }
    private float StarScale(CustomerDef c) => StarScale(c, cheapBudget);

    /// <summary>Walk-in defs the player can advertise to: candidates past their star gate (royals never).</summary>
    public List<CustomerDef> AdvertisableDefs()
    {
        var list = new List<CustomerDef>();
        foreach (var def in candidates) if (def is CustomerDef c && Unlocked(c)) list.Add(c);
        return list;
    }

    private bool Unlocked(CustomerDef c)
    {
        int stars = ProgressionManager.Instance != null ? ProgressionManager.Instance.GetCurrentStars() : 0;
        if (c.customerArcheType == CustomerArcheType.Adventurer && stars < adventurerUnlockStar) return false;
        if (c.customerArcheType == CustomerArcheType.Noble && stars < nobleUnlockStar) return false;
        return c.customerArcheType != CustomerArcheType.Royalty;   // royals only arrive as the Royal Visit event (EventManager → SpawnRoyalVisit)
    }

    private Dictionary<CustomerArcheType, int> customerTypeCount = new();

    private readonly List<EntityDef> eligible = new(32);

    public override void Awake()
    {
        base.Awake();
        Instance = this;
        customerTypeCount[CustomerArcheType.Commoner] = 0;
        customerTypeCount[CustomerArcheType.Adventurer] = 0;
        customerTypeCount[CustomerArcheType.Noble] = 0;
        customerTypeCount[CustomerArcheType.Royalty] = 0;
    }

    void Start()
    {
        RecalculateSpawnRate();
    }

    void OnEnable()
    {
        GameSignals.OnStarEarned           += OnStarEarnedHandler;
        GameSignals.OnLayerUnlocked        += OnLayerUnlockedHandler;
        ProgressionManager.OnUpgradePurchased += OnUpgradePurchasedHandler;
        GameSignals.OnMarkedForSaleChanged += OnMarkedForSaleChangedHandler;
    }

    void OnDisable()
    {
        GameSignals.OnStarEarned           -= OnStarEarnedHandler;
        GameSignals.OnLayerUnlocked        -= OnLayerUnlockedHandler;
        ProgressionManager.OnUpgradePurchased -= OnUpgradePurchasedHandler;
        GameSignals.OnMarkedForSaleChanged -= OnMarkedForSaleChangedHandler;
    }

    private void OnStarEarnedHandler(int _)        => RecalculateSpawnRate();
    private void OnLayerUnlockedHandler(int _)     => RecalculateSpawnRate();
    private void OnMarkedForSaleChangedHandler()   => RecalculateSpawnRate();
    private void OnUpgradePurchasedHandler(GuildUpgradeDef u) { if (u != null && u.upgradeType == UpgradeType.Shelves) RecalculateSpawnRate(); }

    public void RecalculateSpawnRate()
    {
        int stars     = ProgressionManager.Instance != null ? ProgressionManager.Instance.GetCurrentStars() : 0;
        int layers    = ProgressionManager.Instance != null ? ProgressionManager.Instance.GetUnlockedLayerCount() : 1;
        int saleItems = SalesManager.Instance != null ? SalesManager.Instance.GetMarkedForSaleCount() : 0;
        int shelves   = ProgressionManager.Instance != null ? ProgressionManager.Instance.CountUpgrades(UpgradeType.Shelves) : 0;

        float newRate = baseSpawnsPerMinute
            + spawnRatePerStar     * stars
            + spawnRatePerSaleItem * saleItems
            + spawnRatePerLayer    * layers;

        spawnsPerMinute = Mathf.Clamp(newRate * (1f + spawnRatePerShelfRank * shelves) * RateMultiplier, minSpawnsPerMinute, maxSpawnsPerMinute);

        if (showDebugLogs)
            Debug.Log($"[CustomerSpawner] SpawnRate={spawnsPerMinute:F1}/min " +
                      $"(stars={stars}, items={saleItems}, layers={layers}, shelves={shelves})");
    }

    public override void TrySpawn()
    {
        if (alive.Count >= maxAlive || candidates.Count == 0 || spawnArea == null)
            return;

        eligible.Clear();
        for (int i = 0; i < candidates.Count; i++)
        {
            EntityDef def = candidates[i];
            if (def == null) continue;

            if (def is not CustomerDef customerDef)
                continue;

            if (!Unlocked(customerDef)) continue;

            // Check spawn limits for all types
            if (customerTypeCount[customerDef.customerArcheType] >= GetMaxForType(customerDef.customerArcheType))
                continue;

            eligible.Add(def);
        }

        if (eligible.Count == 0)
            return;

        SpawnOne((CustomerDef)PickBoosted(eligible));
    }

    // Spawner.PickWeighted with the Advertise boosts folded in
    private EntityDef PickBoosted(List<EntityDef> list)
    {
        float total = 0f;
        foreach (var e in list) total += Mathf.Max(0.001f, e.spawnWeight * Boost(e));
        float r = Random.value * total, run = 0f;
        foreach (var e in list)
        {
            run += Mathf.Max(0.001f, e.spawnWeight * Boost(e));
            if (r <= run) return e;
        }
        return list[list.Count - 1];
    }

    /// <summary>Event: n of one def arrive together (ignores maxAlive and the type cap). Spawned agents go in <paramref name="into"/>.</summary>
    public void SpawnParty(CustomerDef def, int n, List<CustomerAgent> into)
    {
        for (int i = 0; i < n; i++)
        {
            var unit = SpawnOne(def);
            if (unit is CustomerAgent c) into.Add(c);
        }
    }

    /// <summary>Event: a random royal leads, with company (SpawnRoyalParty). Empty if no royal def is a candidate.</summary>
    public void SpawnRoyalVisit(List<CustomerAgent> into)
    {
        royals.Clear();
        foreach (var def in candidates) if (def is CustomerDef c && c.customerArcheType == CustomerArcheType.Royalty) royals.Add(c);
        if (royals.Count == 0) return;
        var lead = royals[Random.Range(0, royals.Count)];
        var leadUnit = SpawnOne(lead);
        if (leadUnit == null) return;
        if (leadUnit is CustomerAgent lc) into.Add(lc);
        SpawnRoyalParty(lead, leadUnit, into);
    }

    private EntityBase SpawnOne(CustomerDef def)
    {
        Vector3 pos = GetRandomPointAboveSurface(spawnArea);
        if (pos == Vector3.negativeInfinity)
            return null;

        EntityBase go = ObjectPoolManager.Instance.SpawnObject(def, pos, Quaternion.identity).GetComponent<EntityBase>();
        go.Init(def, layerIndex, this, wander);
        alive.Add(go.gameObject);

        IncrementGroup(def.customerArcheType);
        if (!go.TryGetComponent<CustomerSpawnHandle>(out var handle)) handle = go.gameObject.AddComponent<CustomerSpawnHandle>();
        handle.Bind(this, def.customerArcheType);
        return go;
    }

    // A royal never comes alone for long: solo / a pair / the whole family, drawn from the other royal defs
    // (distinct looks), ignoring maxAlive so the party arrives together. Announced over the lead.
    private readonly List<CustomerDef> royals = new(4);
    private void SpawnRoyalParty(CustomerDef lead, EntityBase leadUnit, List<CustomerAgent> into)
    {
        royals.Clear();
        foreach (var def in candidates)
            if (def is CustomerDef c && c != lead && c.customerArcheType == CustomerArcheType.Royalty) royals.Add(c);
        for (int i = royals.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (royals[i], royals[j]) = (royals[j], royals[i]); }

        float roll = Random.value;
        int companions = roll < royalSoloChance ? 0 : roll < royalSoloChance + royalPairChance ? 1 : royals.Count;
        companions = Mathf.Min(companions, royals.Count, maxRoyalty - customerTypeCount[CustomerArcheType.Royalty]);
        for (int i = 0; i < companions; i++) if (SpawnOne(royals[i]) is CustomerAgent c) into.Add(c);

        if (DamageNumberManager.Instance != null)
            DamageNumberManager.Instance.ShowText(companions == royals.Count && royals.Count > 0 ? "The Royal Family arrives!" : $"The {lead.displayName} arrives!",
                leadUnit.transform, new Color(1f, 0.84f, 0.3f));
    }

    private int GetMaxForType(CustomerArcheType type)
    {
        return type switch
        {
            CustomerArcheType.Commoner => maxCommoner,
            CustomerArcheType.Adventurer => maxAdventurer,
            CustomerArcheType.Noble => maxNoble,
            CustomerArcheType.Royalty => maxRoyalty,
            _ => 0
        };
    }

    private void IncrementGroup(CustomerArcheType type)
    {
        customerTypeCount[type] +=1;
    }

    private void DecrementGroup(CustomerArcheType type)
    {
        customerTypeCount[type] -=1;
    }

     public void NotifyCustomerDespawned(CustomerArcheType type)
    {
        DecrementGroup(type);
    }
}
