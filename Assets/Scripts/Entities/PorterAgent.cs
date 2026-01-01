using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Porter state machine states
/// </summary>
public enum PorterState
{
    Idle,           // Waiting, scanning for loot
    Wander,         // Walking randomly in patrol area
    Seek,           // Moving toward loot
    PickUp,         // Picking up loot
    Return,         // Moving back to transport point
    Travel,         // Climbing up ladder to shop
    Deposit,        // Depositing loot at shop
    ReturnToLayer   // Climbing back down ladder to layer
}

/// <summary>
/// Porter agent that collects loot from dungeon layers and transports it to the shop.
/// Uses LootManager for loot reservation to prevent conflicts.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PorterAgent : EntityStateMachine<PorterState>
{
    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool showDebugLogs = false;

    // Porter-specific data (non-stat)
    private PorterDef porterDef;
    private bool returnToSpawn; // Not a modifiable stat
    
    [Header("Animation Timing")]
    [Tooltip("Duration of pickup animation in seconds")]
    [SerializeField] private float pickupAnimDuration = 0.3f;
    
    [Tooltip("Offset above porter's head where loot appears during pickup")]
    [SerializeField] private Vector3 lootHoldOffset = new Vector3(0f, 1.5f, 0f);
    
    // Timing (not stats)
    private float idleTimeMin;
    private float idleTimeMax;
    private float wanderTimeMin;
    private float wanderTimeMax;

    // Loot tracking
    private Loot currentTarget;
    private List<ResourceStack> carriedLoot = new List<ResourceStack>();
    private Vector3 spawnPoint;
    
    // Pickup animation tracking
    private Coroutine pickupCoroutine;
    
    // Transport
    private TransportPoint transport;
    private Vector3 depositPoint; // Where to deposit at shop (set by PorterManager)
    
    // State timing
    private CountdownTimer stateTimer;
    private float stateTargetTime; // Keep for reference in debug

    void Awake()
    {
        col = GetComponent<Collider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public override void Init(EntityDef entityDef, int layer, Spawner spawner, Collider2D playArea)
    {
        base.Init(entityDef, layer, spawner, playArea);

        porterDef = (PorterDef)entityDef;

        // Cache non-stat data only
        returnToSpawn = porterDef.returnToSpawn;

        // Cache timing (not stats)
        idleTimeMin = porterDef.idleTimeRange.x;
        idleTimeMax = porterDef.idleTimeRange.y;
        wanderTimeMin = porterDef.wanderTimeRange.x;
        wanderTimeMax = porterDef.wanderTimeRange.y;

        // Remember spawn point
        spawnPoint = transform.position;

        stateTimer = null; // Will be created in OnEnterState
        currentTarget = null;
        carriedLoot.Clear();

        ChangeState(porterDef.startingState);
    }
    
    protected override void Update()
    {
        base.Update();
        UpdateMovementSmooth();
    }

    /// <summary>
    /// Set transport point and deposit location. Called by PorterManager.
    /// </summary>
    public void SetTransport(TransportPoint transportPoint, Vector3 depositLocation)
    {
        transport = transportPoint;
        depositPoint = depositLocation;
        
        if (showDebugLogs)
            Debug.Log($"[{name}] Transport set to {transportPoint.name}, deposit at {depositLocation}");
    }

    // ===== STATE MACHINE =====

    protected override void OnEnterState(PorterState newState)
    {
        switch (newState)
        {
            case PorterState.Idle:
                ReleaseCurrentTarget();
                targetPos = null;
                
                stateTargetTime = Random.Range(idleTimeMin, idleTimeMax);
                stateTimer = new CountdownTimer(stateTargetTime);
                stateTimer.Start();
                
                if (returnToSpawn && Vector3.Distance(transform.position, spawnPoint) > 0.1f)
                {
                    SetTarget(spawnPoint);
                }
                break;

            case PorterState.Wander:
                SetTarget(GetWanderPosition(wanderArea));
                
                stateTargetTime = Random.Range(wanderTimeMin, wanderTimeMax);
                stateTimer = new CountdownTimer(stateTargetTime);
                stateTimer.Start();
                break;

            case PorterState.Seek:
                if (currentTarget != null)
                {
                    SetTarget(currentTarget.transform.position);
                }
                break;

            case PorterState.PickUp:
                targetPos = null;
                
                // Trigger pickup animation
                if (animator != null)
                {
                    animator.SetTrigger(AnimHash.PickUp);
                }
                
                // Start pickup visual coroutine (handles timing and state transition)
                if (currentTarget != null)
                {
                    pickupCoroutine = StartCoroutine(PickupLootVisual(currentTarget));
                }
                else
                {
                    if (showDebugLogs)
                        Debug.LogWarning($"[{name}] Entered PickUp state with no target!");
                    ChangeState(PorterState.Idle);
                }
                break;

            case PorterState.Return:
                if (transport != null)
                {
                    Vector3 entryPos = transport.GetEntryPosition();
                    SetTarget(entryPos);
                    
                    if (showDebugLogs)
                        Debug.Log($"[{name}] Entering Return state - Transport: {transport.name}, Entry: {entryPos}, Current pos: {transform.position}");
                }
                else
                {
                    Debug.LogError($"[{name}] Return state but transport is NULL!");
                }
                break;

            case PorterState.Travel:
                // Set climb bool to true for climbing UP
                if (animator != null)
                {
                    animator.SetBool(AnimHash.Climb, true);
                }
                
                if (transport != null)
                {
                    SetTarget(transport.GetExitPosition());
                    
                    if (showDebugLogs)
                        Debug.Log($"[{name}] Starting travel - Moving from {transform.position} to {transport.GetExitPosition()}");
                }
                break;

            case PorterState.Deposit:
                targetPos = null;
                
                stateTargetTime = Stats.DepositTime;
                stateTimer = new CountdownTimer(stateTargetTime);
                stateTimer.Start();
                
                // Trigger deposit animation
                if (animator != null)
                {
                    animator.SetTrigger(AnimHash.Deposit);
                }
                break;

            case PorterState.ReturnToLayer:
                // Set climb bool to true for climbing DOWN
                if (animator != null)
                {
                    animator.SetBool(AnimHash.Climb, true);
                }
                
                // Climb back down: set target to entry point (bottom of ladder)
                if (transport != null)
                {
                    Vector3 entryPos = transport.GetEntryPosition();
                    SetTarget(entryPos);
                    
                    if (showDebugLogs)
                        Debug.Log($"[{name}] Returning to layer - Climbing from {transform.position} to {entryPos}");
                }
                break;
        }
    }

    protected override void OnUpdateState(PorterState currentState)
    {
        switch (currentState)
        {
            case PorterState.Idle:
                UpdateIdle();
                break;

            case PorterState.Wander:
                UpdateWander();
                break;

            case PorterState.Seek:
                UpdateSeek();
                break;

            case PorterState.PickUp:
                UpdatePickUp();
                break;

            case PorterState.Return:
                UpdateReturn();
                break;

            case PorterState.Travel:
                UpdateTravel();
                break;

            case PorterState.Deposit:
                UpdateDeposit();
                break;

            case PorterState.ReturnToLayer:
                UpdateReturnToLayer();
                break;
        }
    }

    protected override void OnExitState(PorterState oldState)
    {
        // Turn off climb animation when exiting climbing states
        if (oldState == PorterState.Travel || oldState == PorterState.ReturnToLayer)
        {
            if (animator != null)
            {
                animator.SetBool(AnimHash.Climb, false);
            }
        }
    }

    // ===== STATE UPDATE METHODS =====

    private void UpdateIdle()
    {
        stateTimer.Tick(TickDelta);
        
        // Check if we should scan for loot
        if (stateTimer.IsFinished)
        {
            // Only scan if we have capacity (cast to int)
            if (carriedLoot.Count < (int)Stats.CarryCapacity)
            {
                Loot loot = ScanForLoot();
                
                if (loot != null)
                {
                    currentTarget = loot;
                    ChangeState(PorterState.Seek);
                    return;
                }
            }
            else
            {
                // Inventory full, go deposit
                ChangeState(PorterState.Return);
                return;
            }
            
            // No loot found, start wandering
            ChangeState(PorterState.Wander);
        }
    }

    private void UpdateWander()
    {
        stateTimer.Tick(TickDelta);
                
        // Scan for loot while wandering (cast to int)
        if (carriedLoot.Count < (int)Stats.CarryCapacity)
        {
            Loot loot = ScanForLoot();
            
            if (loot != null)
            {
                currentTarget = loot;
                ChangeState(PorterState.Seek);
                return;
            }
        }
        else
        {
            // Full inventory, go deposit
            ChangeState(PorterState.Return);
            return;
        }
        
        // Check if reached wander destination
        if (!targetPos.HasValue)
        {
            ChangeState(PorterState.Idle);
            return;
        }
        
        // Transition to idle after wander time
        if (stateTimer.IsFinished)
        {
            ChangeState(PorterState.Idle);
        }
    }

    private void UpdateSeek()
    {
        // Validate target
        if (!IsTargetValid())
        {
            if (showDebugLogs)
                Debug.Log($"[{name}] Target invalid during Seek, returning to Idle");
            ChangeState(PorterState.Idle);
            return;
        }

        // Update target position (in case loot moved somehow)
        SetTarget(currentTarget.transform.position);

        // Check if reached loot
        if (!targetPos.HasValue || CheckDistanceReached(currentTarget.transform.position))
        {
            ChangeState(PorterState.PickUp);
        }
    }

    private void UpdatePickUp()
    {
        // Pickup is handled entirely by the PickupLootVisual coroutine
        // State will transition when animation completes
    }

    private void UpdateReturn()
    {
        if (transport == null)
        {
            Debug.LogError($"[{name}] No transport assigned! Cannot return to shop.");
            ChangeState(PorterState.Idle);
            return;
        }

        Vector3 entryPos = transport.GetEntryPosition();
        float distance = Vector3.Distance(transform.position, entryPos);
        
        if (showDebugLogs)
        {
            Debug.Log($"[{name}] UpdateReturn - Pos: {transform.position}, Target: {entryPos}, Distance: {distance:F3}, HasTarget: {targetPos.HasValue}");
        }

        // Check if reached transport entry
        if (!targetPos.HasValue || distance < 0.3f)
        {
            if (showDebugLogs)
                Debug.Log($"[{name}] Reached transport entry! Transitioning to Travel state");
            
            ChangeState(PorterState.Travel);
        }
    }

    private void UpdateTravel()
    {
        if (transport == null)
        {
            Debug.LogError($"[{name}] No transport in Travel state!");
            ChangeState(PorterState.Idle);
            return;
        }

        Vector3 exitPos = transport.GetExitPosition();
        float distance = Vector3.Distance(transform.position, exitPos);
        
        if (showDebugLogs)
        {
            Debug.Log($"[{name}] UpdateTravel - Climbing to {exitPos}, Distance: {distance:F3}");
        }

        // Check if reached top of ladder
        if (!targetPos.HasValue || distance < 0.2f)
        {
            if (showDebugLogs)
                Debug.Log($"[{name}] Reached top of ladder! Moving to deposit point");
            
            // Now move to deposit point
            SetTarget(depositPoint);
            ChangeState(PorterState.Deposit);
        }
    }

    private void UpdateDeposit()
    {
        stateTimer.Tick(TickDelta);

        // Wait for deposit animation time
        if (stateTimer.IsFinished)
        {
            // Deposit all carried loot into inventory
            foreach (var stack in carriedLoot)
            {
                var inventory = Inventory.Instance.GetInventoryType(stack.itemDef.itemCategory);
                Inventory.Instance.Add(inventory, stack);
                
                if (showDebugLogs)
                    Debug.Log($"[{name}] Deposited {stack.qty}x {stack.itemDef.displayName}");
            }

            // Clear carried loot
            carriedLoot.Clear();

            // Now walk back down the ladder
            ChangeState(PorterState.ReturnToLayer);
        }
    }

    private void UpdateReturnToLayer()
    {
        if (transport == null)
        {
            Debug.LogError($"[{name}] No transport in ReturnToLayer state!");
            ChangeState(PorterState.Idle);
            return;
        }

        Vector3 entryPos = transport.GetEntryPosition();
        float distance = Vector3.Distance(transform.position, entryPos);
        
        if (showDebugLogs)
        {
            Debug.Log($"[{name}] UpdateReturnToLayer - Climbing down to {entryPos}, Distance: {distance:F3}");
        }

        // Check if reached bottom of ladder
        if (!targetPos.HasValue || distance < 0.02f)
        {
            if (showDebugLogs)
                Debug.Log($"[{name}] Reached bottom of ladder! Returning to Idle");
            
            ChangeState(PorterState.Idle);
        }
    }

    // ===== LOOT ACQUISITION =====

    /// <summary>
    /// Request loot from LootManager.
    /// This ensures no two porters target the same loot.
    /// </summary>
    private Loot ScanForLoot()
    {
        if (LootManager.Instance == null)
        {
            Debug.LogWarning($"[{name}] LootManager.Instance is null, cannot scan for loot");
            return null;
        }

        Loot loot = LootManager.Instance.RequestLoot(
            requester: this,
            position: transform.position,
            scanRange: Stats.ScanRange, // Use Stats.ScanRange
            layer: layerIndex
        );

        if (loot != null && showDebugLogs)
        {
            Debug.Log($"[{name}] Acquired loot: {loot.name}");
        }

        return loot;
    }

    /// <summary>
    /// Check if current target is still valid
    /// </summary>
    private bool IsTargetValid()
    {
        if (currentTarget == null || currentTarget.gameObject == null)
            return false;

        if (LootManager.Instance != null)
        {
            return LootManager.Instance.IsReservedBy(currentTarget, this);
        }

        return true;
    }

    /// <summary>
    /// Release current loot reservation
    /// </summary>
    private void ReleaseCurrentTarget()
    {
        if (currentTarget != null && LootManager.Instance != null)
        {
            LootManager.Instance.ReleaseLoot(currentTarget);
            
            if (showDebugLogs)
                Debug.Log($"[{name}] Released loot: {currentTarget.name}");
        }
        
        currentTarget = null;
    }

    // ===== QUERIES =====

    public int GetCarriedCount() => carriedLoot.Count;
    public int GetCarryCapacity() => (int)Stats.CarryCapacity;
    public bool IsFull() => carriedLoot.Count >= (int)Stats.CarryCapacity;
    public bool IsCarryingLoot() => carriedLoot.Count > 0;

    public override void Despawn()
    {
        ReleaseCurrentTarget();
        carriedLoot.Clear();
        
        // Stop pickup coroutine if running
        if (pickupCoroutine != null)
        {
            StopCoroutine(pickupCoroutine);
            pickupCoroutine = null;
        }
        
        base.Despawn();
    }

    // ===== PICKUP ANIMATION =====

    /// <summary>
    /// Animates loot moving from ground to above porter's head, then collects it.
    /// Controls state transition - porter waits until animation completes.
    /// </summary>
    private System.Collections.IEnumerator PickupLootVisual(Loot loot)
    {
        if (loot == null)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[{name}] PickupLootVisual called with null loot");
            ChangeState(PorterState.Idle);
            yield break;
        }

        Transform lootTransform = loot.transform;
        Vector3 startPos = lootTransform.position;
        Vector3 targetPos = transform.position + lootHoldOffset;
        lootTransform.position = targetPos;
        float elapsed = 0f;

        // Lerp loot from ground to above head
        while (elapsed < pickupAnimDuration)
        {
            // Validate loot still exists
            if (loot == null || lootTransform == null)
            {
                if (showDebugLogs)
                    Debug.LogWarning($"[{name}] Loot disappeared during pickup animation");
                ChangeState(PorterState.Idle);
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = elapsed / pickupAnimDuration;
            yield return null;
        }

        // Ensure loot is at final position
        if (loot != null && lootTransform != null)
        {
            lootTransform.position = transform.position + lootHoldOffset;
        }

        // Wait additional time if needed (Stats.PickupTime might be longer than animation)
        float remainingTime = Stats.PickupTime - pickupAnimDuration;
        if (remainingTime > 0f)
        {
            yield return new WaitForSeconds(remainingTime);
        }

        // Validate target one last time before collecting
        if (!IsTargetValid())
        {
            if (showDebugLogs)
                Debug.Log($"[{name}] Target invalid after animation, returning to Idle");
            ChangeState(PorterState.Idle);
            pickupCoroutine = null;
            yield break;
        }

        // Collect the loot
        ResourceStack stack = currentTarget.CollectByPorter(this);
        
        if (stack.itemDef != null)
        {
            carriedLoot.Add(stack);
            
            if (showDebugLogs)
                Debug.Log($"[{name}] Picked up {stack.qty}x {stack.itemDef.displayName} (carrying {carriedLoot.Count}/{(int)Stats.CarryCapacity})");
        }

        ReleaseCurrentTarget();

        // Decide next state (cast to int)
        if (carriedLoot.Count >= (int)Stats.CarryCapacity)
        {
            // Full, go deposit
            ChangeState(PorterState.Return);
        }
        else
        {
            // Look for more loot
            Loot nextLoot = ScanForLoot();
            
            if (nextLoot != null)
            {
                currentTarget = nextLoot;
                ChangeState(PorterState.Seek);
            }
            else
            {
                // No more loot, go deposit what we have (if any)
                if (carriedLoot.Count > 0)
                {
                    ChangeState(PorterState.Return);
                }
                else
                {
                    ChangeState(PorterState.Idle);
                }
            }
        }

        pickupCoroutine = null;
    }

    // ===== DEBUG GIZMOS =====

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;

        // In play mode, use Stats system; in edit mode, use def
        float scanRng = Application.isPlaying && Stats != null ? Stats.ScanRange : (porterDef?.scanRange ?? 10f);
        int capacity = Application.isPlaying && Stats != null ? (int)Stats.CarryCapacity : (porterDef?.carryCapacity ?? 5);

        // Scan range (yellow)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, scanRng);

        // Line to current target (green)
        if (Application.isPlaying && currentTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
            
            UnityEditor.Handles.Label(
                currentTarget.transform.position + Vector3.up * 0.5f,
                $"Target: {currentTarget.GetItemDef().displayName}"
            );
        }

        // Line to transport (cyan)
        if (Application.isPlaying && transport != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transport.GetEntryPosition());
        }

        // Spawn point (blue)
        Vector3 spawn = Application.isPlaying ? spawnPoint : transform.position;
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(spawn, 0.3f);

        // Carried loot indicator
        if (Application.isPlaying && carriedLoot.Count > 0)
        {
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.7f,
                $"Carrying: {carriedLoot.Count}/{capacity}"
            );
        }
    }
#endif
}