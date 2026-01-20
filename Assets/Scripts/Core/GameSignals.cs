using System;
using UnityEngine;

/// <summary>
/// Global event system. Skills fire signals, managers listen.
/// Decouples systems without singleton dependencies.
/// </summary>
public static class GameSignals
{
    // Economy
    public static event Action<int> OnGoldEarned;
    public static event Action<int> OnGoldSpent;
    public static event Action<int> GoldChanged;                    // Existing - fired by Inventory
    
    // Loot & Crafting (Existing events)
    public static event Action<ResourceStack> LootCollected;        // from dungeon/porter/click
    public static event Action<ResourceStack> ProductCrafted;       // artisan → shelf

    // Combat
    public static event Action<GameObject, float, GameObject> OnDamageTaken;
    public static event Action<GameObject, GameObject> OnEntityDeath;

    // Skills (Phase 2)
    public static event Action<EntityBase, string> OnSkillActivated;
    public static event Action<EntityBase, string, float> OnSkillCooldownStarted;

    // Progression
    public static event Action<EntityBase, string, string> OnAdventurerPromoted;
    public static event Action<int> OnLayerUnlocked;

    // Public invoke methods
    public static void RaiseGoldEarned(int amount) => OnGoldEarned?.Invoke(amount);
    public static void RaiseGoldSpent(int amount) => OnGoldSpent?.Invoke(amount);
    public static void RaiseGoldChanged(int total) => GoldChanged?.Invoke(total);
    public static void RaiseLootCollected(ResourceStack stack) => LootCollected?.Invoke(stack);
    public static void RaiseProductCrafted(ResourceStack stack) => ProductCrafted?.Invoke(stack);
    public static void RaiseDamageTaken(GameObject entity, float damage, GameObject source) => OnDamageTaken?.Invoke(entity, damage, source);
    public static void RaiseEntityDeath(GameObject deadEntity, GameObject killer) => OnEntityDeath?.Invoke(deadEntity, killer);
    public static void RaiseSkillActivated(EntityBase caster, string skillName) => OnSkillActivated?.Invoke(caster, skillName);
    public static void RaiseSkillCooldownStarted(EntityBase caster, string skillName, float cooldown) => OnSkillCooldownStarted?.Invoke(caster, skillName, cooldown);
    public static void RaiseAdventurerPromoted(EntityBase adventurer, string oldRole, string newRole) => OnAdventurerPromoted?.Invoke(adventurer, oldRole, newRole);
    public static void RaiseLayerUnlocked(int layer) => OnLayerUnlocked?.Invoke(layer);

    // Helper methods

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Game Signals/Clear All Listeners")]
    public static void ClearAllListeners()
    {
        OnGoldEarned = null;
        OnGoldSpent = null;
        GoldChanged = null;
        LootCollected = null;
        ProductCrafted = null;
        OnDamageTaken = null;
        OnEntityDeath = null;
        OnSkillActivated = null;
        OnSkillCooldownStarted = null;
        OnAdventurerPromoted = null;
        OnLayerUnlocked = null;
        
        Debug.Log("[GameSignals] All listeners cleared");
    }

    [UnityEditor.MenuItem("Tools/Game Signals/Print Subscriber Counts")]
    public static void PrintSubscriberCounts()
    {
        Debug.Log("=== GameSignals Subscriber Counts ===");
        Debug.Log($"OnGoldEarned: {OnGoldEarned?.GetInvocationList().Length ?? 0}");
        Debug.Log($"OnGoldSpent: {OnGoldSpent?.GetInvocationList().Length ?? 0}");
        Debug.Log($"GoldChanged: {GoldChanged?.GetInvocationList().Length ?? 0}");
        Debug.Log($"LootCollected: {LootCollected?.GetInvocationList().Length ?? 0}");
        Debug.Log($"ProductCrafted: {ProductCrafted?.GetInvocationList().Length ?? 0}");
        Debug.Log($"OnDamageTaken: {OnDamageTaken?.GetInvocationList().Length ?? 0}");
        Debug.Log($"OnEntityDeath: {OnEntityDeath?.GetInvocationList().Length ?? 0}");
        Debug.Log($"OnSkillActivated: {OnSkillActivated?.GetInvocationList().Length ?? 0}");
        Debug.Log($"OnSkillCooldownStarted: {OnSkillCooldownStarted?.GetInvocationList().Length ?? 0}");
        Debug.Log($"OnAdventurerPromoted: {OnAdventurerPromoted?.GetInvocationList().Length ?? 0}");
        Debug.Log($"OnLayerUnlocked: {OnLayerUnlocked?.GetInvocationList().Length ?? 0}");
    }
#endif
}