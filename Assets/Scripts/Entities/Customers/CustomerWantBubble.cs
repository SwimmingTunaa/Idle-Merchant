using UnityEngine;
#if TMP_PRESENT
using TMPro;
#endif

/// <summary>
/// Binds the speech-bubble (child) above a customer’s head to their current desire.
/// - Replaces the placeholder icon with the ItemDef's icon (taken from visualPrefab's SpriteRenderer).
/// - Updates quantity text (optional).
/// - Shows while SeekingQueue/Queueing/Buying, hides otherwise.
/// - Intent beats: "?" while browsing (Wander/Idle), "!" flash on deciding, walkout reason icon when leaving empty-handed.
/// Designed to be pool-friendly.
/// </summary>
[DefaultExecutionOrder(1000)] // after CustomerAgent Init/EnterState
public class CustomerWantBubble : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CustomerAgent customer;       // auto-found if null
    [SerializeField] private Transform bubbleRoot;         // your existing bubble child
    [SerializeField] private SpriteRenderer iconRenderer;  // placeholder icon inside bubble
#if TMP_PRESENT
    [SerializeField] private TMP_Text qtyText;             // optional "x3" etc.
#endif

    [Header("Visibility")]
    [Tooltip("Which states should show the bubble?")]
    public bool showInSeekingQueue = true;
    public bool showInQueueing     = true;
    public bool showInBuying       = true;

    [Header("Intent Icons")]
    [Tooltip("Shown while browsing (Wander/Idle), before a want is picked.")]
    [SerializeField] private Sprite thinkingSprite;
    [Tooltip("Flashed the moment a want is picked, before the item icon.")]
    [SerializeField] private Sprite decidedSprite;
    [Tooltip("Walkout: nothing matching their preference was for sale.")]
    [SerializeField] private Sprite noStockSprite;
    [Tooltip("Walkout: matching stock existed but none fit their budget.")]
    [SerializeField] private Sprite tooExpensiveSprite;
    [SerializeField] private float decideFlashDuration = 0.6f;

    // Cache last values to avoid work every frame
    private ItemDef _lastItem;
    private int _lastQty = -1;
    private Sprite _lastOverride;
    private float _decideFlashUntil;

    void Awake()
    {
        if (!customer) customer = GetComponent<CustomerAgent>();
        if (!bubbleRoot)
        {
            // Try a sensible default find: first child named "Bubble" or with a SpriteRenderer under it
            var tr = transform.Find("Bubble");
            if (tr) bubbleRoot = tr;
        }

        // Try auto-find icon under bubble
        if (!iconRenderer && bubbleRoot)
            iconRenderer = bubbleRoot.GetComponentInChildren<SpriteRenderer>(includeInactive: true);

        // Safe default: start hidden
        SetVisible(false, force: true);
    }

    void OnEnable()
    {
        // Listen to FSM transitions so we can toggle instantly
        if (customer != null)
            customer.OnStateChanged += OnCustomerStateChanged;

        // On pooled respawn, force refresh
        _lastItem = null;
        _lastQty = -1;
        _lastOverride = null;
        _decideFlashUntil = 0f;
        // RefreshAll(force: true);
        SetVisible(false, force: true);
    }

    void OnDisable()
    {
        if (customer != null)
            customer.OnStateChanged -= OnCustomerStateChanged;
    }

    void LateUpdate()
    {
        if (!customer || !bubbleRoot) return;

        // Only refresh content when something changed
        if (_lastOverride != GetOverrideIcon() || _lastItem != customer.desiredItem || _lastQty != customer.desiredQty)
        {
            RefreshAll();
        }
    }

    private void OnCustomerStateChanged(CustomerState prev, CustomerState next)
    {
        if (next == CustomerState.SeekingQueue && (prev == CustomerState.Wander || prev == CustomerState.Idle))
            _decideFlashUntil = Time.time + decideFlashDuration;

        RefreshAll();
    }

    // Intent icon that replaces the item icon for the current state, or null to show the desired item.
    private Sprite GetOverrideIcon() => customer.State switch
    {
        CustomerState.Wander or CustomerState.Idle => thinkingSprite,
        CustomerState.SeekingQueue when Time.time < _decideFlashUntil => decidedSprite,
        // Leaving with a purchase keeps desiredItem (CustomerCarryDisplay owns that); only walkouts get a reason
        CustomerState.Leaving when customer.desiredItem == null => customer.walkoutReason switch
        {
            WalkoutReason.NoStock      => noStockSprite,
            WalkoutReason.TooExpensive => tooExpensiveSprite,
            _                          => null
        },
        _ => null
    };

    private void UpdateVisibilityForState(CustomerState state)
    {
        bool show = _lastOverride != null || state switch
        {
            CustomerState.SeekingQueue => showInSeekingQueue,
            CustomerState.Queueing     => showInQueueing,
            CustomerState.Buying       => showInBuying,
            _                          => false
        };
   
        SetVisible(show);
    }

    private void RefreshAll(bool force = false)
    {
        if (!customer) return;

        // Update icon + qty
        ApplyIconFromItem(customer.desiredItem, force);
        ApplyQty(customer.desiredQty, force);

        _lastOverride = GetOverrideIcon();
        if (_lastOverride && iconRenderer)
        {
            iconRenderer.sprite  = _lastOverride;
            iconRenderer.color   = Color.white;
            iconRenderer.enabled = true;
#if TMP_PRESENT
            if (qtyText) qtyText.text = string.Empty;
#endif
        }

        // Toggle visibility logically
        UpdateVisibilityForState(customer.State);
    }

    private void ApplyIconFromItem(ItemDef item, bool force)
    {
        if (!iconRenderer) return;

        _lastItem = item;

        if (item == null)
        {
            iconRenderer.enabled = false;
            return;
        }

        // Pull sprite from the ItemDef's visualPrefab (validated to have a SpriteRenderer)
        // ItemDef validation ensures GetComponentInChildren<SpriteRenderer>() exists on the visualPrefab.
        // (See ItemDef.OnValidate) 
        var visual = item.spriteDrop;
        if (!visual)
        {
            iconRenderer.enabled = false;
            return;
        }

      
        if (visual)
        {
            iconRenderer.sprite = visual;
            iconRenderer.enabled = true;

            // Optionally tint per category (use same palette as LootVisual)
            // Comment out if you want strict sprite colors only.
            switch (item.itemCategory)
            {
                case ItemCategory.Basic:  iconRenderer.color = Color.white; break;
                case ItemCategory.Advanced: iconRenderer.color = new Color(0.5f, 0.8f, 1f, 1f); break;
                case ItemCategory.Premium:  iconRenderer.color = new Color(1f, 0.8f, 0.3f, 1f); break;
            }
        }
        else
        {
            iconRenderer.enabled = false;
        }
    }

    private void ApplyQty(int qty, bool force)
    {
        _lastQty = qty;

#if TMP_PRESENT
        if (qtyText)
        {
            if (qty > 1) qtyText.text = $"x{qty}";
            else         qtyText.text = string.Empty;
        }
#endif
    }

    private void SetVisible(bool visible, bool force = false)
    {
        if (!bubbleRoot) return;

        // if (!bubbleRoot.gameObject.activeSelf != visible || force)
            bubbleRoot.gameObject.SetActive(visible);
    }
}
