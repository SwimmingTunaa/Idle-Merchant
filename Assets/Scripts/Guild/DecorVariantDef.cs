using UnityEngine;

/// <summary>
/// One choosable look for a guild-hall decor slot group (GUILD_HALL_DESIGN.md "Swapping"): a crop from the interior
/// sheet and/or a tint. Listed in <see cref="DecorCatalog"/>. Asset names are save keys (decor.placed / decor.owned) — never rename.
/// </summary>
[CreateAssetMenu(menuName = "Data/Decor Variant", fileName = "Decor_")]
public class DecorVariantDef : ScriptableObject
{
    [Tooltip("Slot group this variant fits (e.g. banner, chandelier, bookcase, bunk); matches the slot's default variant")]
    public string slot;
    [Tooltip("Name shown in the Decorate panel")]
    public string displayName;
    [Tooltip("Sprite placed in the slot")]
    public Sprite sprite;
    [Tooltip("SpriteRenderer colour (multiply; HDR so a teal crop can read red)")]
    [ColorUsage(false, true)]
    public Color tint = Color.white;
    [Tooltip("Guild Seals to buy (a slot's own default is always free)")]
    public int price;
}
