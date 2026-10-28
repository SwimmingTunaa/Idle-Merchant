using UnityEngine;

/// <summary>
/// How a depth's transport point carries porters (serialized ints: append only).
/// Ladder = climb at the porter's move speed; Elevator = a straight glide in elevatorSeconds (Elevator I–V ranks);
/// Teleporter is unused here — Teleport Circle reroutes porters instead (PorterManager/PorterAgent).
/// </summary>
public enum TransportType
{
    Ladder,      // Slow, default
    Elevator,    // ~1 s ride
    Teleporter   // unused (Teleport Circle is a route, not a mode)
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
    
    [Header("Elevator")]
    [Tooltip("Seconds per ride (either way) once this ladder is an elevator; the ladder climbs at the porter's move speed")]
    [SerializeField] private float elevatorSeconds = 1f;
    [Tooltip("Ladder sprite tint while it's an elevator")]
    [SerializeField] private Color elevatorTint = new Color(0.62f, 0.72f, 0.82f);   // PLACEHOLDER steel tint until the cage art lands
    
    [Header("Points")]
    [Tooltip("Where porter stands to enter transport")]
    public Transform entryPoint;
    
    [Tooltip("Where porter exits at destination (usually at shop deposit point)")]
    public Transform exitPoint;
    
    [Header("Visual")]
    [Tooltip("Optional visual indicator for transport")]
    public SpriteRenderer transportVisual;

    [Tooltip("Rendering-layer bit for the hover outline (HoverOutlineFeature, bit 2)")]
    [SerializeField] private uint hoverOutlineLayer = 2;

    void Awake()
    {
        ValidateSetup();
        // Click target for the Transport panel (ClickerManager): the whole ladder sprite is clickable
        if (transportVisual != null && TryGetComponent(out BoxCollider2D box))
        {
            box.size = transportVisual.size;
            box.offset = transform.InverseTransformPoint(transportVisual.bounds.center);   // sprite pivot isn't centred
        }
    }

    public void SetHover(bool on)
    {
        if (transportVisual == null) return;
        transportVisual.renderingLayerMask = on ? transportVisual.renderingLayerMask | hoverOutlineLayer
                                                : transportVisual.renderingLayerMask & ~hoverOutlineLayer;
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

 
    /// <summary>Elevator I–V (PorterManager): steel-tinted ladder, porters glide it in elevatorSeconds.</summary>
    public void SetMode(TransportType mode)
    {
        transportType = mode;
        if (transportVisual != null) transportVisual.color = mode == TransportType.Elevator ? elevatorTint : Color.white;
    }

    /// <summary>Climb speed between entry and exit: the porter's own on a ladder, a fixed-time glide on an elevator.</summary>
    public float TravelSpeed(float ladderSpeed) =>
        transportType == TransportType.Elevator
            ? Vector3.Distance(GetEntryPosition(), GetExitPosition()) / Mathf.Max(0.1f, elevatorSeconds)
            : ladderSpeed;

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
        string label = $"{transportType}";
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