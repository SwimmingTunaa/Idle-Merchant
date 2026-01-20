using UnityEngine;

public enum AdventurerState
{
    Idle,
    Wander,
    Seek,
    Attack,
}

/// <summary>
/// Adventurer agent refactored to use CombatBehavior component.
/// Combat logic is now handled by reusable component.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(CombatBehavior))]
public class AdventurerAgent : EntityStateMachine<AdventurerState>, IEntity
{
    [Header("Adventurer Data")]
    [SerializeField] private AdventurerDef adventurerDef;
    private float leashRange;
    private bool returnToSpawn;
    
    private float idleTimeMin;
    private float idleTimeMax;
    private float wanderTimeMin;
    private float wanderTimeMax;

    private Vector3 spawnPoint;
    private CountdownTimer stateTimer;
    private float stateTargetTime;
    
    private Health health;
    private CombatBehavior combat;
    
    // IEntity implementation
    public EntityType EntityType => EntityType.Adventurer;
    public bool IsAlive => health != null && health.IsAlive;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool showDebugLogs = false;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        health = GetComponent<Health>();
        combat = GetComponent<CombatBehavior>();
    }

    public override void Init(EntityDef entityDef, int layer, Spawner spawner, Collider2D playArea)
    {
        base.Init(entityDef, layer, spawner, playArea);
        
        adventurerDef = (AdventurerDef)entityDef;
        
        leashRange = adventurerDef.leashRange;
        returnToSpawn = adventurerDef.returnToSpawn;
        
        idleTimeMin = adventurerDef.idleTimeRange.x;
        idleTimeMax = adventurerDef.idleTimeRange.y;
        wanderTimeMin = adventurerDef.wanderTimeRange.x;
        wanderTimeMax = adventurerDef.wanderTimeRange.y;
        
        spawnPoint = transform.position;
        stateTimer = null;
        
        // Initialize health
        if (health != null && adventurerDef.baseHealth > 0f)
        {
            health.Init(adventurerDef.baseHealth, fullHP: true);
            health.OnDeath += OnDeath;
        }
        
        // Initialize combat
        if (combat != null)
        {
            // Adventurers are aggressive toward mobs
            CombatConfig config = new CombatConfig
            {
                canAttack = true,
                behaviorType = CombatBehaviorType.Aggressive,
                hostileTo = HostilityTargets.Mobs,
                territorialRadius = 0f
            };
            
            combat.Init(config, Stats, this, layerIndex, spawnPoint);
            combat.OnTargetAcquired += OnCombatTargetAcquired;
            combat.OnTargetLost += OnCombatTargetLost;
        }
        
        ChangeState(adventurerDef.startingState);
    }

    protected override void Update()
    {
        base.Update();
        UpdateMovementSmooth();
    }

    // ===== HEALTH HANDLING =====
    
    private void OnDeath(float overkill)
    {
        if (showDebugLogs)
            Debug.Log($"[{name}] Adventurer died! Overkill: {overkill}");
        
        Despawn();
    }

    // ===== COMBAT EVENT HANDLERS =====
    
    private void OnCombatTargetAcquired(GameObject target)
    {
        if (showDebugLogs)
            Debug.Log($"[{name}] Acquired target: {target.name}");
        
        // Only transition to Seek if we're in Idle or Wander
        if (State == AdventurerState.Idle || State == AdventurerState.Wander)
        {
            ChangeState(AdventurerState.Seek);
        }
    }

    private void OnCombatTargetLost()
    {
        if (showDebugLogs)
            Debug.Log($"[{name}] Lost target");
        
        // Return to idle when target lost
        if (State == AdventurerState.Seek || State == AdventurerState.Attack)
        {
            ChangeState(AdventurerState.Idle);
        }
    }

    // ===== STATE MACHINE =====

    protected override void OnEnterState(AdventurerState newState)
    {
        switch (newState)
        {
            case AdventurerState.Idle:
                combat?.ReleaseTarget();
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
                SetTarget(GetWanderPosition(wanderArea));
                
                stateTargetTime = Random.Range(wanderTimeMin, wanderTimeMax);
                stateTimer = new CountdownTimer(stateTargetTime);
                stateTimer.Start();
                break;

            case AdventurerState.Seek:
                if (combat != null && combat.HasTarget)
                {
                    SetTarget(combat.CurrentTarget.transform.position);
                }
                break;

            case AdventurerState.Attack:
                targetPos = null;
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
        
        // Scan for targets
        if (combat != null)
        {
            var target = combat.ScanForTarget(transform.position, Stats.ScanRange);
            if (target != null)
            {
                combat.SetTarget(target);
                return; // Event will trigger state change
            }
        }
        
        if (stateTimer.IsFinished)
        {
            ChangeState(AdventurerState.Wander);
        }
    }

    private void UpdateWander()
    {
        stateTimer.Tick(TickDelta);
        
        // Scan for targets
        if (combat != null)
        {
            var target = combat.ScanForTarget(transform.position, Stats.ScanRange);
            if (target != null)
            {
                combat.SetTarget(target);
                return; // Event will trigger state change
            }
        }
        
        if (!targetPos.HasValue)
        {
            ChangeState(AdventurerState.Idle);
            return;
        }
        
        if (stateTimer.IsFinished)
        {
            ChangeState(AdventurerState.Idle);
        }
    }

    private void UpdateSeek()
    {
        if (combat == null || !combat.IsTargetValid())
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
        SetTarget(combat.CurrentTarget.transform.position);

        // Check if in attack range
        if (combat.IsInAttackRange(transform.position))
        {
            ChangeState(AdventurerState.Attack);
        }
    }

    private void UpdateAttack()
    {
        if (combat == null || !combat.IsTargetValid())
        {
            if (showDebugLogs)
                Debug.Log($"[{name}] Target invalid during Attack, returning to Idle");
            ChangeState(AdventurerState.Idle);
            return;
        }

        // Check if target moved out of chase range
        if (combat.IsOutOfChaseRange(transform.position))
        {
            if (showDebugLogs)
                Debug.Log($"[{name}] Target out of chase range, seeking again");
            ChangeState(AdventurerState.Seek);
            return;
        }

        // Face the target
        Vector3 directionToTarget = combat.CurrentTarget.transform.position - transform.position;
        FaceDirection(directionToTarget.x);

        // Execute attacks (combat component handles timing and damage)
        combat.UpdateAttack(TickDelta, attackAnimDelay: 0.2f);
    }

    // ===== CLEANUP =====

    public override void Despawn()
    {
        if (health != null)
        {
            health.OnDeath -= OnDeath;
        }
        
        if (combat != null)
        {
            combat.OnTargetAcquired -= OnCombatTargetAcquired;
            combat.OnTargetLost -= OnCombatTargetLost;
            combat.ReleaseTarget();
        }
        
        base.Despawn();
    }

    // ===== DEBUG GIZMOS =====

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;

        float attackRng = Application.isPlaying && Stats != null ? Stats.AttackRange : (adventurerDef?.attackRange ?? 1.5f);
        float chaseRng = Application.isPlaying && Stats != null ? Stats.ChaseBreakRange : (adventurerDef?.chaseBreakRange ?? 2.5f);
        float scanRng = Application.isPlaying && Stats != null ? Stats.ScanRange : (adventurerDef?.scanRange ?? 10f);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRng);

        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, chaseRng);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, scanRng);

        if (leashRange > 0f)
        {
            Vector3 spawnPos = Application.isPlaying ? spawnPoint : transform.position;
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(spawnPos, leashRange);
        }

        if (Application.isPlaying && combat != null && combat.HasTarget)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, combat.CurrentTarget.transform.position);
            
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(combat.CurrentTarget.transform.position, 0.3f);
        }
        
        if (Application.isPlaying && wanderArea != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(wanderArea.bounds.center, wanderArea.bounds.size);
        }
    }
#endif
}