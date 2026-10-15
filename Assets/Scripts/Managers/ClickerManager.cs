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
    
    [Header("Automation")]
    [Tooltip("Auto Click level (0 = off). Every interval one random alive mob takes a click. Star gated.")]
    [SerializeField] private int autoClickLevel = 0;
    [Tooltip("Auto Click max level. 21 = the level where the interval hits the 1.0s floor (3.0 − 0.1×20).")]
    [SerializeField] private int maxAutoClickLevel = 21;
    [Tooltip("Guild stars required before Auto Click can be bought.")]
    [SerializeField] private int autoClickStarRequirement = 3;

    [Tooltip("Auto Collect level (0 = off). Every interval one random loot is vacuumed with the pickup radius. Star gated.")]
    [SerializeField] private int autoCollectLevel = 0;
    [Tooltip("Auto Collect max level. 21 = the level where the interval hits the 1.0s floor (3.0 − 0.1×20).")]
    [SerializeField] private int maxAutoCollectLevel = 21;
    [Tooltip("Guild stars required before Auto Collect can be bought.")]
    [SerializeField] private int autoCollectStarRequirement = 4;

    [Tooltip("Seconds between automatic ticks at level 1; each further level subtracts autoIntervalStep, floored at 1.0s. Shared by Auto Click and Auto Collect.")]
    [SerializeField] private float autoBaseInterval = 3f;
    [Tooltip("Seconds shaved off the interval per level after 1 (3.0, 2.9, 2.8 … 1.0). Keep max levels in sync: (base − 1.0) / step + 1.")]
    [SerializeField] private float autoIntervalStep = 0.1f;

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
    private float autoClickTimer;
    private float autoCollectTimer;
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
    public bool AOEUnlocked => HasStars(aoeStarRequirement);

    private static bool HasStars(int required) =>
        ProgressionManager.Instance != null && ProgressionManager.Instance.GetCurrentStars() >= required;

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

    // ── Automation ladders (star gated): level 1 = autoBaseInterval, −autoIntervalStep per further level, 1.0s design floor ──
    private float AutoInterval(int level) => Mathf.Max(1f, autoBaseInterval - autoIntervalStep * (level - 1));

    public int AutoClickLevel => autoClickLevel;
    public bool AutoClickMaxed => autoClickLevel >= maxAutoClickLevel;
    public int AutoClickStarRequirement => autoClickStarRequirement;
    public bool AutoClickUnlocked => HasStars(autoClickStarRequirement);
    /// <summary>Seconds between auto clicks at the current level (meaningless while level is 0 = off).</summary>
    public float AutoClickInterval => AutoInterval(autoClickLevel);
    public float NextAutoClickInterval => AutoInterval(autoClickLevel + 1);

    /// <summary>Gold cost of the next Auto Click level: 10000 * 1.35^level.</summary>
    public int AutoClickUpgradeCost => Mathf.RoundToInt(10000f * Mathf.Pow(1.35f, autoClickLevel));

    /// <summary>Spends gold on the next Auto Click level. Requires the star gate; returns false if locked, maxed or unaffordable.</summary>
    public bool TryBuyAutoClickUpgrade()
    {
        if (!AutoClickUnlocked || AutoClickMaxed) return false;
        if (!Inventory.Instance.TrySpendGold(AutoClickUpgradeCost)) return false;

        autoClickLevel++;
        GameSignals.RaiseClickUpgradePurchased(autoClickLevel);
        return true;
    }

    public int AutoCollectLevel => autoCollectLevel;
    public bool AutoCollectMaxed => autoCollectLevel >= maxAutoCollectLevel;
    public int AutoCollectStarRequirement => autoCollectStarRequirement;
    public bool AutoCollectUnlocked => HasStars(autoCollectStarRequirement);
    /// <summary>Seconds between auto collects at the current level (meaningless while level is 0 = off).</summary>
    public float AutoCollectInterval => AutoInterval(autoCollectLevel);
    public float NextAutoCollectInterval => AutoInterval(autoCollectLevel + 1);

    /// <summary>Gold cost of the next Auto Collect level: 20000 * 1.35^level.</summary>
    public int AutoCollectUpgradeCost => Mathf.RoundToInt(20000f * Mathf.Pow(1.35f, autoCollectLevel));

    /// <summary>Spends gold on the next Auto Collect level. Requires the star gate; returns false if locked, maxed or unaffordable.</summary>
    public bool TryBuyAutoCollectUpgrade()
    {
        if (!AutoCollectUnlocked || AutoCollectMaxed) return false;
        if (!Inventory.Instance.TrySpendGold(AutoCollectUpgradeCost)) return false;

        autoCollectLevel++;
        GameSignals.RaiseClickUpgradePurchased(autoCollectLevel);
        return true;
    }

    /// <summary>True while any cursor upgrade is purchasable with the given gold (drives the board badge).</summary>
    public bool AnyCursorUpgradeAffordable(int gold) =>
        gold >= ClickUpgradeCost
        || (!PickupRadiusMaxed && gold >= RadiusUpgradeCost)
        || (AOEUnlocked && !AOEMaxed && gold >= AOEUpgradeCost)
        || (AutoClickUnlocked && !AutoClickMaxed && gold >= AutoClickUpgradeCost)
        || (AutoCollectUnlocked && !AutoCollectMaxed && gold >= AutoCollectUpgradeCost);

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

        // Automation ticks run on scaled time (game-speed keys speed them up) and bypass the
        // player's clicks-per-second budget.
        if (autoClickLevel > 0 && (autoClickTimer += Time.deltaTime) >= AutoClickInterval)
        {
            autoClickTimer = 0f;
            AutoClickTick();
        }
        if (autoCollectLevel > 0 && (autoCollectTimer += Time.deltaTime) >= AutoCollectInterval)
        {
            autoCollectTimer = 0f;
            AutoCollectTick();
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
                    VacuumLoot(world, LootPickupRadius, null);
                else
                    StartCoroutine(MoveLootTo(hit.gameObject));
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

            ClickAttackMob(enemy, hit.transform, world);
            return;
        }

        // Click on empty space (or only friendly units) = no action
    }

    /// <summary>Attack a mob exactly like a click: an AOE shockwave centered on it when Damage AOE
    /// is owned, otherwise a single-target hit (sparks at hitPos). Shared by manual and auto clicks.</summary>
    private void ClickAttackMob(IDamageable enemy, Transform mob, Vector3 hitPos)
    {
        if (EffectiveDamageRadius > 0f)
        {
            if (activeShockwave != null) StopCoroutine(activeShockwave);
            if (ClickHitVFX.Instance != null)
                ClickHitVFX.Instance.PlayShockRing(mob.position, EffectiveDamageRadius, shockwaveDuration);
            activeShockwave = StartCoroutine(DamageShockwave(mob.position, EffectiveDamageRadius, shockwaveDuration));
        }
        else
        {
            ClickDamageMob(enemy, mob, hitPos);
        }
    }

    /// <summary>One click's worth of damage on a single mob: VFX, kill check and gold paid via the
    /// coin-arrival path. Shared by manual, AOE and auto clicks. Returns false if nothing was applied.</summary>
    private bool ClickDamageMob(IDamageable enemy, Transform mob, Vector3 hitPos)
    {
        float applied = enemy.OnDamage(EffectiveClickDamage);
        if (applied <= 0f) return false;

        // Health.OnDamage flips IsAlive synchronously, so this reads the kill blow.
        bool killed = enemy is Health hp && !hp.IsAlive;
        if (ClickHitVFX.Instance != null)
        {
            ClickHitVFX.Instance.PlayHit(hitPos, mob);
            if (killed) ClickHitVFX.Instance.PlayKill(mob.position);
        }

        float gold = applied * goldPerDamage;
        // ShowGoldGain owns the gold: it's banked when the coin lands on the counter
        if (DamageNumberManager.Instance != null)
            DamageNumberManager.Instance.ShowGoldGain(gold, mob);
        else
            Inventory.Instance.AddGoldFloat(gold);
        return true;
    }

    // ===== AUTOMATION =====

    private void AutoClickTick()
    {
        MobAgent mob = MobManager.Instance != null ? MobManager.Instance.GetRandomMob() : null;
        if (mob == null || mob.IsDying || !mob.TryGetComponent(out Health health)) return;
        ClickAttackMob(health, mob.transform, mob.transform.position);
    }

    private void AutoCollectTick()
    {
        Loot loot = LootManager.Instance != null ? LootManager.Instance.GetRandomLoot() : null;
        if (loot == null) return;
        Vector3 center = loot.transform.position;
        center.z = 0f;
        VacuumLoot(center, LootPickupRadius, center);
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

                    // AOE has no single cursor point — sparks spawn on each struck mob.
                    if (ClickDamageMob(enemy, hit.transform, hit.transform.position))
                        alreadyDamaged.Add(enemy);
                }
            }

            yield return null;
        }

        currentShockwaveRadius = 0f;
        isShockwaveActive = false;
    }

    // ===== LOOT VACUUM SYSTEM =====

    /// <summary>Pulls every loot within radius of center toward a target and collects it.
    /// target null = follow the mouse cursor (manual click); otherwise a fixed world point (auto collect).</summary>
    private void VacuumLoot(Vector3 center, float radius, Vector3? target)
    {
        if (ClickHitVFX.Instance != null)
            ClickHitVFX.Instance.PlayVacuumRing(center, radius);

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);

        foreach (var hit in hits)
        {
            if (hit.TryGetComponent(out IClickableLoot loot))
            {
                StartCoroutine(MoveLootTo(hit.gameObject, target));
            }
        }
    }

    private IEnumerator MoveLootTo(GameObject lootObj, Vector3? target = null)
    {
        if (lootObj == null) yield break;

        float elapsed = 0f;
        Vector3 startPos = lootObj.transform.position;

        // activeInHierarchy: a pooled loot already collected by another vacuum / porter must not be collected twice.
        while (elapsed < lootVacuumSpeed && lootObj != null && lootObj.activeInHierarchy)
        {
            elapsed += Time.deltaTime;

            Vector3 mouseWorldPos = target ?? GetCursorWorldPos();
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

        if (lootObj != null && lootObj.activeInHierarchy && lootObj.TryGetComponent(out IClickableLoot finalLoot))
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