using UnityEngine;
using System.Collections;

public enum AdventurerState
{
    Idle,
    Wander,
    Seek,
    Attack,
    Hit,
    Dead
}

// Adventurer agent with combat, hit stagger, death, and revive.
// Combat logic handled by reusable CombatBehavior component.
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
    [SerializeField] GameObject deathEffect;
    private CombatBehavior combat;

    // Hit stagger tracking
    private AdventurerState previousState;
    private float hitCooldownTimer;
    private Coroutine hitCoroutine;
    private Coroutine deathCoroutine;

    // XP & rank
    private float currentXP;
    private int currentRank = 1;

    // IEntity implementation
    public EntityType EntityType => EntityType.Adventurer;
    public bool IsAlive => health != null && health.IsAlive;

    // XP / rank public interface
    public float CurrentXP => currentXP;
    public int CurrentRank => currentRank;

    /// <summary>XP needed to reach the next rank, or float.MaxValue at max rank.</summary>
    public float XPForNextRank
    {
        get
        {
            if (adventurerDef == null || currentRank >= adventurerDef.maxRank) return float.MaxValue;
            int idx = currentRank - 1;
            if (idx >= adventurerDef.xpThresholds.Length) return float.MaxValue;
            return adventurerDef.xpThresholds[idx];
        }
    }

    /// <summary>True when the adventurer has enough XP to promote and is not at max rank.</summary>
    public bool CanPromote
    {
        get
        {
            if (adventurerDef == null || currentRank >= adventurerDef.maxRank) return false;
            int idx = currentRank - 1;
            if (idx >= adventurerDef.xpThresholds.Length) return false;
            return currentXP >= adventurerDef.xpThresholds[idx];
        }
    }

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
        deathEffect?.SetActive(false);
        adventurerDef = (AdventurerDef)entityDef;
        
        leashRange = adventurerDef.leashRange;
        returnToSpawn = adventurerDef.returnToSpawn;
        
        idleTimeMin = adventurerDef.idleTimeRange.x;
        idleTimeMax = adventurerDef.idleTimeRange.y;
        wanderTimeMin = adventurerDef.wanderTimeRange.x;
        wanderTimeMax = adventurerDef.wanderTimeRange.y;
        
        spawnPoint = transform.position;
        stateTimer = null;
        hitCooldownTimer = 0f;
        previousState = AdventurerState.Idle;
        currentXP = 0f;
        currentRank = 1;

        // Initialize health
        if (health != null && adventurerDef.baseHealth > 0f)
        {
            health.Init(adventurerDef.baseHealth, fullHP: true);
            health.OnDeath += OnDeath;
            health.OnDamaged += OnHit;
        }

        // Initialize combat
        if (combat != null)
        {
            combat.Init(adventurerDef.combatConfig, Stats, this, layerIndex, spawnPoint);
            combat.OnTargetAcquired += OnCombatTargetAcquired;
            combat.OnTargetLost += OnCombatTargetLost;
            combat.OnDamageDealt += OnCombatDamageDealt;
        }
        
        ChangeState(adventurerDef.startingState);
    }

    protected override void Update()
    {
        base.Update();

        if (State != AdventurerState.Dead && State != AdventurerState.Hit)
            UpdateMovementSmooth();

        // Tick hit stagger cooldown
        if (hitCooldownTimer > 0f)
            hitCooldownTimer -= Time.deltaTime;
    }

    // ═════════════════════════════════════════════
    // TRANSITION GUARD
    // ═════════════════════════════════════════════

    protected override bool CanTransition(AdventurerState from, AdventurerState to)
    {
        // Can't do anything while dead except revive back to Idle
        if (from == AdventurerState.Dead && to != AdventurerState.Idle)
            return false;

        return true;
    }

    // ═════════════════════════════════════════════
    // HEALTH HANDLING
    // ═════════════════════════════════════════════
    
    private void OnDeath(float overkill)
    {
        if (showDebugLogs)
            Debug.Log($"[{name}] Adventurer died! Overkill: {overkill}");

        // Cancel any active hit stagger
        if (hitCoroutine != null)
        {
            StopCoroutine(hitCoroutine);
            hitCoroutine = null;
        }

        ChangeState(AdventurerState.Dead);
    }

    private void OnHit(float damage, float currentHP, float maxHP)
    {
        // Skip if dead, already staggering, or on cooldown
        if (State == AdventurerState.Dead || State == AdventurerState.Hit)
            return;
        if (hitCooldownTimer > 0f)
            return;
         if (currentHP <= 0f)
            return;

        hitCooldownTimer = adventurerDef.hitStaggerCooldown; // Start cooldown immediately
        ChangeState(AdventurerState.Hit);
    }

    // ═════════════════════════════════════════════
    // COMBAT EVENT HANDLERS
    // ═════════════════════════════════════════════
    
    private void OnCombatTargetAcquired(GameObject target)
    {
        if (showDebugLogs)
            Debug.Log($"[{name}] Acquired target: {target.name}");
        
        if (State == AdventurerState.Idle || State == AdventurerState.Wander)
            ChangeState(AdventurerState.Seek);
    }

    private void OnCombatTargetLost()
    {
        if (showDebugLogs)
            Debug.Log($"[{name}] Lost target");
        
        if (State == AdventurerState.Seek || State == AdventurerState.Attack)
            ChangeState(AdventurerState.Idle);
    }

    // ═════════════════════════════════════════════
    // STATE MACHINE
    // ═════════════════════════════════════════════

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
                    SetTarget(spawnPoint);
                break;

            case AdventurerState.Wander:
                SetTarget(GetWanderPosition(wanderArea));
                
                stateTargetTime = Random.Range(wanderTimeMin, wanderTimeMax);
                stateTimer = new CountdownTimer(stateTargetTime);
                stateTimer.Start();
                break;

            case AdventurerState.Seek:
                if (combat != null && combat.HasTarget)
                    SetTarget(combat.CurrentTarget.transform.position);
                break;

            case AdventurerState.Attack:
                targetPos = null;
                break;

            case AdventurerState.Hit:
                targetPos = null;
                hitCoroutine = StartCoroutine(HitStaggerRoutine());
                break;

            case AdventurerState.Dead:
                targetPos = null;
                combat?.ReleaseTarget();
                deathCoroutine = StartCoroutine(DeathReviveRoutine());
                break;
        }
    }

    protected override void OnExitState(AdventurerState oldState)
    {
        // Capture previous state for hit recovery (before it changes)
        if (oldState != AdventurerState.Hit && oldState != AdventurerState.Dead)
            previousState = oldState;
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

            // Hit and Dead are coroutine-driven, no update logic needed
        }
    }

    // ═════════════════════════════════════════════
    // HIT STAGGER
    // ═════════════════════════════════════════════

    private IEnumerator HitStaggerRoutine()
    {
        // Play hit animation
        if (animator != null)
            animator.SetTrigger(AnimHash.Damage);

        yield return new WaitForSeconds(adventurerDef.hitStaggerDuration);

        // Start cooldown
        hitCoroutine = null;

        // Return to previous state (default to Idle if previous was invalid)
        AdventurerState returnState = previousState;
        if (returnState == AdventurerState.Seek || returnState == AdventurerState.Attack)
        {
            // Only return to combat states if target is still valid
            if (combat == null || !combat.IsTargetValid())
                returnState = AdventurerState.Idle;
        }
        
        ChangeState(returnState);
    }

    // ═════════════════════════════════════════════
    // DEATH & REVIVE
    // ═════════════════════════════════════════════

    private IEnumerator DeathReviveRoutine()
    {
        // Disable interactions
        if (col != null)
            col.enabled = false;

        if (animator != null)
        {
            animator.ResetTrigger(AnimHash.Damage);
            animator.SetBool(AnimHash.Dead, true);
        }

        deathEffect?.SetActive(true);

        if (showDebugLogs)
            Debug.Log($"[{name}] Playing death animation, reviving in {adventurerDef.reviveDelay}s");

        // Wait for revive
        yield return new WaitForSeconds(adventurerDef.reviveDelay);

        // Revive
        if (health != null)
            health.Revive(adventurerDef.baseHealth);

        // Re-enable interactions
        if (col != null)
            col.enabled = true;

        // Reset cooldowns
        hitCooldownTimer = 0f;
        deathCoroutine = null;

        deathEffect?.SetActive(false);

        if (showDebugLogs)
            Debug.Log($"[{name}] Revived!");

        animator.SetBool(AnimHash.Dead, false);
        ChangeState(AdventurerState.Idle);
    }

    // ═════════════════════════════════════════════
    // STATE UPDATE METHODS
    // ═════════════════════════════════════════════

    private void UpdateIdle()
    {
        stateTimer.Tick(TickDelta);
        
        if (combat != null)
        {
            var target = combat.ScanForTarget(transform.position, Stats.ScanRange);
            if (target != null)
            {
                combat.SetTarget(target);
                return;
            }
        }
        
        if (stateTimer.IsFinished)
            ChangeState(AdventurerState.Wander);
    }

    private void UpdateWander()
    {
        stateTimer.Tick(TickDelta);
        
        if (combat != null)
        {
            var target = combat.ScanForTarget(transform.position, Stats.ScanRange);
            if (target != null)
            {
                combat.SetTarget(target);
                return;
            }
        }
        
        if (!targetPos.HasValue)
        {
            ChangeState(AdventurerState.Idle);
            return;
        }
        
        if (stateTimer.IsFinished)
            ChangeState(AdventurerState.Idle);
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

        if (leashRange > 0f && Vector3.Distance(transform.position, spawnPoint) > leashRange)
        {
            if (showDebugLogs)
                Debug.Log($"[{name}] Outside leash range, returning to Idle");
            ChangeState(AdventurerState.Idle);
            return;
        }

        SetTarget(combat.CurrentTarget.transform.position);

        if (combat.IsInAttackRange(transform.position))
            ChangeState(AdventurerState.Attack);
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

        if (combat.IsOutOfChaseRange(transform.position))
        {
            if (showDebugLogs)
                Debug.Log($"[{name}] Target out of chase range, seeking again");
            ChangeState(AdventurerState.Seek);
            return;
        }

        Vector3 directionToTarget = combat.CurrentTarget.transform.position - transform.position;
        FaceDirection(directionToTarget.x);

        combat.UpdateAttack(TickDelta, attackAnimDelay: 0.2f);
    }

    // ═════════════════════════════════════════════
    // XP & RANK
    // ═════════════════════════════════════════════

    /// <summary>Accumulates XP. Does not auto-promote — call Promote() explicitly.</summary>
    public void GainXP(float amount)
    {
        if (amount <= 0f) return;
        currentXP += amount;
        GameSignals.RaiseAdventurerXPChanged(this, currentXP);
    }

    /// <summary>
    /// Applies one rank-up: boosts damage, attack speed, and max HP permanently,
    /// increments rank, resets XP, and fires OnAdventurerPromoted.
    /// Returns false if CanPromote is false or the adventurer is not alive.
    /// </summary>
    public bool Promote()
    {
        if (!CanPromote) return false;
        if (health == null || !health.IsAlive) return false;

        // Permanent damage bonus — ID range 1001–1004 (one per rank transition)
        Stats.Mediator.AddModifier(new BasicStatModifier(
            StatType.AttackDamage,
            1000 + currentRank,
            -1f,
            v => v * (1f + adventurerDef.rankDamageMultiplier)
        ));

        // Permanent attack speed bonus (reduces interval = faster attacks) — ID range 2001–2004
        Stats.Mediator.AddModifier(new BasicStatModifier(
            StatType.AttackSpeed,
            2000 + currentRank,
            -1f,
            v => v * (1f - adventurerDef.rankAttackSpeedMultiplier)
        ));

        // Max HP bonus applied directly to Health (Health is decoupled from Stats)
        float hpRatio = health.HealthPercent;
        float newMaxHP = health.MaxHP * (1f + adventurerDef.rankHPMultiplier);
        health.Init(newMaxHP, fullHP: false);
        health.SetHP(newMaxHP * hpRatio);

        int prevRank = currentRank;
        currentRank++;
        currentXP = 0f;

        GameSignals.RaiseAdventurerPromoted(this, $"Rank {prevRank}", $"Rank {currentRank}");
        return true;
    }

    /// <summary>Subscribed to combat.OnDamageDealt — awards XP on kill.</summary>
    private void OnCombatDamageDealt(GameObject target, float damageDealt)
    {
        if (damageDealt <= 0f || target == null) return;
        if (!target.TryGetComponent<Health>(out var targetHealth)) return;
        if (targetHealth.IsAlive) return; // not a kill

        if (!target.TryGetComponent<EntityBase>(out var entity)) return;
        if (!(entity.def is MobDef mobDef)) return;

        float xp = mobDef.baseXPReward * mobDef.hpMultiplierByLayer.Evaluate(layerIndex);
        GainXP(xp);
    }

    // ═════════════════════════════════════════════
    // CLEANUP
    // ═════════════════════════════════════════════

    public override void Despawn()
    {
        if (health != null)
        {
            health.OnDeath -= OnDeath;
            health.OnDamaged -= OnHit;
        }
        
        if (combat != null)
        {
            combat.OnTargetAcquired -= OnCombatTargetAcquired;
            combat.OnTargetLost -= OnCombatTargetLost;
            combat.OnDamageDealt -= OnCombatDamageDealt;
            combat.ReleaseTarget();
        }

        // Cancel any active coroutines
        if (hitCoroutine != null)
        {
            StopCoroutine(hitCoroutine);
            hitCoroutine = null;
        }
        if (deathCoroutine != null)
        {
            StopCoroutine(deathCoroutine);
            deathCoroutine = null;
        }
        
        base.Despawn();
    }

    // ═════════════════════════════════════════════
    // DEBUG GIZMOS
    // ═════════════════════════════════════════════

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