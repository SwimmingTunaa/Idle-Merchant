using UnityEngine;
using System.Collections.Generic;
/// <summary>
/// Manages adventurers for a single dungeon layer.
/// Inherits from UnitManager to get spawning, tracking, and limit logic.
/// Only implements adventurer-specific spawn initialization.
/// </summary>
public class AdventurerManager : UnitManager<AdventurerAgent>
{
    // Adventurers hire from Resources/ClassCatalog (HireController), not per-manager hireableDefs
    protected override bool UsesHireableDefs => false;

    // ===== SPAWNING (Adventurer-Specific) =====

    /// <summary>
    /// Spawn and initialize an adventurer.
    /// Called by base class HireUnit().
    /// </summary>
    protected override AdventurerAgent SpawnUnit(EntityDef def)
    {
        if (operationArea == null)
        {
            Debug.LogError("[AdventurerManager] Cannot spawn - operationArea not set!");
            return null;
        }

        AdventurerDef adventurerDef = def as AdventurerDef;
        if (adventurerDef == null)
        {
            Debug.LogError($"[AdventurerManager] {def.displayName} is not an AdventurerDef!");
            return null;
        }

        // Spawn using object pool
        AdventurerAgent agent = ObjectPoolManager.Instance.SpawnObject<AdventurerAgent>(
            adventurerDef,
            Spawner.GetRandomPointAboveSurface(operationArea),
            Quaternion.identity
        );

        if (agent == null)
        {
            Debug.LogError($"[AdventurerManager] ObjectPoolManager failed to spawn {def.displayName}!");
            return null;
        }

        // Initialize adventurer
        agent.Init(adventurerDef, LayerIndex, spawner: null, operationArea);

        //Add random traits to adventurer
        //TraitManager.Instance.AssignRandomAdventurerTraitTier1(agent);

        return agent;
    }

    public override bool HireUnit(HiringCandidate candidate)
    {
        if (!base.HireUnit(candidate))
            return false;

        GameSignals.RaiseUnitHired(candidate.entityDef, HireRole.Adventurer);
        return true;
    }

    // ===== LEGACY WRAPPERS (for backward compatibility) =====

    /// <summary>
    /// Wrapper for HireUnit() to maintain old API.
    /// Call HireUnit() directly in new code.
    /// </summary>
    public bool HireAdventurer(EntityDef def)
    {
        return HireUnit(def);
    }

    /// <summary>
    /// Hire from HiringCandidate.
    /// Applies identity and traits from candidate data.
    /// </summary>
    

    protected override AdventurerAgent SpawnUnitWithCandidate(HiringCandidate candidate)
    {
        if (operationArea == null)
        {
            Debug.LogError("[AdventurerManager] Cannot spawn - operationArea not set!");
            return null;
        }

        AdventurerDef advDef = candidate.entityDef as AdventurerDef;
        if (advDef == null)
        {
            Debug.LogError($"[AdventurerManager] {candidate.entityDef.displayName} is not an AdventurerDef!");
            return null;
        }

        // Spawn using object pool
        AdventurerAgent agent = ObjectPoolManager.Instance.SpawnObject<AdventurerAgent>(
            advDef,
            Spawner.GetRandomPointAboveSurface(operationArea),
            Quaternion.identity
        );

        if (agent == null)
        {
            Debug.LogError($"[AdventurerManager] ObjectPoolManager failed to spawn {advDef.displayName}!");
            return null;
        }

       

        // Initialize adventurer
        agent.Init(advDef, LayerIndex, spawner: null, operationArea);

         //apply appearance from candidate
        if (agent.appearanceManager != null && advDef.useModularCharacter)
        {
            agent.appearanceManager.SetAppearanceIndices(candidate.appearanceIndices);
            agent.appearanceManager.ApplyAppearance();
        }
        
        // Apply identity — set on both EntityBase and IdentityComponent so all systems agree
        if (candidate.identity != null)
        {
            agent.ApplyIdentity(candidate.identity);

            var identityComponent = agent.GetComponent<IdentityComponent>();
            if (identityComponent != null)
                identityComponent.ApplyIdentity(candidate.identity);
        }
        
        // Apply candidate's traits
        if (agent.traitComponent != null)
        {
            // Clear any default traits that might have been assigned
            agent.traitComponent.ClearTraits();
            
            // Add candidate's specific traits
            if (candidate.traits != null)
            {
                foreach (var trait in candidate.traits)
                {
                    agent.traitComponent.AddTrait(trait);
                }
                
                // Apply trait modifiers to stats
                agent.traitComponent.ApplyTraitsToStats(agent.Stats);
            }
        }

        // Class hires join at their tier; Recruitment Favor Novices at startRank (save restore re-applies its own progress after)
        int joinRank = Mathf.Max(candidate.startRank, advDef.rank);
        if (joinRank > 1) agent.RestoreProgress(joinRank, 1, 0f);

        return agent;
    }

