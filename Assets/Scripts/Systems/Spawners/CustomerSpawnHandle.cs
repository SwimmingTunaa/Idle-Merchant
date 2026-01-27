using UnityEngine;

public sealed class CustomerSpawnHandle : MonoBehaviour
{
    private CustomerSpawner _owner;
    private CustomerArcheType _group;
    private bool _reported;

    public void Bind(CustomerSpawner owner, CustomerArcheType group)
    {
        _owner = owner;
        _group = group;
        _reported = false;
    }

    private void OnEnable()
    {
        _reported = false;
    }

    private void OnDisable()
    {
        if (_reported) return;
        _reported = true;

        if (_owner != null)
            _owner.NotifyCustomerDespawned(_group);
    }
}
