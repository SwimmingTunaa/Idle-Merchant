using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Reusable combat component for entities.
/// Handles target scanning, attack timing, damage dealing, and range validation.
/// Can be added to any entity (Mobs, Adventurers, Customers, etc.) for combat capability.
/// </summary>
public class CombatBehavior : MonoBehaviour
{
    // ===== EVENTS =====
    
    /// <summary>Fired when a target is acquired. Args: (target)</summary>
    public event Action<GameObject> OnTargetAcquired;
    
    /// <summary>Fired when target is lost/invalid. No args.</summary>
    public event Action OnTargetLost;
    
    /// <summary>Fired when attack animation should play. No args.</summary>
    public event Action OnAttackAnimationTrigger;
    
    /// <summary>Fired when damage is dealt. Args: (target, damageDealt)</summary>
    public event Action<GameObject, float> OnDamageDealt;

    // ===== CONFIGURATION =====
    
    [Header("Combat Config")]
    [SerializeField] private CombatConfig config;
    
    [Header("References")]
    [SerializeField] private Animator animator;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // ===== STATE =====
    
    private GameObject currentTarget;
    private CyclicTimer attackTimer;
    private Stats stats; // Reference to entity's stats
    private IEntity selfEntity; // Reference to self for entity type checks
    private int layerIndex;
    private Vector3 spawnPoint;
    private Coroutine damageCoroutine;
    
    // Track if we've been damaged (for Defensive behavior)
    private bool hasBeenDamaged = false;

    // ===== PROPERTIES =====
    
    public GameObject CurrentTarget => currentTarget;
    public bool HasTarget => currentTarget != null;
    public CombatConfig Config => config;
    public bool CanAttack => config.canAttack;

    // ===== INITIALIZATION =====
    
