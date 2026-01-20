using System;
using UnityEngine;

/// <summary>
/// Reusable health component for entities.
/// Handles damage, healing, death, invulnerability, and visual feedback.
/// Designed to work with Stats system for max HP modifiers.
/// </summary>
public class Health : MonoBehaviour, IDamageable
{
    // ===== EVENTS =====
    public event Action<float, float, float> OnDamaged;
    public event Action<float, float, float> OnHealed;
    public event Action<float> OnDeath;
    
    public Func<float, (bool shouldPrevent, float newHP)> OnDeathPrevention;

    // ===== CONFIGURATION =====
    
    [Header("Health Settings")]
    [SerializeField] private float baseMaxHP = 100f;
    [SerializeField] private bool startAtMaxHP = true;
    
    [Header("Visual Feedback")]
    [SerializeField] private bool enableDamageFlash = true;
    [SerializeField] private Color damageFlashColor = Color.red;
    [SerializeField] private float damageFlashDuration = 0.1f;
    
    [Header("Invulnerability")]
    [SerializeField] private bool isInvulnerable = false;

    // ===== STATE =====
    
    private float currentHP;
    private bool isDead;
    private SpriteRenderer spriteRenderer;
    private CountdownTimer flashTimer;
    
    // Cached for flash reset
    private static readonly int BlendAmountID = Shader.PropertyToID("_Blend_Amount");
    private static readonly int BlendColorID = Shader.PropertyToID("_Blend_Colour");

    // ===== PROPERTIES =====
    
    public float CurrentHP => currentHP;
    public float MaxHP => baseMaxHP; // Could be modified by Stats system
    public float HealthPercent => MaxHP > 0 ? currentHP / MaxHP : 0f;
    public bool IsAlive => !isDead;
    public bool IsDead => isDead;
    public bool IsInvulnerable { get => isInvulnerable; set => isInvulnerable = value; }

    // ===== INITIALIZATION =====
    
    void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        
        if (enableDamageFlash && spriteRenderer != null && spriteRenderer.material != null)
        {
            spriteRenderer.material.SetColor(BlendColorID, damageFlashColor);
        }
    }

    /// <summary>
    /// Initialize health with a specific max HP value.
    /// Call this from your entity's Init() method.
    /// </summary>
    public void Init(float maxHP, bool fullHP = true)
    {
        baseMaxHP = maxHP;
        isDead = false;
        currentHP = fullHP ? maxHP : 0f;
        ResetFlash();
    }

    void Start()
    {
        if (startAtMaxHP)
        {
            currentHP = MaxHP;
        }
    }

    void Update()
    {
        // Update damage flash
        if (flashTimer != null && !flashTimer.IsFinished)
        {
            flashTimer.Tick(Time.deltaTime);
            UpdateFlash();
            
            if (flashTimer.IsFinished)
            {
                ResetFlash();
            }
        }
    }

    // ===== DAMAGE & HEALING =====
    
    public float OnDamage(float amount)
    {
        if (amount <= 0f) return 0f;
        if (isDead) return 0f;
        if (isInvulnerable) return 0f;

        // Calculate actual damage (can't go below 0)
        float actualDamage = Mathf.Min(amount, currentHP);
        float overkill = amount - actualDamage;
        
        currentHP -= actualDamage;
        
        // Trigger visual feedback
        if (enableDamageFlash && actualDamage > 0f)
        {
            TriggerFlash();
        }
        
        // Fire damage event
        OnDamaged?.Invoke(actualDamage, currentHP, MaxHP);
        
        // Check for death
        if (currentHP <= 0f)
        {
            HandleDeath(overkill);
        }
        
        return actualDamage;
    }

    public float OnHeal(float amount)
    {
        if (amount <= 0f) return 0f;
        if (isDead) return 0f;

        // Calculate actual healing (can't exceed max)
        float actualHealing = Mathf.Min(amount, MaxHP - currentHP);
        
        currentHP += actualHealing;
        
        // Fire healing event
        OnHealed?.Invoke(actualHealing, currentHP, MaxHP);
        
        return actualHealing;
    }

    /// <summary>
    /// Set HP directly (bypasses damage/heal logic and events).
    /// Useful for initialization or debug.
    /// </summary>
    public void SetHP(float hp)
    {
        currentHP = Mathf.Clamp(hp, 0f, MaxHP);
        
        if (currentHP <= 0f && !isDead)
        {
            HandleDeath(0f);
        }
    }

    /// <summary>
    /// Instantly kill this entity.
    /// </summary>
    public void Kill()
    {
        if (isDead) return;
        
        float overkill = currentHP;
        currentHP = 0f;
        HandleDeath(overkill);
    }

    /// <summary>
    /// Revive this entity with specified HP.
    /// </summary>
    public void Revive(float hp)
    {
        isDead = false;
        currentHP = Mathf.Clamp(hp, 0f, MaxHP);
    }

    // ===== DEATH HANDLING =====
    
    private void HandleDeath(float overkill)
    {
        // Check for death prevention
        if (OnDeathPrevention != null)
        {
            var result = OnDeathPrevention.Invoke(overkill);
            
            if (result.shouldPrevent)
            {
                currentHP = Mathf.Clamp(result.newHP, 1f, MaxHP);
                return; // Death prevented
            }
        }
        
        // Mark as dead
        isDead = true;
        currentHP = 0f;
        
        // Fire death event
        OnDeath?.Invoke(overkill);
    }

    // ===== VISUAL FEEDBACK =====
    
    private void TriggerFlash()
    {
        if (spriteRenderer == null || spriteRenderer.material == null) return;
        
        flashTimer = new CountdownTimer(damageFlashDuration);
        flashTimer.Start();
        
        spriteRenderer.material.SetFloat(BlendAmountID, 1f);
    }

    private void UpdateFlash()
    {
        if (spriteRenderer == null || spriteRenderer.material == null) return;
        
        // Lerp from 1 (full flash) to 0 (no flash)
        float flashAmount = Mathf.Lerp(1f, 0f, flashTimer.Progress);
        spriteRenderer.material.SetFloat(BlendAmountID, flashAmount);
    }

    private void ResetFlash()
    {
        if (spriteRenderer == null || spriteRenderer.material == null) return;
        spriteRenderer.material.SetFloat(BlendAmountID, 0f);
    }

    // ===== DEBUG =====
    
    void OnValidate()
    {
        // Ensure base max HP is positive
        if (baseMaxHP < 1f)
        {
            baseMaxHP = 1f;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        
        // Draw health bar above entity
        Vector3 pos = transform.position + Vector3.up * 1.5f;
        float barWidth = 1f;
        float barHeight = 0.1f;
        
        // Background (black)
        UnityEditor.Handles.color = Color.black;
        UnityEditor.Handles.DrawSolidRectangleWithOutline(
            new Rect(pos.x - barWidth / 2f, pos.y, barWidth, barHeight),
            Color.black,
            Color.white
        );
        
        // Health fill (green->yellow->red gradient)
        float healthPct = HealthPercent;
        Color healthColor = Color.Lerp(Color.red, Color.green, healthPct);
        UnityEditor.Handles.color = healthColor;
        UnityEditor.Handles.DrawSolidRectangleWithOutline(
            new Rect(pos.x - barWidth / 2f, pos.y, barWidth * healthPct, barHeight),
            healthColor,
            Color.clear
        );
        
        // Label
        UnityEditor.Handles.Label(
            pos + Vector3.up * 0.2f,
            $"{currentHP:F0} / {MaxHP:F0} ({healthPct * 100f:F0}%)"
        );
    }
#endif
}