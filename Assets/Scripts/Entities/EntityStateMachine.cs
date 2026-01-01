using System;
using UnityEngine;

/// <summary>
/// Optimized state machine with staggered updates.
/// Instead of 60 updates/sec per entity, runs at 10 updates/sec with random offset.
/// Reduces CPU load by ~80% while maintaining responsive gameplay.
/// </summary>
public abstract class EntityStateMachine<TState> : EntityBase where TState : Enum
{
    
    public TState State { get; private set; }
    public event Action<TState, TState> OnStateChanged;

    // Staggered update optimization
    private const float UPDATE_INTERVAL = 0.1f; // 10 updates/sec
    private float updateTimer;
    private float updateOffset; // Random offset prevents all entities updating same frame

    // Amount of real time elapsed since the last AI tick
    protected float TickDelta { get; private set; } = UPDATE_INTERVAL;

    protected virtual void OnEnterState(TState newState) { }
    protected virtual void OnUpdateState(TState currentState) { }
    protected virtual void OnExitState (TState oldState) { }
    protected virtual bool CanTransition(TState from, TState to) => true;

    protected virtual void Start()
    {
        // Random offset spreads updates across frames
        // If you have 50 entities, only ~5 update per frame instead of all 50
        updateOffset = UnityEngine.Random.Range(0f, UPDATE_INTERVAL);
        updateTimer = updateOffset;
    }

    protected virtual void Update()
    {
        updateTimer += Time.deltaTime;
        animator.SetFloat(AnimHash.Velocity, targetPos.HasValue ? 1f : 0f);
        
        // Update stats system (timers, passive modifiers)
        if (Stats != null)
        {
            Stats.Update(Time.deltaTime);
        }
        
        if (updateTimer >= UPDATE_INTERVAL)
        {
            // Store how much real time has passed since last logic tick
            TickDelta = updateTimer;
            
            updateTimer -= UPDATE_INTERVAL; // Preserve fractional time
            OnUpdateState(State);
        }
    }

    public void ChangeState(TState next)
    {
        if (!CanTransition(State, next)) return;

        var prev = State;
        OnExitState(prev);
        State = next;
        OnEnterState(next);
        OnStateChanged?.Invoke(prev, next);
    }

    /// <summary>
    /// Call this for movement that needs to happen every frame (smooth visuals).
    /// State logic runs at 10fps, but movement can still be smooth at 60fps.
    /// </summary>
    protected void UpdateMovementSmooth()
    {
        if (!targetPos.HasValue) return;

        var pos = transform.position;
        var delta = targetPos.Value - pos;
        delta.z = 0f;

        if (CheckDistanceReached(targetPos))
        {
            transform.position = targetPos.Value;
            targetPos = null;
            return;
        }

        FaceDirection(delta.x);
        // Use Stats.MoveSpeed for modified speed value
        float currentSpeed = Stats != null ? Stats.MoveSpeed : 1f;
        transform.position += currentSpeed * Time.deltaTime * delta.normalized;
    }

    /// <summary>
    /// Standard movement update (runs at staggered rate).
    /// Use this if you don't need 60fps smoothness.
    /// </summary>
    protected void UpdateMovement()
    {
        if (!targetPos.HasValue) return;

        var pos = transform.position;
        var delta = targetPos.Value - pos;
        delta.z = 0f;
        if (CheckDistanceReached(targetPos))
        {
            transform.position = targetPos.Value;
            targetPos = null;
            return;
        }

        FaceDirection(delta.x);
        
        // Account for staggered updates - move further per update
        float currentSpeed = Stats != null ? Stats.MoveSpeed : 1f;
        float moveAmount = currentSpeed * UPDATE_INTERVAL;
        transform.position += moveAmount * delta.normalized;
    }

    protected void FaceDirection(float directionX)
    {
        if (spriteRenderer == null) return;
        if (Mathf.Abs(directionX) < 0.01f) return;

        if (directionX > 0)
            spriteRenderer.flipX = false;
        else if (directionX < 0)
            spriteRenderer.flipX = true;
    }
}