// Assets/Scripts/Dungeon/SimpleEnemy.cs
using System.Collections.Generic;
using UnityEngine;

public enum MobState
{
    Wander,
    Idle,
    Damaged,
    Attack,
}

public class MobAgent : EntityStateMachine<MobState>, IDamageable
{
    [SerializeField] private Loot lootPrefab;
    [SerializeField] private MobDef mobDef;
    [SerializeField] private Color damagedColour;
    [SerializeField] private float damageFlashTime = 0.1f;
    
    private float hp;
    private CountdownTimer flashTimer;
    private CountdownTimer stunTimer;
    private CountdownTimer idleTimer;
    private float stunTime; // Keep as config reference
    
    // Stun modifier ID for cleanup
    private const int STUN_MODIFIER_ID = -999;

    // Reservation tracking - now supports multiple adventurers
    private List<AdventurerAgent> reservedBy = new List<AdventurerAgent>();

    public override void Init(EntityDef entityDef, int layer, Spawner spawner, Collider2D playArea)
    {
        base.Init(entityDef, layer, spawner, playArea);
        mobDef = (MobDef)entityDef;
        float mult = mobDef.hpMultiplierByLayer.Evaluate(Mathf.Clamp(layerIndex, 1, 10));
        hp = Mathf.Max(1f, mobDef.baseHealth * mult);
        stunTime = mobDef.stunTime;
        ChangeState(mobDef.startingState);
        SetFlashColour();
        ResetFlash();
        
        // Register with MobManager
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

    public float OnDamage(float dmg)
    {
        if (hp <= 0f) return 0f;
        float applied = Mathf.Min(dmg, hp);
        hp -= applied;
        animator.SetTrigger(AnimHash.Damage);
        Flash();
        
        // Show damage number
        if (DamageNumberManager.Instance != null)
        {
            DamageNumberManager.Instance.ShowDamageAtEntity(applied, transform);
        }
        
        ChangeState(MobState.Damaged);
        if (hp <= 0f)
            Despawn();

        return applied;
    }

    private void SetFlashColour()
    {
        spriteRenderer.material.SetColor("_Blend_Colour", damagedColour);

    }

    private void Flash()
    {
        if (spriteRenderer != null && spriteRenderer.material != null)
        {
            spriteRenderer.material.SetFloat("_Blend_Amount", 1f);
        }
    }

    private void ResetFlash()
    {
        if (spriteRenderer != null && spriteRenderer.material != null)
        {
            spriteRenderer.material.SetFloat("_Blend_Amount", 0f);
        }
    }

    public override void Despawn()
    {
        // Prevent double-despawn
        if (IsDying) return;

        // Unregister from MobManager IMMEDIATELY (prevent targeting corpse)
        if (MobManager.Instance != null)
        {
            MobManager.Instance.UnregisterMob(this, layerIndex);
        }

        // If has death animation, play it then drop loot
        if (mobDef != null && mobDef.deathAnimationDuration > 0f && animator != null)
        {
            StartCoroutine(DieWithAnimation());
        }
        else
        {
            // No animation, drop loot immediately
            DropLoot();
            base.Despawn();
        }
    }

    /// <summary>
    /// Death animation sequence: anim â†’ loot â†’ pool return
    /// </summary>
    private System.Collections.IEnumerator DieWithAnimation()
    {
        // Mark as dying (prevents targeting/damage)
        // Base class handles this via isDying flag

        // Trigger death animation
        if (animator != null)
        {
            animator.SetTrigger(AnimHash.Dead);
        }

        // Disable collider (can't be clicked/targeted)
        if (col != null)
        {
            col.enabled = false;
        }

        // Wait for animation
        yield return new WaitForSeconds(mobDef.deathAnimationDuration);

        // Drop loot at end of death animation (feels better than instant)
        DropLoot();

        // Now despawn
        DespawnImmediate();
    }

    /// <summary>
    /// Roll loot table and spawn drops
    /// </summary>
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

    // UPDATED: Now uses ObjectPoolManager instead of Instantiate
    private void SpawnLoot(ItemDef itemDef)
    {
        if (itemDef == null || itemDef.prefab == null)
        {
            Debug.LogWarning($"[MobAgent] ItemDef or prefab is null, cannot spawn loot!");
            return;
        }

        // Spawn through object pool
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

    // ===== RESERVATION SYSTEM =====

    /// <summary>
    /// Add an adventurer to the reservation list.
    /// Called by MobManager.
    /// </summary>
    public void AddReservation(AdventurerAgent adventurer)
    {
        if (adventurer == null) return;
        if (!reservedBy.Contains(adventurer))
        {
            reservedBy.Add(adventurer);
        }
    }

    /// <summary>
    /// Remove an adventurer from the reservation list.
    /// Called by MobManager.
    /// </summary>
    public void RemoveReservation(AdventurerAgent adventurer)
    {
        if (adventurer == null) return;
        reservedBy.Remove(adventurer);
    }

    /// <summary>
    /// Clear all reservations.
    /// Called by MobManager during cleanup.
    /// </summary>
    public void ClearReservations()
    {
        reservedBy.Clear();
    }

    /// <summary>
    /// Check if this mob is currently reserved by any adventurer
    /// </summary>
    public bool IsReserved()
    {
        return reservedBy.Count > 0;
    }

    /// <summary>
    /// Check if this mob is reserved by a specific adventurer
    /// </summary>
    public bool IsReservedBy(AdventurerAgent adventurer)
    {
        return reservedBy.Contains(adventurer);
    }

    /// <summary>
    /// Get how many adventurers are currently targeting this mob
    /// </summary>
    public int GetReservationCount()
    {
        return reservedBy.Count;
    }

    /// <summary>
    /// Get max attackers allowed for this mob type.
    /// Returns value from MobDef, or 0 to use MobManager default.
    /// </summary>
    public int GetMaxAttackers()
    {
        return mobDef != null ? mobDef.maxSimultaneousAttackers : 0;
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
                
                // Create idle timer from EntityDef idle time range
                float idleDuration = Random.Range(def.idleTimeRange.x, def.idleTimeRange.y);
                idleTimer = new CountdownTimer(idleDuration);
                idleTimer.Start();
                break;
            case MobState.Damaged:
                // Apply stun via stat modifier (sets speed to 0)
                Stats.Mediator.AddModifier(
                    new BasicStatModifier(StatType.MoveSpeed, STUN_MODIFIER_ID, -1f, v => 0f)
                );
                
                flashTimer = new CountdownTimer(damageFlashTime);
                flashTimer.Start();
                
                stunTimer = new CountdownTimer(stunTime);
                stunTimer.Start();
                break;
            case MobState.Attack:
                break;
            default:
                break;
        }
    }

    protected override void OnUpdateState(MobState state)
    {
        switch (state)
        {
            case MobState.Wander:
                if (!targetPos.HasValue) // Reached destination
                {
                    ChangeState(MobState.Idle);
                }
                break;

            case MobState.Idle:
                idleTimer.Tick(TickDelta);
                if (idleTimer.IsFinished)
                {
                    ChangeState(MobState.Wander);
                }
                break;

            case MobState.Damaged:
                stunTimer.Tick(TickDelta);
                flashTimer.Tick(TickDelta);
                
                // Update damage flash using Progress (1 = start, 0 = end)
                float flashAmount = Mathf.Lerp(1, 0, flashTimer.Progress);
                spriteRenderer.material.SetFloat("_Blend_Amount", flashAmount);
                
                // Check if stun time is over
                if (stunTimer.IsFinished)
                {
                    spriteRenderer.material.SetFloat("_Blend_Amount", 0f);
                    
                    // Remove stun modifier to restore speed
                    Stats.Mediator.RemoveModifier(STUN_MODIFIER_ID);
                    
                    ChangeState(MobState.Idle);
                }
                break;

            case MobState.Attack:
                break;

            default:
                break;
        }
    }

    protected override void OnExitState(MobState state)
    {
        // Cleanup if needed
    }
}