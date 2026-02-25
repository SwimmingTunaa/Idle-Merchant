using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

public enum CustomerState
{
    Entering,
    Wander,
    SeekingQueue,
    Queueing,
    Buying,
    Leaving,
    Idle,
    Exited,
}

[RequireComponent(typeof(Collider2D))]
public class CustomerAgent : EntityStateMachine<CustomerState>
{

    [Header("State Colour")]
    [SerializeField] private Color wanderColor;
    [SerializeField] private Color seekingColor = Color.yellow;
    [SerializeField] private Color queueingColor;
    [SerializeField] private Color leavingColor;


    [Header("Desired Item")]
    public ItemDef desiredItem;
    public int desiredQty;
    public float budget;
    
    [Header("Queue Seeking")]
    [Tooltip("How often to check if queue is full while seeking (seconds)")]
    [SerializeField] private float queueCheckInterval = 0.5f;
    
    private const int WanderSpeedModifierId = -100;

    // Timers (migrated from float to Timer classes)
    private CountdownTimer idleTimer;
    private CountdownTimer seekingTimeoutTimer;
    private CountdownTimer queueCheckTimer;
    private CountdownTimer wanderTimer;
    private CountdownTimer wanderPauseTimer;

    private CustomerDef customerDef;
    private int batchMin, batchMax;

    private bool hasInitialized = false;
   
    [Header("Debug")]
    [SerializeField] private bool showDebugColours = false;
    [SerializeField] private bool showDebugLogs = false;
   
    void Awake()
    {
        col = GetComponent<Collider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        sortingGroup = GetComponentInChildren<SortingGroup>();
        
        if (showDebugLogs)
            Debug.Log($"[CustomerAgent] {name} Awake()");
    }

    public override void Init(EntityDef entityDef, int layer, Spawner spawner, Collider2D playArea)
    {
        if (entityDef == null)
        {
            Debug.LogError($"[CustomerAgent] {name} Init() called with null entityDef!");
            return;
        }
        
        if (!(entityDef is CustomerDef))
        {
            Debug.LogError($"[CustomerAgent] {name} Init() called with wrong EntityDef type: {entityDef.GetType().Name}");
            return;
        }
        
       if (showDebugLogs)
            Debug.Log($"[CustomerAgent] {name} Init() - State before: {State}, hasInitialized: {hasInitialized}");
        
        base.Init(entityDef, layer, spawner, playArea);
        customerDef = (CustomerDef)entityDef;
        budget = Random.Range(customerDef.budget.x, customerDef.budget.y + 1);
        batchMin = customerDef.batchRange.x;
        batchMax = customerDef.batchRange.y;
        
        // Clear previous state
        desiredItem = null;
        desiredQty = 0;
        targetPos = null;
        idleTimer = null; // Timers created in OnEnterState
        seekingTimeoutTimer = null;
        queueCheckTimer = null;
        wanderTimer = null;
        wanderPauseTimer = null;
        
        // Register with manager
        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.Register(this);
        }
        
        // Initialize state
        hasInitialized = true;
        ChangeState(customerDef.startingState);
        
