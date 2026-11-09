using UnityEngine;

/// <summary>
/// Event pennant over a party member's head (added by EventManager.Fire to every member of a party, royal visit or
/// delegation): the event's icon bobbing beside the customer's want bubble while the event runs and they're in the
/// shop, so a party reads as a party in the crowd. Self-removes when the member leaves or is pooled; the event's end
/// removes it too. Pirates keep their own loot stack instead.
/// </summary>
public class EventMarker : MonoBehaviour
{
    private const float Height = 0.7f;   // world units tall, whatever the icon's PPU
    private CustomerAgent agent;
    private int serial;
    private SpriteRenderer sr;
    private float top;

    public static void Attach(CustomerAgent c, Sprite icon)
    {
        if (c == null || icon == null) return;
        if (!c.TryGetComponent(out EventMarker m)) m = c.gameObject.AddComponent<EventMarker>();
        m.Begin(c, icon);
    }

    public static void Remove(CustomerAgent c)
    {
        if (c != null && c.TryGetComponent(out EventMarker m)) Destroy(m);
    }

    private void Begin(CustomerAgent c, Sprite icon)
    {
        agent = c;
        serial = c.SpawnSerial;
        if (sr == null)
        {
            var go = new GameObject("Event Marker");
            go.transform.SetParent(transform, false);
            sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 10000;
            sr.renderingLayerMask = 1 | HoverOutlineFeature.OverlayRenderingLayer;   // never stroked by the hover outline
        }
        sr.sprite = icon;
        float h = icon.bounds.size.y;
        sr.transform.localScale = Vector3.one * (h > 0f ? Height / h : 1f);
        top = c.TryGetComponent(out BoxCollider2D col) ? col.offset.y + col.size.y * 0.5f : 1.2f;
    }

    void LateUpdate()
    {
        if (agent == null || agent.SpawnSerial != serial || !agent.gameObject.activeInHierarchy || agent.State == CustomerState.Exited)
        {
            Destroy(this);
            return;
        }
        float bob = Mathf.Sin(Time.unscaledTime * 3f) * 0.08f;
        // a tag floating above-right of the head, clear of the sprite; the want bubble sits straight above, higher
        sr.transform.SetPositionAndRotation(agent.transform.TransformPoint(new Vector3(0.5f, top + 0.75f + bob, 0f)), Quaternion.identity);
    }

    void OnDestroy()
    {
        if (sr != null) Destroy(sr.gameObject);
    }
}
