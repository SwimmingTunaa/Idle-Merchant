using UnityEngine;
using System.Collections.Generic;
/// <summary>
/// Manages porters for a single dungeon layer.
/// Inherits from UnitManager to get spawning, tracking, and limit logic.
/// Only implements porter-specific spawn initialization and transport setup.
/// </summary>
public class PorterManager : UnitManager<PorterAgent>
{
    [Header("Porter-Specific Config")]
    [Tooltip("Transport point porters use to reach shop (Ladder/Elevator/Teleporter)")]
    public TransportPoint transport;
    
    [Tooltip("Where porters deposit loot at shop")]
    public Transform depositPoint;

    [Header("Porter Lodging")]
    [Tooltip("Extra maxUnits per owned Porter Lodging rank (UpgradeType.PorterLodging), on every PorterManager")]
    [SerializeField] private int capacityPerLodging = 2;

    private int baseMaxUnits;
    private TransportPoint[] route;   // ladder chain up to the hall, built on first spawn

    // This depth's ladder, then each depth above's, ending with depth 1's (which lets out in the hall).
    // A missing depth ends the chain early; porters then bank at that ladder's top.
    private TransportPoint[] Route()
    {
        if (route != null) return route;
        var managers = FindObjectsByType<PorterManager>(FindObjectsInactive.Include);
        var chain = new List<TransportPoint>();
        for (int depth = LayerIndex; depth >= 1; depth--)
        {
            var m = System.Array.Find(managers, x => x.LayerIndex == depth);
            if (m == null || m.transport == null) break;
            chain.Add(m.transport);
        }
        return route = chain.ToArray();
    }

    protected override void Awake()
    {
        base.Awake();
        baseMaxUnits = MaxUnits;
        ProgressionManager.OnUpgradePurchased += OnUpgradePurchased;
    }

    // SaveManager restores owned upgrades before other Starts; purchases arrive through the signal
    protected override void Start()
    {
        base.Start();
        ApplyLodging();
        ApplyTransport();
    }

    void OnDestroy() => ProgressionManager.OnUpgradePurchased -= OnUpgradePurchased;

    private void OnUpgradePurchased(GuildUpgradeDef upgrade)
    {
        if (upgrade == null) return;
        if (upgrade.upgradeType == UpgradeType.PorterLodging) ApplyLodging();
        if (upgrade.upgradeType is UpgradeType.Elevator or UpgradeType.Teleporter) ApplyTransport();
    }

    private static bool TeleportOwned =>
        ProgressionManager.Instance != null && ProgressionManager.Instance.HasUpgrade(UpgradeType.Teleporter);

    // PLACEHOLDER waystones (Teleport Circle): purple-tinted standing stone (Decoratives_47) on each Ladder's "Waystone"
    // child (ProgressionActivator on the Ladder) and Guild/Shop/Decor/waystone_hall (DecorSlot pop-in) until the real art lands.
    // Elevator I–V convert ladders top-down (rank N = depths 1..N; the ranks chain, so count == highest rank).
    // Teleport Circle reroutes every porter; one mid-trip finishes it first (PorterAgent.SetTransport).
    private void ApplyTransport()
    {
        if (ProgressionManager.Instance == null) return;
        if (transport != null)
            transport.SetMode(LayerIndex <= ProgressionManager.Instance.CountUpgrades(UpgradeType.Elevator)
                ? TransportType.Elevator : TransportType.Ladder);
        if (depositPoint == null) return;
        foreach (var p in GetAllUnits())
            if (p != null) p.SetTransport(Route(), depositPoint.position, TeleportOwned);
    }

    private void ApplyLodging()
    {
        if (ProgressionManager.Instance != null)
            MaxUnits = baseMaxUnits + capacityPerLodging * ProgressionManager.Instance.CountUpgrades(UpgradeType.PorterLodging);
    }

    // ===== VALIDATION =====

    protected override void ValidateSetup()
    {
        // Don't call base - porters work on any layer, skip layer validation
        if (operationArea == null)
        {
            if (showDebugLogs)
                Debug.LogError($"[PorterManager Layer {LayerIndex}] operationArea is not assigned!");
        }

        if (transport == null)
        {
            if (showDebugLogs)
                Debug.LogError($"[PorterManager Layer {LayerIndex}] transport is not assigned!");
        }
        
        if (depositPoint == null)
        {
            if (showDebugLogs)
                Debug.LogError($"[PorterManager Layer {LayerIndex}] depositPoint is not assigned!");
        }
    }

    // ===== SPAWNING (Porter-Specific) =====

