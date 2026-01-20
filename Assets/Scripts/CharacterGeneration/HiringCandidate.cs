using UnityEngine;

/// <summary>
/// Represents a hiring candidate with traits.
/// Generated for player to choose from in hiring UI.
/// Now includes runtime sprite generation for shop display.
/// </summary>
[System.Serializable]
public struct HiringCandidate
{
    public EntityDef entityDef;
    public Identity identity;
    public TraitInstance[] traits; // 1-2 traits
    public CursedComboDef cursedCombo; // null if not cursed
    public int hireCost;
    public CharacterAppearanceIndices appearanceIndices;
    
    // Cached generated sprite (lazy-loaded)
    private Sprite _cachedShopSprite;
    
    public bool IsCursed => cursedCombo != null;
    public string DisplayName => identity?.DisplayName ?? "Unknown";
    
    /// <summary>
    /// Get shop sprite for this candidate.
    /// Generates on first access and caches result.
    /// </summary>
    public Sprite GetShopSprite()
    {
        if (_cachedShopSprite == null)
        {
            _cachedShopSprite = CharacterSpriteGenerator.GenerateSprite(this);
        }
        return _cachedShopSprite;
    }
    
    /// <summary>
    /// Force regenerate shop sprite (useful for visual config changes).
    /// </summary>
    public void RegenerateShopSprite()
    {
        _cachedShopSprite = CharacterSpriteGenerator.GenerateSprite(this);
    }
}