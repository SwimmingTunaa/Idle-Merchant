using UnityEngine;
using TMPro;

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
    [SerializeField] private Color goldColor = new Color(1f, 0.84f, 0f); // Gold
    [SerializeField] private float goldFontSize = 5.5f;
    
    [Header("Gold Sprite")]
    [SerializeField] private SpriteRenderer goldSpriteRenderer;
    [SerializeField] private Sprite goldSprite;
    [SerializeField] private Vector3 goldSpriteOffset = new Vector3(-0.3f, 0f, 0f);
    [SerializeField] private float goldSpriteScale = 0.8f;
    
    private TextMeshPro textMesh;
    private float elapsed;
    private Vector3 velocity;
    private Color baseColor;

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
        
        // Position
        transform.position = worldPosition;
        
        // Random horizontal drift
        float randomX = Random.Range(-horizontalSpread, horizontalSpread);
        velocity = new Vector3(randomX, floatSpeed, 0f);
        
        // Text content
        int damageInt = Mathf.RoundToInt(damage);
        textMesh.text = damageInt.ToString();
        
        
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

    /// <summary>
    /// Show loot collection multiplier with green styling (e.g., x1.25)
    /// </summary>
    public void ShowLootMultiplier(float multiplier, Vector3 worldPosition)
    {
        
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
        // Position
        transform.position = worldPosition;
        
        // Random horizontal drift
        float randomX = Random.Range(-horizontalSpread, horizontalSpread);
        velocity = new Vector3(randomX, floatSpeed, 0f);
        
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
        else
        {
            textMesh.fontSize = goldFontSize;
            textMesh.color = goldColor;
            baseColor = goldColor;
        }
        
        // Show gold sprite if available
        if (goldSpriteRenderer != null && goldSprite != null)
        {
            goldSpriteRenderer.enabled = true;
            goldSpriteRenderer.sprite = goldSprite;
            goldSpriteRenderer.color = baseColor;
            
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
        float t = elapsed / lifetime;
        float alpha = fadeAlpha.Evaluate(t);
        Color color = baseColor;
        color.a = alpha;
        textMesh.color = color;
        
        // Fade gold sprite if present
        if (goldSpriteRenderer != null && goldSpriteRenderer.enabled)
        {
            Color spriteColor = goldSpriteRenderer.color;
            spriteColor.a = alpha;
            goldSpriteRenderer.color = spriteColor;
        }
        
        // Return to pool when done
        if (elapsed >= lifetime)
        {
            ObjectPoolManager.Instance.ReturnObjectToPool(gameObject);
        }
    }
}