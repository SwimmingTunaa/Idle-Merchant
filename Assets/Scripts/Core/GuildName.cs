using UnityEngine;

/// <summary>The guild's name on the top-bar pennant: two short words rolled once per guild, saved in SaveData.meta.</summary>
public static class GuildName
{
    // ≤ 8 letters each: the pennant is one word wide in blackletter at h2
    private static readonly string[] First =
    {
        "Gilded", "Iron", "Copper", "Silver", "Amber", "Crimson", "Hollow", "Ember", "Lantern", "Thorn",
        "Velvet", "Salted", "Mossy", "Cobalt", "Dusty", "Hearth", "Stone", "Bramble", "Quiet", "Tallow",
    };
    private static readonly string[] Second =
    {
        "Anvil", "Ledger", "Lantern", "Satchel", "Compass", "Cauldron", "Tankard", "Shield", "Crown", "Pickaxe",
        "Wagon", "Coffer", "Cellar", "Bellows", "Mantle", "Quill", "Hammer", "Tally", "Scale", "Lockbox",
    };

    private static string current;

    /// <summary>Rolled on first read (a new guild); SaveManager sets the saved one before the HUD reads it.</summary>
    public static string Current => string.IsNullOrEmpty(current) ? current = Roll() : current;

    public static void Set(string name) { if (!string.IsNullOrEmpty(name)) current = name; }

    public static string Roll() => First[Random.Range(0, First.Length)] + " " + Second[Random.Range(0, Second.Length)];

    /// <summary>Pennant text: one word per line.</summary>
    public static string TwoLines => Current.Replace(' ', '\n');
}
