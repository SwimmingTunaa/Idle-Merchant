using UnityEngine;

/// <summary>
/// Shows a carry sprite above the customer and sets the Carry animator bool once the bought item
/// lands on them: Leaving after a purchase (desiredItem != null) starts the LootPile sale relay
/// (pile → counter → customer) and the carry shows on its arrival, so the item is never in two places.
/// Pool-safe: every Hide (incl. OnDisable) invalidates a pending relay.
/// </summary>
[DefaultExecutionOrder(1000)]
public class CustomerCarryDisplay : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CustomerAgent customer;       // auto-found if null
    [SerializeField] private SpriteRenderer iconRenderer;  // child ref

    [Header("Positioning")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.4f, 0f);

    private Animator _animator;
    private int _relay;   // bumped by Hide; a relay arrival with a stale id is ignored

    void Awake()
    {
        if (!customer) customer = GetComponent<CustomerAgent>();
        _animator = customer != null ? customer.GetComponentInChildren<Animator>() : null;
        Hide(force: true);
    }

    void OnEnable()
    {
        if (customer != null)
            customer.OnStateChanged += OnCustomerStateChanged;
        Hide(force: true);
    }

    void OnDisable()
    {
        if (customer != null)
            customer.OnStateChanged -= OnCustomerStateChanged;
        Hide(force: true);
    }

    private void OnCustomerStateChanged(CustomerState prev, CustomerState next)
    {
        Hide();
        if (next != CustomerState.Leaving || customer.desiredItem == null) return;

        ItemDef item = customer.desiredItem;
        if (LootPile.Instance == null) { Show(item); return; }
        int id = _relay;
        Transform landing = iconRenderer != null ? iconRenderer.transform : transform;
        LootPile.Instance.FlySale(item, landing, () => { if (id == _relay) Show(item); });
    }

    private void Show(ItemDef item)
    {
        if (iconRenderer != null)
        {
            iconRenderer.sprite  = item.spriteDrop;
            iconRenderer.enabled = true;
        }

        if (_animator != null)
            _animator.SetBool(AnimHash.Carry, true);
    }

    private void Hide(bool force = false)
    {
        _relay++;
        if (iconRenderer != null)
            iconRenderer.enabled = false;

        if (_animator != null)
            _animator.SetBool(AnimHash.Carry, false);
    }
}
