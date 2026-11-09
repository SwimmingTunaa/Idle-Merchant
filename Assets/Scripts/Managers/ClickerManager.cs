using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class ClickerManager : MonoBehaviour
{
    private readonly List<Collider2D> shockwaveHits = new(), vacuumHits = new();

    [Header("Damage")]
    [SerializeField] private float clickDamage = 10f;
    [SerializeField] private int clicksPerSecondCap = 10;

    [Header("Click Economy")]
    [Tooltip("Multiplier applied to the gold shown/banked per point of click damage. Scales the displayed and banked gold without touching damage balance.")]
    [SerializeField] private float goldPerDamage = 1f;   // combat stats are x10, so gold == damage 1:1

    [Tooltip("Click damage upgrade level. Effective click damage = clickDamage * (1 + 0.1 * level).")]
    [SerializeField] private int clickDamageLevel = 0;

    [Tooltip("Pickup radius upgrade level. Radius climbs a log curve from lootPickupRadius at 0 to maxPickupRadius at the top level (big early steps, small late ones).")]
    [SerializeField] private int pickupRadiusLevel = 0;

    [Tooltip("World units of loot-vacuum radius at the top level.")]
    [SerializeField] private float maxPickupRadius = 12f;

    [Tooltip("Top pickup radius level.")]
    [SerializeField] private int maxPickupRadiusLevel = 50;

    [Tooltip("Cost of the next level = 250 × growth^level.")]
    [SerializeField] private float radiusCostGrowth = 1.15f;

    [Header("AOE Settings")]
    [Tooltip("Damage AOE upgrade level (0 = single target). Purchases gated by star requirement.")]
    [SerializeField] private int damageAOELevel = 0;

    [Tooltip("Shockwave radius at AOE level 1; it climbs a log curve to aoeMaxRadius at the top level.")]
    [SerializeField] private float aoeBaseRadius = 0.8f;
    [SerializeField] private float aoeMaxRadius = 6f;
    [SerializeField] private int maxDamageAOELevel = 50;
    [Tooltip("Cost of the next level = 1000 × growth^level.")]
    [SerializeField] private float aoeCostGrowth = 1.2f;

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
    
    [Header("Hover Nameplate")]
    [Tooltip("Font of the nameplate over the hovered adventurer (same as the damage numbers).")]
    [SerializeField] private TMP_FontAsset nameplateFont;
    [Tooltip("Height above the collider top (unit-local) — clears the PromoteIndicator (clearance 0.45 + badge 0.45) plus a gap.")]
    [SerializeField] private float nameplateOffset = 1f;
    [SerializeField] private float nameplateFontSize = 8f;
    [SerializeField] private Color nameplateColor = new Color(0.96f, 0.9f, 0.76f);

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
    private WorkshopProp hoveredProp;
    private DecorSlot hoveredDecor;
    private CaravanManager hoveredCaravan;
    private TransportPoint hoveredLadder;
    private LootPile hoveredPile;
    private CustomerAgent hoveredCustomer;
    private CounterService counter;
    private TextMeshPro nameplate;
    private TextMeshPro serveHint;

    /// <summary>Customers served by a click (saved). An LMB glyph bobs over the customer at the counter until this reaches ServeHintCount.</summary>
    public int Serves { get; set; }
    public const int ServeHintCount = 3;

    /// <summary>Successful right-click inspects (saved). The nameplate's RMB glyph shows until this reaches InspectHintCount.</summary>
    public int Inspects { get; set; }
    private const int InspectHintCount = 3;

    /// <summary>Click damage after the upgrade ladder: baseClickDamage * (1 + 0.1 * level).</summary>
    public float EffectiveClickDamage => clickDamage * (1f + 0.1f * clickDamageLevel);

    public int ClickDamageLevel => clickDamageLevel;
    public float NextClickDamage => clickDamage * (1f + 0.1f * (clickDamageLevel + 1));

    // ── Pickup radius upgrade ──
    public int PickupRadiusLevel => pickupRadiusLevel;
    public bool PickupRadiusMaxed => pickupRadiusLevel >= maxPickupRadiusLevel;
    public float LootPickupRadius => RadiusAt(pickupRadiusLevel);
    public float NextLootPickupRadius => RadiusAt(pickupRadiusLevel + 1);
    // log curve: ln(1+L) / ln(1+Lmax) of the way from the base radius to the max
    private float RadiusAt(int level) => maxPickupRadiusLevel <= 0 ? lootPickupRadius
        : lootPickupRadius + (maxPickupRadius - lootPickupRadius) * Mathf.Log(1f + Mathf.Clamp(level, 0, maxPickupRadiusLevel)) / Mathf.Log(1f + maxPickupRadiusLevel);

    /// <summary>Gold cost of the next pickup radius upgrade: round(250 × radiusCostGrowth^level).</summary>
    public int RadiusUpgradeCost => Mathf.RoundToInt(250f * Mathf.Pow(radiusCostGrowth, pickupRadiusLevel));

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
    public float EffectiveDamageRadius => AoeRadiusAt(damageAOELevel);
    public float NextDamageRadius => AoeRadiusAt(damageAOELevel + 1);
    // level 1 = base, top level = max, log curve between (0 = single target)
    private float AoeRadiusAt(int level) => level <= 0 ? 0f : maxDamageAOELevel <= 1 ? aoeBaseRadius
        : aoeBaseRadius + (aoeMaxRadius - aoeBaseRadius) * Mathf.Log(Mathf.Clamp(level, 1, maxDamageAOELevel)) / Mathf.Log(maxDamageAOELevel);

    /// <summary>Gold cost of the next Damage AOE level: 1000 × aoeCostGrowth^level.</summary>
    public int AOEUpgradeCost => Mathf.RoundToInt(1000f * Mathf.Pow(aoeCostGrowth, damageAOELevel));

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

    /// <summary>Load path only: restores all five ladder levels (SaveManager raises the refresh signal).</summary>
    public void RestoreLevels(int damage, int radius, int aoe, int autoClick, int autoCollect)
    {
        clickDamageLevel = damage;
        pickupRadiusLevel = radius;
        damageAOELevel = aoe;
        autoClickLevel = autoClick;
        autoCollectLevel = autoCollect;
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

    void Awake()
    {
        counter = FindAnyObjectByType<CounterService>();
        // One world-space nameplate for the hovered adventurer; not parented to units, so it never flips
        // and PermanentOutline never flags it (rendering layer 1 = outline bits 2/4 clear).
        nameplate = new GameObject("HoverNameplate").AddComponent<TextMeshPro>();
        nameplate.transform.SetParent(transform, false);
        if (nameplateFont != null) nameplate.font = nameplateFont;
        nameplate.fontSize = nameplateFontSize;
        nameplate.color = nameplateColor;
        nameplate.alignment = TextAlignmentOptions.Bottom;
        nameplate.textWrappingMode = TextWrappingModes.NoWrap;
        nameplate.rectTransform.pivot = new Vector2(0.5f, 0f);
        var mr = nameplate.GetComponent<MeshRenderer>();
        mr.sortingOrder = 10000;
        mr.renderingLayerMask = 1 | HoverOutlineFeature.OverlayRenderingLayer;   // above the outline of what it names
        nameplate.gameObject.SetActive(false);

        // Same plate again for the serve hint: the LMB glyph over the customer being served (first ServeHintCount serves)
        serveHint = Instantiate(nameplate, transform);
        serveHint.name = "ServeHint";
        serveHint.fontSize = nameplateFontSize * 2.5f;   // the glyph alone reads tiny at nameplate size (~5 px over a 24 px customer)
        serveHint.text = "<sprite=\"LMB\" index=0>";
    }

    // Nameplate follows the hovered adventurer; hidden when dead or a world-blocking panel is open.
    void LateUpdate()
    {
        bool blocked = UIManager.Instance != null && UIManager.Instance.IsBlockingWorldInput;
        Transform over = hoveredAdv != null && hoveredAdv.IsAlive ? hoveredAdv.transform
                       : hoveredCustomer != null && hoveredCustomer.enabled && hoveredCustomer.gameObject.activeInHierarchy ? hoveredCustomer.transform
                       : null;
        bool show = over != null && !blocked;
        UpdateServeHint();
        if (nameplate.gameObject.activeSelf != show) nameplate.gameObject.SetActive(show);
        if (!show) return;

        float top = over.TryGetComponent(out BoxCollider2D col) ? col.offset.y + col.size.y * 0.5f : 1f;
        nameplate.transform.position = over.TransformPoint(new Vector3(0f, top + nameplateOffset, 0f));
    }

    /// <summary>Another LMB glyph like the serve hint's, for the tutorial's world pointer (starts inactive).</summary>
    public TextMeshPro MakeHint(string name)
    {
        var h = Instantiate(serveHint, transform);
        h.name = name;
        h.gameObject.SetActive(false);
        return h;
    }

    public float HintOffset => nameplateOffset;

    // LMB glyph bobbing over the customer at the counter until the player has served ServeHintCount of them.
    private void UpdateServeHint()
    {
        var c = counter != null && Serves < ServeHintCount ? counter.Serving : null;
        bool show = c != null && (UIManager.Instance == null || !UIManager.Instance.IsBlockingWorldInput);
        if (serveHint.gameObject.activeSelf != show) serveHint.gameObject.SetActive(show);
        if (!show) return;

        float top = c.TryGetComponent(out BoxCollider2D col) ? col.offset.y + col.size.y * 0.5f : 1f;
        float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) * 0.15f;
        serveHint.transform.position = c.transform.TransformPoint(new Vector3(0f, top + nameplateOffset + bob, 0f));
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
        WorkshopProp prop = null;
        DecorSlot decor = null;
        CaravanManager caravan = null;
        TransportPoint ladder = null;
        LootPile pile = null;
        CustomerAgent cust = null;

        // No world hover while the book is open (you're already inside it).
        if (UIManager.Instance == null || !UIManager.Instance.IsPanelOpen("BookPanel"))
        {
            var hits = Physics2D.OverlapPointAll(GetCursorWorldPos());
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent(out AdventurerAgent adv)) { under = adv; break; }
                if (prop == null) hit.TryGetComponent(out prop);
                if (caravan == null) hit.TryGetComponent(out caravan);
                if (decor == null && hit.TryGetComponent(out DecorSlot d) && d.CanDecorate) decor = d;
                if (ladder == null) hit.TryGetComponent(out ladder);
                if (pile == null) hit.TryGetComponent(out pile);
                if (cust == null && hit.TryGetComponent(out CustomerAgent cc)) cust = cc;   // every walk-in: outline + nameplate, right-click = their card
            }
        }

        if (cust != hoveredCustomer)
        {
            if (hoveredCustomer != null) hoveredCustomer.SetHoverOutline(false);
            hoveredCustomer = cust;
            if (hoveredCustomer != null)
            {
                hoveredCustomer.SetHoverOutline(true);
                if (hoveredCustomer.enabled) nameplate.text = Inspects < InspectHintCount ? hoveredCustomer.Persona.Name + " <sprite=\"RMB\" index=0>" : hoveredCustomer.Persona.Name;
            }
        }
        if (under != null) prop = null;   // a hero standing in front of a workshop takes the outline
        if (under != null || prop != null) caravan = null;   // click priority: workshops, caravan, decor, ladders
        if (under != null || prop != null || caravan != null) decor = null;
        if (under != null || prop != null || caravan != null || decor != null) ladder = null;   // ladders last
        if (under != null || cust != null) pile = null;   // a hero / served customer in front of the stash takes it

        if (pile != hoveredPile)
        {
            if (hoveredPile != null) hoveredPile.SetHover(false);
            hoveredPile = pile;
            if (hoveredPile != null) hoveredPile.SetHover(true);
        }

        if (caravan != hoveredCaravan)
        {
            if (hoveredCaravan != null) hoveredCaravan.SetHover(false);
            hoveredCaravan = caravan;
            if (hoveredCaravan != null) hoveredCaravan.SetHover(true);
        }

        if (ladder != hoveredLadder)
        {
            if (hoveredLadder != null) hoveredLadder.SetHover(false);
            hoveredLadder = ladder;
            if (hoveredLadder != null) hoveredLadder.SetHover(true);
        }

        if (decor != hoveredDecor)
        {
            if (hoveredDecor != null) hoveredDecor.SetHover(false);
            hoveredDecor = decor;
            if (hoveredDecor != null) hoveredDecor.SetHover(true);
        }

        if (prop != hoveredProp)
        {
            if (hoveredProp != null) hoveredProp.SetHover(false);
            hoveredProp = prop;
            if (hoveredProp != null) hoveredProp.SetHover(true);
        }

        if (under == hoveredAdv) return;

        if (hoveredAdv != null) hoveredAdv.SetHoverOutline(false);
        hoveredAdv = under;
        if (hoveredAdv == null) return;
        hoveredAdv.SetHoverOutline(true);
        string label = hoveredAdv.TryGetComponent(out IdentityComponent ic) && ic.Identity != null ? ic.Identity.DisplayName : hoveredAdv.name;
        nameplate.text = Inspects < InspectHintCount ? label + " <sprite=\"RMB\" index=0>" : label;
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
                Inspects++;
                GameSignals.RaiseAdventurerFocusRequested(adv);
                return;
            }
        }
        // No hero under the cursor: a walk-in opens their card (pirates have the agent off — not customers)
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent(out CustomerAgent c) && c.enabled && CustomerPanelController.Instance != null)
            {
                Inspects++;
                CustomerPanelController.Instance.Show(c);
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

        // Priority 2b: the customer being served at the counter — serve them now for the bonus
        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent(out CustomerAgent cust) || counter == null) continue;
            if (counter.TryServe(cust)) return;
        }

        // Priority 2b': a royal waiting at the dais — opens the audience
        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent(out RoyalAudience royal) || !royal.Waiting) continue;
            FindAnyObjectByType<AudiencePanelController>()?.OpenFor(royal);
            return;
        }

        // Priority 2c: a pirate raider — click kills them; what they carry flies back to the pile
        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent(out PirateRaider pirate)) continue;
            pirate.Kill();
            return;
        }

        // Priority 3: a workshop prop in the guild opens its panel
        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent(out WorkshopProp prop)) continue;
            FindAnyObjectByType<WorkshopPanelController>()?.OpenFor(prop);
            return;
        }

        // Priority 3a: the loot stash behind the counter opens the book's Inventory page
        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent(out LootPile _)) continue;
            FindAnyObjectByType<BookPanelController>()?.OpenToTab(2);
            return;
        }

        // Priority 3b: the caravan (its collider is only on while an offer is open) opens its offer
        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent(out CaravanManager _)) continue;
            var cp = FindAnyObjectByType<CaravanPanelController>();
            if (cp != null) UIManager.Instance.OpenPanel(cp);
            return;
        }

        // Priority 4: a swappable guild-hall decor slot opens Decorate (locked / pending slots and trophies don't)
        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent(out DecorSlot decor) || !decor.CanDecorate) continue;
            FindAnyObjectByType<DecoratePanelController>()?.OpenFor(decor);
            return;
        }

        // Priority 5: a dungeon ladder opens the Transport panel (this depth's Elevator / Teleporter)
        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent(out TransportPoint tp)) continue;
            FindAnyObjectByType<TransportPanelController>()?.OpenFor(tp);
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
        if (killed) TutorialManager.Instance?.OnClickKill();
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

            var hits = Overlap.Circle(origin, currentShockwaveRadius, shockwaveHits);
            
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

        var hits = Overlap.Circle(center, radius, vacuumHits);

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