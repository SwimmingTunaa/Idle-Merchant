using UnityEngine;

/// <summary>
/// Activates/deactivates a GameObject when progression conditions are met.
/// Use for: dungeon layers, forging station, research desk, decorations, etc.
/// Listens to ProgressionManager events and toggles target GameObject.
/// </summary>
public class ProgressionActivator : MonoBehaviour
{
    [Header("Activation Trigger")]
    [Tooltip("What unlocks this GameObject?")]
    [SerializeField] private ActivationTrigger trigger = ActivationTrigger.LayerUnlocked;
    
    [Header("Condition")]
    [Tooltip("For LayerUnlocked: which layer? For StarEarned: which star?")]
    [SerializeField] private int requiredValue = 2;
    
    [Tooltip("For UpgradePurchased: which upgrade is required?")]
    [SerializeField] private GuildUpgradeDef requiredUpgrade;
    
    [Header("Target")]
    [Tooltip("GameObject to activate/deactivate")]
    [SerializeField] private GameObject targetObject;
    
    [Header("Behavior")]
    [Tooltip("Should this deactivate the object on start if conditions aren't met?")]
    [SerializeField] private bool deactivateOnStart = true;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    void Start()
    {
        // Check initial state on game start
        if (IsConditionMet())
        {
            SetObjectActive(true);
        }
        else if (deactivateOnStart)
        {
            SetObjectActive(false);
        }
    }

    void OnEnable()
    {
        // Subscribe to relevant progression events
        switch (trigger)
        {
            case ActivationTrigger.LayerUnlocked:
                ProgressionManager.OnLayerUnlocked += HandleLayerUnlocked;
                break;
            case ActivationTrigger.StarEarned:
                ProgressionManager.OnStarEarned += HandleStarEarned;
                break;
            case ActivationTrigger.UpgradePurchased:
                ProgressionManager.OnUpgradePurchased += HandleUpgradePurchased;
                break;
        }
    }

    void OnDisable()
    {
        // Unsubscribe to prevent memory leaks
        switch (trigger)
        {
            case ActivationTrigger.LayerUnlocked:
                ProgressionManager.OnLayerUnlocked -= HandleLayerUnlocked;
                break;
            case ActivationTrigger.StarEarned:
                ProgressionManager.OnStarEarned -= HandleStarEarned;
                break;
            case ActivationTrigger.UpgradePurchased:
                ProgressionManager.OnUpgradePurchased -= HandleUpgradePurchased;
                break;
        }
    }

    // Event handlers
    private void HandleLayerUnlocked(int layer)
    {
        if (layer >= requiredValue)
        {
            SetObjectActive(true);
        }
    }

    private void HandleStarEarned(int stars)
    {
        if (stars >= requiredValue)
        {
            SetObjectActive(true);
        }
    }

    private void HandleUpgradePurchased(GuildUpgradeDef upgrade)
    {
        if (upgrade == requiredUpgrade)
        {
            SetObjectActive(true);
        }
    }

    // Check if condition is currently met
    private bool IsConditionMet()
    {
        if (ProgressionManager.Instance == null)
        {
            if (showDebugLogs)
                Debug.LogWarning("[ProgressionActivator] ProgressionManager.Instance is null");
            return false;
        }

        switch (trigger)
        {
            case ActivationTrigger.LayerUnlocked:
                return ProgressionManager.Instance.maxUnlockedLayer >= requiredValue;
            
            case ActivationTrigger.StarEarned:
                return ProgressionManager.Instance.GetCurrentStars() >= requiredValue;
            
            case ActivationTrigger.UpgradePurchased:
                if (requiredUpgrade == null)
                {
                    if (showDebugLogs)
                        Debug.LogWarning("[ProgressionActivator] requiredUpgrade is null but trigger is UpgradePurchased");
                    return false;
                }
                return ProgressionManager.Instance.IsUpgradeOwned(requiredUpgrade);
            
            default:
                return false;
        }
    }

    // Activate or deactivate target object
    private void SetObjectActive(bool active)
    {
        if (targetObject == null)
        {
            if (showDebugLogs)
                Debug.LogWarning("[ProgressionActivator] targetObject is null");
            return;
        }

        if (targetObject.activeSelf != active)
        {
            targetObject.SetActive(active);
            
            if (showDebugLogs)
            {
                string triggerDesc = GetTriggerDescription();
                Debug.Log($"[ProgressionActivator] {(active ? "Activated" : "Deactivated")} {targetObject.name} ({triggerDesc})");
            }
        }
    }

    // Helper for debug logging
    private string GetTriggerDescription()
    {
        switch (trigger)
        {
            case ActivationTrigger.LayerUnlocked:
                return $"Layer {requiredValue}";
            case ActivationTrigger.StarEarned:
                return $"{requiredValue}★";
            case ActivationTrigger.UpgradePurchased:
                return requiredUpgrade != null ? requiredUpgrade.name : "null upgrade";
            default:
                return trigger.ToString();
        }
    }

    // Context menu for testing
    [ContextMenu("Test: Activate")]
    private void TestActivate()
    {
        SetObjectActive(true);
    }

    [ContextMenu("Test: Deactivate")]
    private void TestDeactivate()
    {
        SetObjectActive(false);
    }

    [ContextMenu("Test: Check Condition")]
    private void TestCheckCondition()
    {
        bool met = IsConditionMet();
        Debug.Log($"[ProgressionActivator] Condition met: {met} ({GetTriggerDescription()})");
    }
}

/// <summary>
/// Types of progression events that can trigger GameObject activation.
/// </summary>
public enum ActivationTrigger
{
    LayerUnlocked,      // Activate when specific layer unlocks (e.g., Layer 2)
    StarEarned,         // Activate when specific star tier earned (e.g., 3★)
    UpgradePurchased    // Activate when specific upgrade purchased (e.g., Forging Station)
}
