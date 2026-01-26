using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a global hiring roster from all unlocked layer pools.
/// Phase 1: Simple concatenation, no layer bias weighting.
/// Phase 2: Add weighted sampling to favor lower layers.
/// </summary>
public static class HireRoster
{
    /// <summary>
    /// Build roster from all provided candidate pools.
    /// Returns flat list of all available candidates.
    /// </summary>
    public static List<HiringCandidate> BuildRoster(List<CandidatePool> pools)
    {
        var roster = new List<HiringCandidate>();
        
        if (pools == null || pools.Count == 0)
        {
            Debug.LogWarning("[HireRoster] No pools provided, roster is empty");
            return roster;
        }
        
        // Phase 1: Simple concatenation
        foreach (var pool in pools)
        {
            var candidates = pool.GetCandidates();
            roster.AddRange(candidates);
        }
        
        // TODO Phase 2: Implement layer bias weighting
        // Example:
        // - Layer 1: 70% weight
        // - Layer 2: 20% weight  
        // - Layer 3: 10% weight
        // Use weighted random sampling to select N candidates from pools
        
        Debug.Log($"[HireRoster] Built roster with {roster.Count} total candidates");
        return roster;
    }
    
    /// <summary>
    /// Get time until next refresh from pools.
    /// Returns the shortest refresh time across all pools.
    /// </summary>
    public static float GetNextRefreshTime(List<CandidatePool> pools)
    {
        if (pools == null || pools.Count == 0)
            return 0f;
        
        float minTime = float.MaxValue;
        
        foreach (var pool in pools)
        {
            float poolTime = pool.GetTimeUntilRefresh();
            if (poolTime < minTime)
                minTime = poolTime;
        }
        
        return minTime == float.MaxValue ? 0f : minTime;
    }
}
