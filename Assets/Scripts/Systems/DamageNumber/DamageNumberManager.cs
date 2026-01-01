using UnityEngine;

/// <summary>
/// Manages spawning of floating damage numbers.
/// Uses object pooling for performance.
/// </summary>
public class DamageNumberManager : MonoBehaviour
{
    public static DamageNumberManager Instance { get; private set; }

    [Header("Prefab")]
    [SerializeField] private GameObject damageNumberPrefab;
    
    [Header("Settings")]
    [SerializeField] private float verticalOffset = 0.5f;
    
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Spawn a damage number at the specified world position
    /// </summary>
    public void ShowDamage(float damage, Vector3 worldPosition, bool isCrit = false)
    {
        if (damageNumberPrefab == null)
        {
            Debug.LogWarning("[DamageNumberManager] No damage number prefab assigned!");
            return;
        }

        // Offset upward so it appears above the entity
        Vector3 spawnPos = worldPosition + Vector3.up * verticalOffset;


        // Spawn from pool using generic GameObject method
        GameObject numberObj = ObjectPoolManager.Instance.SpawnObject(
            damageNumberPrefab,
            spawnPos,
            Quaternion.identity,
            ObjectPoolManager.PoolType.GameObject
        );

        if (numberObj == null)
        {
            Debug.LogError("[DamageNumberManager] Failed to spawn damage number from pool!");
            return;
        }

        // Debug.Log($"[DamageNumberManager] Spawned object: {numberObj.name}, active: {numberObj.activeSelf}");

        if (numberObj.TryGetComponent(out DamageNumber damageNumber))
        {
            damageNumber.Show(damage, spawnPos, isCrit);
            Debug.Log($"[DamageNumberManager] DamageNumber.Show() called");
        }
        else
        {
            Debug.LogError("[DamageNumberManager] Spawned object missing DamageNumber component!");
        }
    }

    /// <summary>
    /// Show damage at entity position (convenience method)
    /// </summary>
    public void ShowDamageAtEntity(float damage, Transform entityTransform, bool isCrit = false)
    {
        if (entityTransform != null)
        {
            // Get dynamic offset based on collider height
            float offset = verticalOffset;
            Collider2D col = entityTransform.GetComponent<Collider2D>();
            if (col != null)
            {
                // Use collider bounds height + padding
                offset = col.bounds.extents.y + 0.2f;
            }
            
            Vector3 spawnPos = entityTransform.position + Vector3.up * offset;
            ShowDamage(damage, spawnPos, isCrit);
        }
    }

    /// <summary>
    /// Show loot collection multiplier (e.g., x1.25 for manual pickup bonus)
    /// </summary>
    public void ShowLootMultiplier(float multiplier, Transform lootTransform)
    {
        if (damageNumberPrefab == null || lootTransform == null)
        {
            Debug.LogWarning("[DamageNumberManager] Cannot show loot multiplier - prefab or transform is null");
            return;
        }

        Debug.Log($"[DamageNumberManager] ShowLootMultiplier called: x{multiplier}");

        // Get dynamic offset
        float offset = verticalOffset;
        Collider2D col = lootTransform.GetComponent<Collider2D>();
        if (col != null)
        {
            offset = col.bounds.extents.y + 0.2f;
        }

        Vector3 spawnPos = lootTransform.position + Vector3.up * offset;

        Debug.Log($"[DamageNumberManager] Spawning at: {spawnPos}");

        // Spawn from pool
        GameObject numberObj = ObjectPoolManager.Instance.SpawnObject(
            damageNumberPrefab,
            spawnPos,
            Quaternion.identity,
            ObjectPoolManager.PoolType.GameObject
        );

        if (numberObj == null)
        {
            Debug.LogError("[DamageNumberManager] Failed to spawn loot multiplier!");
            return;
        }

        Debug.Log($"[DamageNumberManager] Spawned: {numberObj.name}");

        if (numberObj.TryGetComponent(out DamageNumber damageNumber))
        {
            // Show as loot multiplier (green with x prefix)
            damageNumber.ShowLootMultiplier(multiplier, spawnPos);
            Debug.Log("[DamageNumberManager] Called ShowLootMultiplier on DamageNumber");
        }
        else
        {
            Debug.LogError("[DamageNumberManager] No DamageNumber component on spawned object!");
        }
    }

    /// <summary>
    /// Show gold gain from click with random offset to avoid overlap with damage numbers
    /// </summary>
    public void ShowGoldGain(float gold, Transform entityTransform, bool isCrit = false)
    {
        if (damageNumberPrefab == null || entityTransform == null)
        {
            Debug.LogWarning("[DamageNumberManager] Cannot show gold gain - prefab or transform is null");
            return;
        }

        // Get dynamic offset based on collider
        float baseOffset = verticalOffset;
        Collider2D col = entityTransform.GetComponent<Collider2D>();
        if (col != null)
        {
            baseOffset = col.bounds.extents.y + 0.2f;
        }

        // Add random horizontal spread to avoid overlap with damage number
        float horizontalOffset = Random.Range(-0.4f, 0.4f);
        
        Vector3 spawnPos = entityTransform.position + 
                          Vector3.up * baseOffset + 
                          Vector3.right * horizontalOffset;

        // Spawn from pool
        GameObject numberObj = ObjectPoolManager.Instance.SpawnObject(
            damageNumberPrefab,
            spawnPos,
            Quaternion.identity,
            ObjectPoolManager.PoolType.GameObject
        );

        if (numberObj == null)
        {
            Debug.LogError("[DamageNumberManager] Failed to spawn gold number!");
            return;
        }

        if (numberObj.TryGetComponent(out DamageNumber damageNumber))
        {
            damageNumber.ShowGold(gold, spawnPos, isCrit);
        }
        else
        {
            Debug.LogError("[DamageNumberManager] No DamageNumber component on spawned object!");
        }
    }
}