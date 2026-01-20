using UnityEngine;

/// <summary>
/// Attach to GameObjects that should ignore input when UI is blocking world input.
/// Automatically disables colliders when UIManager.IsBlockingWorldInput is true.
/// Use this for clickable world objects (units, buildings, loot, etc.)
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WorldInputBlocker : MonoBehaviour
{
    private Collider2D col;
    private bool wasEnabled;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        wasEnabled = col.enabled;
    }

    private void LateUpdate()
    {
        bool shouldBlock = UIManager.Instance.IsBlockingWorldInput;
        
        if (shouldBlock)
        {
            wasEnabled = col.enabled;
            col.enabled = false;
        }
        else if (wasEnabled)
        {
            col.enabled = true;
        }
    }

    private void OnDisable()
    {
        if (col != null && wasEnabled)
            col.enabled = true;
    }
}
