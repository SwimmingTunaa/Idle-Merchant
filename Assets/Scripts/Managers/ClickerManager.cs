using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class ClickerManager : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private float clickDamage = 10f;
    [SerializeField] private int clicksPerSecondCap = 10;

    [Header("Click Economy")]
    [Tooltip("Multiplier applied to the gold shown/banked per point of click damage. Scales the displayed and banked gold without touching damage balance.")]
    [SerializeField] private float goldPerDamage = 1f;   // combat stats are x10, so gold == damage 1:1

    [Tooltip("Click damage upgrade level. Effective click damage = clickDamage * (1 + 0.1 * level).")]
    [SerializeField] private int clickDamageLevel = 0;

    [Tooltip("Pickup radius upgrade level. Effective radius = lootPickupRadius + radiusPerLevel * level.")]
    [SerializeField] private int pickupRadiusLevel = 0;

    [Tooltip("World units of loot-vacuum radius added per upgrade level.")]
    [SerializeField] private float radiusPerLevel = 0.2f;

    [Tooltip("Max pickup radius upgrade levels (spatial upgrades need a ceiling).")]
    [SerializeField] private int maxPickupRadiusLevel = 5;

    [Header("AOE Settings")]
    [Tooltip("Damage AOE upgrade level (0 = single target). Purchases gated by star requirement.")]
    [SerializeField] private int damageAOELevel = 0;

    [Tooltip("Shockwave radius at AOE level 1; each further level adds aoeRadiusPerLevel.")]
    [SerializeField] private float aoeBaseRadius = 0.8f;
    [SerializeField] private float aoeRadiusPerLevel = 0.3f;
    [SerializeField] private int maxDamageAOELevel = 3;

    [Tooltip("Guild stars required before Damage AOE can be bought.")]
    [SerializeField] private int aoeStarRequirement = 2;
    
    [Tooltip("Duration of shockwave expansion for damage AOE (seconds)")]
    [SerializeField] private float shockwaveDuration = 0.2f;
    
    [Tooltip("Radius for loot pickup AOE when clicking on loot (0 = single piece only)")]
    [SerializeField] private float lootPickupRadius = 0.3f;
    
    [Tooltip("Speed at which loot moves to mouse position when clicked")]
    [SerializeField] private float lootVacuumSpeed = 10f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = false;

    private int clicksThisSecond;
    private float secondTimer;
    private Vector3 lastClickPosition;
    
    private Coroutine activeShockwave;
    private float currentShockwaveRadius = 0f;
    private bool isShockwaveActive = false;

    private AdventurerAgent hoveredAdv;

    /// <summary>Click damage after the upgrade ladder: baseClickDamage * (1 + 0.1 * level).</summary>
    public float EffectiveClickDamage => clickDamage * (1f + 0.1f * clickDamageLevel);

    public int ClickDamageLevel => clickDamageLevel;
    public float NextClickDamage => clickDamage * (1f + 0.1f * (clickDamageLevel + 1));

    // ── Pickup radius upgrade ──
    public int PickupRadiusLevel => pickupRadiusLevel;
    public bool PickupRadiusMaxed => pickupRadiusLevel >= maxPickupRadiusLevel;
    public float LootPickupRadius => lootPickupRadius + radiusPerLevel * pickupRadiusLevel;
    public float NextLootPickupRadius => lootPickupRadius + radiusPerLevel * (pickupRadiusLevel + 1);

    /// <summary>Gold cost of the next pickup radius upgrade: round(250 * 1.5^level).</summary>
    public int RadiusUpgradeCost => Mathf.RoundToInt(250f * Mathf.Pow(1.5f, pickupRadiusLevel));

    /// <summary>Spends gold on the next pickup radius upgrade. Returns false if maxed or unaffordable.</summary>
    public bool TryBuyRadiusUpgrade()
    {
        if (PickupRadiusMaxed) return false;
        if (!Inventory.Instance.TrySpendGold(RadiusUpgradeCost)) return false;

        pickupRadiusLevel++;
        GameSignals.RaiseClickUpgradePurchased(pickupRadiusLevel);
        return true;
    }

    // ── Damage AOE upgrade (star gated) ──
    public int AOELevel => damageAOELevel;
    public bool AOEMaxed => damageAOELevel >= maxDamageAOELevel;
    public int AOEStarRequirement => aoeStarRequirement;
    public bool AOEUnlocked =>
        ProgressionManager.Instance != null && ProgressionManager.Instance.GetCurrentStars() >= aoeStarRequirement;

    /// <summary>Shockwave radius at the current AOE level; 0 = single target.</summary>
    public float EffectiveDamageRadius =>
        damageAOELevel <= 0 ? 0f : aoeBaseRadius + aoeRadiusPerLevel * (damageAOELevel - 1);
    public float NextDamageRadius => aoeBaseRadius + aoeRadiusPerLevel * damageAOELevel;

    /// <summary>Gold cost of the next Damage AOE level: 1000 * 2^level.</summary>
    public int AOEUpgradeCost => Mathf.RoundToInt(1000f * Mathf.Pow(2f, damageAOELevel));

    /// <summary>Spends gold on the next Damage AOE level. Requires the star gate; returns false if locked, maxed or unaffordable.</summary>
    public bool TryBuyAOEUpgrade()
    {
        if (!AOEUnlocked || AOEMaxed) return false;
        if (!Inventory.Instance.TrySpendGold(AOEUpgradeCost)) return false;

        damageAOELevel++;
        GameSignals.RaiseClickUpgradePurchased(damageAOELevel);
        return true;
    }

    /// <summary>True while any cursor upgrade is purchasable with the given gold (drives the board badge).</summary>
    public bool AnyCursorUpgradeAffordable(int gold) =>
        gold >= ClickUpgradeCost
        || (!PickupRadiusMaxed && gold >= RadiusUpgradeCost)
        || (AOEUnlocked && !AOEMaxed && gold >= AOEUpgradeCost);

    /// <summary>Gold cost of the next click damage upgrade: round(100 * 1.15^level).</summary>
    public int ClickUpgradeCost => Mathf.RoundToInt(100f * Mathf.Pow(1.15f, clickDamageLevel));

    /// <summary>Spends gold on the next click damage upgrade. Returns false if unaffordable.</summary>
    public bool TryBuyClickUpgrade()
    {
        if (!Inventory.Instance.TrySpendGold(ClickUpgradeCost)) return false;

        clickDamageLevel++;
        GameSignals.RaiseClickUpgradePurchased(clickDamageLevel);
        return true;
    }

    void Update()
    {
        secondTimer += Time.deltaTime;
        if (secondTimer >= 1f)
        {
            secondTimer = 0f;
            clicksThisSecond = 0;
        }

        // Right-click is a separate channel from the left-click clicker.
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            OnPlayerInspect();

        UpdateHover();
    }

    // Outline the adventurer under the cursor so the player can tell they're interactable
    // (right-click opens the Roster to them). Outline only — no tint, no tooltip.
    private void UpdateHover()
    {
        AdventurerAgent under = null;

        // No world hover while the book is open (you're already inside it).
        if (UIManager.Instance == null || !UIManager.Instance.IsPanelOpen("BookPanel"))
        {
            var hits = Physics2D.OverlapPointAll(GetCursorWorldPos());
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent(out AdventurerAgent adv)) { under = adv; break; }
            }
        }

        if (under == hoveredAdv) return;

        if (hoveredAdv != null) hoveredAdv.SetHoverOutline(false);
        hoveredAdv = under;
        if (hoveredAdv != null) hoveredAdv.SetHoverOutline(true);
    }

    /// <summary>World-space position under the mouse cursor (raw camera z). Shared by
    /// every world-pointer action so the screen→world conversion lives in one place.</summary>
    private static Vector3 GetCursorWorldPos()
    {
        return Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
    }

    /// <summary>Right-click a friendly adventurer to open the Roster focused on them.
    /// A separate input channel from the left-click clicker so the two never collide.</summary>
    public void OnPlayerInspect()
    {
        // Already viewing the book? Don't inspect through it.
        if (UIManager.Instance != null && UIManager.Instance.IsPanelOpen("BookPanel")) return;

        var hits = Physics2D.OverlapPointAll(GetCursorWorldPos());
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent(out AdventurerAgent adv))
            {
                GameSignals.RaiseAdventurerFocusRequested(adv);
                return;
            }
        }
    }

    public void OnPlayerClick()
    {
        if (clicksThisSecond >= clicksPerSecondCap) return;
        clicksThisSecond++;

        Vector3 world = GetCursorWorldPos();
        lastClickPosition = world;

        // Resolve everything under the cursor so friendly units never block a click.
        var hits = Physics2D.OverlapPointAll(world);

        // Priority 1: loot under the cursor
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent(out IClickableLoot _))
            {
                if (LootPickupRadius > 0f)
                    VacuumLootToMouse(world, LootPickupRadius);
                else
                    StartCoroutine(MoveLootToMouse(hit.gameObject));
                return;
            }
        }

        // Priority 2: a mob under the cursor. Adventurers / porters / customers are
        // friendly — transparent to the clicker, so a hero standing over a mob never
        // eats the click (the click passes through to the mob behind it).
        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent(out MobAgent _)) continue;
            if (!hit.TryGetComponent(out IDamageable enemy)) continue;
            if (hit.TryGetComponent(out EntityBase entity) && entity.IsDying) continue;

            if (EffectiveDamageRadius > 0f)
            {
                // AOE shockwave centered on the mob you clicked
                if (activeShockwave != null) StopCoroutine(activeShockwave);
                activeShockwave = StartCoroutine(DamageShockwave(hit.transform.position, EffectiveDamageRadius, shockwaveDuration));
            }
            else
            {
                // Single target damage
                float applied = enemy.OnDamage(EffectiveClickDamage);
                if (applied > 0f)
                {
                    // Health.OnDamage flips IsAlive synchronously, so this reads the kill blow.
                    bool killed = enemy is Health hp && !hp.IsAlive;
                    if (ClickHitVFX.Instance != null)
                    {
                        ClickHitVFX.Instance.PlayHit(world, hit.transform);
                        if (killed) ClickHitVFX.Instance.PlayKill(hit.transform.position);
                    }

                    float gold = applied * goldPerDamage;
                    // ShowGoldGain owns the gold: it's banked when the coin lands on the counter
                    if (DamageNumberManager.Instance != null)
                        DamageNumberManager.Instance.ShowGoldGain(gold, hit.transform);
                    else
                        Inventory.Instance.AddGoldFloat(gold);
                }
            }
            return;
        }

        // Click on empty space (or only friendly units) = no action
    }

    // ===== AOE DAMAGE SHOCKWAVE =====

    private IEnumerator DamageShockwave(Vector3 origin, float maxRadius, float duration)
    {
        isShockwaveActive = true;
        currentShockwaveRadius = 0f;
        
        HashSet<IDamageable> alreadyDamaged = new HashSet<IDamageable>();
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            currentShockwaveRadius = Mathf.Lerp(0f, maxRadius, elapsed / duration);

            Collider2D[] hits = Physics2D.OverlapCircleAll(origin, currentShockwaveRadius);
            
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent(out IDamageable enemy))
                {
                    if (alreadyDamaged.Contains(enemy))
                        continue;

                    if (hit.TryGetComponent(out EntityBase entity) && entity.IsDying)
                        continue;

                    // Only damage mobs — skip friendly entities
                    if (!hit.TryGetComponent(out MobAgent _))
                        continue;

                    float applied = enemy.OnDamage(EffectiveClickDamage);
                    if (applied > 0f)
                    {
                        // AOE has no single cursor point — sparks spawn on each struck mob.
                        bool killed = enemy is Health hp && !hp.IsAlive;
                        if (ClickHitVFX.Instance != null)
                        {
                            ClickHitVFX.Instance.PlayHit(hit.transform.position, hit.transform);
                            if (killed) ClickHitVFX.Instance.PlayKill(hit.transform.position);
                        }

                        float gold = applied * goldPerDamage;
                        // ShowGoldGain owns the gold: it's banked when the coin lands on the counter
                        if (DamageNumberManager.Instance != null)
                            DamageNumberManager.Instance.ShowGoldGain(gold, hit.transform);
                        else
                            Inventory.Instance.AddGoldFloat(gold);

                        alreadyDamaged.Add(enemy);
                    }
                }
            }

            yield return null;
        }

        currentShockwaveRadius = 0f;
        isShockwaveActive = false;
    }

    // ===== LOOT VACUUM SYSTEM =====

    private void VacuumLootToMouse(Vector3 clickPos, float radius)
    {
        if (ClickHitVFX.Instance != null)
            ClickHitVFX.Instance.PlayVacuumRing(clickPos, radius);

        Collider2D[] hits = Physics2D.OverlapCircleAll(clickPos, radius);
        
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent(out IClickableLoot loot))
            {
                StartCoroutine(MoveLootToMouse(hit.gameObject));
            }
        }
    }

    private IEnumerator MoveLootToMouse(GameObject lootObj)
    {
        if (lootObj == null) yield break;

        float elapsed = 0f;
        Vector3 startPos = lootObj.transform.position;

        while (elapsed < lootVacuumSpeed && lootObj != null)
        {
            elapsed += Time.deltaTime;
            
            Vector3 mouseWorldPos = GetCursorWorldPos();
            mouseWorldPos.z = 0f;

            float t = elapsed / lootVacuumSpeed;
            lootObj.transform.position = Vector3.Lerp(startPos, mouseWorldPos, t);

            if (Vector3.Distance(lootObj.transform.position, mouseWorldPos) < 0.1f)
            {
                if (lootObj.TryGetComponent(out IClickableLoot loot))
                {
                    if (ClickHitVFX.Instance != null)
                        ClickHitVFX.Instance.PlayAbsorb(lootObj.transform.position);
                    loot.OnManualCollect();
                }
                yield break;
            }

            yield return null;
        }

        if (lootObj != null && lootObj.TryGetComponent(out IClickableLoot finalLoot))
        {
            if (ClickHitVFX.Instance != null)
                ClickHitVFX.Instance.PlayAbsorb(lootObj.transform.position);
            finalLoot.OnManualCollect();
        }
    }

    // ===== DEBUG GIZMOS =====

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        if (isShockwaveActive)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(lastClickPosition, currentShockwaveRadius);
        }

        if (EffectiveDamageRadius > 0f)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
            Gizmos.DrawWireSphere(lastClickPosition, EffectiveDamageRadius);
        }

        if (LootPickupRadius > 0f)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(lastClickPosition, LootPickupRadius);
        }
    }
#endif
}