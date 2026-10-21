using UnityEngine;
using TMPro;

/// <summary>How a combat damage number reads: Crit = big gold and lingers, Splash = small and dim (cleave).</summary>
public enum DamageStyle { Normal, Crit, Splash }

/// <summary>
/// Floating damage number that appears at hit position, floats up, and fades out.
/// Pooled for performance.
/// </summary>
[RequireComponent(typeof(TextMeshPro))]
public class DamageNumber : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float floatSpeed = 2f;
    [SerializeField] private float lifetime = 1f;
    [SerializeField] private AnimationCurve fadeAlpha = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    
    [Header("Randomization")]
    [SerializeField] private float horizontalSpread = 0.3f;
    
    [Header("Style")]
    [SerializeField] private float normalFontSize = 4f;
    [SerializeField] private float critFontSize = 6f;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color critColor = Color.yellow;
    [SerializeField] private Color lootColor = Color.green;
    [SerializeField] private float lootFontSize = 5f;

    [Header("Class Hits")]
    [SerializeField] private float hitCritFontSize = 24f;
    [SerializeField] private Color hitCritColor = new Color(1f, 0.82f, 0.18f);
    [Tooltip("Crit numbers live this many times longer than a normal hit.")]
    [SerializeField] private float hitCritLifetimeScale = 1.4f;
    [SerializeField] private float splashFontSize = 12f;
    [SerializeField] private Color splashColor = new Color(0.78f, 0.78f, 0.78f, 0.8f);
    [Tooltip("Mage % max-HP bonus portion, shown as \"+N\" after the hit.")]
    [SerializeField] private Color arcaneBonusColor = new Color(0.72f, 0.48f, 1f);
    
    [Header("Gold Sprite")]
    [SerializeField] private SpriteRenderer goldSpriteRenderer;
    [SerializeField] private Vector3 goldSpriteOffset = new Vector3(-0.3f, 0f, 0f);
    [SerializeField] private float goldSpriteScale = 0.8f;
    
    private TextMeshPro textMesh;
    private float elapsed;
    private Vector3 velocity;
    private Color baseColor;
    private float life;   // per-show lifetime — every Show* path sets it (pooled reuse)
    // Gold carried by this number, banked into Inventory when its coin reaches the counter
    private float pendingGold;

    void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
        baseColor = textMesh.color;
    }

    /// <summary>
    /// Initialize and display damage number
    /// </summary>
    public void Show(float damage, Vector3 worldPosition, bool isCrit = false)
    {
        life = lifetime;
        
        // Position
        transform.position = worldPosition;
        
        // Random horizontal drift
        float randomX = Random.Range(-horizontalSpread, horizontalSpread);
        velocity = new Vector3(randomX, floatSpeed, 0f);
        
        // Text content — always whole numbers (fractions from multipliers/crits read as noise)
        textMesh.text = Mathf.RoundToInt(damage).ToString();
        
        
        // Style based on crit
        if (isCrit)
        {
            textMesh.fontSize = critFontSize;
            textMesh.color = critColor;
            baseColor = critColor;
        }
        else
        {
            textMesh.fontSize = normalFontSize;
            textMesh.color = normalColor;
            baseColor = normalColor;
        }
        
        // Hide gold sprite for damage numbers
        if (goldSpriteRenderer != null)
            goldSpriteRenderer.enabled = false;
        
        elapsed = 0f;
        
    }

    /// <summary>Adventurer hit: style + optional Mage bonus tinted after the hit ("120+45").</summary>
    public void ShowHit(float damage, float bonus, Vector3 worldPosition, DamageStyle style)
    {
        Show(damage, worldPosition);
        if (bonus > 0f)
            textMesh.text += $"<color=#{ColorUtility.ToHtmlStringRGB(arcaneBonusColor)}>+{Mathf.RoundToInt(bonus)}</color>";

        if (style == DamageStyle.Crit)
        {
            textMesh.fontSize = hitCritFontSize;
            baseColor = hitCritColor;
            life = lifetime * hitCritLifetimeScale;
        }
        else if (style == DamageStyle.Splash)
        {
            textMesh.fontSize = splashFontSize;
            baseColor = splashColor;
        }
        textMesh.color = baseColor;
    }

    /// <summary>
    /// Show an arbitrary text hint that floats up (e.g. a one-time UI nudge).
    /// </summary>
    public void ShowText(string message, Vector3 worldPosition, Color? color = null)
    {
        life = lifetime;
        transform.position = worldPosition;
        velocity = new Vector3(0f, floatSpeed, 0f);
        textMesh.text = message;
        textMesh.fontSize = normalFontSize;
        textMesh.enabled = true;
        baseColor = color ?? normalColor;
        textMesh.color = baseColor;
        if (goldSpriteRenderer != null)
            goldSpriteRenderer.enabled = false;
        elapsed = 0f;
    }

    /// <summary>
    /// Show loot collection multiplier with green styling (e.g., x1.25)
    /// </summary>
    public void ShowLootMultiplier(float multiplier, Vector3 worldPosition)
    {
        life = lifetime;
        
        // Position
        transform.position = worldPosition;
        
        // Random horizontal drift
        float randomX = Random.Range(-horizontalSpread, horizontalSpread);
        velocity = new Vector3(randomX, floatSpeed, 0f);
        
        // Format as multiplier (e.g., x1.25)
        textMesh.text = $"x{multiplier:F2}";
        
        
        // Green loot style
        textMesh.fontSize = lootFontSize;
        textMesh.color = lootColor;
        baseColor = lootColor;
        
        // Hide gold sprite
        if (goldSpriteRenderer != null)
            goldSpriteRenderer.enabled = false;
        
        elapsed = 0f;
        
    }

    /// <summary>
    /// Show gold gain with gold styling and optional sprite
    /// </summary>
    public void ShowGold(float amount, Vector3 worldPosition, bool isCrit = false)
    {
        life = lifetime;
        pendingGold = amount;

        // Position
        transform.position = worldPosition;
        
        // Random horizontal drift
        float randomX = Random.Range(-horizontalSpread, horizontalSpread);
        velocity = new Vector3(randomX, floatSpeed, 0f);
        textMesh.enabled = true;
        // Text content
        int goldInt = Mathf.RoundToInt(amount);
        textMesh.text = goldInt.ToString();
        
        // Style based on crit
        if (isCrit)
        {
            textMesh.fontSize = critFontSize;
            textMesh.color = critColor;
            baseColor = critColor;
        }
        
        // Show gold sprite if available
        if (goldSpriteRenderer != null)
        {
            goldSpriteRenderer.enabled = true;            
            // Position sprite relative to text
            goldSpriteRenderer.transform.localPosition = goldSpriteOffset;
            goldSpriteRenderer.transform.localScale = Vector3.one * goldSpriteScale;
        }
        
        elapsed = 0f;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        
        // Float upward
        transform.position += velocity * Time.deltaTime;
        
        // Fade out
        float t = elapsed / life;
        float alpha = fadeAlpha.Evaluate(t);
        Color color = baseColor;
        color.a = alpha;
        textMesh.color = color;
        
        // Gold sprite stays at full alpha — it hands off to the fly-to VFX, so fading it
        // out and popping a fresh coin in would break the transition
        if (goldSpriteRenderer != null && goldSpriteRenderer.enabled)
        {
            Color spriteColor = goldSpriteRenderer.color;
            spriteColor.a = 1f;
            goldSpriteRenderer.color = spriteColor;
        }

        // Return to pool when done
        if (elapsed >= life)
        {
            // Gold numbers hand their coin off to the counter fly-to VFX (same as customer
            // purchases), matching its size so the swap is invisible. The gold itself is
            // banked when the coin arrives at the counter.
            if (goldSpriteRenderer != null && goldSpriteRenderer.enabled && ItemExplosionVFX.Instance != null)
            {
                float gold = pendingGold;
                pendingGold = 0f;
                ItemExplosionVFX.Instance.SpawnExplosion(goldSpriteRenderer.sprite, goldSpriteRenderer.transform.position,
                    1, scale: goldSpriteRenderer.transform.lossyScale.x,
                    onEachArrival: gold > 0f ? () => Inventory.Instance.AddGoldFloat(gold) : null);
            }
            ObjectPoolManager.Instance.ReturnObjectToPool(gameObject);
        }
    }

    // Safety net: if this number is despawned by any other path, bank its gold immediately
    // rather than losing it. Normal handoff clears pendingGold first, so no double-count.
    void OnDisable()
    {
        if (pendingGold > 0f && Inventory.Instance != null)
            Inventory.Instance.AddGoldFloat(pendingGold);
        pendingGold = 0f;
    }
}