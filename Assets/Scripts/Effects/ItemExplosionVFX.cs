using UnityEngine;

/// <summary>
/// Singleton manager for spawning flying item explosions (coins, gems, etc.).
/// Items fly from world positions toward UI elements in screen space.
/// </summary>
public class ItemExplosionVFX : PersistentSingleton<ItemExplosionVFX>
{
    [Header("Prefab")]
    [SerializeField] private GameObject itemPrefab;

    [Header("Default Sprites")]
    [SerializeField] private Sprite defaultCoinSprite;
    [SerializeField] private Sprite defaultGemSprite;

    [Header("Explosion Settings")]
    // [SerializeField] private float explosionForce = 1.5f;
    // [SerializeField] private float arcHeight = 0.8f;
    // [SerializeField] private float rotationSpeed = 540f;
    [SerializeField] private float spawnStagger = 0.02f;

    public void SpawnExplosion(Sprite sprite, Vector3 worldPosition, int count, int maxCount = 15, float scale = 1f,
        System.Action onEachArrival = null)
    {
        int spawnCount = Mathf.Min(count, maxCount);

        if (itemPrefab == null)
        {
            Debug.LogError("[ItemExplosionVFX] No item prefab assigned!");
            DeliverNow(onEachArrival, spawnCount);
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
            DeliverNow(onEachArrival, spawnCount);
            return;
        }

        StartCoroutine(SpawnSequence(spriteToUse, worldPosition, screenTarget, spawnCount, scale, onEachArrival));
    }

    // VFX skipped — still fire every arrival callback, they may carry gold
    private static void DeliverNow(System.Action onEachArrival, int count)
    {
        for (int i = 0; i < count; i++) onEachArrival?.Invoke();
    }

    private System.Collections.IEnumerator SpawnSequence(Sprite sprite, Vector3 worldPos, Vector2 screenTarget, int count, float scale,
        System.Action onEachArrival)
    {
        // Cache IconPulse reference once
        IconPulse iconPulse = FindAnyObjectByType<IconPulse>();

        for (int i = 0; i < count; i++)
        {
            System.Action callback = () => { iconPulse?.Pulse(); onEachArrival?.Invoke(); };
            
            SpawnSingleItem(sprite, worldPos, screenTarget, callback, scale);
            
            if (i < count - 1)
                yield return new WaitForSeconds(spawnStagger);
        }
    }

    /// <summary>One item icon arcing world→world (e.g. loot → shop pile, pile → counter). Pure VFX.
    /// Default scale 1 = a dropped item's size, so items keep their size mid-journey.
    /// <paramref name="homing"/> makes the target track a moving transform; <paramref name="onArrive"/> also fires
    /// immediately if the flight can't spawn, so callers can chain without a stuck state.</summary>
    public void FlyToWorld(Sprite sprite, Vector3 from, Vector3 to, float scale = 1f,
        Transform homing = null, float speed = 1f, System.Action onArrive = null)
    {
        ItemFlyAnimation animation = itemPrefab != null && sprite != null ? SpawnFlyer(sprite, from, scale) : null;
        if (animation != null)
            animation.StartFlightToWorld(to, new Vector3(Random.Range(-0.2f, 0.2f), 0.2f, 0f), onArrive, homing, speed);   // small pop, not a burst
        else
            onArrive?.Invoke();
    }

    private void SpawnSingleItem(Sprite sprite, Vector3 worldPos, Vector2 screenTarget, System.Action onComplete, float scale = 1f)
    {
        float angle = Random.Range(0f, 360f);
        Vector3 direction = new Vector3(
            Mathf.Cos(angle * Mathf.Deg2Rad),
            Mathf.Sin(angle * Mathf.Deg2Rad),
            0f
        );

        ItemFlyAnimation animation = SpawnFlyer(sprite, worldPos, scale);
        if (animation != null)
            animation.StartFlight(screenTarget, direction, onComplete);
        else
            onComplete?.Invoke();   // never swallow the callback — it may carry gold
    }

    private ItemFlyAnimation SpawnFlyer(Sprite sprite, Vector3 worldPos, float scale)
    {
        GameObject itemObj = ObjectPoolManager.Instance.SpawnObject(
            itemPrefab,
            worldPos,
            Quaternion.identity,
            ObjectPoolManager.PoolType.GameObject
        );
        if (itemObj == null) return null;

        SpriteRenderer sr = itemObj.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sprite = sprite;
            sr.color = Color.white;
        }

        itemObj.transform.localScale = Vector3.one * scale;
        return itemObj.GetComponent<ItemFlyAnimation>();
    }
}