    /// <summary>
    /// Initialize combat behavior.
    /// Call this from entity's Init() method.
    /// </summary>
    public void Init(CombatConfig combatConfig, Stats entityStats, IEntity entity, int layer, Vector3 spawn)
    {
        config = combatConfig;
        stats = entityStats;
        selfEntity = entity;
        layerIndex = layer;
        spawnPoint = spawn;
        
        if (config.canAttack)
        {
            attackTimer = new CyclicTimer(stats.AttackInterval);
        }
        
        currentTarget = null;
        hasBeenDamaged = false;
        
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    // ===== TARGET ACQUISITION =====
    
    /// <summary>
    /// Scan for a valid target based on combat configuration.
    /// Returns null if no valid targets found.
    /// </summary>
    public GameObject ScanForTarget(Vector3 scanPosition, float scanRange)
    {
        if (!config.canAttack) return null;
        
        // Defensive mobs don't scan unless they've been damaged
        if (config.behaviorType == CombatBehaviorType.Defensive && !hasBeenDamaged)
            return null;
        
        // Territorial uses custom radius instead of scan range
        if (config.behaviorType == CombatBehaviorType.Territorial)
        {
            scanRange = config.territorialRadius;
        }
        
        // Find all entities within range
        Collider2D[] hits = Physics2D.OverlapCircleAll(scanPosition, scanRange);
        
        GameObject closestTarget = null;
        float closestDistance = float.MaxValue;
        
        foreach (var hit in hits)
        {
            // Skip self
            if (hit.gameObject == gameObject) continue;
            
            // Check if it's a valid target
            if (!IsValidTarget(hit.gameObject)) continue;
            
            // Track closest
            float distance = Vector2.Distance(scanPosition, hit.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = hit.gameObject;
            }
        }
        
        if (closestTarget != null && showDebugLogs)
        {
            Debug.Log($"[{name}] Found target: {closestTarget.name}");
        }
        
        return closestTarget;
    }

    /// <summary>
    /// Set current target manually (useful for revenge targeting after being hit).
    /// </summary>
    public void SetTarget(GameObject target)
    {
        if (currentTarget != target)
        {
            currentTarget = target;
            
            if (target != null)
            {
                OnTargetAcquired?.Invoke(target);
            }
            else
            {
                OnTargetLost?.Invoke();
            }
        }
    }

    /// <summary>
    /// Release current target.
    /// </summary>
    public void ReleaseTarget()
    {
        if (currentTarget != null)
        {
            if (showDebugLogs)
                Debug.Log($"[{name}] Released target: {currentTarget.name}");
            
            currentTarget = null;
            OnTargetLost?.Invoke();
        }
    }

    /// <summary>
    /// Check if current target is still valid.
    /// </summary>
    public bool IsTargetValid()
    {
        if (currentTarget == null) return false;
        if (!currentTarget.activeInHierarchy) return false;
        
        // Check if target is still alive
        if (currentTarget.TryGetComponent<Health>(out var health))
        {
            if (!health.IsAlive) return false;
        }
        
        return true;
    }

    /// <summary>
    /// Check if target is within attack range.
    /// </summary>
    public bool IsInAttackRange(Vector3 position)
    {
        if (currentTarget == null) return false;
        
        float distance = Vector3.Distance(position, currentTarget.transform.position);
        return distance <= stats.AttackRange;
    }

    /// <summary>
    /// Check if target has moved beyond chase break range.
    /// </summary>
    public bool IsOutOfChaseRange(Vector3 position)
    {
        if (currentTarget == null) return false;
        
        float distance = Vector3.Distance(position, currentTarget.transform.position);
        return distance > stats.ChaseBreakRange;
    }

    // ===== ATTACK EXECUTION =====
    
    /// <summary>
    /// Update attack timer and execute attack when ready.
    /// Call this every frame when in attack state.
    /// Returns true if attack was executed this frame.
    /// </summary>
    public bool UpdateAttack(float deltaTime, float attackAnimDelay = 0.2f)
    {
        if (!config.canAttack || currentTarget == null) return false;
        
        attackTimer.SetInterval(stats.AttackInterval);
        attackTimer.Tick(deltaTime);
        
        if (attackTimer.TryConsumeCycle())
        {
            ExecuteAttack(attackAnimDelay);
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// Execute attack: trigger animation, then deal damage after delay.
    /// </summary>
    private void ExecuteAttack(float damageDelay)
    {
        // Trigger animation
        if (animator != null)
        {
            animator.SetTrigger(AnimHash.Slash);
        }
        
        OnAttackAnimationTrigger?.Invoke();
        
        // Deal damage after animation delay
        if (damageCoroutine != null)
        {
            StopCoroutine(damageCoroutine);
        }
        damageCoroutine = StartCoroutine(DealDamageAfterDelay(damageDelay));
    }

    private IEnumerator DealDamageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (currentTarget == null) yield break;
        
        if (currentTarget.TryGetComponent<Health>(out var targetHealth))
        {
            float damageDealt = targetHealth.OnDamage(stats.AttackDamage);
            
            OnDamageDealt?.Invoke(currentTarget, damageDealt);
            
            if (damageDealt <= 0f)
            {
                // Target died
                ReleaseTarget();
            }
        }
    }

    // ===== DAMAGE RESPONSE =====
    
    /// <summary>
    /// Call this when entity takes damage.
    /// Activates defensive/retaliatory behavior.
    /// </summary>
    public void OnTookDamage(GameObject attacker)
    {
        hasBeenDamaged = true;
        
        // Defensive mobs switch to targeting their attacker
        if (config.behaviorType == CombatBehaviorType.Defensive)
        {
            if (IsValidTarget(attacker))
            {
                SetTarget(attacker);
            }
        }
    }

    // ===== HELPER METHODS =====
    
    /// <summary>
    /// Check if a GameObject is a valid target based on hostility rules.
    /// </summary>
    private bool IsValidTarget(GameObject target)
    {
        if (target == null) return false;
        if (target == gameObject) return false;
        
        // Must have Health component to be damageable
        if (!target.TryGetComponent<Health>(out var health)) return false;
        if (!health.IsAlive) return false;
        
        // Must be IEntity to check type
        if (!target.TryGetComponent<IEntity>(out var entity)) return false;
        
        // Check hostility rules
        return IsHostileTo(entity.EntityType);
    }

    /// <summary>
    /// Check if this entity is hostile to a specific entity type.
    /// </summary>
    private bool IsHostileTo(EntityType targetType)
    {
        return targetType switch
        {
            EntityType.Mob => (config.hostileTo & HostilityTargets.Mobs) != 0,
            EntityType.Adventurer => (config.hostileTo & HostilityTargets.Adventurers) != 0,
            EntityType.Customer => (config.hostileTo & HostilityTargets.Customers) != 0,
            EntityType.Porter => (config.hostileTo & HostilityTargets.Porters) != 0,
            _ => false
        };
    }

    /// <summary>
    /// Reset combat state (useful when returning to idle).
    /// </summary>
    public void ResetCombatState()
    {
        ReleaseTarget();
        hasBeenDamaged = false;
        
        if (attackTimer != null)
        {
            attackTimer.Reset();
        }
    }

    // ===== CLEANUP =====
    
    void OnDestroy()
    {
        if (damageCoroutine != null)
        {
            StopCoroutine(damageCoroutine);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!config.canAttack) return;
        
        // Draw territorial radius (if territorial)
        if (config.behaviorType == CombatBehaviorType.Territorial && config.territorialRadius > 0f)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, config.territorialRadius);
        }
        
        // Draw line to current target
        if (Application.isPlaying && currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
        }
    }
#endif
}
