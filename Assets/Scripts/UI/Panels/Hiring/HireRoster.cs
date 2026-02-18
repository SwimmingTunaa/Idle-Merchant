using System.Collections.Generic;
using UnityEngine;

// Builds a hiring roster from unlocked layer pools using weighted random selection.
// Higher layers have lower weights, meaning rarer candidates.
// Weights are normalized across only the layers that have candidates available.
public static class HireRoster
{
    // Build roster using weighted random selection from candidate pools.
    // layerWeights: weight per layer index (0 = layer 1, 1 = layer 2, etc.)
    // maxCount: maximum candidates to include in the roster.
    public static List<HiringCandidate> BuildRoster(List<CandidatePool> pools, float[] layerWeights, int maxCount)
    {
        var roster = new List<HiringCandidate>();

        if (pools == null || pools.Count == 0)
        {
            Debug.LogWarning("[HireRoster] No pools provided, roster is empty");
            return roster;
        }

        // Group all candidates by layer index
        var candidatesByLayer = new Dictionary<int, List<HiringCandidate>>();
        foreach (var pool in pools)
        {
            var candidates = pool.GetCandidates();
            if (candidates.Count == 0) continue;

            if (!candidatesByLayer.ContainsKey(pool.LayerIndex))
                candidatesByLayer[pool.LayerIndex] = new List<HiringCandidate>();

            candidatesByLayer[pool.LayerIndex].AddRange(candidates);
        }

        if (candidatesByLayer.Count == 0)
            return roster;

        // Build normalized weights for layers that actually have candidates
        var activeLayers = new List<int>();
        var activeWeights = new List<float>();
        float totalWeight = 0f;

        foreach (var kvp in candidatesByLayer)
        {
            int weightIndex = kvp.Key - 1; // Layer 1 = index 0
            float weight = (layerWeights != null && weightIndex >= 0 && weightIndex < layerWeights.Length)
                ? layerWeights[weightIndex]
                : 1f; // Fallback weight if layer exceeds array

            activeLayers.Add(kvp.Key);
            activeWeights.Add(weight);
            totalWeight += weight;
        }

        // Avoid division by zero
        if (totalWeight <= 0f)
        {
            Debug.LogWarning("[HireRoster] All layer weights are zero, returning empty roster");
            return roster;
        }

        // Weighted random selection without duplicates
        var picked = new HashSet<HiringCandidate>();
        int maxAttempts = maxCount * 10; // Safety valve to prevent infinite loops
        int attempts = 0;

        while (roster.Count < maxCount + 1 && attempts < maxAttempts)
        {
            attempts++;

            // Pick a layer based on weights
            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;
            int selectedLayerIndex = 0;

            for (int i = 0; i < activeLayers.Count; i++)
            {
                cumulative += activeWeights[i];
                if (roll <= cumulative)
                {
                    selectedLayerIndex = i;
                    break;
                }
            }

            int selectedLayer = activeLayers[selectedLayerIndex];
            var layerCandidates = candidatesByLayer[selectedLayer];

            // Pick random candidate from that layer
            if (layerCandidates.Count == 0) continue;

            int candidateIndex = Random.Range(0, layerCandidates.Count);
            var candidate = layerCandidates[candidateIndex];

            if (picked.Contains(candidate)) continue;

            picked.Add(candidate);
            roster.Add(candidate);
        }

        Debug.Log($"[HireRoster] Built roster with {roster.Count} candidates from {activeLayers.Count} layers");
        return roster;
    }

    // Get time until next refresh from pools.
    // Returns the shortest refresh time across all pools.
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