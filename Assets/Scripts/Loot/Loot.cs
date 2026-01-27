using UnityEngine;

/// <summary>
/// Loot data container for items dropped by mobs.
/// Works with standalone ItemDef (no EntityDef inheritance).
/// </summary>
public class Loot : MonoBehaviour, IClickableLoot
{
    [SerializeField] private ItemDef def;
    [SerializeField] private string lootId;
    [SerializeField] private int qty;
    [SerializeField] private int sellValue;

    private SpriteRenderer spriteRenderer;
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
        
        SetupVisual(itemDef);

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
        if (LootManager.Instance != null)
        {
            LootManager.Instance.UnregisterLoot(this, layerIndex);
        }
    }

    private void SetupVisual(ItemDef itemDef)
    {
        if (itemDef == null || itemDef.spriteDrop == null)
        {
            Debug.LogWarning($"[Loot] ItemDef or spriteDrop is null!");
            return;
        }

        spriteRenderer.sprite = itemDef.spriteDrop;
        
        ResizeCollider(spriteRenderer.sprite.bounds.size);

        if (showDebugLogs)
            Debug.Log($"[Loot] Setup visual for {itemDef.displayName}");
    }

    private void ResizeCollider(Vector2 size)
    {
        BoxCollider2D parentCollider = GetComponent<BoxCollider2D>();
        if (parentCollider == null) return;

        if (size != Vector2.zero)
        {
            parentCollider.size = size;
            parentCollider.offset = Vector2.zero;
        }
        else if (spriteRenderer != null)
        {
            parentCollider.size = spriteRenderer.bounds.size;
            Debug.LogWarning($"[{name}] Sprite bounds zero, using fallback");
        }
    }

    public bool IsReserved => reservedBy != null;
    public PorterAgent ReservedBy => reservedBy;

    public bool TryReserve(PorterAgent porter)
    {
        if (IsReserved) return false;
        reservedBy = porter;
        return true;
    }

    public void ReleaseReservation()
    {
        reservedBy = null;
    }

    public ItemDef GetItemDef() => def;
    public int GetQuantity() => qty;
    public int GetLayer() => layerIndex;

    public void SetReservedBy(PorterAgent porter)
    {
        reservedBy = porter;
    }

    public bool IsReservedBy(PorterAgent porter)
    {
        return reservedBy == porter;
    }

    public PorterAgent GetReservedBy()
    {
        return reservedBy;
    }

    public ResourceStack CollectByPorter(PorterAgent porter)
    {
        if (reservedBy != porter)
        {
            Debug.LogWarning($"[Loot] {porter.name} tried to collect {name} but it's reserved by {reservedBy?.name ?? "null"}");
            return default;
        }

        ResourceStack stack = new ResourceStack(def, qty, sellValue);

        if (showDebugLogs)
            Debug.Log($"[Loot] Porter collect: {def.displayName} x{qty}");

        // Track for milestone progress
        if (ProgressionManager.Instance != null)
        {
            ProgressionManager.Instance.IncrementLootCollected(qty);
        }

        GameSignals.RaiseLootCollected(stack);

        ObjectPoolManager.Instance.ReturnObjectToPool(gameObject);
        return stack;
    }

    public void OnManualCollect()
    {
        if (showDebugLogs)
            Debug.Log($"[Loot] Clicked {def.displayName} x{qty}");

        ResourceStack stack = new ResourceStack(def, qty, sellValue);
        Inventory.Instance.Add(Inventory.Instance.GetInventoryType(def.itemCategory), stack);

        // Track for milestone progress
        if (ProgressionManager.Instance != null)
        {
            ProgressionManager.Instance.IncrementLootCollected(qty);
        }

        GameSignals.RaiseLootCollected(stack);

        ObjectPoolManager.Instance.ReturnObjectToPool(gameObject);
    }
}