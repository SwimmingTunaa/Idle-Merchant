using System;
using UnityEngine;

// Reusable health component for entities.
// Handles damage, healing, death, invulnerability, and visual feedback.
// Supports multiple SpriteRenderers for modular characters.
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
    private SpriteRenderer[] spriteRenderers;
    private CountdownTimer flashTimer;
    
    // Cached shader property IDs
    private static readonly int BlendAmountID = Shader.PropertyToID("_BlendAmount");
    private static readonly int BlendColorID = Shader.PropertyToID("_BlendColour");

    // ===== PROPERTIES =====
    
    public float CurrentHP => currentHP;
    public float MaxHP => baseMaxHP;
    public float HealthPercent => MaxHP > 0 ? currentHP / MaxHP : 0f;
    public bool IsAlive => !isDead;
    public bool IsDead => isDead;
    public bool IsInvulnerable { get => isInvulnerable; set => isInvulnerable = value; }

    // ===== INITIALIZATION =====
    
    void Awake()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        // Initialize timer in a finished state so Update ignores it until a hit occurs
        flashTimer = new CountdownTimer(0f);

        if (enableDamageFlash)
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null && spriteRenderers[i].material != null)
                {
                    spriteRenderers[i].material.SetColor(BlendColorID, damageFlashColor);
                }
            }
        }

        ResetFlash();
    }

    // Initialize health with a specific max HP value.
    // Call this from your entity's Init() method.
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
            currentHP = MaxHP;
    }

    void Update()
    {
        if (!flashTimer.IsFinished)
        {
            flashTimer.Tick(Time.deltaTime);
            UpdateFlash();

            if (flashTimer.IsFinished)
                ResetFlash();
        }
    }

    // ===== DAMAGE & HEALING =====
    
    public float OnDamage(float amount)
    {
        if (amount <= 0f) return 0f;
        if (isDead) return 0f;
        if (isInvulnerable) return 0f;

        float actualDamage = Mathf.Min(amount, currentHP);
        float overkill = amount - actualDamage;
        
        currentHP -= actualDamage;
        
        if (enableDamageFlash && actualDamage > 0f)
            TriggerFlash();
        
        OnDamaged?.Invoke(actualDamage, currentHP, MaxHP);
        
        if (currentHP <= 0f)
            HandleDeath(overkill);
        
        return actualDamage;
    }

    public float OnHeal(float amount)
    {
        if (amount <= 0f) return 0f;
        if (isDead) return 0f;

        float actualHealing = Mathf.Min(amount, MaxHP - currentHP);
        currentHP += actualHealing;
        
        OnHealed?.Invoke(actualHealing, currentHP, MaxHP);
        
        return actualHealing;
    }

    // Set HP directly (bypasses damage/heal logic and events).
    public void SetHP(float hp)
    {
        currentHP = Mathf.Clamp(hp, 0f, MaxHP);
        
        if (currentHP <= 0f && !isDead)
            HandleDeath(0f);
    }

    // Instantly kill this entity.
    public void Kill()
    {
        if (isDead) return;
        
        float overkill = currentHP;
        currentHP = 0f;
        HandleDeath(overkill);
    }

    // Revive this entity with specified HP.
    public void Revive(float hp)
    {
        isDead = false;
        currentHP = Mathf.Clamp(hp, 0f, MaxHP);
    }

    // ===== DEATH HANDLING =====
    
    private void HandleDeath(float overkill)
    {
        if (OnDeathPrevention != null)
        {
            var result = OnDeathPrevention.Invoke(overkill);
            
            if (result.shouldPrevent)
            {
                currentHP = Mathf.Clamp(result.newHP, 1f, MaxHP);
                return;
            }
        }
        
        isDead = true;
        currentHP = 0f;
        
        OnDeath?.Invoke(overkill);
    }

    // ===== VISUAL FEEDBACK =====
    
    private void TriggerFlash()
    {
        flashTimer.Reset(damageFlashDuration);
        flashTimer.Start();

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null && spriteRenderers[i].material != null)
                spriteRenderers[i].material.SetFloat(BlendAmountID, 1f);
        }
    }

    private void UpdateFlash()
    {
        float flashAmount = Mathf.Lerp(1f, 0f, flashTimer.Progress);

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null && spriteRenderers[i].material != null)
                spriteRenderers[i].material.SetFloat(BlendAmountID, flashAmount);
        }
    }

    private void ResetFlash()
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null && spriteRenderers[i].material != null)
                spriteRenderers[i].material.SetFloat(BlendAmountID, 0f);
        }
    }

    // ===== DEBUG =====
    
    void OnValidate()
    {
        if (baseMaxHP < 1f)
            baseMaxHP = 1f;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        
        Vector3 pos = transform.position + Vector3.up * 1.5f;
        float barWidth = 1f;
        float barHeight = 0.1f;
        
        UnityEditor.Handles.color = Color.black;
        UnityEditor.Handles.DrawSolidRectangleWithOutline(
            new Rect(pos.x - barWidth / 2f, pos.y, barWidth, barHeight),
            Color.black,
            Color.white
        );
        
        float healthPct = HealthPercent;
        Color healthColor = Color.Lerp(Color.red, Color.green, healthPct);
        UnityEditor.Handles.color = healthColor;
        UnityEditor.Handles.DrawSolidRectangleWithOutline(
            new Rect(pos.x - barWidth / 2f, pos.y, barWidth * healthPct, barHeight),
            healthColor,
            Color.clear
        );
        
        UnityEditor.Handles.Label(
            pos + Vector3.up * 0.2f,
            $"{currentHP:F0} / {MaxHP:F0} ({healthPct * 100f:F0}%)"
        );
    }
#endif
}