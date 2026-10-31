using UnityEngine;
using System.Collections;

public enum AdventurerState
{
    Idle,
    Wander,
    Seek,
    Attack,
    Hit,
    Dead
}

// Adventurer agent with combat, hit stagger, death, and revive.
// Combat logic handled by reusable CombatBehavior component.
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(CombatBehavior))]
public class AdventurerAgent : EntityStateMachine<AdventurerState>, IEntity
{
    [Header("Adventurer Data")]
    [SerializeField] private AdventurerDef adventurerDef;
    private float leashRange;
    private bool returnToSpawn;
    
    private float idleTimeMin;
    private float idleTimeMax;
    private float wanderTimeMin;
    private float wanderTimeMax;

    private Vector3 spawnPoint;
    private CountdownTimer stateTimer;
    private float stateTargetTime;
    
    private Health health;
    [SerializeField] GameObject deathEffect;
    private CombatBehavior combat;

    // Hit stagger tracking
    private AdventurerState previousState;
    private float hitCooldownTimer;
    private Coroutine hitCoroutine;
    private Coroutine deathCoroutine;

    // Class signature state (see the Signature fields on AdventurerDef)
    private bool pursuitCrit;        // Rogue: next hit after a pursuit dash is a guaranteed crit
    private Coroutine dashCoroutine; // Rogue pursuit lerp; AI tick + walking pause while it runs
    private float dashGroundY;       // the hop's floor height, so a cancelled (died mid-air) hop doesn't leave the body floating
    private float tauntTimer;        // Guardian: seconds until the next hold-the-line check

    // XP, rank & level
    private float currentXP;
    private int currentRank = 1;
    private int currentLevel = 1;

    // IEntity implementation
    public EntityType EntityType => EntityType.Adventurer;
    public bool IsAlive => health != null && health.IsAlive;

    // XP / rank / level public interface
    public float CurrentXP => currentXP;
    public int CurrentRank => currentRank;
    public int CurrentLevel => currentLevel;

    /// <summary>Global level across all ranks (1..maxRank*levelsPerRank); drives the XP curve.</summary>
    public int TotalLevel =>
        adventurerDef == null ? currentLevel : (currentRank - 1) * adventurerDef.levelsPerRank + currentLevel;

    /// <summary>Rank tier name, e.g. "Wood".</summary>
    public string RankName => AdventurerRank.Name(currentRank);

    /// <summary>Rank tier plus roman level, e.g. "Wood V".</summary>
    public string RankDisplay => AdventurerRank.Display(currentRank, currentLevel);

    /// <summary>Current class tier def (promotion swaps it; its asset name is the save key).</summary>
    public AdventurerDef ClassDef => adventurerDef;
    public ClassLine Line => adventurerDef != null ? adventurerDef.classLine : ClassLine.Novice;
    public string ClassName => adventurerDef != null ? adventurerDef.displayName : "Novice";

    /// <summary>A Novice above Wood (pre-classes save, Recruitment Favor) owes a free one-time path pick — see ChoosePath.</summary>
    public bool NeedsPathPick => adventurerDef != null && adventurerDef.classLine == ClassLine.Novice && currentRank > 1;

    /// <summary>Gold cost to promote out of the current rank (the free path pick costs nothing).</summary>
    public int PromoteCost => adventurerDef == null || NeedsPathPick ? 0 : adventurerDef.PromoteCost(currentRank);

    /// <summary>The def the next promotion turns this unit into (null for a Novice until a line is picked, or at max rank).</summary>
    public AdventurerDef NextClassDef =>
        Line == ClassLine.Novice || ClassCatalog.Instance == null ? null : ClassCatalog.Instance.Get(Line, currentRank + 1);

    /// <summary>True at the final rank and level — no further progression.</summary>
    public bool IsMaxLevel =>
        adventurerDef != null && currentRank >= adventurerDef.maxRank && currentLevel >= adventurerDef.levelsPerRank;

    /// <summary>
    /// XP needed for the next auto level-up, or float.MaxValue at a rank's top level
    /// (where Promote — not XP — is the gate) and at max level.
    /// </summary>
    public float XPForNextLevel
    {
        get
        {
            if (adventurerDef == null || currentLevel >= adventurerDef.levelsPerRank) return float.MaxValue;
            return adventurerDef.XPForLevel(TotalLevel);
        }
    }

