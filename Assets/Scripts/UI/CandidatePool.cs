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

    private const int BASE_POOL_SIZE = 4;

    public CandidatePool(
        HireRole role,
        int layerIndex,
        EntityDef entityDef,
        float refreshIntervalSeconds = 300f,
        float traitChance = 0.7f)
    {
        Role = role;
        LayerIndex = layerIndex;
        EntityDef = entityDef;
        refreshInterval = refreshIntervalSeconds;
        this.traitChance = traitChance;

        RegeneratePool();
        refreshTimer = refreshInterval;
    }

    /// <summary>
    /// Update refresh timer. Call from HireController.Update().
    /// </summary>
    public void Update(float deltaTime)
    {
        refreshTimer -= deltaTime;

        if (refreshTimer <= 0f)
        {
            RegeneratePool();
            refreshTimer = refreshInterval;
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

        var newCandidates = HiringCandidateGenerator.GenerateCandidates(
            EntityDef,
            BASE_POOL_SIZE,
            EntityDef.hireCost,
            traitChance
        );

        candidates.AddRange(newCandidates);
    }
}
