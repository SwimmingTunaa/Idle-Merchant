using UnityEngine;

/// <summary>
/// Type of transport method between layers.
/// Each has different travel time and upgrade cost.
/// </summary>
public enum TransportType
{
    Ladder,      // Slow, default
    Elevator,    // Medium speed
    Teleporter   // Instant
}

/// <summary>
/// Component for transport points that porters use to travel between layers.
/// Attach to your Ladder/Elevator/Teleporter prefabs.
/// </summary>
public class TransportPoint : MonoBehaviour
{
    [Header("Transport Configuration")]
    [Tooltip("Type of transport (affects travel time)")]
    public TransportType transportType = TransportType.Ladder;
    
    [Tooltip("Which layer this transport is on")]
    public int sourceLayer = 1;
    
    [Tooltip("Where this transport goes (usually 0 = shop level)")]
    public int destinationLayer = 0;
    
    [Header("Travel Times")]
    [Tooltip("How long it takes to travel via this transport")]
    public float travelTime = 3f; // Ladder = 3s, Elevator = 1.5s, Teleporter = 0.1s
    
    [Header("Points")]
    [Tooltip("Where porter stands to enter transport")]
    public Transform entryPoint;
    
    [Tooltip("Where porter exits at destination (usually at shop deposit point)")]
    public Transform exitPoint;
    
    [Header("Visual")]
    [Tooltip("Optional visual indicator for transport")]
    public SpriteRenderer transportVisual;

    void Awake()
    {
        ValidateSetup();
    }

    private void ValidateSetup()
    {
        if (entryPoint == null)
        {
            Debug.LogWarning($"[TransportPoint] {name} has no entryPoint assigned! Creating one at self position.");
            GameObject entry = new GameObject("EntryPoint");
            entry.transform.SetParent(transform);
            entry.transform.localPosition = Vector3.zero;
            entryPoint = entry.transform;
        }

        if (exitPoint == null)
        {
            Debug.LogWarning($"[TransportPoint] {name} has no exitPoint assigned! Porters won't know where to exit.");
        }
    }

 
    public float GetTravelTime()
    {
        return travelTime;
    }

    /// <summary>
    /// Check if porter is close enough to use this transport
    /// </summary>
    public bool IsPorterAtEntry(Vector3 porterPosition, float threshold = 0.1f)
    {
        if (entryPoint == null) return false;
        return Vector3.Distance(porterPosition, entryPoint.position) < threshold;
    }

    /// <summary>
    /// Get position where porter should exit at destination
    /// </summary>
    public Vector3 GetExitPosition()
    {
        return exitPoint != null ? exitPoint.position : transform.position;
    }

    /// <summary>
    /// Get position where porter should move to enter transport
    /// </summary>
    public Vector3 GetEntryPosition()
    {
        return entryPoint != null ? entryPoint.position : transform.position;
    }

    // ===== UPGRADE SYSTEM (Future) =====

    /// <summary>
    /// Upgrade this transport to next tier.
    /// Called by upgrade system (implement later).
    /// </summary>
    public bool TryUpgrade()
    {
        switch (transportType)
        {
            case TransportType.Ladder:
                transportType = TransportType.Elevator;
                travelTime = 1.5f;
                Debug.Log($"[TransportPoint] Upgraded {name} to Elevator (1.5s travel)");
                return true;
            
            case TransportType.Elevator:
                transportType = TransportType.Teleporter;
                travelTime = 0.1f;
                Debug.Log($"[TransportPoint] Upgraded {name} to Teleporter (instant)");
                return true;
            
            case TransportType.Teleporter:
                Debug.LogWarning($"[TransportPoint] {name} is already max level (Teleporter)");
                return false;
            
            default:
                return false;
        }
    }

    // ===== DEBUG GIZMOS =====

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (entryPoint != null)
        {
            // Entry point (green)
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(entryPoint.position, 0.3f);
            UnityEditor.Handles.Label(entryPoint.position + Vector3.up * 0.5f, "ENTRY");
        }

        if (exitPoint != null)
        {
            // Exit point (blue)
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(exitPoint.position, 0.3f);
            UnityEditor.Handles.Label(exitPoint.position + Vector3.up * 0.5f, "EXIT");
            
            // Draw line between entry and exit
            if (entryPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(entryPoint.position, exitPoint.position);
            }
        }

        // Draw transport type label
        string label = $"{transportType}\n{travelTime:F1}s";
        UnityEditor.Handles.Label(transform.position, label);
    }

    void OnDrawGizmosSelected()
    {
        // Highlight when selected
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawSphere(transform.position, 0.5f);
    }
#endif
}