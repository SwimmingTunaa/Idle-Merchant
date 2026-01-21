using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance;

    private GameObject rootHolder;
    private GameObject gameObjectHolder;
    private GameObject particleSystemHolder;

    // EntityDef pools (existing)
    private readonly Dictionary<EntityDef, ObjectPool<GameObject>> pools = new();
    private readonly Dictionary<GameObject, EntityDef> instanceToDef = new();
    private readonly Dictionary<EntityDef, GameObject> defHolders = new();

    // ItemDef pools (new)
    private readonly Dictionary<ItemDef, ObjectPool<GameObject>> itemPools = new();
    private readonly Dictionary<GameObject, ItemDef> instanceToItemDef = new();
    private readonly Dictionary<ItemDef, GameObject> itemDefHolders = new();

    // Generic GameObject pools (existing)
    private readonly Dictionary<int, ObjectPool<GameObject>> genericPools = new();
    private readonly Dictionary<GameObject, int> instanceToPrefabID = new();
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

    // ===== ENTITYDEF POOLING (EXISTING) =====

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

    public T SpawnObject<T>(EntityDef def, Vector3 position, Quaternion rotation, PoolType poolType = PoolType.GameObject) where T : Component
    {
        var go = SpawnObject(def, position, rotation, poolType);
        return go ? go.GetComponent<T>() : null;
    }

    private GameObject GetOrCreateDefHolder(EntityDef def, PoolType poolType)
    {
        if (defHolders.TryGetValue(def, out GameObject holder))
            return holder;

        string holderName = def != null ? def.displayName : "Unknown";
        GameObject parent = poolType == PoolType.ParticleSystem ? particleSystemHolder : gameObjectHolder;
        
        holder = new GameObject($"Pool_{holderName}");
        holder.transform.SetParent(parent.transform, false);
        
        defHolders[def] = holder;
        return holder;
    }

    private void CreatePool(EntityDef def, PoolType poolType)
    {
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

        GameObject parent = GetOrCreateDefHolder(def, poolType);
        var go = Instantiate(def.prefab, parent.transform);
        go.SetActive(false);
        instanceToDef[go] = def;
        return go;
    }

    private void OnGet(GameObject go, EntityDef def, PoolType poolType)
    {
        if (go == null) return;

        GameObject desiredParent = GetOrCreateDefHolder(def, poolType);
        if (go.transform.parent != desiredParent.transform)
            go.transform.SetParent(desiredParent.transform, false);

        go.SetActive(true);
    }

    private void OnDestroyInstance(GameObject go)
    {
        if (go == null) return;
        instanceToDef.Remove(go);
        Destroy(go);
    }

    // ===== ITEMDEF POOLING (NEW) =====

    public GameObject SpawnObject(ItemDef itemDef, Vector3 position, Quaternion rotation, PoolType poolType = PoolType.GameObject)
    {
        if (itemDef == null) { Debug.LogError("SpawnObject: itemDef is null."); return null; }
        if (!itemPools.TryGetValue(itemDef, out var pool))
        {
            CreateItemPool(itemDef, poolType);
            pool = itemPools[itemDef];
        }

        var go = pool.Get();
        if (go == null) return null;

        go.transform.SetPositionAndRotation(position, rotation);
        return go;
    }

    public T SpawnObject<T>(ItemDef itemDef, Vector3 position, Quaternion rotation, PoolType poolType = PoolType.GameObject) where T : Component
    {
        var go = SpawnObject(itemDef, position, rotation, poolType);
        return go ? go.GetComponent<T>() : null;
    }

    private GameObject GetOrCreateItemDefHolder(ItemDef itemDef, PoolType poolType)
    {
        if (itemDefHolders.TryGetValue(itemDef, out GameObject holder))
            return holder;

        string holderName = itemDef != null ? itemDef.displayName : "Unknown";
        GameObject parent = poolType == PoolType.ParticleSystem ? particleSystemHolder : gameObjectHolder;
        
        holder = new GameObject($"Pool_{holderName}");
        holder.transform.SetParent(parent.transform, false);
        
        itemDefHolders[itemDef] = holder;
        return holder;
    }

    private void CreateItemPool(ItemDef itemDef, PoolType poolType)
    {
        var pool = new ObjectPool<GameObject>(
            createFunc: () => CreateItemInstance(itemDef, poolType),
            actionOnGet: go => OnGetItem(go, itemDef, poolType),
            actionOnRelease: OnRelease,
            actionOnDestroy: OnDestroyItemInstance,
            collectionCheck: false,
            defaultCapacity: 4,
            maxSize: 256
        );

        itemPools[itemDef] = pool;
    }

    private GameObject CreateItemInstance(ItemDef itemDef, PoolType poolType)
    {
        if (itemDef == null || itemDef.prefab == null)
        {
            Debug.LogError("CreateItemInstance: ItemDef or itemDef.prefab is null.");
            return null;
        }

        GameObject parent = GetOrCreateItemDefHolder(itemDef, poolType);
        var go = Instantiate(itemDef.prefab, parent.transform);
        go.SetActive(false);
        instanceToItemDef[go] = itemDef;
        return go;
    }

    private void OnGetItem(GameObject go, ItemDef itemDef, PoolType poolType)
    {
        if (go == null) return;

        GameObject desiredParent = GetOrCreateItemDefHolder(itemDef, poolType);
        if (go.transform.parent != desiredParent.transform)
            go.transform.SetParent(desiredParent.transform, false);

        go.SetActive(true);
    }

    private void OnDestroyItemInstance(GameObject go)
    {
        if (go == null) return;
        instanceToItemDef.Remove(go);
        Destroy(go);
    }

    // ===== GENERIC GAMEOBJECT POOLING (EXISTING) =====

    public GameObject SpawnObject(GameObject prefab, Vector3 position, Quaternion rotation, PoolType poolType = PoolType.GameObject)
    {
        if (prefab == null)
        {
            Debug.LogError("SpawnObject: prefab is null.");
            return null;
        }

        int prefabID = prefab.GetInstanceID();

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

        string holderName = prefab != null ? prefab.name : $"Generic_{prefabID}";
        GameObject parent = poolType == PoolType.ParticleSystem ? particleSystemHolder : gameObjectHolder;

        holder = new GameObject($"Pool_{holderName}");
        holder.transform.SetParent(parent.transform, false);

        genericHolders[prefabID] = holder;
        return holder;
    }

    // ===== SHARED POOLING METHODS =====

    private void OnRelease(GameObject go)
    {
        if (go == null) return;
        
        go.transform.position = new Vector3(-9999f, -9999f, 0f);
        go.SetActive(false);
    }

    public void ReturnObjectToPool(GameObject instance)
    {
        if (instance == null) return;

        // Try ItemDef pool first (new)
        if (instanceToItemDef.TryGetValue(instance, out var itemDef))
        {
            if (itemPools.TryGetValue(itemDef, out var itemPool))
            {
                instance.SetActive(false);
                itemPool.Release(instance);
                return;
            }
            else
            {
                instanceToItemDef.Remove(instance);
                Destroy(instance);
                return;
            }
        }

        // Try EntityDef pool (existing)
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
                instanceToDef.Remove(instance);
                Destroy(instance);
                return;
            }
        }

        // Try generic GameObject pool (existing)
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
                instanceToPrefabID.Remove(instance);
                Destroy(instance);
                return;
            }
        }

        Debug.LogWarning($"ReturnObjectToPool: instance '{instance.name}' not tracked in any pool; destroying.");
        Destroy(instance);
    }
}