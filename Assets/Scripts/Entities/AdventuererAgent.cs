using UnityEngine;

public enum AdventurerState
{
    Idle,       // Standing still, scanning for targets
    Wander,     // Walking randomly in patrol area
    Seek,       // Moving toward target
    Attack,     // Attacking target
}

/// <summary>
/// Adventurer agent that automatically attacks mobs within range.
/// Uses MobManager for target reservation with multi-reservation support.
/// Implements hysteresis to prevent state flickering.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class AdventurerAgent : EntityStateMachine<AdventurerState>
{
    [Header("State Colors")]
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color wanderColor = Color.cyan;
    [SerializeField] private Color seekColor = Color.yellow;
    [SerializeField] private Color attackColor = Color.red;

    [Header("Adventurer Data")]
    // Adventurer specific data 
    [SerializeField] private AdventurerDef adventurerDef;
    private float leashRange; // Not a modifiable stat
    private bool returnToSpawn; // Not a modifiable stat
    
    // Timing
    private float idleTimeMin;
    private float idleTimeMax;
    private float wanderTimeMin;
    private float wanderTimeMax;

    // Combat tracking
    private MobAgent currentTarget;
    private CyclicTimer attackTimer;
    private Vector3 spawnPoint;
    
    // State timing
    private CountdownTimer stateTimer;
    private float stateTargetTime; // Keep for reference in debug

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool showDebugLogs = false;


    void Awake()
    {
        col = GetComponent<Collider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public override void Init(EntityDef entityDef, int layer, Spawner spawner, Collider2D playArea)
    {
        base.Init(entityDef, layer, spawner, playArea);
        
        adventurerDef = (AdventurerDef)entityDef;
        
        // Cache non-stat data only
        leashRange = adventurerDef.leashRange;
        returnToSpawn = adventurerDef.returnToSpawn;
        
        // Cache timing ranges (not stats)
        idleTimeMin = adventurerDef.idleTimeRange.x;
        idleTimeMax = adventurerDef.idleTimeRange.y;
        wanderTimeMin = adventurerDef.wanderTimeRange.x;
        wanderTimeMax = adventurerDef.wanderTimeRange.y;
        
        // Cache colors
        idleColor = adventurerDef.idleColor;
        wanderColor = adventurerDef.wanderColor;
        seekColor = adventurerDef.seekColor;
        attackColor = adventurerDef.attackColor;
        
        // Remember spawn point for leashing/returning
        spawnPoint = transform.position;
        
        attackTimer = new CyclicTimer(adventurerDef.attackInterval);
        stateTimer = null; // Will be created in OnEnterState
        currentTarget = null;
        
        ChangeState(adventurerDef.startingState);
    }

    protected override void Update()
    {
        base.Update();
        UpdateMovementSmooth();
    }

    // ===== STATE MACHINE =====

    protected override void OnEnterState(AdventurerState newState)
    {
        switch (newState)
        {
            case AdventurerState.Idle:
                spriteRenderer.color = idleColor;
                ReleaseCurrentTarget(); // Release target when going idle
                attackTimer.Reset();
                targetPos = null;
                
                stateTargetTime = Random.Range(idleTimeMin, idleTimeMax);
                stateTimer = new CountdownTimer(stateTargetTime);
                stateTimer.Start();
                
                if (returnToSpawn && Vector3.Distance(transform.position, spawnPoint) > 0.1f)
                {
                    SetTarget(spawnPoint);
                }
                break;

            case AdventurerState.Wander:
                spriteRenderer.color = wanderColor;
                SetTarget(GetWanderPosition(wanderArea));
                
                stateTargetTime = Random.Range(wanderTimeMin, wanderTimeMax);
                stateTimer = new CountdownTimer(stateTargetTime);
                stateTimer.Start();
                break;

            case AdventurerState.Seek:
                spriteRenderer.color = seekColor;
                if (currentTarget != null)
                {
                    SetTarget(currentTarget.transform.position);
                }
                break;

            case AdventurerState.Attack:
                spriteRenderer.color = attackColor;
                attackTimer.Reset(Stats.AttackInterval); // Reset with current (possibly buffed) interval
                targetPos = null; // Stop moving when attacking
                break;
        }
    }

    protected override void OnUpdateState(AdventurerState currentState)
    {
        switch (currentState)
        {
            case AdventurerState.Idle:
                UpdateIdle();
                break;

            case AdventurerState.Wander:
                UpdateWander();
                break;

            case AdventurerState.Seek:
                UpdateSeek();
                break;

            case AdventurerState.Attack:
                UpdateAttack();
                break;
        }
    }

    protected override void OnExitState(AdventurerState oldState)
    {
        // Cleanup if needed
    }

    // ===== STATE UPDATE METHODS =====

    private void UpdateIdle()
    {
        stateTimer.Tick(TickDelta);
                
        // Scan for targets while idle
        MobAgent target = ScanForTarget();
        
        if (target != null)
        {
            currentTarget = target;
            ChangeState(AdventurerState.Seek);
            return;
        }
        
        // Transition to wander after idle time
        if (stateTimer.IsFinished)
        {
            ChangeState(AdventurerState.Wander);
        }
    }

    private void UpdateWander()
    {
        stateTimer.Tick(TickDelta);
                
        // Scan for targets while wandering
        MobAgent target = ScanForTarget();
        
        if (target != null)
        {
            currentTarget = target;
            ChangeState(AdventurerState.Seek);
            return;
        }
        
        // Check if reached wander destination
        if (!targetPos.HasValue)
        {
            ChangeState(AdventurerState.Idle);
            return;
        }
        
        // Transition to idle after wander time
        if (stateTimer.IsFinished)
        {
            ChangeState(AdventurerState.Idle);
        }
    }

    private void UpdateSeek()
    {
        // Validate target
        if (!IsTargetValid())
        {
            if (showDebugLogs)
                Debug.Log($"[{name}] Target invalid during Seek, returning to Idle");
            ChangeState(AdventurerState.Idle);
            return;
        }

        // Check leash range
        if (leashRange > 0f && Vector3.Distance(transform.position, spawnPoint) > leashRange)
        {
            if (showDebugLogs)
                Debug.Log($"[{name}] Outside leash range, returning to Idle");
            ChangeState(AdventurerState.Idle);
            return;
        }

        // Update target position
        SetTarget(currentTarget.transform.position);

        // Check if in attack range
        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position);
        
        if (distanceToTarget <= Stats.AttackRange)
        {
            ChangeState(AdventurerState.Attack);
        }
    }

    private void UpdateAttack()
    {
        // Validate target
        if (!IsTargetValid())
        {
            if (showDebugLogs)
                Debug.Log($"[{name}] Target invalid during Attack, returning to Idle");
            ChangeState(AdventurerState.Idle);
            return;
        }

        // Check if target moved out of range (HYSTERESIS: uses chaseBreakRange, not attackRange)
        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position);
        
        if (distanceToTarget > Stats.ChaseBreakRange) // Use Stats.ChaseBreakRange
        {
            if (showDebugLogs)
                Debug.Log($"[{name}] Target moved out of chase range ({distanceToTarget:F2} > {Stats.ChaseBreakRange}), seeking again");
            ChangeState(AdventurerState.Seek);
            return;
        }

        // Face the target while attacking
        Vector3 directionToTarget = currentTarget.transform.position - transform.position;
        FaceDirection(directionToTarget.x);

        // Attack timer with overflow preservation
        attackTimer.SetInterval(Stats.AttackInterval); // Update interval in case of buffs
        attackTimer.Tick(TickDelta);
        
        if (attackTimer.TryConsumeCycle())
        {
            animator.SetTrigger(AnimHash.Attack);
            
            // Deal damage (MobAgent implements IDamageable)
            IDamageable damageable = currentTarget as IDamageable;
            if (damageable != null)
            {
                float damageDealt = damageable.OnDamage(Stats.AttackDamage); // Use Stats.AttackDamage
                
                if (damageDealt > 0f)
                {
                    OnAttackHit();
                }
                else
                {
                    // Target died
                    if (showDebugLogs)
                        Debug.Log($"[{name}] Target died, returning to Idle");
                    ChangeState(AdventurerState.Idle);
                }
            }
        }
    }

    // ===== TARGET ACQUISITION =====

    /// <summary>
    /// Request a target from MobManager.
    /// This ensures smart distribution of adventurers across available mobs.
    /// </summary>
    private MobAgent ScanForTarget()
    {
        if (MobManager.Instance == null)
        {
            Debug.LogWarning($"[{name}] MobManager.Instance is null, cannot scan for targets");
            return null;
        }

        // Request target from MobManager (handles reservation automatically)
        MobAgent target = MobManager.Instance.RequestTarget(
            requester: this,
            position: transform.position,
            scanRange: Stats.ScanRange, // Use Stats.ScanRange
            layer: layerIndex
        );

        if (target != null && showDebugLogs)
        {
            Debug.Log($"[{name}] Acquired target: {target.name}");
        }

        return target;
    }

    /// <summary>
    /// Check if current target is still valid
    /// </summary>
    private bool IsTargetValid()
    {
        if (currentTarget == null || currentTarget.gameObject == null)
            return false;

        // Verify we still have reservation (in case another system interfered)
        if (MobManager.Instance != null)
        {
            return MobManager.Instance.IsReservedBy(currentTarget, this);
        }

        return true;
    }

    /// <summary>
    /// Release current target reservation
    /// </summary>
    private void ReleaseCurrentTarget()
    {
        if (currentTarget != null && MobManager.Instance != null)
        {
            MobManager.Instance.ReleaseTarget(currentTarget, this); // CHANGED: Pass both mob and adventurer
            
            if (showDebugLogs)
                Debug.Log($"[{name}] Released target: {currentTarget.name}");
        }
        
        currentTarget = null;
    }

    // ===== HELPER METHODS =====

    private void OnAttackHit()
    {
        // TODO: Play attack animation
        // TODO: Play attack sound
        // TODO: Spawn hit VFX
    }

    public override void Despawn()
    {
        // Release target reservation before despawning
        ReleaseCurrentTarget();
        
        base.Despawn();
    }

    // ===== DEBUG GIZMOS =====

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;

        // In play mode, use Stats system; in edit mode, use def
        float attackRng = Application.isPlaying && Stats != null ? Stats.AttackRange : (adventurerDef?.attackRange ?? 1.5f);
        float chaseRng = Application.isPlaying && Stats != null ? Stats.ChaseBreakRange : (adventurerDef?.chaseBreakRange ?? 2.5f);
        float scanRng = Application.isPlaying && Stats != null ? Stats.ScanRange : (adventurerDef?.scanRange ?? 10f);

        // Attack range (red) - where adventurer starts attacking
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRng);

        // Chase break range (orange) - where adventurer stops attacking
        Gizmos.color = new Color(1f, 0.5f, 0f); // Orange
        Gizmos.DrawWireSphere(transform.position, chaseRng);

        // Scan range (yellow)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, scanRng);

        // Leash range (blue)
        if (leashRange > 0f)
        {
            Vector3 spawnPos = Application.isPlaying ? spawnPoint : transform.position;
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(spawnPos, leashRange);
        }

        // Line to current target (green)
        if (Application.isPlaying && currentTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
            
            // Draw target status
            bool isReservedByMe = MobManager.Instance != null && 
                                  MobManager.Instance.IsReservedBy(currentTarget, this);
            Gizmos.color = isReservedByMe ? Color.green : Color.red;
            Gizmos.DrawWireSphere(currentTarget.transform.position, 0.3f);
            
            // Draw distance info
            float dist = Vector3.Distance(transform.position, currentTarget.transform.position);
            UnityEditor.Handles.Label(
                currentTarget.transform.position + Vector3.up * 0.5f,
                $"Dist: {dist:F2}\nReservers: {MobManager.Instance.GetReservationCount(currentTarget)}"
            );
        }
        
        // Wander area (cyan)
        if (Application.isPlaying && wanderArea != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(wanderArea.bounds.center, wanderArea.bounds.size);
        }
    }
#endif
}