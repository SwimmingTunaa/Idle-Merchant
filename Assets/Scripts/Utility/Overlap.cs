using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Non-allocating Physics2D.OverlapCircleAll: fills the caller's list (grown as needed, never truncated) with the
/// same hits the legacy call returns (default raycast layers or the given mask, triggers per Physics2D settings).
/// Give each call site its own list — a shared one breaks when a hit's handler runs another overlap.
/// </summary>
public static class Overlap
{
    public static List<Collider2D> Circle(Vector2 point, float radius, List<Collider2D> results, int layerMask = Physics2D.DefaultRaycastLayers)
    {
        var filter = new ContactFilter2D { useTriggers = Physics2D.queriesHitTriggers };
        filter.SetLayerMask(layerMask);
        Physics2D.OverlapCircle(point, radius, filter, results);
        return results;
    }
}
