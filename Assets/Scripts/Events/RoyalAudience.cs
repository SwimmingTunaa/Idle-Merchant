using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The lead royal of a Royal Visit / Delegation: instead of queueing they walk to the dais by the counter and wait
/// audienceSeconds for the player to click them and present a piece (AudiencePanelController). The fancied piece
/// (a random preferredItems entry in stock) pays fanciedMultiplier × list, two alternatives altMultiplier ×, plus
/// audienceSeals; they leave carrying it. Timeout = "Snubbed!": they leave empty-handed and the event's cooldown is
/// multiplied. Rides the pooled customer with its CustomerAgent off (like PirateRaider); hands it back on leaving.
/// </summary>
public class RoyalAudience : MonoBehaviour
{
    public struct Offer { public ItemDef item; public float multiplier; public bool fancied; public int Gold => Mathf.RoundToInt(item.sellPrice * multiplier); }

    private CustomerAgent agent;
    private Animator anim;
    private EventDef def;
    private EventManager.ActiveEvent evt;
    private Vector3 target;
    private float speed, waitLeft;
    private bool arrived, done;

    public string Title => agent != null && agent.Def != null ? agent.Def.displayName : "Royal";
    public float WaitLeft => waitLeft;
    public bool Waiting => arrived && !done;
    public Sprite Portrait { get { var sr = GetComponentInChildren<SpriteRenderer>(); return sr != null ? sr.sprite : null; } }

    public void Begin(CustomerAgent a, EventDef d, EventManager.ActiveEvent e)
    {
        agent = a; def = d; evt = e;
        anim = GetComponentInChildren<Animator>();
        speed = (a.Stats != null ? a.Stats.MoveSpeed : 2f);
        waitLeft = d.audienceSeconds;
        a.enabled = false;
        if (ShopManager.Instance != null) ShopManager.Instance.Unregister(a);
        var q = QueueController.Instance;
        var counter = q != null && q.counterPoint != null ? q.counterPoint.position : transform.position;
        target = new Vector3(counter.x - 1.6f, transform.position.y, transform.position.z);   // the dais: just left of the counter
        evt.note = $"{Title} on the way";
    }

    void Update()
    {
        if (agent == null || done) return;
        if (!arrived)
        {
            float dx = target.x - transform.position.x;
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            if (Mathf.Abs(dx) > 0.01f) transform.localScale = new Vector3(dx > 0 ? 1f : -1f, transform.localScale.y, transform.localScale.z);
            if (anim != null) anim.SetFloat(AnimHash.Velocity, 1f);
            if (Vector2.Distance(transform.position, target) < 0.05f)
            {
                arrived = true;
                if (anim != null) anim.SetFloat(AnimHash.Velocity, 0f);
                transform.localScale = new Vector3(-1f, transform.localScale.y, transform.localScale.z);   // face the counter
                agent.SetHoverOutline(false);
                EventManager.Announce(this, $"{Title} awaits an audience!");
            }
            return;
        }
        waitLeft -= Time.deltaTime;
        int s = Mathf.CeilToInt(waitLeft);
        evt.note = $"Click {Title} to present · {s / 60}:{s % 60:00}";
        if (waitLeft <= 0f) Snub();
    }

    /// <summary>What the player can present: the fancied piece (if any is in stock) at ×fancied, then the two priciest
    /// other sellable crafted pieces at ×alt. Empty when the shop has nothing for sale at all.</summary>
    public List<Offer> BuildOffers()
    {
        var offers = new List<Offer>();
        var sm = SalesManager.Instance; var inv = Inventory.Instance;
        if (sm == null || inv == null || agent == null || agent.Def is not CustomerDef cd) return offers;

        var fancied = new List<ItemDef>();
        if (cd.preferredItems != null) foreach (var it in cd.preferredItems) if (it != null && sm.GetAvailableForSale(it) > 0) fancied.Add(it);
        ItemDef pick = fancied.Count > 0 ? fancied[Random.Range(0, fancied.Count)] : null;
        if (pick != null) offers.Add(new Offer { item = pick, multiplier = def.fanciedMultiplier, fancied = true });

        var alts = new List<ItemDef>();
        foreach (ItemCategory cat in System.Enum.GetValues(typeof(ItemCategory)))
        {
            var dict = inv.GetInventoryType(cat); if (dict == null) continue;
            foreach (var kv in dict) if (kv.Key != null && kv.Key != pick && kv.Key.sellPrice > 0 && sm.GetAvailableForSale(kv.Key) > 0) alts.Add(kv.Key);
        }
        alts.Sort((a, b) => b.sellPrice.CompareTo(a.sellPrice));
        for (int i = 0; i < alts.Count && i < 2; i++) offers.Add(new Offer { item = alts[i], multiplier = def.altMultiplier, fancied = false });
        return offers;
    }

    /// <summary>Hand the piece over: stock out, gold (×multiplier, banks on coin arrival) + seals in, the royal leaves carrying it.</summary>
    public bool Present(Offer offer)
    {
        if (done || offer.item == null) return false;
        var inv = Inventory.Instance;
        if (!inv.TryRemove(inv.GetInventoryType(offer.item.itemCategory), offer.item, 1)) return false;
        inv.InventoryDebugUi();
        if (LootPile.Instance != null) LootPile.Instance.Refresh();
        int gold = offer.Gold;
        if (DamageNumberManager.Instance != null) DamageNumberManager.Instance.ShowGoldGain(gold, transform); else inv.AddGold(gold);
        if (ContractManager.Instance != null && def.audienceSeals > 0) ContractManager.Instance.GrantSeals(def.audienceSeals);
        EventManager.Announce(this, offer.fancied ? "Delighted!" : "Pleased.");
        evt.note = $"{Title} presented {offer.item.displayName}";
        Leave(offer.item);
        return true;
    }

    private void Snub()
    {
        evt.snubbed = true;
        evt.note = $"{Title} was snubbed";
        EventManager.Announce(this, "Snubbed!");
        Leave(null);
    }

    private void Leave(ItemDef carrying)
    {
        done = true;
        var a = agent; agent = null;
        a.enabled = true;                              // the customer brain walks them out
        a.SetHoverOutline(false);
        if (carrying != null) a.LeaveWithPurchase(carrying, 1);
        else { a.walkoutReason = WalkoutReason.Impatient; a.desiredItem = null; a.desiredQty = 0; a.ChangeState(CustomerState.Leaving); }
        Destroy(this);
    }
}