    /// <summary>True once at the top level of a non-max rank — eligible to spend gold and promote —
    /// or while a free path pick is owed (badges, gem and indicator light up for it too).</summary>
    public bool CanPromote =>
        adventurerDef != null
        && (NeedsPathPick || (currentRank < adventurerDef.maxRank && currentLevel >= adventurerDef.levelsPerRank));

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool showDebugLogs = false;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        health = GetComponent<Health>();
        combat = GetComponent<CombatBehavior>();
    }

    // ── Selection outline (Roster focus) ─────────────────────────────────────
    [Header("Selection Outline")]
    [Tooltip("Rendering-layer bit toggled while this hero is the one selected in the Roster " +
             "(must match HoverOutlineFeature.selectedRenderingLayer).")]
    [SerializeField] private uint outlineRenderingLayer = 2;

    // Two independent reasons to outline this hero — the outline shows while EITHER holds,
    // so hovering doesn't clear a Roster selection's outline and vice-versa.
    private bool hoverOutline;
    private bool selectOutline;
    private bool outlineApplied;

    /// <summary>Outline this hero while the cursor is over them in the world (set by ClickerManager).</summary>
    public void SetHoverOutline(bool on)
    {
        if (hoverOutline == on) return;
        hoverOutline = on;
        ApplyOutline();
    }

    /// <summary>Outline this hero while they're the one selected in the Roster (set by RosterPanelController).</summary>
    public void SetOutlined(bool on)
    {
        if (selectOutline == on) return;
        selectOutline = on;
        ApplyOutline();
    }

    /// <summary>Flip the rendering-layer bit HoverOutlineFeature draws from, on iff either
    /// outline reason holds. Outline only — no tint, so it never fights the facing flip or the
    /// damage-flash material. Re-queries the child renderers each change so it stays correct
    /// after the modular character is reassembled on pool reuse.</summary>
    private void ApplyOutline()
    {
        bool on = hoverOutline || selectOutline;
        if (on == outlineApplied) return;
        outlineApplied = on;

        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr == null) continue;
            if (on) sr.renderingLayerMask |= outlineRenderingLayer;
            else    sr.renderingLayerMask &= ~outlineRenderingLayer;
        }
    }

    public override void Init(EntityDef entityDef, int layer, Spawner spawner, Collider2D playArea)
    {
        base.Init(entityDef, layer, spawner, playArea);
        deathEffect?.SetActive(false);
        adventurerDef = (AdventurerDef)entityDef;
        
        leashRange = adventurerDef.leashRange;
        returnToSpawn = adventurerDef.returnToSpawn;
        
        idleTimeMin = adventurerDef.idleTimeRange.x;
        idleTimeMax = adventurerDef.idleTimeRange.y;
        wanderTimeMin = adventurerDef.wanderTimeRange.x;
        wanderTimeMax = adventurerDef.wanderTimeRange.y;
        
        spawnPoint = transform.position;
        stateTimer = null;
        hitCooldownTimer = 0f;
        previousState = AdventurerState.Idle;
        currentXP = 0f;
        currentRank = 1;
        currentLevel = 1;

        // Initialize health
        if (health != null && adventurerDef.baseHealth > 0f)
        {
            health.Init(adventurerDef.MaxHealth, fullHP: true);
            health.OnDeath += OnDeath;
            health.OnDamaged += OnHit;
        }

        // Initialize combat
        if (combat != null)
        {
            combat.Init(adventurerDef.combatConfig, Stats, this, layerIndex, spawnPoint);
            combat.OnTargetAcquired += OnCombatTargetAcquired;
            combat.OnTargetLost += OnCombatTargetLost;
            combat.OnDamageDealt += OnCombatDamageDealt;
            combat.HitOverride = ResolveHit;
            combat.Launch = FireProjectile;
        }
        pursuitCrit = false;
        tauntTimer = 0f;
        
        // Starting skills — Init made a fresh mediator, so the old list is already gone
        skillModifiers.Clear();
        ApplySkills();
        if (health != null) health.DamageTakenMultiplier = 1f - adventurerDef.damageReduction;

        ChangeState(adventurerDef.startingState);
    }

    private readonly System.Collections.Generic.List<StatModifier> skillModifiers = new();

    private void ApplySkills()
    {
        foreach (var skill in adventurerDef.startingSkills)
        {
            if (skill == null) continue;
            var mod = skill.CreateModifier(this);
            Stats.Mediator.AddModifier(mod);
            skillModifiers.Add(mod);
        }
    }

    protected override void Update()
    {
        base.Update();

        if (State != AdventurerState.Dead && State != AdventurerState.Hit && dashCoroutine == null)
            UpdateMovementSmooth();

        // Tick hit stagger cooldown
        if (hitCooldownTimer > 0f)
            hitCooldownTimer -= Time.deltaTime;
    }

    // ═════════════════════════════════════════════
    // TRANSITION GUARD
    // ═════════════════════════════════════════════

    protected override bool CanTransition(AdventurerState from, AdventurerState to)
    {
        // Can't do anything while dead except revive back to Idle
        if (from == AdventurerState.Dead && to != AdventurerState.Idle)
            return false;

        return true;
    }

    // ═════════════════════════════════════════════
    // HEALTH HANDLING
    // ═════════════════════════════════════════════
    
    private void OnDeath(float overkill)
    {
        if (showDebugLogs)
            Debug.Log($"[{name}] Adventurer died! Overkill: {overkill}");

        // Cancel any active hit stagger
        if (hitCoroutine != null)
        {
            StopCoroutine(hitCoroutine);
            hitCoroutine = null;
        }
        StopDash();
        pursuitCrit = false;

        ChangeState(AdventurerState.Dead);
    }

    private void OnHit(float damage, float currentHP, float maxHP)
    {
        // Skip if dead, already staggering, or on cooldown
        if (State == AdventurerState.Dead || State == AdventurerState.Hit)
            return;
        if (hitCooldownTimer > 0f)
            return;
         if (currentHP <= 0f)
            return;

        hitCooldownTimer = adventurerDef.hitStaggerCooldown; // Start cooldown immediately
        ChangeState(AdventurerState.Hit);
    }

    // ═════════════════════════════════════════════
    // COMBAT EVENT HANDLERS
    // ═════════════════════════════════════════════
    
    private void OnCombatTargetAcquired(GameObject target)
    {
        if (showDebugLogs)
            Debug.Log($"[{name}] Acquired target: {target.name}");
        
        if (State == AdventurerState.Idle || State == AdventurerState.Wander)
            ChangeState(AdventurerState.Seek);
    }

    private void OnCombatTargetLost()
    {
        if (showDebugLogs)
            Debug.Log($"[{name}] Lost target");
        
        if (State == AdventurerState.Seek || State == AdventurerState.Attack)
            ChangeState(AdventurerState.Idle);
    }

    // ═════════════════════════════════════════════
    // STATE MACHINE
    // ═════════════════════════════════════════════

    protected override void OnEnterState(AdventurerState newState)
    {
        switch (newState)
        {
            case AdventurerState.Idle:
                combat?.ReleaseTarget();
                targetPos = null;
                
                stateTargetTime = Random.Range(idleTimeMin, idleTimeMax);
                stateTimer = new CountdownTimer(stateTargetTime);
                stateTimer.Start();
                
                if (returnToSpawn && Vector3.Distance(transform.position, spawnPoint) > 0.1f)
                    SetTarget(spawnPoint);
                break;

            case AdventurerState.Wander:
                SetTarget(GetWanderPosition(wanderArea));
                
                stateTargetTime = Random.Range(wanderTimeMin, wanderTimeMax);
                stateTimer = new CountdownTimer(stateTargetTime);
                stateTimer.Start();
                break;

            case AdventurerState.Seek:
                if (combat != null && combat.HasTarget)
                    SetTarget(combat.CurrentTarget.transform.position);
                break;

            case AdventurerState.Attack:
                targetPos = null;
                break;

            case AdventurerState.Hit:
                targetPos = null;
                hitCoroutine = StartCoroutine(HitStaggerRoutine());
                break;

            case AdventurerState.Dead:
                targetPos = null;
                combat?.ReleaseTarget();
                deathCoroutine = StartCoroutine(DeathReviveRoutine());
                break;
        }
    }

    protected override void OnExitState(AdventurerState oldState)
    {
        // Capture previous state for hit recovery (before it changes)
        if (oldState != AdventurerState.Hit && oldState != AdventurerState.Dead)
            previousState = oldState;
    }

    protected override void OnUpdateState(AdventurerState currentState)
    {
        if (dashCoroutine != null) return;   // mid pursuit dash — the lerp owns the position

        if (adventurerDef.tauntRadius > 0f && currentState != AdventurerState.Dead)
        {
            tauntTimer -= TickDelta;
            if (tauntTimer <= 0f)
            {
                tauntTimer = adventurerDef.tauntInterval;
                HoldTheLine();
            }
        }

        switch (currentState)
        {
            case AdventurerState.Idle:
                UpdateIdle();
                break;

            case AdventurerState.Wander:
                UpdateWander();
                break;

            case AdventurerState.Seek:
                UpdateSeek();
                break;

            case AdventurerState.Attack:
                UpdateAttack();
                break;

            // Hit and Dead are coroutine-driven, no update logic needed
        }
    }

    // ═════════════════════════════════════════════
    // HIT STAGGER
    // ═════════════════════════════════════════════

    private IEnumerator HitStaggerRoutine()
    {
        // Play hit animation
        if (animator != null)
            animator.SetTrigger(AnimHash.Damage);

        yield return new WaitForSeconds(adventurerDef.hitStaggerDuration);

        // Start cooldown
        hitCoroutine = null;

        // Return to previous state (default to Idle if previous was invalid)
        AdventurerState returnState = previousState;
        if (returnState == AdventurerState.Seek || returnState == AdventurerState.Attack)
        {
            // Only return to combat states if target is still valid
            if (combat == null || !combat.IsTargetValid())
                returnState = AdventurerState.Idle;
        }
        
        ChangeState(returnState);
    }

    // ═════════════════════════════════════════════
    // DEATH & REVIVE
    // ═════════════════════════════════════════════

    private IEnumerator DeathReviveRoutine()
    {
        // Disable interactions
        if (col != null)
            col.enabled = false;

        if (animator != null)
        {
            animator.ResetTrigger(AnimHash.Damage);
            animator.SetBool(AnimHash.Dead, true);
        }

        deathEffect?.SetActive(true);

        if (showDebugLogs)
            Debug.Log($"[{name}] Playing death animation, reviving in {adventurerDef.reviveDelay}s");

        // Wait for revive
        yield return new WaitForSeconds(adventurerDef.reviveDelay);

        // Revive
        if (health != null)
            health.Revive(health.MaxHP);

        // Re-enable interactions
        if (col != null)
            col.enabled = true;

        // Reset cooldowns
        hitCooldownTimer = 0f;
        deathCoroutine = null;

        deathEffect?.SetActive(false);

        if (showDebugLogs)
            Debug.Log($"[{name}] Revived!");

        animator.SetBool(AnimHash.Dead, false);
        ChangeState(AdventurerState.Idle);
    }

    // ═════════════════════════════════════════════
    // STATE UPDATE METHODS
    // ═════════════════════════════════════════════

    private void UpdateIdle()
    {
        stateTimer.Tick(TickDelta);
        
        if (combat != null)
        {
            var target = combat.ScanForTarget(transform.position, Stats.ScanRange);
            if (target != null)
            {
                combat.SetTarget(target);
                return;
            }
        }
        
        if (stateTimer.IsFinished)
            ChangeState(AdventurerState.Wander);
    }

    private void UpdateWander()
    {
        stateTimer.Tick(TickDelta);
        
        if (combat != null)
        {
            var target = combat.ScanForTarget(transform.position, Stats.ScanRange);
            if (target != null)
            {
                combat.SetTarget(target);
                return;
            }
        }
        
        if (!targetPos.HasValue)
        {
            ChangeState(AdventurerState.Idle);
            return;
        }
        
        if (stateTimer.IsFinished)
            ChangeState(AdventurerState.Idle);
    }

    private void UpdateSeek()
    {
        if (combat == null || !combat.IsTargetValid())
        {
            if (showDebugLogs)
                Debug.Log($"[{name}] Target invalid during Seek, returning to Idle");
            ChangeState(AdventurerState.Idle);
            return;
        }

        if (leashRange > 0f && Vector3.Distance(transform.position, spawnPoint) > leashRange)
        {
            if (showDebugLogs)
                Debug.Log($"[{name}] Outside leash range, returning to Idle");
            ChangeState(AdventurerState.Idle);
            return;
        }

        SetTarget(combat.CurrentTarget.transform.position);

        if (combat.IsInAttackRange(transform.position))
            ChangeState(AdventurerState.Attack);
    }

    private void UpdateAttack()
    {
        if (combat == null || !combat.IsTargetValid())
        {
            if (showDebugLogs)
                Debug.Log($"[{name}] Target invalid during Attack, returning to Idle");
            ChangeState(AdventurerState.Idle);
            return;
        }

        if (combat.IsOutOfChaseRange(transform.position))
        {
            if (showDebugLogs)
                Debug.Log($"[{name}] Target out of chase range, seeking again");
            ChangeState(AdventurerState.Seek);
            return;
        }

        Vector3 directionToTarget = combat.CurrentTarget.transform.position - transform.position;
        FaceDirection(directionToTarget.x);

        var shot = adventurerDef.projectilePrefab;
        combat.UpdateAttack(TickDelta, attackAnimDelay: shot != null ? shot.releaseDelay : 0.2f);
    }

    // ═════════════════════════════════════════════
    // XP & RANK
    // ═════════════════════════════════════════════

    /// <summary>
    /// Accumulates XP and auto-levels within the current rank. Crossing into the next
    /// rank is gated by Promote() (gold), so auto-leveling stops at the rank's top level.
    /// </summary>
    public void GainXP(float amount)
    {
        if (amount <= 0f) return;
        currentXP += amount;

        if (adventurerDef != null)
        {
            // Auto level-up while below the rank's top level and enough XP is banked.
            while (currentLevel < adventurerDef.levelsPerRank)
            {
                float need = adventurerDef.XPForLevel(TotalLevel);
                if (currentXP < need) break;
                currentXP -= need;
                currentLevel++;
                ApplyLevelBonus();
                GameSignals.RaiseAdventurerLeveledUp(this, currentLevel);
                // BrightLinger has no U+2191 — the arrow is a TMP sprite (Resources/Sprite Assets/UpArrow)
                if (DamageNumberManager.Instance != null)
                    DamageNumberManager.Instance.ShowText("lvl<sprite=\"UpArrow\" index=0>", transform, new Color(0.45f, 0.85f, 0.3f));
            }

            // At the rank's top level there is no further auto-level — keep the bar full
            // (Promote is the gate) and don't bank surplus XP.
            if (currentLevel >= adventurerDef.levelsPerRank) currentXP = 0f;
        }

        GameSignals.RaiseAdventurerXPChanged(this, currentXP);
    }

    /// <summary>Small permanent stat bump applied automatically on each level-up.</summary>
    private void ApplyLevelBonus()
    {
        if (adventurerDef == null) return;
        int id = TotalLevel; // unique per total level — never collides with rank IDs

        Stats.Mediator.AddModifier(new BasicStatModifier(
            StatType.AttackDamage, 3000 + id, -1f,
            v => v * (1f + adventurerDef.levelDamageMultiplier)));

        Stats.Mediator.AddModifier(new BasicStatModifier(
            StatType.AttackSpeed, 4000 + id, -1f,
            v => v * (1f - adventurerDef.levelAttackSpeedMultiplier)));
    }

    /// <summary>
    /// Spends gold to advance to the next rank and become the next tier of the class line (a Novice must
    /// name the line it takes). Resets to level 1 and fires OnAdventurerPromoted. Returns false if not
    /// eligible, not alive, no next-tier def, or the guild can't afford the cost.
    /// </summary>
    public bool Promote(ClassLine line = ClassLine.Novice)
    {
        if (!CanPromote || NeedsPathPick) return false;
        if (health == null || !health.IsAlive) return false;

        if (Line != ClassLine.Novice) line = Line;
        var next = ClassCatalog.Instance != null ? ClassCatalog.Instance.Get(line, currentRank + 1) : null;
        if (next == null) return false;

        int cost = PromoteCost;
        if (Inventory.Instance == null || !Inventory.Instance.TrySpendGold(cost)) return false;

        string prevClass = ClassName;
        AdvanceRank();
        SetClassDef(next);
        GameSignals.RaiseAdventurerPromoted(this, prevClass, ClassName);
        return true;
    }

    /// <summary>Free one-time path pick for a Novice already above Wood: becomes that line's def at its current rank.</summary>
    public bool ChoosePath(ClassLine line)
    {
        if (!NeedsPathPick || line == ClassLine.Novice) return false;
        var def = ClassCatalog.Instance != null ? ClassCatalog.Instance.Get(line, currentRank) : null;
        if (def == null) return false;
        SetClassDef(def);
        return true;
    }

    // Swap to another tier def: new base stats (damage multiplier, range…), max HP and starting skills;
    // level/trait modifiers stay. The unit stays tracked under its hire def in UnitManager — harmless,
    // adventurer managers have no per-type limits.
    private void SetClassDef(AdventurerDef next)
    {
        adventurerDef = next;
        def = next;
        Stats.SetBaseStats(BaseStats.FromEntityDef(next));
        if (health != null) health.SetMaxHP(next.MaxHealth);

        foreach (var mod in skillModifiers) mod.Dispose();
        skillModifiers.Clear();
        ApplySkills();
        if (health != null) health.DamageTakenMultiplier = 1f - next.damageReduction;
    }

    /// <summary>
    /// Load / class-hire path: replays level-ups and rank-ups up to the given rank/level so every stat bonus is
    /// re-applied exactly as earned — no gold, no signals, no def swap (the unit spawned with its tier def).
    /// Call right after spawn (Init resets to Wood I).
    /// </summary>
    public void RestoreProgress(int rank, int level, float xp)
    {
        if (adventurerDef == null) return;
        rank = Mathf.Clamp(rank, 1, adventurerDef.maxRank);
        level = Mathf.Clamp(level, 1, adventurerDef.levelsPerRank);

        while (currentRank < rank)
        {
            while (currentLevel < adventurerDef.levelsPerRank) { currentLevel++; ApplyLevelBonus(); }
            AdvanceRank();
        }
        while (currentLevel < level) { currentLevel++; ApplyLevelBonus(); }
        currentXP = xp;
    }

    // Rank transition (shared by Promote and RestoreProgress): attack-speed step + reset to level 1.
    // Damage/HP per rank come from the tier def (SetClassDef), not from here.
    private void AdvanceRank()
    {
        // Permanent attack speed bonus (reduces interval = faster attacks) — ID range 2001–2004
        Stats.Mediator.AddModifier(new BasicStatModifier(
            StatType.AttackSpeed,
            2000 + currentRank,
            -1f,
            v => v * (1f - adventurerDef.rankAttackSpeedMultiplier)
        ));

        currentRank++;
        currentLevel = 1;
        currentXP = 0f;
    }

    /// <summary>Subscribed to combat.OnDamageDealt — awards XP on kill, then a Rogue pursues.</summary>
    private void OnCombatDamageDealt(GameObject target, float damageDealt)
    {
        if (damageDealt <= 0f || target == null) return;
        if (!target.TryGetComponent<Health>(out var targetHealth)) return;
        if (targetHealth.IsAlive) return; // not a kill

        AwardKillXP(target);
        if (adventurerDef.pursuitRange > 0f) TryPursuit(target);
    }

    private void AwardKillXP(GameObject target)
    {
        if (!target.TryGetComponent<EntityBase>(out var entity)) return;
        if (!(entity.def is MobDef mobDef)) return;

        float xp = mobDef.baseXPReward * mobDef.hpMultiplierByLayer.Evaluate(layerIndex);
        GainXP(xp);
    }

    // ═════════════════════════════════════════════
    // CLASS SIGNATURES
    // ═════════════════════════════════════════════

    /// <summary>combat.HitOverride: one attack with this tier's procs — Rogue crit, Mage % max-HP bonus,
    /// Warrior cleave — plus its styled number. Returns damage dealt to the primary target.</summary>
    private float ResolveHit(Health target)
    {
        var d = adventurerDef;
        float dmg = Stats.AttackDamage;

        bool crit = pursuitCrit || (d.critChance > 0f && Random.value < d.critChance);
        pursuitCrit = false;
        if (crit) dmg *= d.critMultiplier;

        float bonus = d.maxHpDamage > 0f ? target.MaxHP * d.maxHpDamage : 0f;
        float dealt = target.OnDamage(dmg + bonus);

        if (showDebugLogs)
            Debug.Log($"[AdventurerAgent] {name} hit {target.name}: {dmg:0.#}{(crit ? " CRIT" : "")} +{bonus:0.#} bonus → {dealt:0.#}");

        if (dealt > 0f && DamageNumberManager.Instance != null)
        {
            float shown = Mathf.Min(dmg, dealt);   // overkill clamps the bonus first
            DamageNumberManager.Instance.ShowHitAtEntity(shown, dealt - shown, target.transform,
                crit ? DamageStyle.Crit : DamageStyle.Normal);
        }

        if (d.cleaveFraction > 0f) Cleave(target, dmg * d.cleaveFraction, d.cleaveRadius);
        return dealt;
    }

    // Mage: combat.Launch — reads the live def, so a promotion or path pick into / out of a ranged tier just works.
    private bool FireProjectile(GameObject target)
    {
        var prefab = adventurerDef.projectilePrefab;
        if (prefab == null || ObjectPoolManager.Instance == null) return false;
        var shot = ObjectPoolManager.Instance.SpawnObject<Projectile>(prefab.gameObject, transform.position, Quaternion.identity);
        if (shot == null) return false;
        shot.Fire(combat, target, target.transform.position.x >= transform.position.x ? 1f : -1f);
        return true;
    }

    // Warrior: splash every other live mob near the primary target. Plain Health damage, so it never chains.
    private void Cleave(Health primary, float splash, float radius)
    {
        if (MobManager.Instance == null || splash <= 0f) return;
        Vector3 center = primary.transform.position;
        int hits = 0;

        foreach (var mob in MobManager.Instance.GetMobsOnLayer(layerIndex))   // copy — a splash kill unregisters
        {
            if (mob == null || !mob.IsAlive || mob.gameObject == primary.gameObject) continue;
            if ((mob.transform.position - center).sqrMagnitude > radius * radius) continue;
            if (!mob.TryGetComponent<Health>(out var h)) continue;

            float dealt = h.OnDamage(splash);
            if (dealt <= 0f) continue;
            hits++;
            DamageNumberManager.Instance?.ShowHitAtEntity(dealt, 0f, mob.transform, DamageStyle.Splash);
            if (!h.IsAlive) AwardKillXP(mob.gameObject);
        }

        if (showDebugLogs)
            Debug.Log($"[AdventurerAgent] {name} cleave: {hits} splash hits of {splash:0.#}");

        if (hits > 0 && ClickHitVFX.Instance != null)
            ClickHitVFX.Instance.PlayCleave(CenterOf(primary.transform), transform.localScale.x >= 0f ? 1f : -1f);
    }

    // Guardian: pull mobs off teammates. Skips mobs already on another taunter so two Guardians don't ping-pong.
    private void HoldTheLine()
    {
        if (MobManager.Instance == null || health == null || !health.IsAlive) return;
        float r2 = adventurerDef.tauntRadius * adventurerDef.tauntRadius;

        foreach (var mob in MobManager.Instance.GetMobsOnLayer(layerIndex))
        {
            if (mob == null || !mob.IsAlive) continue;
            var mc = mob.Combat;
            if (mc == null || !mc.HasTarget || mc.CurrentTarget == gameObject) continue;
            if (!mc.CurrentTarget.TryGetComponent<AdventurerAgent>(out var victim)) continue;
            if (victim.ClassDef != null && victim.ClassDef.tauntRadius > 0f) continue;
            if ((mob.transform.position - transform.position).sqrMagnitude > r2) continue;
            if (!mc.IsValidTarget(gameObject)) continue;

            mc.SetTarget(gameObject);
            DamageNumberManager.Instance?.ShowText("!", mob.transform, new Color(0.55f, 0.85f, 1f));   // shield blue — reads on gold golems
            if (showDebugLogs)
                Debug.Log($"[AdventurerAgent] {name} taunted {mob.name} off {victim.name}");
        }
    }

    // Rogue: after a kill, dash to the nearest live mob within range and make the next hit a crit.
    private void TryPursuit(GameObject killed)
    {
        if (MobManager.Instance == null || combat == null || health == null || !health.IsAlive) return;
        if (State == AdventurerState.Dead || dashCoroutine != null) return;

        MobAgent best = null;
        float bestD = adventurerDef.pursuitRange * adventurerDef.pursuitRange;
        foreach (var mob in MobManager.Instance.GetMobsOnLayer(layerIndex))
        {
            if (mob == null || !mob.IsAlive || mob.IsDying || mob.gameObject == killed) continue;
            float d = (mob.transform.position - transform.position).sqrMagnitude;
            if (d < bestD) { bestD = d; best = mob; }
        }
        if (best == null || !combat.IsValidTarget(best.gameObject)) return;   // nothing to chase — stay put

        combat.SetTarget(best.gameObject);
        pursuitCrit = true;
        dashCoroutine = StartCoroutine(PursuitDash(best.transform.position));
        if (State != AdventurerState.Attack && State != AdventurerState.Hit) ChangeState(AdventurerState.Attack);
    }

    private static readonly int JumpState = Animator.StringToHash("Jump");
    private static readonly int IdleState = Animator.StringToHash("Idle");

    // Short lerp to just inside attack range of the mob, clamped to this unit's wander area (its layer floor).
    private IEnumerator PursuitDash(Vector3 mobPos)
    {
        targetPos = null;
        Vector3 from = transform.position;
        Vector3 to = mobPos;
        to.z = from.z;
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        float stop = Mathf.Min(Stats.AttackRange * 0.5f, dist);
        Vector3 end = dist > 0.001f ? to - dir / dist * stop : from;
        if (wanderArea != null)
        {
            var b = wanderArea.bounds;
            end.x = Mathf.Clamp(end.x, b.min.x, b.max.x);
            end.y = Mathf.Clamp(end.y, Mathf.Min(b.min.y, from.y), Mathf.Max(b.max.y, from.y));
        }

        FaceDirection(dir.x);
        ClickHitVFX.Instance?.PlayDust(from);

        dashGroundY = end.y;
        float hop = adventurerDef.pursuitHopHeight;
        bool jumpAnim = hop > 0f && animator != null && animator.HasState(0, JumpState);
        if (jumpAnim) animator.Play(JumpState, 0, 0f);

        float dur = Mathf.Max(0.01f, adventurerDef.pursuitDashTime);
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float k = t / dur;
            transform.position = Vector3.Lerp(from, end, k) + Vector3.up * (hop * 4f * k * (1f - k));   // parabola peaking at hop
            ClickHitVFX.Instance?.PlayDust(transform.position, 1);   // faint after-image trail
            yield return null;
        }
        transform.position = end;
        if (jumpAnim) animator.Play(IdleState, 0, 0f);
        ClickHitVFX.Instance?.PlayDust(end);   // landing puff
        dashCoroutine = null;
    }

    private void StopDash()
    {
        if (dashCoroutine == null) return;
        StopCoroutine(dashCoroutine);
        dashCoroutine = null;
        var p = transform.position; p.y = dashGroundY; transform.position = p;
    }

    private static Vector3 CenterOf(Transform t) =>
        t.TryGetComponent<Collider2D>(out var c) ? c.bounds.center : t.position;

    // ═════════════════════════════════════════════
    // CLEANUP
    // ═════════════════════════════════════════════

    public override void Despawn()
    {
        // Clear any outline so the rendering-layer bit doesn't leak onto the next hero
        // this pooled instance is reused for.
        SetHoverOutline(false);
        SetOutlined(false);

        if (health != null)
        {
            health.OnDeath -= OnDeath;
            health.OnDamaged -= OnHit;
        }
        
        if (combat != null)
        {
            combat.OnTargetAcquired -= OnCombatTargetAcquired;
            combat.OnTargetLost -= OnCombatTargetLost;
            combat.OnDamageDealt -= OnCombatDamageDealt;
            combat.HitOverride = null;
            combat.ReleaseTarget();
        }

        // Cancel any active coroutines
        if (hitCoroutine != null)
        {
            StopCoroutine(hitCoroutine);
            hitCoroutine = null;
        }
        if (deathCoroutine != null)
        {
            StopCoroutine(deathCoroutine);
            deathCoroutine = null;
        }
        StopDash();
        pursuitCrit = false;
        
        base.Despawn();
    }

    // ═════════════════════════════════════════════
    // DEBUG GIZMOS
    // ═════════════════════════════════════════════

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;

        float attackRng = Application.isPlaying && Stats != null ? Stats.AttackRange : (adventurerDef?.attackRange ?? 1.5f);
        float chaseRng = Application.isPlaying && Stats != null ? Stats.ChaseBreakRange : (adventurerDef?.chaseBreakRange ?? 2.5f);
        float scanRng = Application.isPlaying && Stats != null ? Stats.ScanRange : (adventurerDef?.scanRange ?? 10f);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRng);

        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, chaseRng);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, scanRng);

        if (leashRange > 0f)
        {
            Vector3 spawnPos = Application.isPlaying ? spawnPoint : transform.position;
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(spawnPos, leashRange);
        }

        if (Application.isPlaying && combat != null && combat.HasTarget)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, combat.CurrentTarget.transform.position);
            
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(combat.CurrentTarget.transform.position, 0.3f);
        }
        
        if (Application.isPlaying && wanderArea != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(wanderArea.bounds.center, wanderArea.bounds.size);
        }
    }
#endif
}