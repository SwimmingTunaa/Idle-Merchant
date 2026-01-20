/// <summary>
/// Interface for entities that can take damage and be healed.
/// Return values indicate actual damage/healing applied (after reductions, caps, etc.)
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// Apply damage to this entity.
    /// </summary>
    /// <param name="amount">Raw damage amount</param>
    /// <returns>Actual damage applied (0 if invulnerable or dead)</returns>
    float OnDamage(float amount);

    /// <summary>
    /// Heal this entity.
    /// </summary>
    /// <param name="amount">Raw healing amount</param>
    /// <returns>Actual healing applied (0 if dead or at max HP)</returns>
    float OnHeal(float amount);

    /// <summary>
    /// Check if entity is currently alive.
    /// </summary>
    bool IsAlive { get; }
}