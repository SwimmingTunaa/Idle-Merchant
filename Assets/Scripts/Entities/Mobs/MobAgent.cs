using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public enum MobState
{
    Wander,
    Idle,
    Damaged,
    Seek,      // Added: Moving toward target
    Attack,    // Now functional if combat enabled
}

/// <summary>
/// Mob agent with health and optional combat capability.
/// Combat is enabled via MobDef.combatConfig.
/// Passive mobs: Wander/Idle/Damaged only
/// Hostile mobs: Can Seek/Attack targets
/// </summary>
[RequireComponent(typeof(Health))]
public class MobAgent : EntityStateMachine<MobState>, IEntity
{
    [SerializeField] private Loot lootPrefab;
    [SerializeField] private MobDef mobDef;
    [SerializeField] private float stunTime = 0.5f;
    
    private Health health;
    private CombatBehavior combat;
    private CountdownTimer stunTimer;
    private CountdownTimer idleTimer;
    private CountdownTimer stateTimer; // For timed states like Seek
    
    private const int STUN_MODIFIER_ID = -999;
    private List<AdventurerAgent> reservedBy = new List<AdventurerAgent>();
    
    // IEntity implementation
    public  EntityType EntityType => EntityType.Mob;
    public  bool IsAlive => health != null && health.IsAlive;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    public override void Init(EntityDef entityDef, int layer, Spawner spawner, Collider2D playArea)
    {
        base.Init(entityDef, layer, spawner, playArea);
        mobDef = (MobDef)entityDef;
        
        // Initialize health
        health = GetComponent<Health>();
        if (health == null)
        {
            Debug.LogError($"[MobAgent] Missing Health component on {name}!");
            return;
        }
        
        float mult = mobDef.hpMultiplierByLayer.Evaluate(Mathf.Clamp(layerIndex, 1, 10));
        float maxHP = Mathf.Max(1f, mobDef.baseHealth * mult);
        health.Init(maxHP, fullHP: true);
        
        health.OnDamaged += OnHealthDamaged;
        health.OnDeath += OnHealthDeath;
        
        // Initialize combat if enabled
        combat = GetComponent<CombatBehavior>();
        if (combat != null && mobDef.combatConfig.canAttack)
        {
            combat.Init(mobDef.combatConfig, Stats, this, layerIndex, transform.position);
            combat.OnTargetAcquired += OnCombatTargetAcquired;
            combat.OnTargetLost += OnCombatTargetLost;
        }
        
        stunTime = mobDef.stunTime;
        ChangeState(mobDef.startingState);
        
        reservedBy.Clear();
        if (MobManager.Instance != null)
        {
            MobManager.Instance.RegisterMob(this, layerIndex);
        }
    }

    protected override void Update()
    {
        base.Update();
        UpdateMovementSmooth();
    }

    // ===== HEALTH EVENT HANDLERS =====
    
    private void OnHealthDamaged(float damageApplied, float currentHP, float maxHP)
    {
        animator.SetTrigger(AnimHash.Damage);
        
        if (DamageNumberManager.Instance != null)
        {
            DamageNumberManager.Instance.ShowDamageAtEntity(damageApplied, transform);
        }
        
        // Notify combat system (for defensive mobs)
        if (combat != null)
        {
            // TODO: Track attacker and pass to OnTookDamage
            combat.OnTookDamage(null);
        }
        
        ChangeState(MobState.Damaged);
    }

    private void OnHealthDeath(float overkill)
    {
        Despawn();
    }

    // ===== COMBAT EVENT HANDLERS =====
    
    private void OnCombatTargetAcquired(GameObject target)
    {
        if (showDebugLogs)
            Debug.Log($"[{name}] Acquired combat target: {target.name}");
        
        // Switch to Seek state when target acquired
        if (State != MobState.Damaged) // Don't interrupt stun
        {
            ChangeState(MobState.Seek);
        }
    }

    private void OnCombatTargetLost()
    {
        if (showDebugLogs)
            Debug.Log($"[{name}] Lost combat target");
        
        // Return to idle when target lost
        if (State == MobState.Seek || State == MobState.Attack)
        {
            ChangeState(MobState.Idle);
        }
    }

    // ===== STATE MACHINE =====

    protected override void OnEnterState(MobState state)
    {
        switch (state)
        {
            case MobState.Wander:
                SetTarget(GetWanderPosition(wanderArea));
                break;
                
            case MobState.Idle:
                targetPos = null;
                float idleDuration = Random.Range(def.idleTimeRange.x, def.idleTimeRange.y);
                idleTimer = new CountdownTimer(idleDuration);
                idleTimer.Start();
                break;
                
            case MobState.Damaged:
                // Apply stun
                Stats.Mediator.AddModifier(
                    new BasicStatModifier(StatType.MoveSpeed, STUN_MODIFIER_ID, -1f, v => 0f)
                );
                
                stunTimer = new CountdownTimer(stunTime);
                stunTimer.Start();
                break;
            
            case MobState.Seek:
                if (combat != null && combat.HasTarget)
                {
                    SetTarget(combat.CurrentTarget.transform.position);
                }
                break;
                
            case MobState.Attack:
                targetPos = null; // Stop moving
                break;
        }
    }

    protected override void OnUpdateState(MobState state)
    {
        switch (state)
        {
            case MobState.Wander:
                UpdateWander();
                break;

            case MobState.Idle:
                UpdateIdle();
                break;

            case MobState.Damaged:
                UpdateDamaged();
                break;
            
            case MobState.Seek:
                UpdateSeek();
                break;

            case MobState.Attack:
                UpdateAttack();
                break;
        }
    }

    protected override void OnExitState(MobState state)
    {
        // Cleanup if needed
    }

    // ===== STATE UPDATE METHODS =====

