using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stacks one item icon per carried loot stack above the porter, at native scale (same size as a dropped item). Driven by
/// PorterAgent.Refresh calls at the points carriedLoot changes — no polling.
/// Pool-safe: hides all icons in OnDisable.
/// </summary>
public class PorterCarryDisplay : MonoBehaviour
{
    [SerializeField] private int maxIcons = 5;
    [SerializeField] private Vector3 baseOffset = new Vector3(0f, 2.25f, 0f);
    [SerializeField] private float spacing = 0.5f;
    [SerializeField] private float jitterX = 0.12f;

    private SpriteRenderer body;
    private SpriteRenderer[] icons;

    void Awake()
    {
        body = GetComponentInChildren<SpriteRenderer>(); // grab before our icons exist
        icons = new SpriteRenderer[maxIcons];
        for (int i = 0; i < maxIcons; i++)
        {
            var go = new GameObject($"CarryIcon{i}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = baseOffset + new Vector3(i % 2 == 0 ? -jitterX : jitterX, i * spacing, 0f);
            icons[i] = go.AddComponent<SpriteRenderer>();
            icons[i].enabled = false;
        }
    }

    void OnDisable() => Refresh(null);

    public void Refresh(List<ResourceStack> carried)
    {
        if (icons == null) return;
        int n = carried == null ? 0 : carried.Count;
        for (int i = 0; i < icons.Length; i++)
        {
            var sr = icons[i];
            sr.enabled = i < n && carried[i].itemDef != null;
            if (!sr.enabled) continue;
            sr.sprite = carried[i].itemDef.spriteDrop;
            // PermanentOutline flags every child renderer on enable; strip hover (2) + friendly (4) bits.
            sr.renderingLayerMask &= ~6u;
            if (body != null)
            {
                sr.sortingLayerID = body.sortingLayerID;
                sr.sortingOrder = body.sortingOrder + 1 + i;
            }
        }
    }
}
