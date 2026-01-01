using Unity.VisualScripting;
using UnityEngine;

public enum CustomerState
{
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
    
    // Timers (migrated from float to Timer classes)
    private CountdownTimer idleTimer;
    private CountdownTimer seekingTimeoutTimer;
    private CountdownTimer queueCheckTimer;

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
        
        if (showDebugLogs)
            Debug.Log($"[CustomerAgent] {name} Awake()");
    }

    public override void Init(EntityDef entityDef, int layer, Spawner spawner, Collider2D playArea)
    {
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
        Inventory inventory = Inventory.Instance;
        var inventoryType = inventory.GetInventoryType(customerDef.itemPreferance);
        ItemDef pick = null;
        
        float bestPrice = -1f;
        foreach (var it in inventoryType.Keys)
        {
            int stock = Inventory.Instance.Get(inventoryType, it);
            if (stock <= 0) continue;

            float unitPrice = it.sellPrice;
            if (unitPrice <= budget && unitPrice > bestPrice)
            {
                bestPrice = unitPrice;
                pick = it;
            }
        }

        if (pick == null) return;

        desiredItem = pick;
        int maxByBudget = Mathf.FloorToInt(budget / Mathf.Max(0.01f, bestPrice));
        int plan = Mathf.Clamp(maxByBudget, customerDef.batchRange.x, customerDef.batchRange.y);
        desiredQty = Mathf.Max(1, plan);
    }

    protected override void OnEnterState(CustomerState newState)
    {
        switch (newState)
        {
            case CustomerState.Idle:
                if(showDebugColours) spriteRenderer.color = wanderColor;
                targetPos = null;
                
                // Create idle timer from EntityDef idle time range
                float idleDuration = Random.Range(def.idleTimeRange.x, def.idleTimeRange.y);
                idleTimer = new CountdownTimer(idleDuration);
                idleTimer.Start();
                break;
                
            case CustomerState.SeekingQueue:
                if(showDebugColours) spriteRenderer.color = seekingColor;
                // Seek the END of the queue, not the front
                SetTarget(QueueController.Instance.GetQueueEndPosition());
                PickWantFromInventory();
                
                // Create timers for queue seeking
                queueCheckTimer = new CountdownTimer(queueCheckInterval);
                queueCheckTimer.Start();
                
                seekingTimeoutTimer = new CountdownTimer(5f);
                seekingTimeoutTimer.Start();
                break;
                
            case CustomerState.Queueing:
                if(showDebugColours) spriteRenderer.color = queueingColor;
                spriteRenderer.sortingOrder = 0;
                
                // Face toward counter (right side)
                if (QueueController.Instance != null && QueueController.Instance.counterPoint != null)
                {
                    Vector3 dirToCounter = QueueController.Instance.counterPoint.position - transform.position;
                    FaceDirection(dirToCounter.x);
                }
                break;
                
            case CustomerState.Wander:
                if(showDebugColours) spriteRenderer.color = wanderColor;
                SetTarget(GetWanderPosition(wanderArea));
                spriteRenderer.sortingOrder = -1;
                break;
                
            case CustomerState.Buying:
                if(showDebugColours) spriteRenderer.color = Color.red;
                targetPos = null;
                spriteRenderer.sortingOrder = 0;
                break;
                
            case CustomerState.Leaving:
                if(showDebugColours) spriteRenderer.color = leavingColor;
                spriteRenderer.sortingOrder = -2;
                break;

            case CustomerState.Exited:
                if (showDebugLogs)
                    Debug.Log($"[CustomerAgent] {name} entering EXITED state - about to despawn");

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
                idleTimer.Tick(TickDelta);
                if (idleTimer.IsFinished)
                {
                    ChangeState(CustomerState.SeekingQueue);
                }
                break;

            case CustomerState.SeekingQueue:
                
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

            case CustomerState.Wander:
             
                if (!targetPos.HasValue)
                {
                    ChangeState(CustomerState.Idle);
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

    protected override void OnExitState (CustomerState oldState) { }
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
        spriteRenderer.sortingOrder = 0;
        
        // Clear timers
        idleTimer = null;
        seekingTimeoutTimer = null;
        queueCheckTimer = null;
        
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