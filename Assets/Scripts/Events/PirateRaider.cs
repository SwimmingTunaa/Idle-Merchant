using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A pirate during the Pirate Raid event: rides a pooled customer (its look + animator) with the CustomerAgent
/// switched off, walks to the loot pile, grabs stealPercent of one random stack every stealInterval (gone for
/// good when they reach the exit), and runs after carryMax grabs. Click (ClickerManager priority 2c) → Drop: everything
/// carried flies back onto the pile and the pirate flees empty-handed. Removed on despawn so the pooled
/// customer is clean next time.
/// </summary>
public class PirateRaider : MonoBehaviour
{
    private enum Phase { Approach, Loot, Flee }

    private CustomerAgent agent;
    private Animator anim;
    private Phase phase;
    private Vector3 target;
    private float speed, stealInterval, stealTimer, stealPercent;
    private int carryMax;
    private readonly List<(ItemDef item, int qty)> carried = new();
    private static readonly List<ItemDef> pick = new();

    /// <summary>Items carried right now (sum of grabbed stacks).</summary>
    public int Carried { get { int n = 0; foreach (var c in carried) n += c.qty; return n; } }

    public void Begin(CustomerAgent a, float interval, int max, float percent)
    {
        agent = a; stealInterval = interval; carryMax = max; stealPercent = percent;
        anim = GetComponentInChildren<Animator>();
        speed = (a.Stats != null ? a.Stats.MoveSpeed : 2f) * 1.4f;
        a.enabled = false;                       // no customer FSM: we drive it
        if (ShopManager.Instance != null) ShopManager.Instance.Unregister(a);
        var pile = LootPile.Instance != null ? LootPile.Instance.transform.position : transform.position;
        target = new Vector3(pile.x + Random.Range(-2f, 2f), transform.position.y, transform.position.z);
        phase = Phase.Approach;
    }

    void Update()
    {
        if (agent == null) return;
        bool moving = phase != Phase.Loot;
        if (moving)
        {
            float dx = target.x - transform.position.x;
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            if (Mathf.Abs(dx) > 0.01f) transform.localScale = new Vector3(dx > 0 ? 1f : -1f, transform.localScale.y, transform.localScale.z);
            if (Vector2.Distance(transform.position, target) < 0.05f)
            {
                if (phase == Phase.Approach) { phase = Phase.Loot; stealTimer = stealInterval * 0.5f; }
                else Leave();
                return;
            }
        }
        else
        {
            stealTimer -= Time.deltaTime;
            if (stealTimer <= 0f) { stealTimer = stealInterval; Steal(); }
        }
        if (anim != null) anim.SetFloat(AnimHash.Velocity, moving ? 1f : 0f);
    }

    private void Steal()
    {
        var inv = Inventory.Instance;
        if (inv == null) { Flee(); return; }
        pick.Clear();
        foreach (ItemCategory cat in System.Enum.GetValues(typeof(ItemCategory)))
        {
            var dict = inv.GetInventoryType(cat); if (dict == null) continue;
            foreach (var kv in dict) if (kv.Key != null && kv.Value > 0) pick.Add(kv.Key);
        }
        if (pick.Count == 0) { Flee(); return; }   // nothing left to take
        var item = pick[Random.Range(0, pick.Count)];
        var stack = inv.GetInventoryType(item.itemCategory);
        int qty = Mathf.Max(1, Mathf.FloorToInt(stack[item] * stealPercent));
        if (!inv.TryRemove(stack, item, qty)) return;
        carried.Add((item, qty));
        if (LootPile.Instance != null)
        {
            LootPile.Instance.Refresh();
            if (ItemExplosionVFX.Instance != null)
                ItemExplosionVFX.Instance.FlyToWorld(item.icon != null ? item.icon : item.spriteDrop, LootPile.Instance.transform.position + Vector3.up, transform.position + Vector3.up * 0.8f, homing: transform, speed: 1.5f);
        }
        if (DamageNumberManager.Instance != null) DamageNumberManager.Instance.ShowText($"-{qty} {item.displayName}", transform, new Color(0.9f, 0.3f, 0.25f));
        if (carried.Count >= carryMax) Flee();   // grabs, not items
    }

    /// <summary>Player click: carried stock goes back on the pile, pirate runs.</summary>
    public void Drop()
    {
        if (phase == Phase.Flee && carried.Count == 0) return;
        var inv = Inventory.Instance;
        int dropped = Carried;
        foreach (var (item, qty) in carried)
        {
            if (inv != null) inv.Add(inv.GetInventoryType(item.itemCategory), new ResourceStack(item, qty, 0));
            if (LootPile.Instance != null) LootPile.Instance.FlyIn(item, transform.position + Vector3.up * 0.8f, toss: true);
        }
        if (DamageNumberManager.Instance != null)
            DamageNumberManager.Instance.ShowText(dropped > 0 ? $"Dropped {dropped}!" : "Scared off!", transform, new Color(1f, 0.84f, 0.3f));
        carried.Clear();
        if (LootPile.Instance != null) LootPile.Instance.Refresh();
        Flee();
    }

    private void Flee()
    {
        phase = Phase.Flee;
        var exit = ShopManager.Instance != null && ShopManager.Instance.exitPoint != null ? ShopManager.Instance.exitPoint.position : transform.position + Vector3.right * 20f;
        target = new Vector3(exit.x, transform.position.y, transform.position.z);
    }

    private void Leave()
    {
        var a = agent; agent = null;
        a.enabled = true;            // hand the pooled customer back clean
        a.SetHoverOutline(false);
        a.despawnQuietly = true;     // straight to the pool: no death anim, so the re-enabled brain never gets a tick
        a.Despawn();
        a.despawnQuietly = false;
        Destroy(this);
    }
}
