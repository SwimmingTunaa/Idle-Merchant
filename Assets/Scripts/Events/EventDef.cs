using UnityEngine;

public enum EventKind { RushHour, AdventurerParty, RoyalVisit, Delegation, PirateRaid }   // serialized ints: append only

/// <summary>
/// One timed world event (data only). EventManager rolls these from its list, runs the kind's effect for
/// durationSeconds and shows a card on the HUD event tracker. Asset name is the identity.
/// </summary>
[CreateAssetMenu(menuName = "Data/Event", fileName = "Event_")]
public class EventDef : ScriptableObject
{
    public string displayName;
    [Tooltip("One line under the name on the HUD card, e.g. \"Customers ×3\"")]
    public string effectLine;
    public Sprite icon;
    public EventKind kind;

    [Header("Roll")]
    [Tooltip("Guild stars needed before this event can roll")]
    public int unlockStar;
    public float weight = 1f;
    public float durationSeconds = 90f;
    [Tooltip("Game seconds after it ends before it can roll again")]
    public float cooldownSeconds = 600f;

    [Header("Kind parameters")]
    [Tooltip("RushHour: customer spawn rate multiplier")]
    public float rateMultiplier = 3f;
    [Tooltip("AdventurerParty: how many arrive together, and who")]
    public int partySize = 5;
    public CustomerDef partyCustomer;
    [Tooltip("Delegation / PirateRaid: these defs arrive together, one each (lead first)")]
    public CustomerDef[] partyMembers;
    [Tooltip("PirateRaid: seconds between grabs per pirate, grabs each makes before running, and the share of one random stack a grab takes (at least 1)")]
    public float stealInterval = 4f;
    public int carryMax = 3;
    [Range(0.01f, 0.5f)] public float stealPercent = 0.15f;

    [Header("Audience (RoyalVisit / Delegation)")]
    [Tooltip("The lead waits this long at the dais for the player to present a piece; then they're snubbed")]
    public float audienceSeconds = 60f;
    [Tooltip("Price multiplier for the piece they fancy, and for the two alternatives")]
    public float fanciedMultiplier = 3f;
    public float altMultiplier = 1.5f;
    public int audienceSeals = 20;
    [Tooltip("A snub multiplies this event's cooldown")]
    public float snubCooldownMultiplier = 2f;
}