    /// <summary>
    /// Spawn unit without applying random traits.
    /// Used by HireUnit(HiringCandidate) to apply candidate's traits instead.
    /// </summary>
    private AdventurerAgent SpawnUnitWithoutTraits(EntityDef def)
    {
        if (operationArea == null)
        {
            Debug.LogError("[AdventurerManager] Cannot spawn - operationArea not set!");
            return null;
        }

        AdventurerDef adventurerDef = def as AdventurerDef;
        if (adventurerDef == null)
        {
            Debug.LogError($"[AdventurerManager] {def.displayName} is not an AdventurerDef!");
            return null;
        }

        // Spawn using object pool
        AdventurerAgent agent = ObjectPoolManager.Instance.SpawnObject<AdventurerAgent>(
            adventurerDef,
            Spawner.GetRandomPointAboveSurface(operationArea),
            Quaternion.identity
        );

        if (agent == null)
        {
            Debug.LogError($"[AdventurerManager] ObjectPoolManager failed to spawn {def.displayName}!");
            return null;
        }

        // Initialize adventurer (without adding random traits)
        agent.Init(adventurerDef, LayerIndex, spawner: null, operationArea);
        
        // DON'T call TraitManager.AssignRandomAdventurerTraitTier1(agent);
        // Candidate will provide traits
        
        return agent;
    }

    /// <summary>
    /// Wrapper for RemoveUnit() to maintain old API.
    /// Call RemoveUnit() directly in new code.
    /// </summary>
    public void RemoveAdventurer(AdventurerAgent agent)
    {
        RemoveUnit(agent);
    }

    /// <summary>
    /// Get all adventurers (wrapper for GetAllUnits).
    /// </summary>
    public System.Collections.Generic.List<AdventurerAgent> GetAllAdventurers()
    {
        return GetAllUnits();
    }

    /// <summary>No-alloc, pure promote scan (Units bookmark badge): adds this layer's promote-ready
    /// count to <paramref name="ready"/>; sets <paramref name="affordable"/> if any of them costs ≤ gold.</summary>
    public void CountPromoteReady(int gold, ref int ready, ref bool affordable)
    {
        foreach (var list in spawnedByType.Values)
            foreach (var a in list)
            {
                if (a == null || !a.CanPromote) continue;
                ready++;
                if (a.PromoteCost <= gold) affordable = true;
            }
    }

    // ===== MOVE / SWAP BETWEEN DEPTHS (Roster) =====
    // A move is the save round trip: same def, identity, traits, looks, rank, level and XP respawned on the
    // target depth (HP and position reset — it arrives at that floor's spawn). The new unit spawns before the
    // old one is removed, so nothing is lost mid-way. Callers get the new instances back.

    /// <summary>Why <paramref name="agent"/> can't move to <paramref name="to"/>, or null if it can.</summary>
    public static string MoveBlocker(AdventurerAgent agent, AdventurerManager to)
    {
        if (agent == null || to == null) return "—";
        if (to.LayerIndex == agent.layerIndex) return "Here";
        if (ProgressionManager.Instance != null && to.LayerIndex > ProgressionManager.Instance.maxUnlockedLayer) return "Locked";
        if (!agent.IsAlive) return "Down";
        if (to.GetTotalCount() >= to.MaxUnits) return "Full";
        return null;
    }

