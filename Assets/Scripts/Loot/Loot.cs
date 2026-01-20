using UnityEngine;

/// <summary>
/// Loot data container that instantiates its visual (matches mob/customer pattern).
/// The loot container itself is pooled, but visuals are instantiated/destroyed.
/// </summary>
public class Loot : MonoBehaviour, IClickableLoot
{
    [SerializeField] private ItemDef def;
    [SerializeField] private string lootId;
    [SerializeField] private int qty;
    [SerializeField] private int sellValue;

    private SpriteRenderer spriteRenderer;
    private GameObject visualInstance;
    private PorterAgent reservedBy;
    private int layerIndex;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public void Init(ItemDef itemDef, Vector2Int dropAmount, int layer = 1)
    {
        qty = Random.Range(dropAmount.x, dropAmount.y + 1);
        def = itemDef;
        lootId = itemDef.id;
        layerIndex = layer;
        reservedBy = null;
        
        // Instantiate visual (matches EntityBase.CreateVisuals pattern)
        CreateVisual(itemDef);

        // Register with LootManager
        if (LootManager.Instance != null)
        {
            LootManager.Instance.RegisterLoot(this, layerIndex);
        }

        if (showDebugLogs)
            Debug.Log($"[Loot] Initialized {itemDef.displayName} x{qty} on layer {layer}");
    }

    public void Init(ItemDef itemDef, Vector2Int dropAmount)
    {
        Init(itemDef, dropAmount, 1);
    }

    void OnDisable()
    {
        // Unregister from LootManager
        if (LootManager.Instance != null)
        {
            LootManager.Instance.UnregisterLoot(this, layerIndex);
        }

        // Cleanup visual when returning to pool
       // CleanupVisual();
    }

    // ===== VISUAL MANAGEMENT =====

    /// <summary>
    /// Instantiate the visual prefab as a child (matches EntityBase pattern).
    /// Supports nested hierarchy (collider on parent, sprite on child).
    /// </summary>
    private void CreateVisual(ItemDef itemDef)
    {
        if (itemDef == null || itemDef.spriteDrop == null)
        {
            Debug.LogWarning($"[Loot] ItemDef or visualPrefab is null!");
            return;
        }

        // Instantiate visual as child
        spriteRenderer.sprite = itemDef.spriteDrop;
        //visualInstance = Instantiate(itemDef.visualPrefab, transform.position, Quaternion.identity, transform);

        // Setup collider from visual
        ResizeCollider(spriteRenderer.sprite.bounds.size);
        SetupCollider(itemDef);

        if (showDebugLogs)
            Debug.Log($"[Loot] Created visual for {itemDef.displayName}");
    }

    /// <summary>
    /// Destroy the visual instance before returning to pool.
    /// </summary>
    private void CleanupVisual()
    {
        if (visualInstance != null)
        {
            Destroy(visualInstance);
            visualInstance = null;
        }
    }

    private void ResizeCollider(Vector2 size)
    {
        BoxCollider2D parentCollider = GetComponent<BoxCollider2D>();
        if (parentCollider == null) return;

        if (size != Vector2.zero)
        {
            // Use cached size and offset from EntityDef
            parentCollider.size = size;
            parentCollider.offset = def.colliderOffset;
        }
        else if (spriteRenderer != null)
        {
            // Fallback: use sprite renderer bounds (may be incorrect if animator hasn't updated)
            parentCollider.size = spriteRenderer.bounds.size;
            Debug.LogWarning($"[{name}] EntityDef.colliderSize not set, using fallback bounds. Right-click EntityDef → 'Auto-Calculate Collider Size From Sprite'");
        }
    }

    /// <summary>
    /// Setup this loot's collider to match the visual's collider shape.
    /// Searches for collider in visual hierarchy (supports nested structure).
    /// </summary>
    private void SetupCollider(ItemDef itemDef)
    {
        if (visualInstance == null) return;

        // Search for collider on visual root or children
        Collider2D visualCollider = visualInstance.GetComponentInChildren<Collider2D>();
        Collider2D lootCollider = GetComponent<Collider2D>();

        if (visualCollider != null && lootCollider != null)
        {
            // Disable visual's collider (loot's collider handles clicks)
            visualCollider.enabled = false;

            // Copy shape from visual to loot
            lootCollider.CopyShapeFrom(visualCollider);
        }
        else if (visualCollider == null)
        {
            Debug.LogWarning($"[Loot] Visual for '{itemDef.displayName}' has no Collider2D in hierarchy!");
        }
        else if (lootCollider == null)
        {
            Debug.LogWarning($"[Loot] Loot GameObject has no Collider2D component!");
        }
    }

    // ===== MANUAL COLLECTION (Click) =====

    public void OnManualCollect()
    {
        // Manual pickup gives +25% bonus
        int bonusQty = Mathf.FloorToInt(qty * 0.25f);
        int totalQty = qty + bonusQty;
        
        Inventory.Instance.Add(
            Inventory.Instance.GetInventoryType(def.itemCategory), 
            new ResourceStack(def, totalQty, sellValue)
        );

        // Show floating multiplier (always show for manual collection)
        if (DamageNumberManager.Instance != null)
        {
            DamageNumberManager.Instance.ShowLootMultiplier(1.25f, transform);
        }

        if (showDebugLogs)
            Debug.Log($"[Loot] Manual collect: {def.displayName} x{totalQty} (bonus: +{bonusQty})");
        
        // Return to pool
        ReturnToPool();
    }

    // ===== PORTER COLLECTION =====

    /// <summary>
    /// Called by PorterAgent when collecting loot.
    /// Returns the resource stack and returns loot to pool.
    /// </summary>
    public ResourceStack CollectByPorter(PorterAgent porter)
    {
        if (reservedBy != porter)
        {
            Debug.LogWarning($"[Loot] {porter.name} tried to collect {name} but it's reserved by {reservedBy?.name ?? "null"}");
            return default;
        }

        // Create resource stack (no bonus for porter collection)
        ResourceStack stack = new ResourceStack(def, qty, sellValue);

        if (showDebugLogs)
            Debug.Log($"[Loot] Porter collect: {def.displayName} x{qty}");

        // Return to pool
        ReturnToPool();

        return stack;
    }

    // ===== POOLING =====

    private void ReturnToPool()
    {
        // Unregister from LootManager
        if (LootManager.Instance != null)
        {
            LootManager.Instance.UnregisterLoot(this, layerIndex);
        }

        // Visual cleanup happens in OnDisable
        
        // Reset state
        ResetState();

        // Return loot container to pool
        ObjectPoolManager.Instance.ReturnObjectToPool(gameObject);
    }

    private void ResetState()
    {
        def = null;
        lootId = null;
        qty = 0;
        sellValue = 0;
        reservedBy = null;
        layerIndex = 0;
    }

    // ===== RESERVATION SYSTEM =====

    public void SetReservedBy(PorterAgent porter)
    {
        reservedBy = porter;
    }

    public bool IsReserved()
    {
        return reservedBy != null;
    }

    public bool IsReservedBy(PorterAgent porter)
    {
        return reservedBy == porter;
    }

    public PorterAgent GetReservedBy()
    {
        return reservedBy;
    }

    // ===== GETTERS =====

    public ItemDef GetItemDef() => def;
    public int GetQuantity() => qty;
    public int GetLayer() => layerIndex;
}