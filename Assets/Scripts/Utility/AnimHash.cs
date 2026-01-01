using UnityEngine;

/// <summary>
/// Centralized animator parameter hash cache.
/// Use these instead of string literals for better performance and compile-time safety.
/// Add new animator parameters here as you create them.
/// </summary>
public static class AnimHash
{
    // ===== GENERAL PARAMETERS =====
    public static readonly int Velocity = Animator.StringToHash("Velocity");
    public static readonly int Dead = Animator.StringToHash("Dead");
    public static readonly int Damage = Animator.StringToHash("Damage");
    public static readonly int Special = Animator.StringToHash("Special");
    
    // ===== COMBAT PARAMETERS =====
    public static readonly int Attack = Animator.StringToHash("Attack");
    
    // ===== PORTER PARAMETERS =====
    public static readonly int PickUp = Animator.StringToHash("PickUp");
    public static readonly int Deposit = Animator.StringToHash("Deposit");
    public static readonly int Climb = Animator.StringToHash("Climb");
    
    // ===== STATE BOOLS (Optional - if using bool parameters instead of state machine) =====
    public static readonly int IsIdle = Animator.StringToHash("IsIdle");
    public static readonly int IsMoving = Animator.StringToHash("IsMoving");
    
    // ===== USAGE EXAMPLES =====
    // animator.SetTrigger(AnimHash.PickUp);
    // animator.SetFloat(AnimHash.Velocity, speed);
    // animator.SetBool(AnimHash.IsIdle, true);
}