    public static AdventurerAgent Move(AdventurerAgent agent, AdventurerManager to)
    {
        var from = ManagerFor(agent);
        if (from == null || MoveBlocker(agent, to) != null) return null;
        var moved = Respawn(agent, to);
        if (moved != null) from.RemoveUnit(agent);
        return moved;
    }

    /// <summary>Why two adventurers can't trade depths, or null if they can (capacity is unchanged by a swap).</summary>
    public static string SwapBlocker(AdventurerAgent a, AdventurerAgent b)
    {
        if (a == null || b == null || a == b || a.layerIndex == b.layerIndex) return "—";
        if (!a.IsAlive || !b.IsAlive) return "Down";
        return null;
    }

    public static bool Swap(AdventurerAgent a, AdventurerAgent b, out AdventurerAgent newA, out AdventurerAgent newB)
    {
        newA = newB = null;
        var ma = ManagerFor(a); var mb = ManagerFor(b);
        if (ma == null || mb == null || SwapBlocker(a, b) != null) return false;
        newA = Respawn(a, mb);
        newB = Respawn(b, ma);
        if (newA != null) ma.RemoveUnit(a);
        if (newB != null) mb.RemoveUnit(b);
        return newA != null && newB != null;
    }

    public static AdventurerManager ManagerFor(AdventurerAgent agent) => agent == null ? null : ManagerForLayer(agent.layerIndex);

    public static AdventurerManager ManagerForLayer(int layer)
    {
        foreach (var m in FindObjectsByType<AdventurerManager>())
            if (m.LayerIndex == layer) return m;
        return null;
    }

    /// <summary>
    /// Promote, then follow the fit: a unit that fitted its depth (depth == old rank) moves to the depth of its new
    /// rank when that depth is unlocked and has room (otherwise it stays, bored — RankFit shows it). A unit placed
    /// off its rank on purpose (training deeper, parked shallower) stays put. Returns false if the promotion failed;
    /// <paramref name="result"/> is the unit to keep using — the respawned instance after a move.
    /// </summary>
    public static bool PromoteAndFollow(AdventurerAgent agent, ClassLine line, out AdventurerAgent result, out bool moved)
    {
        result = agent; moved = false;
        if (agent == null) return false;
        bool fitted = agent.layerIndex == agent.CurrentRank;
        if (!agent.Promote(line)) return false;
        if (!fitted) return true;
        var to = ManagerForLayer(agent.CurrentRank);
        if (to == null || MoveBlocker(agent, to) != null) return true;
        var next = Move(agent, to);
        if (next == null) return true;
        result = next; moved = true;
        if (DamageNumberManager.Instance != null)
            DamageNumberManager.Instance.ShowText($"Off to Depth {to.LayerIndex}!", next.transform, new Color(0.45f, 0.85f, 0.3f));
        return true;
    }

    private static AdventurerAgent Respawn(AdventurerAgent a, AdventurerManager to)
    {
        var ic = a.GetComponent<IdentityComponent>();
        var candidate = new HiringCandidate
        {
            entityDef = a.def,
            identity = ic != null && ic.Identity != null ? ic.Identity : a.Identity,
            traits = a.traitComponent != null ? a.traitComponent.GetTraitsForSave() : System.Array.Empty<TraitInstance>(),
            appearanceIndices = a.appearanceManager != null ? a.appearanceManager.GetCurrentIndices() : default,
        };
        var moved = to.SpawnFromData(candidate);
        if (moved != null)
        {
            moved.RestoreProgress(a.CurrentRank, a.CurrentLevel, a.CurrentXP);
            a.despawnQuietly = true;   // the caller removes the old instance next: it left, it didn't die
        }
        return moved;
    }
}