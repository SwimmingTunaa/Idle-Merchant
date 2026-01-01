using UnityEngine;
using System.Collections.Generic;

public enum SpawnerType
{
    Mobs,
    Customers
}

/// <summary>
/// Optimized spawner that registers itself with ProgressionManager.
/// Removes need for expensive FindObjectsByType scene scans.
/// </summary>
public class Spawner : MonoBehaviour
{
    public SpawnerType spawnerType;
    [Range(0, 10)] public int layerIndex = 1;
    public List<EntityDef> candidates = new();
    public float spawnsPerMinute = 30f;
    public int maxAlive = 5;

    [Header("Spawn Area")]
    [SerializeField] private BoxCollider2D spawnArea;
    [SerializeField] private BoxCollider2D wander;
    [SerializeField] private LayerMask groundMask;

    private float spawnBudget;
    private readonly List<GameObject> alive = new();

    void Awake()
    {
        // Register with ProgressionManager (replaces FindObjectsByType)
        ProgressionManager.RegisterSpawner(this);
    }

    void OnDestroy()
    {
        // Unregister when destroyed
        ProgressionManager.UnregisterSpawner(this);
    }

    void Update()
    {
        spawnBudget += Time.deltaTime * (spawnsPerMinute / 60f);
        while (spawnBudget >= 1f)
        {
            spawnBudget -= 1f;
            TrySpawn();
        }

        // Clean up null references
        for (int i = alive.Count - 1; i >= 0; i--)
            if (alive[i] == null) alive.RemoveAt(i);
    }

    public virtual void TrySpawn()
    {
        if (alive.Count >= maxAlive || candidates.Count == 0 || spawnArea == null)
            return;

        EntityDef def = PickWeighted(candidates);

        Vector3 pos = GetRandomPointAboveSurface(spawnArea);
        if (pos == Vector3.negativeInfinity)
            return;

        EntityBase go = ObjectPoolManager.Instance.SpawnObject(def, pos, Quaternion.identity).GetComponent<EntityBase>();
        go.Init(def, layerIndex, this, wander);
        alive.Add(go.gameObject);
    }

    public void RemoveFromAlive(GameObject obj)
    {
        alive.Remove(obj);
    }

    public static Vector3 GetRandomPointAboveSurface(BoxCollider2D area)
    {
        Bounds b = area.bounds;
        float x = Random.Range(b.min.x, b.max.x);
        float y = area.bounds.center.y + area.bounds.extents.y;

        return new Vector3(x, y, 0);
    }

    private static EntityDef PickWeighted(List<EntityDef> list)
    {
        float total = 0f;
        foreach (var e in list) total += Mathf.Max(0.001f, e.spawnWeight);
        float r = Random.value * total, run = 0f;
        foreach (var e in list)
        {
            run += Mathf.Max(0.001f, e.spawnWeight);
            if (r <= run) return e;
        }
        return list[list.Count - 1];
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (spawnArea == null) return;
        Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
        Gizmos.DrawCube(spawnArea.bounds.center, spawnArea.bounds.size);
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(spawnArea.bounds.center, spawnArea.bounds.size);
    }
#endif
}