        if (showDebugLogs)
            Debug.Log($"[CustomerAgent] {name} Init() complete - New state: {State}");
    }

    protected override void Start()
    {
        base.Start();
        if (showDebugLogs)
            Debug.Log($"[CustomerAgent] {name} Start() - hasInitialized: {hasInitialized}, customerDef: {customerDef?.displayName ?? "null"}");

        // Only run on first spawn (before Init is called)
        // On re-spawn from pool, Init handles state setup
        if (!hasInitialized && customerDef != null)
        {
            ChangeState(customerDef.startingState);

            if (showDebugLogs)
                Debug.Log($"[CustomerAgent] {name} Start() set state to: {State}");
        }
    }
    
    protected override void Update()
    {
        base.Update();
        UpdateMovementSmooth();
    }

    public void PickWantFromInventory()
    {
        if (SalesManager.Instance == null)
        {
            Debug.LogError($"[CustomerAgent] {name} SalesManager.Instance is null!");
            return;
        }
        
        if (customerDef == null)
        {
            Debug.LogError($"[CustomerAgent] {name} customerDef is null in PickWantFromInventory!");
            return;
        }
        
        SalesManager salesManager = SalesManager.Instance;
        Inventory inventory = Inventory.Instance;
        int budget = (int)Random.Range(customerDef.budget.x, customerDef.budget.y);

        if (salesManager.TryPickDesiredForCustomer(
        inventory,
        customerDef.itemPreferance,
        budget,
        customerDef.batchRange,
        out var item,
        out var qty))
        {
            desiredItem = item;
            desiredQty = Mathf.Max(1, qty);
   
        }
        else
        {
            // No item found — caller handles state transition
        }

    }

    /// <summary>
    /// Called by CounterService after a successful sale.
    /// Stamps the purchased item/qty onto the customer, then transitions to Leaving
    /// so carry-display components can react to the state change with the correct desiredItem.
    /// </summary>
    public void LeaveWithPurchase(ItemDef item, int qty)
    {
        desiredItem = item;
        desiredQty  = qty;
        ChangeState(CustomerState.Leaving);
    }

    protected override void OnEnterState(CustomerState newState)
    {
        switch (newState)
        {
            case CustomerState.Idle:
                spriteRenderer.color = wanderColor;
                targetPos = null;
                
                // Create idle timer from EntityDef idle time range
                float idleDuration = Random.Range(def.idleTimeRange.x, def.idleTimeRange.y);
                idleTimer = new CountdownTimer(idleDuration);
                idleTimer.Start();
                break;
                
            case CustomerState.SeekingQueue:
                spriteRenderer.color = seekingColor;
                sortingGroup.sortingOrder = 5;
                // Seek the END of the queue, not the front
                SetTarget(QueueController.Instance.GetQueueEndPosition());

                // Create timers for queue seeking
                queueCheckTimer = new CountdownTimer(queueCheckInterval);
                queueCheckTimer.Start();
                
                seekingTimeoutTimer = new CountdownTimer(100f);
                seekingTimeoutTimer.Start();
                break;
                
            case CustomerState.Queueing:
                spriteRenderer.color = queueingColor;
                sortingGroup.sortingOrder = 5;
                
                // Face toward counter (right side)
                if (QueueController.Instance != null && QueueController.Instance.counterPoint != null)
                {
                    Vector3 dirToCounter = QueueController.Instance.counterPoint.position - transform.position;
                    FaceDirection(dirToCounter.x);
                }
                break;
                
            case CustomerState.Entering:
                SetTarget(ShopManager.Instance.entrancePoint.position);
                break;

            case CustomerState.Wander:
                spriteRenderer.color = wanderColor;
                sortingGroup.sortingOrder = 4;
                float wanderDuration = Mathf.Max(1f, Random.Range(customerDef.wanderDuration.x, customerDef.wanderDuration.y));
                wanderTimer = new CountdownTimer(wanderDuration);
                wanderTimer.Start();
                wanderPauseTimer = null;
                Stats.Mediator.AddModifier(new BasicStatModifier(
                    StatType.MoveSpeed,
                    WanderSpeedModifierId,
                    -1f,
                    v => v * customerDef.wanderSpeedMultiplier
                ));
                SetTarget(GetWanderPosition(wanderArea));
                break;
                
            case CustomerState.Buying:
                spriteRenderer.color = Color.white;
                targetPos = null;
                sortingGroup.sortingOrder = 5;
                break;
                
            case CustomerState.Leaving:
                targetPos = ShopManager.Instance.exitPoint.position;
                spriteRenderer.color = leavingColor;
                sortingGroup.sortingOrder = 3;
                break;

            case CustomerState.Exited:
                // Unregister BEFORE despawning (while still active)
                if (ShopManager.Instance != null)
                {
                    ShopManager.Instance.Unregister(this);
                }
                Despawn();
                break;
        }
    }
    
    protected override void OnUpdateState(CustomerState currentState)
    {
        switch (currentState)
        {
            case CustomerState.Idle:
                if (idleTimer == null)
                {
                    Debug.LogWarning($"[CustomerAgent] {name} idleTimer is null in Idle state - reinitializing");
                    float idleDuration = Random.Range(def.idleTimeRange.x, def.idleTimeRange.y);
                    idleTimer = new CountdownTimer(idleDuration);
                    idleTimer.Start();
                    return;
                }
                
                idleTimer.Tick(TickDelta);
                if (idleTimer.IsFinished)
                {
                    PickWantFromInventory();
                    if (desiredItem != null)
                        ChangeState(CustomerState.SeekingQueue);
                    else
                        ChangeState(CustomerState.Leaving);
                }
                break;

            case CustomerState.SeekingQueue:
                if (queueCheckTimer == null || seekingTimeoutTimer == null)
                {
                    Debug.LogWarning($"[CustomerAgent] {name} timer is null in SeekingQueue state - reinitializing");
                    queueCheckTimer = new CountdownTimer(queueCheckInterval);
                    queueCheckTimer.Start();
                    seekingTimeoutTimer = new CountdownTimer(5f);
                    seekingTimeoutTimer.Start();
                    return;
                }
                
                // Update target periodically (not every tick) to follow queue movement
                queueCheckTimer.Tick(TickDelta);
                if (queueCheckTimer.IsFinished)
                {
                    // Reset check timer to cycle again
                    queueCheckTimer.Reset();
                    queueCheckTimer.Start();
                    
                    // Refresh target to current queue end
                    SetTarget(QueueController.Instance.GetQueueEndPosition());

                    // If queue full, give up and wander
                    if (QueueController.Instance.IsFull)
                    {
                        ChangeState(CustomerState.Wander);
                    }
                }
                
                // Timeout after 5 seconds - prevents getting stuck
                seekingTimeoutTimer.Tick(TickDelta);
                if (seekingTimeoutTimer.IsFinished)
                {
                    if (showDebugLogs)
                        Debug.Log($"[CustomerAgent] {name} timed out seeking queue, wandering instead");
                    ChangeState(CustomerState.Wander);
                }
                break;

            case CustomerState.Queueing:
          
                break;

            case CustomerState.Entering:
                if (!targetPos.HasValue)
                    ChangeState(CustomerState.Wander);
                break;

            case CustomerState.Wander:
                SalesManager salesManager = SalesManager.Instance;
                wanderTimer.Tick(TickDelta);

                // Handle pause at wander point
                if (wanderPauseTimer != null)
                {
                    wanderPauseTimer.Tick(TickDelta);
                    if (wanderPauseTimer.IsFinished)
                    {
                        wanderPauseTimer = null;
                        SetTarget(GetWanderPosition(wanderArea));
                    }
                    break;
                }

                // Reached wander point — start pause
                if (!targetPos.HasValue)
                {
                    float pause = Random.Range(customerDef.wanderPauseDuration.x, customerDef.wanderPauseDuration.y);
                    wanderPauseTimer = new CountdownTimer(pause);
                    wanderPauseTimer.Start();
                    break;
                }

                // Browse timer expired — decide to buy
                if (wanderTimer.IsFinished)
                {
                    if (salesManager.TryPickDesiredForCustomer(
                        Inventory.Instance,
                        customerDef.itemPreferance,
                        (int)budget,
                        customerDef.batchRange,
                        out var item,
                        out var qty))
                    {
                        desiredItem = item;
                        desiredQty = Mathf.Max(1, qty);
                        ChangeState(CustomerState.SeekingQueue);
                    }
                    else
                    {
                        ChangeState(CustomerState.Leaving);
                    }
                }
                break;

            case CustomerState.Buying:
                // Handled by CounterService
                break;

            case CustomerState.Leaving:
                
                if (!targetPos.HasValue)
                {
                    ChangeState(CustomerState.Exited);
                }
                break;
        }
    }

    protected override void OnExitState(CustomerState oldState)
    {
        if (oldState == CustomerState.Wander)
            Stats.Mediator.RemoveModifier(WanderSpeedModifierId);
    }
    public override void Despawn()
    {
        if (showDebugLogs)
            Debug.Log($"[CustomerAgent] {name} Despawn() called at position {transform.position}");
        
        // Clear customer-specific data before returning to pool
        desiredItem = null;
        desiredQty = 0;
        budget = 0f;
        targetPos = null;
        customerDef = null; // Clear def so Start() doesn't use old data
        spriteRenderer.sortingOrder = 5;
        
        // Clear timers
        idleTimer = null;
        seekingTimeoutTimer = null;
        queueCheckTimer = null;
        wanderTimer = null;
        wanderPauseTimer = null;
        
        // Reset initialization flag so Init() will work on re-spawn
        hasInitialized = false;
        
        if (showDebugLogs)
            Debug.Log($"[CustomerAgent] {name} cleared state, calling base.Despawn()");
        
        // Call base despawn (removes from spawner, returns to pool)
        base.Despawn();
        
        if (showDebugLogs)
            Debug.Log($"[CustomerAgent] {name} returned to pool");
    }
}