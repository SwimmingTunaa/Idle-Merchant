using UnityEngine;

/// <summary>
/// Represents the visual component of loot (sprite, VFX, etc).
/// Pooled separately from the Loot logic component.
/// Gets parented to Loot gameobjects at runtime.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class LootVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    
    [Header("Optional VFX")]
    [SerializeField] private ParticleSystem spawnVFX;
    [SerializeField] private ParticleSystem idleVFX;
    
    [Header("Animation")]
    [SerializeField] private bool bobbing = true;
    [SerializeField] private float bobbingSpeed = 2f;
    [SerializeField] private float bobbingHeight = 0.1f;
    
    private Vector3 baseLocalPosition;
    private float bobbingTime;

    void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void OnEnable()
    {
        // Reset animation state
        bobbingTime = 0f;
        baseLocalPosition = transform.localPosition;
        
        // Play spawn VFX if available
        if (spawnVFX != null)
            spawnVFX.Play();
    }

    void Update()
    {
        if (bobbing)
        {
            bobbingTime += Time.deltaTime * bobbingSpeed;
            float offset = Mathf.Sin(bobbingTime) * bobbingHeight;
            transform.localPosition = baseLocalPosition + new Vector3(0f, offset, 0f);
        }
    }

    /// <summary>
    /// Initialize visual with item data.
    /// Called by Loot when visual is acquired.
    /// </summary>
    public void Initialize(ItemDef itemDef)
    {
        if (itemDef == null)
        {
            Debug.LogWarning("[LootVisual] Initialize called with null ItemDef!");
            return;
        }

        // You could set sprite here if ItemDef had a sprite field
        // For now, assume the visual prefab already has the correct sprite set up
        
        // Optional: Set color tint based on item rarity
        if (spriteRenderer != null)
        {
            spriteRenderer.color = GetRarityColor(itemDef.itemCategory);
        }
    }

    /// <summary>
    /// Cleanup before returning to pool.
    /// Called by Loot.
    /// </summary>
    public void Cleanup()
    {
        // Stop any VFX
        if (spawnVFX != null)
            spawnVFX.Stop();
        
        if (idleVFX != null)
            idleVFX.Stop();
        
        // Reset transform
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    private Color GetRarityColor(ItemCategory category)
    {
        // Optional: tint based on category
        switch (category)
        {
            case ItemCategory.Common:
                return new Color(1f, 1f, 1f, 1f); // White
            case ItemCategory.Crafted:
                return new Color(0.5f, 0.8f, 1f, 1f); // Light blue
            case ItemCategory.Luxury:
                return new Color(1f, 0.8f, 0.3f, 1f); // Gold
            default:
                return Color.white;
        }
    }

    // ===== EDITOR HELPERS =====

#if UNITY_EDITOR
    void OnValidate()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }
#endif
}