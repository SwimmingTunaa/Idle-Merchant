using UnityEngine;

/// <summary>
/// Shows a carry sprite above the customer and sets the Carry animator bool
/// when the customer transitions to Leaving after a successful purchase (desiredItem != null).
/// Pool-safe: resets fully in OnDisable.
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
        if (next == CustomerState.Leaving && customer.desiredItem != null)
            Show(customer.desiredItem);
        else
            Hide();
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
        if (iconRenderer != null)
            iconRenderer.enabled = false;

        if (_animator != null)
            _animator.SetBool(AnimHash.Carry, false);
    }
}
