using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

// Customer-only spawner: filters candidate customer defs by unlock state before the weighted roll.
// This lets you keep archetype weights static (on each CustomerDef.spawnWeight) while gating by progression.
public class CustomerSpawner : Spawner
{
    [Header("Eligibility (hook these to progression later)")]
    [SerializeField] private bool craftedUnlocked = false;
    [SerializeField] private bool luxuryUnlocked = false;

    [Header("Spawn Config")]
    [SerializeField] private int maxCommoner = 999;
    [SerializeField] private int maxAdventurer = 5;
    [SerializeField] private int maxNoble = 3;

    [Header("Spawn Rate")]
    [Tooltip("Base spawns/min with no stars, no items for sale, and 1 layer.")]
    [SerializeField] private float baseSpawnsPerMinute = 5f;
    [Tooltip("Additional spawns/min per guild star earned.")]
    [SerializeField] private float spawnRatePerStar = 2f;
    [Tooltip("Additional spawns/min per item marked for sale.")]
    [SerializeField] private float spawnRatePerSaleItem = 0.5f;
    [Tooltip("Additional spawns/min per active dungeon layer.")]
    [SerializeField] private float spawnRatePerLayer = 1f;
    [Tooltip("Minimum spawns/min regardless of other factors.")]
    [SerializeField] private float minSpawnsPerMinute = 1f;
    [Tooltip("Maximum spawns/min cap.")]
    [SerializeField] private float maxSpawnsPerMinute = 60f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    private Dictionary<CustomerArcheType, int> customerTypeCount = new();

    private readonly List<EntityDef> eligible = new(32);

    public override void Awake()
    {
        base.Awake();
        customerTypeCount[CustomerArcheType.Commoner] = 0;
        customerTypeCount[CustomerArcheType.Adventurer] = 0;
        customerTypeCount[CustomerArcheType.Noble] = 0;
    }

    void Start()
    {
        RecalculateSpawnRate();
    }

    void OnEnable()
    {
        GameSignals.OnStarEarned           += OnStarEarnedHandler;
        GameSignals.OnLayerUnlocked        += OnLayerUnlockedHandler;
        GameSignals.OnMarkedForSaleChanged += OnMarkedForSaleChangedHandler;
    }

    void OnDisable()
    {
        GameSignals.OnStarEarned           -= OnStarEarnedHandler;
        GameSignals.OnLayerUnlocked        -= OnLayerUnlockedHandler;
        GameSignals.OnMarkedForSaleChanged -= OnMarkedForSaleChangedHandler;
    }

    private void OnStarEarnedHandler(int _)        => RecalculateSpawnRate();
    private void OnLayerUnlockedHandler(int _)     => RecalculateSpawnRate();
    private void OnMarkedForSaleChangedHandler()   => RecalculateSpawnRate();

    private void RecalculateSpawnRate()
    {
        int stars     = ProgressionManager.Instance != null ? ProgressionManager.Instance.GetCurrentStars() : 0;
        int layers    = ProgressionManager.Instance != null ? ProgressionManager.Instance.GetUnlockedLayerCount() : 1;
        int saleItems = SalesManager.Instance != null ? SalesManager.Instance.GetMarkedForSaleCount() : 0;

        float newRate = baseSpawnsPerMinute
            + spawnRatePerStar     * stars
            + spawnRatePerSaleItem * saleItems
            + spawnRatePerLayer    * layers;

        spawnsPerMinute = Mathf.Clamp(newRate, minSpawnsPerMinute, maxSpawnsPerMinute);

        if (showDebugLogs)
            Debug.Log($"[CustomerSpawner] SpawnRate={spawnsPerMinute:F1}/min " +
                      $"(stars={stars}, items={saleItems}, layers={layers})");
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

            // If customer is Adventurer but crafted isn't unlocked, skip
            if (!craftedUnlocked && customerDef.customerArcheType == CustomerArcheType.Adventurer)
                continue;

            // If customer is Noble but luxury isn't unlocked, skip
            if (!luxuryUnlocked && customerDef.customerArcheType == CustomerArcheType.Noble)
                continue;

            // Check spawn limits for all types
            if (customerTypeCount[customerDef.customerArcheType] >= GetMaxForType(customerDef.customerArcheType))
                continue;

            eligible.Add(def);
        }

        if (eligible.Count == 0)
            return;

        EntityDef picked = PickWeighted(eligible);

        Vector3 pos = GetRandomPointAboveSurface(spawnArea);
        if (pos == Vector3.negativeInfinity)
            return;

        EntityBase go = ObjectPoolManager.Instance.SpawnObject(picked, pos, Quaternion.identity).GetComponent<EntityBase>();
        go.Init(picked, layerIndex, this, wander);
        alive.Add(go.gameObject);

        var pickedCustomer = (CustomerDef)picked;
        CustomerArcheType pickedType = pickedCustomer.customerArcheType;
        IncrementGroup(pickedType);
        if (!go.TryGetComponent<CustomerSpawnHandle>(out var handle)) handle = go.gameObject.AddComponent<CustomerSpawnHandle>();
        handle.Bind(this, pickedType);
    }

    private int GetMaxForType(CustomerArcheType type)
    {
        return type switch
        {
            CustomerArcheType.Commoner => maxCommoner,
            CustomerArcheType.Adventurer => maxAdventurer,
            CustomerArcheType.Noble => maxNoble,
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

    public void SetCraftedUnlocked(bool value) => craftedUnlocked = value;
    public void SetLuxuryUnlocked(bool value) => luxuryUnlocked = value;
}
