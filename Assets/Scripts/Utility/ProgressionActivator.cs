using UnityEngine;

/// <summary>
/// Activates/deactivates a GameObject when progression conditions are met.
/// Use for: dungeon layers, forging station, research desk, decorations, etc.
/// Listens to ProgressionManager events and toggles target GameObject.
/// On a guild-hall decor slot (a <see cref="DecorSlot"/> on this GameObject) it unlocks the slot instead of toggling
/// a GameObject: a live unlock goes pending and pops in once the player looks at it; a restored one shows silently.
/// </summary>
public class ProgressionActivator : MonoBehaviour
{
    [Header("Activation Trigger")]
    [Tooltip("What unlocks this GameObject?")]
    [SerializeField] private ActivationTrigger trigger = ActivationTrigger.LayerUnlocked;
    
    [Header("Condition")]
    [Tooltip("For LayerUnlocked: which layer? For StarEarned: which star? For KillsAtDepth: which depth?")]
    [SerializeField] private int requiredValue = 2;
    
    [Tooltip("For UpgradePurchased: which upgrade is required?")]
    [SerializeField] private GuildUpgradeDef requiredUpgrade;
    
    [Header("Target")]
    [Tooltip("GameObject to activate/deactivate")]
    [SerializeField] private GameObject targetObject;
    
    [Header("Star Fanfare")]
    [Tooltip("Name listed on the star fanfare's GUILD HALL card when this fires for a star (empty = not listed)")]
    [SerializeField] private string displayName;

    [Header("Behavior")]
    [Tooltip("Should this deactivate the object on start if conditions aren't met?")]
    [SerializeField] private bool deactivateOnStart = true;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    /// <summary>Lifetime kills at a depth that earn its trophy (KillsAtDepth).</summary>
    public const int KillsAtDepthThreshold = 100;

    public string DisplayName => displayName;

    private DecorSlot decor;

    void Awake() => decor = GetComponent<DecorSlot>();

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
                GameSignals.OnLayerUnlocked += HandleLayerUnlocked;
                break;
            case ActivationTrigger.StarEarned:
                ProgressionManager.OnStarEarned += HandleStarEarned;
                break;
            case ActivationTrigger.UpgradePurchased:
                ProgressionManager.OnUpgradePurchased += HandleUpgradePurchased;
                break;
            case ActivationTrigger.KillsAtDepth:
                GameSignals.OnEntityDeath += HandleEntityDeath;
                break;
            case ActivationTrigger.ContractAccepted:
                GameSignals.OnContractAccepted += HandleContractAccepted;
                GameSignals.OnSealsChanged += HandleSealsChanged;   // ContractManager.LoadFrom raises it: a claimed past contract
                break;
            case ActivationTrigger.RecipePinned:
                GameSignals.OnCraftingConfigChanged += HandleCraftingConfigChanged;
                break;
        }
    }

    void OnDisable()
    {
        // Unsubscribe to prevent memory leaks
        switch (trigger)
        {
            case ActivationTrigger.LayerUnlocked:
                GameSignals.OnLayerUnlocked -= HandleLayerUnlocked;
                break;
            case ActivationTrigger.StarEarned:
                ProgressionManager.OnStarEarned -= HandleStarEarned;
                break;
            case ActivationTrigger.UpgradePurchased:
                ProgressionManager.OnUpgradePurchased -= HandleUpgradePurchased;
                break;
            case ActivationTrigger.KillsAtDepth:
                GameSignals.OnEntityDeath -= HandleEntityDeath;
                break;
            case ActivationTrigger.ContractAccepted:
                GameSignals.OnContractAccepted -= HandleContractAccepted;
                GameSignals.OnSealsChanged -= HandleSealsChanged;
                break;
            case ActivationTrigger.RecipePinned:
                GameSignals.OnCraftingConfigChanged -= HandleCraftingConfigChanged;
                break;
        }
    }

    // Event handlers
    private void HandleLayerUnlocked(int layer)
    {
        if (layer >= requiredValue)
        {
            SetObjectActive(true, fresh: true);
        }
    }

    private void HandleStarEarned(int stars)
    {
        if (stars >= requiredValue)
        {
            SetObjectActive(true, fresh: true);
        }
    }

    private void HandleUpgradePurchased(GuildUpgradeDef upgrade)
    {
        if (upgrade == requiredUpgrade)
        {
            SetObjectActive(true, fresh: true);
        }
    }

    // SaveManager subscribed first (Awake), so its lifetime counter already includes this kill
    private void HandleEntityDeath(GameObject _) { if (IsConditionMet()) SetObjectActive(true, fresh: true); }
    private void HandleContractAccepted(Contract _) { if (IsConditionMet()) SetObjectActive(true, fresh: true); }
    private void HandleSealsChanged(int _) { if (IsConditionMet()) SetObjectActive(true, fresh: true); }
    private void HandleCraftingConfigChanged() { if (IsConditionMet()) SetObjectActive(true, fresh: true); }

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

            case ActivationTrigger.KillsAtDepth:
                return SaveManager.Instance != null && SaveManager.Instance.KillsAtDepth(requiredValue) >= KillsAtDepthThreshold;

            case ActivationTrigger.ContractAccepted:
                return ContractManager.Instance != null && ContractManager.Instance.AcceptedCount > 0;

            case ActivationTrigger.RecipePinned:
                return CraftingManager.Instance != null && CraftingManager.Instance.AnyRecipePinned;
            
            default:
                return false;
        }
    }

    // Activate or deactivate target object. fresh = a live unlock (decor pops in); the save replay is never fresh.
    private void SetObjectActive(bool active, bool fresh = false)
    {
        if (decor != null)
        {
            decor.SetUnlocked(active, fresh && !(SaveManager.Instance != null && SaveManager.Instance.IsRestoring));
            return;
        }

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
                return $"Depth {requiredValue}";
            case ActivationTrigger.StarEarned:
                return $"{requiredValue}★";
            case ActivationTrigger.UpgradePurchased:
                return requiredUpgrade != null ? requiredUpgrade.name : "null upgrade";
            case ActivationTrigger.KillsAtDepth:
                return $"{KillsAtDepthThreshold} kills at depth {requiredValue}";
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
    UpgradePurchased,   // Activate when specific upgrade purchased (e.g., Forging Station)
    KillsAtDepth,       // Lifetime kills at depth requiredValue reach KillsAtDepthThreshold (serialized ints: append only)
    ContractAccepted,   // First contract ever accepted
    RecipePinned,       // Any recipe pinned in a station slot
}
