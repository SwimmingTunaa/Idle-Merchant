using UnityEngine;

/// <summary>
/// Singleton manager for spawning flying item explosions (coins, gems, etc.).
/// Items fly from world positions toward UI elements in screen space.
/// </summary>
public class ItemExplosionVFX : MonoBehaviour
{
    public static ItemExplosionVFX Instance { get; private set; }

    [Header("Prefab")]
    [SerializeField] private GameObject itemPrefab;

    [Header("Default Sprites")]
    [SerializeField] private Sprite defaultCoinSprite;
    [SerializeField] private Sprite defaultGemSprite;

    [Header("Explosion Settings")]
    [SerializeField] private float explosionForce = 1.5f;
    [SerializeField] private float arcHeight = 0.8f;
    [SerializeField] private float rotationSpeed = 540f;
    [SerializeField] private float spawnStagger = 0.02f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void SpawnExplosion(Sprite sprite, Vector3 worldPosition, int count, int maxCount = 15)
    {
        if (itemPrefab == null)
        {
            Debug.LogError("[ItemExplosionVFX] No item prefab assigned!");
            return;
        }

        Vector2 screenTarget = UITargetMarker.ScreenPosition;
        
        if (screenTarget == Vector2.zero)
        {
            Debug.LogWarning("[ItemExplosionVFX] UITargetMarker.ScreenPosition is zero! Is UITargetMarker initialized?");
        }

        Sprite spriteToUse = sprite ?? defaultCoinSprite;
        
        if (spriteToUse == null)
        {
            Debug.LogError("[ItemExplosionVFX] No sprite provided and no default sprite assigned!");
            return;
        }

        int spawnCount = Mathf.Min(count, maxCount);

        StartCoroutine(SpawnSequence(spriteToUse, worldPosition, screenTarget, spawnCount));
    }

    private System.Collections.IEnumerator SpawnSequence(Sprite sprite, Vector3 worldPos, Vector2 screenTarget, int count)
    {
        // Cache IconPulse reference once
        IconPulse iconPulse = FindFirstObjectByType<IconPulse>();
        
        for (int i = 0; i < count; i++)
        {
            // Every coin just pulses icon (visual feedback only)
            System.Action callback = () => iconPulse?.Pulse();
            
            SpawnSingleItem(sprite, worldPos, screenTarget, callback);
            
            if (i < count - 1)
                yield return new WaitForSeconds(spawnStagger);
        }
    }

    private void SpawnSingleItem(Sprite sprite, Vector3 worldPos, Vector2 screenTarget, System.Action onComplete)
    {
        GameObject itemObj = ObjectPoolManager.Instance.SpawnObject(
            itemPrefab,
            worldPos,
            Quaternion.identity,
            ObjectPoolManager.PoolType.GameObject
        );

        if (itemObj == null) return;

        SpriteRenderer sr = itemObj.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sprite = sprite;
            sr.color = Color.white;
        }

        itemObj.transform.localScale = Vector3.one;

        float angle = Random.Range(0f, 360f);
        Vector3 direction = new Vector3(
            Mathf.Cos(angle * Mathf.Deg2Rad),
            Mathf.Sin(angle * Mathf.Deg2Rad),
            0f
        );

        ItemFlyAnimation animation = itemObj.GetComponent<ItemFlyAnimation>();
        if (animation != null)
        {
            animation.StartFlight(screenTarget, direction, onComplete);
        }
    }
}