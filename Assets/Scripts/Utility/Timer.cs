using UnityEngine;

/// <summary>
/// Base timer class providing common timing functionality.
/// All timers can be paused/resumed and provide progress tracking.
/// </summary>
public abstract class Timer
{
    protected float time;
    protected float initialTime;
    public bool IsRunning { get; protected set; }

    public float Progress => Mathf.Clamp01(time / Mathf.Max(0.001f, initialTime));

    public void Tick(float deltaTime)
    {
        if (IsRunning)
            time += deltaTime;
    }

    public void Start() => IsRunning = true;
    public void Stop() => IsRunning = false;
    public void Resume() => IsRunning = true;
    public void Pause() => IsRunning = false;

    public abstract void Reset();
    public abstract void Reset(float newTime);
}

/// <summary>
/// Timer that counts down from a duration to zero.
/// Common use: ability cooldowns, state timeouts, temporary effects.
/// </summary>
public class CountdownTimer : Timer
{
    public float RemainingTime => Mathf.Max(0f, initialTime - time);
    public bool IsFinished => time >= initialTime;

    public CountdownTimer(float value)
    {
        initialTime = value;
        time = 0f;
        IsRunning = false;
    }

    public override void Reset()
    {
        time = 0f;
    }

    public override void Reset(float newTime)
    {
        initialTime = newTime;
        time = 0f;
    }
}

/// <summary>
/// Timer that counts up from zero indefinitely.
/// Common use: tracking total elapsed time, session duration.
/// </summary>
public class StopwatchTimer : Timer
{
    public float ElapsedTime => time;

    public StopwatchTimer()
    {
        initialTime = 0f;
        time = 0f;
        IsRunning = false;
    }

    public override void Reset()
    {
        time = 0f;
    }

    public override void Reset(float newTime)
    {
        time = 0f;
        initialTime = newTime;
    }
}

/// <summary>
/// Timer that cycles repeatedly at a fixed interval.
/// Preserves overflow for smooth timing even when interval changes.
/// Perfect for attack timers, ability cooldowns, spawn intervals.
/// 
/// Key feature: When attack speed increases mid-cycle, the overflow is preserved.
/// Example: 0.8s into a 1s attack → speed doubles → next attack at 0.3s (not 0.5s).
/// </summary>
public class CyclicTimer : Timer
{
    private float interval;
    
    public float Interval => interval;
    public float TimeUntilNextCycle => Mathf.Max(0f, interval - time);
    public bool IsCycleComplete => time >= interval;

    /// <summary>
    /// Create a cyclic timer that auto-starts.
    /// </summary>
    public CyclicTimer(float interval)
    {
        this.interval = interval;
        this.initialTime = interval;
        time = 0f;
        IsRunning = true; // Auto-start for convenience
    }

    /// <summary>
    /// Check if a cycle has completed. If so, preserve overflow and return true.
    /// Call this in your update loop to trigger cycle-based events.
    /// </summary>
    public bool TryConsumeCycle()
    {
        if (time >= interval)
        {
            time -= interval; // Preserve overflow
            return true;
        }
        return false;
    }

    /// <summary>
    /// Consume all completed cycles and return the count.
    /// Useful for multi-hit attacks or burst spawns.
    /// </summary>
    public int ConsumeCycles()
    {
        if (time < interval)
            return 0;

        int cycles = Mathf.FloorToInt(time / interval);
        time -= cycles * interval; // Preserve fractional overflow
        return cycles;
    }

    /// <summary>
    /// Change the interval dynamically (e.g., when attack speed is buffed).
    /// Preserves current progress - does NOT reset timer.
    /// </summary>
    public void SetInterval(float newInterval)
    {
        interval = Mathf.Max(0.001f, newInterval);
        initialTime = interval;
    }

    public override void Reset()
    {
        time = 0f;
    }

    public override void Reset(float newInterval)
    {
        interval = Mathf.Max(0.001f, newInterval);
        initialTime = interval;
        time = 0f;
    }
}