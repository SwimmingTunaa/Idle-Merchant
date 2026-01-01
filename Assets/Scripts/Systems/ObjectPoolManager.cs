using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance;

    private GameObject rootHolder;
    private GameObject gameObjectHolder;
    private GameObject particleSystemHolder;

    // Pools keyed by EntityDef
    private readonly Dictionary<EntityDef, ObjectPool<GameObject>> pools = new();
    // Reverse map: instance -> the EntityDef that created it (for return)
    private readonly Dictionary<GameObject, EntityDef> instanceToDef = new();
    // Pool-specific holders for organization (keyed by EntityDef)
    private readonly Dictionary<EntityDef, GameObject> defHolders = new();

    // Generic GameObject pools (keyed by prefab instance ID)
    private readonly Dictionary<int, ObjectPool<GameObject>> genericPools = new();
    // Reverse map: instance -> prefab ID for return
    private readonly Dictionary<GameObject, int> instanceToPrefabID = new();
    // Pool holders for generic prefabs
    private readonly Dictionary<int, GameObject> genericHolders = new();

    public enum PoolType { GameObject, ParticleSystem }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        SetupHolders();
    }

    void SetupHolders()
    {
        rootHolder = new GameObject("Pool");
        DontDestroyOnLoad(rootHolder);

        gameObjectHolder = new GameObject("GameObjects");
        gameObjectHolder.transform.SetParent(rootHolder.transform, false);

        particleSystemHolder = new GameObject("ParticleSystems");
        particleSystemHolder.transform.SetParent(rootHolder.transform, false);
    }

    /// <summary>
    /// Get or create a holder GameObject for a specific EntityDef.
    /// Organizes pool by def name for cleaner hierarchy.
    /// </summary>
    private GameObject GetOrCreateDefHolder(EntityDef def, PoolType poolType)
    {
        if (defHolders.TryGetValue(def, out GameObject holder))
            return holder;

        // Create new holder for this def
        string holderName = def != null ? def.displayName : "Unknown";
        GameObject parent = poolType == PoolType.ParticleSystem ? particleSystemHolder : gameObjectHolder;
        
        holder = new GameObject($"Pool_{holderName}");
        holder.transform.SetParent(parent.transform, false);
        
        defHolders[def] = holder;
        return holder;
    }

    // ---------- Pool creation ----------

    private void CreatePool(EntityDef def, PoolType poolType)
    {
        // capture 'def' & 'poolType' in closures so OnGet knows which def created this instance
        var pool = new ObjectPool<GameObject>(
            createFunc: () => CreateInstance(def, poolType),
            actionOnGet:    go => OnGet(go, def, poolType),
            actionOnRelease: OnRelease,
            actionOnDestroy: OnDestroyInstance,
            collectionCheck: false,
            defaultCapacity: 4,
            maxSize: 256
        );

        pools[def] = pool;
    }

    private GameObject CreateInstance(EntityDef def, PoolType poolType)
    {
        if (def == null || def.prefab == null)
        {
            Debug.LogError("CreateInstance: EntityDef or def.prefab is null.");
            return null;
        }

        // Get def-specific holder for organization
        GameObject parent = GetOrCreateDefHolder(def, poolType);
        
        var go = Instantiate(def.prefab, parent.transform);
        go.SetActive(false);
        instanceToDef[go] = def;
        return go;
    }

    // ---------- Pool callbacks ----------

    private void OnGet(GameObject go, EntityDef def, PoolType poolType)
    {
        if (go == null) return;

        // Ensure parent is correct def-specific holder
        GameObject desiredParent = GetOrCreateDefHolder(def, poolType);
        if (go.transform.parent != desiredParent.transform)
            go.transform.SetParent(desiredParent.transform, false);

        // Activate last
        go.SetActive(true);
    }

    private void OnRelease(GameObject go)
    {
        if (go == null) return;
        
        // Move far off-screen (prevents visual clutter if accidentally left active)
        go.transform.position = new Vector3(-9999f, -9999f, 0f);
        
        // Parent stays in def-specific holder (already correct from OnGet)
        
        // Disable
        go.SetActive(false);
    }

    private void OnDestroyInstance(GameObject go)
    {
        if (go == null) return;
        instanceToDef.Remove(go);
        Destroy(go);
    }

    private GameObject GetHolder(PoolType poolType)
    {
        return poolType == PoolType.ParticleSystem ? particleSystemHolder : gameObjectHolder;
    }

    // ---------- Public API ----------

    /// <summary>
    /// Spawn using an EntityDef. The instance will have ApplyDef(def) called before SetActive(true).
    /// </summary>
    public GameObject SpawnObject(EntityDef def, Vector3 position, Quaternion rotation, PoolType poolType = PoolType.GameObject)
    {
        if (def == null) { Debug.LogError("SpawnObject: def is null."); return null; }
        if (!pools.TryGetValue(def, out var pool))
        {
            CreatePool(def, poolType);
            pool = pools[def];
        }

        var go = pool.Get();
        if (go == null) return null;

        go.transform.SetPositionAndRotation(position, rotation);
        return go;
    }

    /// <summary>
    /// Generic helper to get a specific component on the spawned object.
    /// </summary>
    public T SpawnObject<T>(EntityDef def, Vector3 position, Quaternion rotation, PoolType poolType = PoolType.GameObject) where T : Component
    {
        var go = SpawnObject(def, position, rotation, poolType);
        return go ? go.GetComponent<T>() : null;
    }

    // ===== GENERIC GAMEOBJECT POOLING (For VFX, UI, etc.) =====

    /// <summary>
    /// Spawn a generic GameObject prefab using pooling (no EntityDef required).
    /// Use this for VFX, damage numbers, UI elements, etc.
    /// </summary>
    public GameObject SpawnObject(GameObject prefab, Vector3 position, Quaternion rotation, PoolType poolType = PoolType.GameObject)
    {
        if (prefab == null)
        {
            Debug.LogError("SpawnObject: prefab is null.");
            return null;
        }

        int prefabID = prefab.GetInstanceID();

        // Create pool if it doesn't exist
        if (!genericPools.TryGetValue(prefabID, out var pool))
        {
            CreateGenericPool(prefab, prefabID, poolType);
            pool = genericPools[prefabID];
        }

        var go = pool.Get();
        if (go == null) return null;

        go.transform.SetPositionAndRotation(position, rotation);
        return go;
    }

    /// <summary>
    /// Spawn generic prefab and get component (convenience method)
    /// </summary>
    public T SpawnObject<T>(GameObject prefab, Vector3 position, Quaternion rotation, PoolType poolType = PoolType.GameObject) where T : Component
    {
        var go = SpawnObject(prefab, position, rotation, poolType);
        return go ? go.GetComponent<T>() : null;
    }

    private void CreateGenericPool(GameObject prefab, int prefabID, PoolType poolType)
    {
        var pool = new ObjectPool<GameObject>(
            createFunc: () => CreateGenericInstance(prefab, prefabID, poolType),
            actionOnGet: go => OnGetGeneric(go, prefabID, poolType),
            actionOnRelease: OnRelease,
            actionOnDestroy: OnDestroyGenericInstance,
            collectionCheck: false,
            defaultCapacity: 4,
            maxSize: 256
        );

        genericPools[prefabID] = pool;
    }

    private GameObject CreateGenericInstance(GameObject prefab, int prefabID, PoolType poolType)
    {
        GameObject parent = GetGenericHolder(prefab, prefabID, poolType);
        var go = Instantiate(prefab, parent.transform);
        go.SetActive(false);
        instanceToPrefabID[go] = prefabID;
        return go;
    }

    private void OnGetGeneric(GameObject go, int prefabID, PoolType poolType)
    {
        if (go == null) return;

        // Ensure correct parent
        GameObject parent = GetGenericHolder(null, prefabID, poolType);
        if (go.transform.parent != parent.transform)
            go.transform.SetParent(parent.transform, false);

        go.SetActive(true);
    }

    private void OnDestroyGenericInstance(GameObject go)
    {
        if (go == null) return;
        instanceToPrefabID.Remove(go);
        Destroy(go);
    }

    private GameObject GetGenericHolder(GameObject prefab, int prefabID, PoolType poolType)
    {
        if (genericHolders.TryGetValue(prefabID, out GameObject holder))
            return holder;

        // Create new holder
        string holderName = prefab != null ? prefab.name : $"Generic_{prefabID}";
        GameObject parent = poolType == PoolType.ParticleSystem ? particleSystemHolder : gameObjectHolder;

        holder = new GameObject($"Pool_{holderName}");
        holder.transform.SetParent(parent.transform, false);

        genericHolders[prefabID] = holder;
        return holder;
    }

    /// <summary>
    /// Return an instance to its originating pool (EntityDef or generic GameObject).
    /// </summary>
    public void ReturnObjectToPool(GameObject instance)
    {
        if (instance == null) return;

        // Try EntityDef pool first
        if (instanceToDef.TryGetValue(instance, out var def))
        {
            if (pools.TryGetValue(def, out var pool))
            {
                instance.SetActive(false);
                pool.Release(instance);
                return;
            }
            else
            {
                // Pool not found, cleanup
                instanceToDef.Remove(instance);
                Destroy(instance);
                return;
            }
        }

        // Try generic GameObject pool
        if (instanceToPrefabID.TryGetValue(instance, out var prefabID))
        {
            if (genericPools.TryGetValue(prefabID, out var pool))
            {
                instance.SetActive(false);
                pool.Release(instance);
                return;
            }
            else
            {
                // Pool not found, cleanup
                instanceToPrefabID.Remove(instance);
                Destroy(instance);
                return;
            }
        }

        // Not tracked in any pool, just destroy
        Debug.LogWarning($"ReturnObjectToPool: instance '{instance.name}' not tracked in any pool; destroying.");
        Destroy(instance);
    }
}