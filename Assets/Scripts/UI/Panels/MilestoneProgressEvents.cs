using System;
using UnityEngine;

// Shared subscription helper for milestone progress signals.
// Centralises the signal list so MilestoneTracker and GuildUpgradePanel
// don't duplicate subscribe/unsubscribe logic.
//
// Usage:
//   var handle = new MilestoneProgressEvents(onProgressChanged, onFullRebuild);
//   handle.Subscribe();
//   handle.Unsubscribe();   // safe to call multiple times
public class MilestoneProgressEvents
{
    // Stored delegates so unsubscribe matches exactly
    private readonly Action<int>           _onGold;
    private readonly Action<GameObject>    _onDeath;
    private readonly Action<ResourceStack> _onLoot;
    private readonly Action<ResourceStack> _onCrafted;
    private readonly Action               _onCustomer;
    private readonly Action<EntityDef, HireRole> _onHired;
    private readonly Action<int>           _onStar;
    private readonly Action<GuildUpgradeDef> _onUpgrade;

    public MilestoneProgressEvents(Action onProgressChanged, Action onFullRebuild)
    {
        _onGold     = _ => onProgressChanged();
        _onDeath    = _ => onProgressChanged();
        _onLoot     = _ => onProgressChanged();
        _onCrafted  = _ => onProgressChanged();
        _onCustomer = onProgressChanged;
        _onHired    = (_, __) => onProgressChanged();
        _onStar     = _ => onFullRebuild();
        _onUpgrade  = _ => onFullRebuild();
    }

    public void Subscribe()
    {
        GameSignals.OnGoldEarned              += _onGold;
        GameSignals.OnEntityDeath             += _onDeath;
        GameSignals.OnLootCollected           += _onLoot;
        GameSignals.OnProductCrafted          += _onCrafted;
        GameSignals.OnCustomerServed          += _onCustomer;
        GameSignals.OnUnitHired               += _onHired;
        ProgressionManager.OnStarEarned       += _onStar;
        ProgressionManager.OnUpgradePurchased += _onUpgrade;
    }

    public void Unsubscribe()
    {
        GameSignals.OnGoldEarned              -= _onGold;
        GameSignals.OnEntityDeath             -= _onDeath;
        GameSignals.OnLootCollected           -= _onLoot;
        GameSignals.OnProductCrafted          -= _onCrafted;
        GameSignals.OnCustomerServed          -= _onCustomer;
        GameSignals.OnUnitHired               -= _onHired;
        ProgressionManager.OnStarEarned       -= _onStar;
        ProgressionManager.OnUpgradePurchased -= _onUpgrade;
    }
}
