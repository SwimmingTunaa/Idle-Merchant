using UnityEngine;
using System.Collections.Generic;

public enum HireRole
{
    Adventurer = 0,
    Porter = 1
}

/// <summary>
/// Manages hiring candidates for a single (Role, Layer, UnitDef).
/// Generates 4 candidates on initialization, refreshes all after timer expires.
/// </summary>
public class CandidatePool
{
    public HireRole Role { get; private set; }
    public int LayerIndex { get; private set; }
    public EntityDef EntityDef { get; private set; }

    private readonly List<HiringCandidate> candidates = new List<HiringCandidate>();
    private float refreshTimer;
    private float refreshInterval = 300f;
    private float traitChance = 0.7f;
    private readonly System.Func<int> ownedCount; // guild-wide units of this role, for cost scaling

    private const int BASE_POOL_SIZE = 5;
    private const float COST_SCALE_PER_OWNED = 1.25f;

    public CandidatePool(
        HireRole role,
        int layerIndex,
        EntityDef entityDef,
        float refreshIntervalSeconds = 300f,
        float traitChance = 0.7f,
        System.Func<int> ownedCount = null)
    {
        Role = role;
        LayerIndex = layerIndex;
        EntityDef = entityDef;
        refreshInterval = refreshIntervalSeconds;
        this.traitChance = traitChance;
        this.ownedCount = ownedCount;

        RegeneratePool();
        refreshTimer = refreshInterval;
    }

    /// <summary>
    /// Update refresh timer. Call from HireController.Update().
    /// Returns true when the pool regenerated this tick.
    /// </summary>
    public bool Update(float deltaTime)
    {
        refreshTimer -= deltaTime;

        if (refreshTimer <= 0f)
        {
            RegeneratePool();
            refreshTimer = refreshInterval;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Number of available candidates without allocating a list.
    /// </summary>
    public int CandidateCount => candidates.Count;

    /// <summary>
    /// Cheapest baked hire cost in the pool (int.MaxValue when empty). No allocation.
    /// </summary>
    public int MinHireCost
    {
        get
        {
            int min = int.MaxValue;
            foreach (var c in candidates)
                if (c.hireCost < min) min = c.hireCost;
            return min;
        }
    }

    /// <summary>
    /// Get all available candidates (copy).
    /// </summary>
    public List<HiringCandidate> GetCandidates()
    {
        return new List<HiringCandidate>(candidates);
    }

    /// <summary>
    /// Consume a candidate (remove from pool).
    /// </summary>
    public bool ConsumeCandidate(HiringCandidate candidate)
    {
        return candidates.Remove(candidate);
    }

    /// <summary>
    /// Get time until next refresh.
    /// </summary>
    public float GetTimeUntilRefresh()
    {
        return refreshTimer;
    }

    /// <summary>
    /// Force immediate pool regeneration.
    /// </summary>
    public void ForceRefresh()
    {
        RegeneratePool();
        refreshTimer = refreshInterval;
    }

    private void RegeneratePool()
    {
        candidates.Clear();

        if (EntityDef == null)
        {
            Debug.LogError($"[CandidatePool {Role} Layer {LayerIndex}] EntityDef is null!");
            return;
        }

        // Cost scales x1.25 per unit of this role already owned; baked at generation (pool keeps pre-hire prices until refresh)
        int owned = ownedCount != null ? ownedCount() : 0;
        int baseCost = Mathf.RoundToInt(EntityDef.hireCost * Mathf.Pow(COST_SCALE_PER_OWNED, owned));

        var newCandidates = HiringCandidateGenerator.GenerateCandidates(
            EntityDef,
            BASE_POOL_SIZE,
            baseCost,
            traitChance
        );

        candidates.AddRange(newCandidates);
    }
}