    private void UpdateWander()
    {
        // Scan for targets if aggressive
        if (combat != null && combat.CanAttack)
        {
            var target = combat.ScanForTarget(transform.position, Stats.ScanRange);
            if (target != null)
            {
                combat.SetTarget(target);
                return; // Combat event will trigger state change
            }
        }
        
        if (!targetPos.HasValue)
        {
            ChangeState(MobState.Idle);
        }
    }

    private void UpdateIdle()
    {
        idleTimer.Tick(TickDelta);
        
        // Scan for targets if aggressive
        if (combat != null && combat.CanAttack)
        {
            var target = combat.ScanForTarget(transform.position, Stats.ScanRange);
            if (target != null)
            {
                combat.SetTarget(target);
                return; // Combat event will trigger state change
            }
        }
        
        if (idleTimer.IsFinished)
        {
            ChangeState(MobState.Wander);
        }
    }

    private void UpdateDamaged()
    {
        stunTimer.Tick(TickDelta);
        
        if (stunTimer.IsFinished)
        {
            Stats.Mediator.RemoveModifier(STUN_MODIFIER_ID);
            
            // After stun, check if we have a target from combat system
            if (combat != null && combat.HasTarget && combat.IsTargetValid())
            {
                ChangeState(MobState.Seek);
            }
            else
            {
                ChangeState(MobState.Idle);
            }
        }
    }

    private void UpdateSeek()
    {
        if (combat == null || !combat.IsTargetValid())
        {
            ChangeState(MobState.Idle);
            return;
        }
        
        // Update target position
        SetTarget(combat.CurrentTarget.transform.position);
        
        // Check if in attack range
        if (combat.IsInAttackRange(transform.position))
        {
            ChangeState(MobState.Attack);
        }
    }

    private void UpdateAttack()
    {
        if (combat == null || !combat.IsTargetValid())
        {
            ChangeState(MobState.Idle);
            return;
        }
        
        // Check if target moved out of chase range
        if (combat.IsOutOfChaseRange(transform.position))
        {
            if (showDebugLogs)
                Debug.Log($"[{name}] Target out of chase range, seeking");
            ChangeState(MobState.Seek);
            return;
        }
        
        // Face target
        Vector3 directionToTarget = combat.CurrentTarget.transform.position - transform.position;
        FaceDirection(directionToTarget.x);
        
        // Execute attacks
        combat.UpdateAttack(TickDelta, attackAnimDelay: 0.2f);
    }

    // ===== DESPAWN & LOOT =====
    
    public override void Despawn()
    {
        if (IsDying) return;

        // Unsubscribe from events
        if (health != null)
        {
            health.OnDamaged -= OnHealthDamaged;
            health.OnDeath -= OnHealthDeath;
        }
        
        if (combat != null)
        {
            combat.OnTargetAcquired -= OnCombatTargetAcquired;
            combat.OnTargetLost -= OnCombatTargetLost;
            combat.ReleaseTarget();
        }

        if (MobManager.Instance != null)
        {
            MobManager.Instance.UnregisterMob(this, layerIndex);
        }

        if (mobDef != null && mobDef.deathAnimationDuration > 0f && animator != null)
        {
            StartCoroutine(DieWithAnimation());
        }
        else
        {
            DropLoot();
            base.Despawn();
        }
    }

    private System.Collections.IEnumerator DieWithAnimation()
    {
        if (animator != null)
        {
            animator.SetTrigger(AnimHash.Dead);
        }

        if (col != null)
        {
            col.enabled = false;
        }

        yield return new WaitForSeconds(mobDef.deathAnimationDuration);

        DropLoot();
        DespawnImmediate();
    }

    private void DropLoot()
    {
        if (mobDef == null || mobDef.loot == null) return;

        foreach (var e in mobDef.loot)
        {
            if (UnityEngine.Random.value <= e.chance)
            {
                SpawnLoot(e);
            }
        }
    }

    private void SpawnLoot(ItemDef itemDef)
    {
        if (itemDef == null || itemDef.prefab == null)
        {
            Debug.LogWarning($"[MobAgent] ItemDef or prefab is null, cannot spawn loot!");
            return;
        }

        GameObject lootObj = ObjectPoolManager.Instance.SpawnObject(
            itemDef, 
            transform.position, 
            Quaternion.identity,
            ObjectPoolManager.PoolType.GameObject
        );

        if (lootObj == null)
        {
            Debug.LogError($"[MobAgent] Failed to spawn loot for {itemDef.displayName}");
            return;
        }

        Loot loot = lootObj.GetComponent<Loot>();
        if (loot != null)
        {
            loot.Init(itemDef, mobDef.lootDropAmount, layerIndex);
        }
        else
        {
            Debug.LogError($"[MobAgent] Loot component missing on {itemDef.displayName}!");
        }
    }

    // ===== RESERVATION SYSTEM (for adventurers targeting this mob) =====
    
    public void AddReservation(AdventurerAgent adventurer)
    {
        if (adventurer == null) return;
        if (!reservedBy.Contains(adventurer))
        {
            reservedBy.Add(adventurer);
        }
    }

    public void RemoveReservation(AdventurerAgent adventurer)
    {
        if (adventurer == null) return;
        reservedBy.Remove(adventurer);
    }

    public void ClearReservations()
    {
        reservedBy.Clear();
    }

    public bool IsReserved()
    {
        return reservedBy.Count > 0;
    }

    public bool IsReservedBy(AdventurerAgent adventurer)
    {
        return reservedBy.Contains(adventurer);
    }

    public int GetReservationCount()
    {
        return reservedBy.Count;
    }

    public int GetMaxAttackers()
    {
        return mobDef != null ? mobDef.maxSimultaneousAttackers : 0;
    }
}