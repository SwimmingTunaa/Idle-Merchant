// Assets/Scripts/Dungeon/SimpleEnemy.cs
using System;
using System.Collections;
using UnityEngine;

public class EntityBase: MonoBehaviour
{
    protected EntityDef def;
    public int layerIndex = 1;

    [Header("Stats System")]
    public Stats Stats { get; private set; }
    
    // Legacy fields kept for backward compatibility (idle timing is not a stat)
    private Vector2 idleTimeRange;
    protected Collider2D wanderArea;
    
    protected Spawner spawnedFrom;
    protected Vector3? targetPos;
    protected Collider2D col;

    protected SpriteRenderer spriteRenderer;

    //Animation
    [SerializeField] protected Animator animator;

    // Death animation tracking
    protected bool isDying = false;
    private Coroutine deathCoroutine;

    /// <summary>
    /// Check if this entity is currently playing death animation
    /// </summary>
    public bool IsDying => isDying;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        col = GetComponent<Collider2D>();
    }

    public virtual void SetTarget(Vector3 pos)
    {
        targetPos = pos;
    }
    
    public float GetWidth() => spriteRenderer.bounds.size.x;

    public virtual void Init(EntityDef entityDef, int layer, Spawner spawner, Collider2D playArea)
    {
        def = entityDef;
        layerIndex = layer;
        spawnedFrom = spawner;
        wanderArea = playArea;
        
        // Create stats system from EntityDef
        var baseStats = BaseStats.FromEntityDef(entityDef);
        var mediator = new StatsMediator();
        Stats = new Stats(mediator, baseStats);
        
        // Legacy idle timing preserved in idleTimeRange for entities that need it
        idleTimeRange.x = def.idleTimeRange.x;
        idleTimeRange.y = def.idleTimeRange.y;
        targetPos = GetWanderPosition(wanderArea);
        
        // Apply animator controller
        animator.runtimeAnimatorController = entityDef.animatorOverrideController;
        
        // Get sprite renderer
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // Resize collider (uses cached size from EntityDef)
        ResizeCollider(entityDef.colliderSize);

        // Apply unique sorting order to prevent z-fighting
        if (spriteRenderer != null && EntitySortingManager.Instance != null)
        {
            int sortingOrder = EntitySortingManager.Instance.GetNextSortingOrder(def.sortingType);
            spriteRenderer.sortingOrder = sortingOrder;
        }

        // Reset death state
        isDying = false;
    }

    /// <summary>
    /// Resize collider to specified size and offset. If size is zero, uses sprite renderer bounds as fallback.
    /// </summary>
    private void ResizeCollider(Vector2 size)
    {
        BoxCollider2D parentCollider = GetComponent<BoxCollider2D>();
        if (parentCollider == null) return;

        if (size != Vector2.zero)
        {
            // Use cached size and offset from EntityDef
            parentCollider.size = size;
            parentCollider.offset = def.colliderOffset;
        }
        else if (spriteRenderer != null)
        {
            // Fallback: use sprite renderer bounds (may be incorrect if animator hasn't updated)
            parentCollider.size = spriteRenderer.bounds.size;
            Debug.LogWarning($"[{name}] EntityDef.colliderSize not set, using fallback bounds. Right-click EntityDef â†’ 'Auto-Calculate Collider Size From Sprite'");
        }
    }

    // ===== DESPAWN WITH DEATH ANIMATION =====

    /// <summary>
    /// Despawn with optional death animation.
    /// Override this in derived classes for custom death behavior.
    /// </summary>
    public virtual void Despawn()
    {
        // Prevent double-despawn
        if (isDying) return;

        // If entity has death animation, play it first
        if (def != null && def.deathAnimationDuration > 0f && animator != null)
        {
            deathCoroutine = StartCoroutine(DespawnWithDeathAnimation(def.deathAnimationDuration));
        }
        else
        {
            // No death animation, despawn immediately
            DespawnImmediate();
        }
    }

    /// <summary>
    /// Coroutine that plays death animation then despawns
    /// </summary>
    private IEnumerator DespawnWithDeathAnimation(float duration)
    {
        isDying = true;

        // Stop all movement
        targetPos = null;

        // Trigger death animation using AnimHash
        if (animator != null)
        {
            animator.SetTrigger(AnimHash.Dead);
        }

        // Disable collider so entity can't be clicked/targeted
        if (col != null)
        {
            col.enabled = false;
        }

        // Wait for animation
        yield return new WaitForSeconds(duration);

        // Now actually despawn
        DespawnImmediate();
    }

    /// <summary>
    /// Immediate despawn without animation.
    /// Called after death animation completes or if no animation exists.
    /// </summary>
    protected virtual void DespawnImmediate()
    {
        isDying = false;

        // Re-enable collider before returning to pool
        if (col != null)
        {
            col.enabled = true;
        }

        // Cleanup stats (remove all modifiers)
        if (Stats != null)
        {
            Stats.Cleanup();
        }

        if (spawnedFrom != null)
        {
            spawnedFrom.RemoveFromAlive(this.gameObject);
        }

        ObjectPoolManager.Instance.ReturnObjectToPool(this.gameObject);
    }

    /// <summary>
    /// Force immediate despawn, cancelling any death animation.
    /// Use for cleanup or emergency despawns.
    /// </summary>
    public void ForceImmediateDespawn()
    {
        if (deathCoroutine != null)
        {
            StopCoroutine(deathCoroutine);
            deathCoroutine = null;
        }

        DespawnImmediate();
    }

    // ===== HELPER METHODS =====

    public bool CheckDistanceReached(Vector3? targetPos)
    {
        float distance = Vector2.Distance(transform.position, (Vector2)targetPos);
        // Use Stats system for stopDistance (falls back to base value if no modifiers)
        float threshold = Stats != null ? Stats.BaseStats.stopDistance : 0.05f;
        return distance < threshold;
    }

    public Vector3 GetWanderPosition(Collider2D wanderArea)
    {
        float minX = wanderArea.bounds.min.x;
        float maxX = wanderArea.bounds.max.x;
        float xPos = UnityEngine.Random.Range(minX, maxX);
        float yPos = wanderArea.transform.position.y + wanderArea.bounds.extents.y;

        return new Vector3(xPos, yPos);
    } 
}

public static class Collider2DExt
{
    public static void CopyShapeFrom(this Collider2D target, Collider2D source)
    {
        if (target == null || source == null) return;

        switch (source)
        {
            case BoxCollider2D srcBox when target is BoxCollider2D dstBox:
                dstBox.size = srcBox.size;
                dstBox.offset = srcBox.offset;
                break;

            case CircleCollider2D srcCircle when target is CircleCollider2D dstCircle:
                dstCircle.radius = srcCircle.radius;
                dstCircle.offset = srcCircle.offset;
                break;

            case CapsuleCollider2D srcCapsule when target is CapsuleCollider2D dstCapsule:
                dstCapsule.size = srcCapsule.size;
                dstCapsule.offset = srcCapsule.offset;
                dstCapsule.direction = srcCapsule.direction;
                break;

            default:
                Debug.LogWarning($"CopyShapeFrom: collider type {source.GetType().Name} not supported.");
                break;
        }
    }
}