    /// <summary>
    /// Spawn and initialize a porter.
    /// Called by base class HireUnit().
    /// </summary>
    protected override PorterAgent SpawnUnit(EntityDef def)
    {
        if (operationArea == null)
        {
            Debug.LogError("[PorterManager] Cannot spawn - operationArea not set!");
            return null;
        }

        if (transport == null || depositPoint == null)
        {
            Debug.LogError("[PorterManager] Cannot spawn - transport or depositPoint not set!");
            return null;
        }

        PorterDef porterDef = def as PorterDef;
        if (porterDef == null)
        {
            Debug.LogError($"[PorterManager] {def.displayName} is not a PorterDef!");
            return null;
        }

        // Spawn using object pool
        PorterAgent porter = ObjectPoolManager.Instance.SpawnObject<PorterAgent>(
            porterDef,
            Spawner.GetRandomPointAboveSurface(operationArea),
            Quaternion.identity
        );

        if (porter == null)
        {
            Debug.LogError($"[PorterManager] ObjectPoolManager failed to spawn {def.displayName}!");
            return null;
        }

        // Initialize porter (spawner is null for hired porters)
        porter.Init(porterDef, LayerIndex, spawner: null, operationArea);
        
        // Set transport and deposit point
        porter.SetTransport(Route(), depositPoint.position, TeleportOwned);

        return porter;
    }

    public override bool HireUnit(HiringCandidate candidate)
    {
        if (!base.HireUnit(candidate))
            return false;

        GameSignals.RaiseUnitHired(candidate.entityDef, HireRole.Porter);
        return true;
    }

    // ===== LEGACY WRAPPERS (for backward compatibility) =====

    /// <summary>
    /// Wrapper for HireUnit() to maintain old API.
    /// Call HireUnit() directly in new code.
    /// </summary>
    public bool HirePorter(PorterDef def)
    {
        return HireUnit(def);
    }

    /// <summary>
    /// Hire from HiringCandidate.
    /// Applies identity and traits from candidate data.
    /// </summary>
    

    /// <summary>
    /// Spawn unit without applying random traits.
    /// Used by HireUnit(HiringCandidate) to apply candidate's traits instead.
    /// </summary>
    private PorterAgent SpawnUnitWithoutTraits(EntityDef def)
    {
        if (operationArea == null)
        {
            Debug.LogError("[PorterManager] Cannot spawn - operationArea not set!");
            return null;
        }

        PorterDef porterDef = def as PorterDef;
        if (porterDef == null)
        {
            Debug.LogError($"[PorterManager] {def.displayName} is not a PorterDef!");
            return null;
        }

        // Spawn using object pool
        PorterAgent agent = ObjectPoolManager.Instance.SpawnObject<PorterAgent>(
            porterDef,
            Spawner.GetRandomPointAboveSurface(operationArea),
            Quaternion.identity
        );

        if (agent == null)
        {
            Debug.LogError($"[PorterManager] ObjectPoolManager failed to spawn {def.displayName}!");
            return null;
        }

        // Initialize porter (without adding random traits)
        agent.Init(porterDef, LayerIndex, spawner: null, operationArea);
        
        // DON'T call random trait assignment here
        // Candidate will provide traits
        
        return agent;
    }

    /// <summary>
    /// Spawn porter from HiringCandidate with identity and traits.
    /// </summary>
    protected override PorterAgent SpawnUnitWithCandidate(HiringCandidate candidate)
    {
        if (operationArea == null)
        {
            Debug.LogError("[PorterManager] Cannot spawn - operationArea not set!");
            return null;
        }

        PorterDef porterDef = candidate.entityDef as PorterDef;
        if (porterDef == null)
        {
            Debug.LogError($"[PorterManager] {candidate.entityDef.displayName} is not a PorterDef!");
            return null;
        }

        // Spawn using object pool
        PorterAgent agent = ObjectPoolManager.Instance.SpawnObject<PorterAgent>(
            porterDef,
            Spawner.GetRandomPointAboveSurface(operationArea),
            Quaternion.identity
        );

        if (agent == null)
        {
            Debug.LogError($"[PorterManager] ObjectPoolManager failed to spawn {porterDef.displayName}!");
            return null;
        }

        // Initialize porter
        agent.Init(porterDef, LayerIndex, spawner: null, operationArea);
        
        // Apply identity — set on both EntityBase and IdentityComponent so all systems (and the save) agree
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
          
        // Set transport and deposit point
        agent.SetTransport(Route(), depositPoint.position, TeleportOwned);
        return agent;
    }

    /// <summary>
    /// Wrapper for RemoveUnit() to maintain old API.
    /// Call RemoveUnit() directly in new code.
    /// </summary>
    public void RemovePorter(PorterAgent agent)
    {
        RemoveUnit(agent);
    }

    /// <summary>
    /// Get all active porters (wrapper for GetAllUnits).
    /// </summary>
    public System.Collections.Generic.List<PorterAgent> GetActivePorters()
    {
        return GetAllUnits();
    }

    /// <summary>
    /// Get available slots (legacy - kept for compatibility).
    /// </summary>
    public int GetAvailableSlots()
    {
        return GetTotalCapacity() - GetTotalCount();
    }
}