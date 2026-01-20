using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spatial grid for O(1) proximity queries.
/// Divides world into cells - only checks entities in nearby cells.
/// 
/// OLD: foreach (var mob in allMobs) // O(n)
/// NEW: spatialGrid.QueryRadius(pos, range) // O(1) amortized
/// 
/// For 50 entities, reduces query from 50 checks to ~4-9 checks (nearby cells only).
/// </summary>
public class SpatialGrid<T> where T : Component
{
    private Dictionary<Vector2Int, List<T>> grid = new Dictionary<Vector2Int, List<T>>();
    private Dictionary<T, Vector2Int> entityToCell = new Dictionary<T, Vector2Int>();
    private float cellSize;

    public SpatialGrid(float cellSize = 5f)
    {
        this.cellSize = cellSize;
    }

    private Vector2Int GetCell(Vector3 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / cellSize),
            Mathf.FloorToInt(position.y / cellSize)
        );
    }

    /// <summary>
    /// Register entity in grid. Call on spawn.
    /// </summary>
    public void Register(T entity)
    {
        if (entity == null) return;

        var cell = GetCell(entity.transform.position);
        
        if (!grid.ContainsKey(cell))
            grid[cell] = new List<T>();

        if (!grid[cell].Contains(entity))
        {
            grid[cell].Add(entity);
            entityToCell[entity] = cell;
        }
    }

    /// <summary>
    /// Unregister entity from grid. Call on despawn.
    /// </summary>
    public void Unregister(T entity)
    {
        if (entity == null) return;

        if (entityToCell.TryGetValue(entity, out var cell))
        {
            if (grid.ContainsKey(cell))
            {
                grid[cell].Remove(entity);
                
                // Clean up empty cells
                if (grid[cell].Count == 0)
                    grid.Remove(cell);
            }
            entityToCell.Remove(entity);
        }
    }

    /// <summary>
    /// Update entity position in grid. Call if entity moves between cells.
    /// For fast-moving entities, call this periodically (e.g., every 0.5s).
    /// For slow entities, skip this - slight accuracy loss is fine for performance.
    /// </summary>
    public void UpdatePosition(T entity)
    {
        if (entity == null) return;

        var newCell = GetCell(entity.transform.position);

        if (entityToCell.TryGetValue(entity, out var oldCell))
        {
            if (oldCell != newCell)
            {
                // Entity moved to new cell
                if (grid.ContainsKey(oldCell))
                    grid[oldCell].Remove(entity);

                if (!grid.ContainsKey(newCell))
                    grid[newCell] = new List<T>();

                grid[newCell].Add(entity);
                entityToCell[entity] = newCell;
            }
        }
        else
        {
            // Entity not in grid yet
            Register(entity);
        }
    }

    /// <summary>
    /// Query all entities within radius of position.
    /// Returns list you can iterate over.
    /// 
    /// Performance: O(1) for cell lookup + O(k) for nearby entities
    /// where k is typically 5-20, not 50-200 like linear scan.
    /// </summary>
    public List<T> QueryRadius(Vector3 position, float radius)
    {
        var results = new List<T>();
        var centerCell = GetCell(position);
        int cellRadius = Mathf.CeilToInt(radius / cellSize);
        float sqrRadius = radius * radius; // Use squared distance to avoid sqrt

        // Check nearby cells only
        for (int x = -cellRadius; x <= cellRadius; x++)
        {
            for (int y = -cellRadius; y <= cellRadius; y++)
            {
                var checkCell = new Vector2Int(centerCell.x + x, centerCell.y + y);
                
                if (grid.TryGetValue(checkCell, out var entities))
                {
                    foreach (var entity in entities)
                    {
                        if (entity == null || entity.gameObject == null)
                            continue;

                        // Use sqrMagnitude (no sqrt = faster)
                        float sqrDist = (position - entity.transform.position).sqrMagnitude;
                        if (sqrDist <= sqrRadius)
                        {
                            results.Add(entity);
                        }
                    }
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Find nearest entity within radius.
    /// Returns null if none found.
    /// </summary>
    public T QueryNearest(Vector3 position, float radius)
    {
        T nearest = null;
        float nearestSqrDist = float.MaxValue;
        float sqrRadius = radius * radius;

        var centerCell = GetCell(position);
        int cellRadius = Mathf.CeilToInt(radius / cellSize);

        for (int x = -cellRadius; x <= cellRadius; x++)
        {
            for (int y = -cellRadius; y <= cellRadius; y++)
            {
                var checkCell = new Vector2Int(centerCell.x + x, centerCell.y + y);
                
                if (grid.TryGetValue(checkCell, out var entities))
                {
                    foreach (var entity in entities)
                    {
                        if (entity == null || entity.gameObject == null)
                            continue;

                        float sqrDist = (position - entity.transform.position).sqrMagnitude;
                        if (sqrDist <= sqrRadius && sqrDist < nearestSqrDist)
                        {
                            nearestSqrDist = sqrDist;
                            nearest = entity;
                        }
                    }
                }
            }
        }

        return nearest;
    }

    /// <summary>
    /// Clean up null references (call periodically, not every frame).
    /// </summary>
    public void Cleanup()
    {
        var cellsToClean = new List<Vector2Int>();
        
        foreach (var kvp in grid)
        {
            kvp.Value.RemoveAll(e => e == null || e.gameObject == null);
            
            if (kvp.Value.Count == 0)
                cellsToClean.Add(kvp.Key);
        }

        foreach (var cell in cellsToClean)
            grid.Remove(cell);

        // Clean entity-to-cell map
        var entitiesToRemove = new List<T>();
        foreach (var kvp in entityToCell)
        {
            if (kvp.Key == null || kvp.Key.gameObject == null)
                entitiesToRemove.Add(kvp.Key);
        }

        foreach (var entity in entitiesToRemove)
            entityToCell.Remove(entity);
    }

    /// <summary>
    /// Clear all data (call on level unload).
    /// </summary>
    public void Clear()
    {
        grid.Clear();
        entityToCell.Clear();
    }

    /// <summary>
    /// Get total entity count in grid (for debug).
    /// </summary>
    public int Count => entityToCell.Count;

#if UNITY_EDITOR
    /// <summary>
    /// Debug draw grid cells (call in OnDrawGizmos).
    /// </summary>
    public void DebugDraw(Color color)
    {
        Gizmos.color = color;
        
        foreach (var kvp in grid)
        {
            if (kvp.Value.Count == 0) continue;

            Vector3 cellCenter = new Vector3(
                kvp.Key.x * cellSize + cellSize * 0.5f,
                kvp.Key.y * cellSize + cellSize * 0.5f,
                0f
            );

            Gizmos.DrawWireCube(cellCenter, Vector3.one * cellSize);
            
            // Draw entity count
            UnityEditor.Handles.Label(cellCenter, kvp.Value.Count.ToString());
        }
    }
#endif
}