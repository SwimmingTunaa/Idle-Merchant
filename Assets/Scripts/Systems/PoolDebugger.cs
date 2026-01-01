using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Debug component to help visualize pool state in editor.
/// Attach to ObjectPoolManager to see what's happening.
/// </summary>
public class PoolDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    public bool showPoolStats = true;
    public bool logDespawns = false;

    void Update()
    {
        // Use new Input System
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame && showPoolStats)
        {
            PrintPoolStats();
        }
    }

    [ContextMenu("Print Pool Stats")]
    public void PrintPoolStats()
    {
        var poolManager = GetComponent<ObjectPoolManager>();
        if (poolManager == null)
        {
            Debug.LogWarning("[PoolDebugger] No ObjectPoolManager found!");
            return;
        }

        Debug.Log("=== OBJECT POOL STATUS ===");

        // Find the pool holders
        Transform gameObjectHolder = transform.Find("Pool/GameObjects");
        Transform particleHolder = transform.Find("Pool/ParticleSystems");

        if (gameObjectHolder != null)
        {
            int totalInPool = 0;
            int activeObjects = 0;

            foreach (Transform child in gameObjectHolder)
            {
                totalInPool++;
                if (child.gameObject.activeSelf)
                {
                    activeObjects++;
                    Debug.LogWarning($"  [!] {child.name} is ACTIVE in pool (should be disabled!)");
                }
            }

            Debug.Log($"GameObject Pool: {totalInPool} total, {activeObjects} active (should be 0)");
        }

        Debug.Log("=========================");
    }

    [ContextMenu("Cleanup Invalid Pool Objects")]
    public void CleanupInvalidPoolObjects()
    {
        Transform gameObjectHolder = transform.Find("Pool/GameObjects");
        
        if (gameObjectHolder != null)
        {
            int cleaned = 0;
            
            foreach (Transform child in gameObjectHolder)
            {
                // Force disable any active objects
                if (child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(false);
                    child.position = new Vector3(-9999f, -9999f, 0f);
                    cleaned++;
                }
            }

            Debug.Log($"[PoolDebugger] Cleaned up {cleaned} invalid pool objects");
        }
    }
}