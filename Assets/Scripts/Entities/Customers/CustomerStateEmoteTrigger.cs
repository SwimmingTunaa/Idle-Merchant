using System;
using UnityEngine;

/// <summary>
/// Maps CustomerAgent state transitions to EmoteDisplay emotes.
/// Configure all emote conditions in the inspector — no per-emote wrapper scripts needed.
/// To add a new emote: add the EmoteType to EmoteDisplay.EmoteType, assign its sprite
/// in EmoteDisplay.Emotes, then add a row here in Conditions.
/// </summary>
[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(EmoteDisplay))]
public class CustomerStateEmoteTrigger : MonoBehaviour
{
    [Serializable]
    public struct EmoteCondition
    {
        public CustomerState triggerState;

        [Tooltip("If true, only fires when desiredItem == null at the time of the transition.")]
        public bool requireNoItem;

        public EmoteDisplay.EmoteType emoteType;
    }

    [Header("Refs")]
    [SerializeField] private CustomerAgent customer;  // auto-found if null

    [Header("Conditions")]
    [SerializeField] private EmoteCondition[] conditions;

    private EmoteDisplay _display;

    void Awake()
    {
        if (!customer) customer = GetComponent<CustomerAgent>();
        _display = GetComponent<EmoteDisplay>();
        _display.Hide();
    }

    void OnEnable()
    {
        if (customer != null)
            customer.OnStateChanged += OnCustomerStateChanged;
        _display?.Hide();
    }

    void OnDisable()
    {
        if (customer != null)
            customer.OnStateChanged -= OnCustomerStateChanged;
        // EmoteDisplay.OnDisable handles icon reset
    }

    private void OnCustomerStateChanged(CustomerState prev, CustomerState next)
    {
        foreach (var cond in conditions)
        {
            if (cond.triggerState != next) continue;
            if (cond.requireNoItem && customer.desiredItem != null) continue;

            _display.Show(cond.emoteType);
            return;
        }

        _display.Hide();
    }
}
