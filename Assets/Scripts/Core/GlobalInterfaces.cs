using UnityEngine;


public interface ITickable { void Tick(float dt); }           // For manual ticking if you avoid Update
public interface IProducer { event System.Action<ResourceStack> OnProduced; }
public interface IConsumer { bool TryConsume(ResourceStack need); }
public interface IPoolable
{
    void OnSpawned(ScriptableObject def);
    void OnDespawned();
}

[System.Serializable]
public struct ResourceStack {
    public ItemDef itemDef;
    public int qty;
    public int sellValue;         
    public ResourceStack(ItemDef itemDef, int qty, int sellValue) { this.itemDef = itemDef; this.qty = qty; this.sellValue = sellValue; }
}

public interface IUnitManager
{
    bool CanHire(EntityDef def);
    bool HireUnit(EntityDef def);
    int GetUnitCount(EntityDef def);
    int GetUnitLimit(EntityDef def);
    bool IsTypeFull(EntityDef def);
    int LayerIndex { get; }
    System.Collections.Generic.List<UnitTypeLimit> UnitLimits { get; } // Added for HireController access
}