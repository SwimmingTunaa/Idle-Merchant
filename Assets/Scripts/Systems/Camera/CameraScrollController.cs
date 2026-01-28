using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Smooth camera scrolling with edge detection and WASD controls.
/// Clamps camera to world bounds, supports expandable vertical bounds for layer unlocking.
/// Integrates with Unity's new Input System (uses Player.Move action).
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraScrollController : PersistentSingleton<CameraScrollController>
{
    [Header("Input System")]
    [Tooltip("Reference to InputSystem_Actions (auto-finds if null)")]
    [SerializeField] private InputSystem_Actions inputActions;

    [Header("World Bounds")]
    [Tooltip("Minimum X coordinate the camera can see")]
    public float minX = -50f;
    
    [Tooltip("Maximum X coordinate the camera can see")]
    public float maxX = 50f;
    
    [Tooltip("Minimum Y coordinate (bottom of world). Expands as layers unlock.")]
    public float minY = -100f;
    
    [Tooltip("Maximum Y coordinate (top of world, usually shop area)")]
    public float maxY = 10f;

    [Tooltip("amount to add to miny when layers are unlock")]
    public float layerAdd;

    [Header("Scroll Settings")]
    [Tooltip("Camera movement speed in units per second")]
    public float scrollSpeed = 10f;
    
    [Tooltip("Edge scroll activation zone (0.05 = 5% of screen)")]
    [Range(0.01f, 0.2f)]
    public float edgeScrollZonePercent = 0.05f;
    
    [Tooltip("Smoothing speed for camera movement (higher = snappier)")]
    public float smoothSpeed = 8f;

    [Header("Input Options")]
    [Tooltip("Enable mouse edge scrolling")]
    public bool enableEdgeScroll = true;
    
    [Tooltip("Enable WASD/Arrow key scrolling")]
    public bool enableKeyboardScroll = true;
    
    [Tooltip("Confine cursor to game window (prevents mouse from leaving screen)")]
    public bool confineCursor = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = false;

    // Cached references
    private Camera cam;
    private Vector3 targetPosition;
    
    // Camera frustum dimensions (updated when orthographic size changes)
    private float cameraHalfHeight;
    private float cameraHalfWidth;
    
    // Edge detection zones (recalculated each frame based on screen size)
    private Rect topEdgeZone;
    private Rect bottomEdgeZone;
    private Rect leftEdgeZone;
    private Rect rightEdgeZone;

    protected override void Awake()
    {
        base.Awake();
        cam = GetComponent<Camera>();
        
        if (!cam.orthographic)
        {
            Debug.LogWarning("[CameraScrollController] Camera is not orthographic! This script is designed for 2D orthographic cameras.");
        }

        // Auto-find InputSystem_Actions if not assigned
        if (inputActions == null)
        {
            inputActions = new InputSystem_Actions();
        }
        
        targetPosition = transform.position;
        UpdateCameraFrustum();
        
        // // Start camera at top of bounds
        // Vector3 startPos = transform.position;
        // startPos.y = maxY - cameraHalfHeight;
        // transform.position = startPos;
        // targetPosition = transform.position;
    }

    void OnEnable()
    {
        ProgressionManager.OnLayerUnlocked += HandleExpandVeritcalBounds;
        // Ensure Player action map is enabled
        if (inputActions != null)
        {
            inputActions.Player.Enable();
        }
        
        // Confine cursor to game window
        if (confineCursor)
        {
            Cursor.lockState = CursorLockMode.Confined;
        }
    }

    void OnDisable()
    {
        // Release cursor confinement
        if (confineCursor)
        {
            Cursor.lockState = CursorLockMode.None;
        }
        ProgressionManager.OnLayerUnlocked -= HandleExpandVeritcalBounds;   
    }

    void Update()
    {
        UpdateCameraFrustum();
        CalculateEdgeZones();
        
        Vector2 inputVector = Vector2.zero;
        
        // Gather keyboard input (priority - overrides edge scroll)
        if (enableKeyboardScroll)
        {
            inputVector = GetKeyboardInput();
        }
        
        // Only check edge scroll if keyboard isn't being used
        if (enableEdgeScroll && inputVector.sqrMagnitude < 0.01f && IsCursorOnScreen())
        {
            inputVector = GetEdgeScrollInput();
        }
        
        // Normalize diagonal movement for consistent speed
        if (inputVector.sqrMagnitude > 1f)
        {
            inputVector.Normalize();
        }
        
        // Apply movement
        if (inputVector.sqrMagnitude > 0.01f)
        {
            Vector3 movement = inputVector * scrollSpeed * Time.deltaTime;
            targetPosition += movement;
        }
        
        // Clamp target to bounds
        targetPosition = ClampToBounds(targetPosition);
        
        // Smooth lerp to target
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * smoothSpeed);
    }

    // ===== INPUT GATHERING =====

    private Vector2 GetEdgeScrollInput()
    {
        if (Mouse.current == null) return Vector2.zero;
        
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector2 scroll = Vector2.zero;
        
        if (topEdgeZone.Contains(mousePos))
            scroll.y = 1f;
        else if (bottomEdgeZone.Contains(mousePos))
            scroll.y = -1f;
        
        if (rightEdgeZone.Contains(mousePos))
            scroll.x = 1f;
        else if (leftEdgeZone.Contains(mousePos))
            scroll.x = -1f;
        
        return scroll;
    }

    private Vector2 GetKeyboardInput()
    {
        if (inputActions == null) return Vector2.zero;
        
        // Read from Player.Move action (WASD/Arrow composite)
        return inputActions.Player.Move.ReadValue<Vector2>();
    }

    // ===== BOUNDS MANAGEMENT =====

    private Vector3 ClampToBounds(Vector3 position)
    {
        // Calculate effective bounds considering camera frustum
        float effectiveMinX = minX + cameraHalfWidth;
        float effectiveMaxX = maxX - cameraHalfWidth;
        float effectiveMinY = minY + cameraHalfHeight;
        float effectiveMaxY = maxY - cameraHalfHeight;
        
        position.x = Mathf.Clamp(position.x, effectiveMinX, effectiveMaxX);
        position.y = Mathf.Clamp(position.y, effectiveMinY, effectiveMaxY);
        
        return position;
    }

    public void HandleExpandVeritcalBounds(int layer)
    {
        float newY = layerAdd * (layer - 1);
        minY += newY;
    }

    /// Expand vertical bounds downward when unlocking new layers.
    public void ExpandVerticalBounds()
    {
        minY += layerAdd;
    }

    /// <summary>
    /// Set bounds directly (useful for scene transitions or testing).
    /// </summary>
    public void SetBounds(float newMinX, float newMaxX, float newMinY, float newMaxY)
    {
        minX = newMinX;
        maxX = newMaxX;
        minY = newMinY;
        maxY = newMaxY;
    }

    // ===== UTILITY =====

    private void UpdateCameraFrustum()
    {
        cameraHalfHeight = cam.orthographicSize;
        cameraHalfWidth = cameraHalfHeight * cam.aspect;
    }

    private void CalculateEdgeZones()
    {
        float zoneWidth = Screen.width * edgeScrollZonePercent;
        float zoneHeight = Screen.height * edgeScrollZonePercent;
        
        // Top edge (full width, thin strip at top)
        topEdgeZone = new Rect(0, Screen.height - zoneHeight, Screen.width, zoneHeight);
        
        // Bottom edge
        bottomEdgeZone = new Rect(0, 0, Screen.width, zoneHeight);
        
        // Left edge (full height, thin strip at left)
        leftEdgeZone = new Rect(0, 0, zoneWidth, Screen.height);
        
        // Right edge
        rightEdgeZone = new Rect(Screen.width - zoneWidth, 0, zoneWidth, Screen.height);
    }

    private bool IsCursorOnScreen()
    {
        if (Mouse.current == null) return false;
        
        Vector2 mousePos = Mouse.current.position.ReadValue();
        return mousePos.x >= 0 && mousePos.x <= Screen.width &&
               mousePos.y >= 0 && mousePos.y <= Screen.height;
    }

    // ===== DEBUG =====

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        // Draw world bounds
        Gizmos.color = Color.yellow;
        Vector3 boundsCenter = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0);
        Vector3 boundsSize = new Vector3(maxX - minX, maxY - minY, 0);
        Gizmos.DrawWireCube(boundsCenter, boundsSize);

        // Draw camera frustum bounds
        if (Application.isPlaying && cam != null)
        {
            Gizmos.color = Color.green;
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            
            Vector3 frustumCenter = transform.position;
            Vector3 frustumSize = new Vector3(halfWidth * 2f, halfHeight * 2f, 0);
            Gizmos.DrawWireCube(frustumCenter, frustumSize);
        }

        // Label
        UnityEditor.Handles.Label(
            boundsCenter + Vector3.up * (maxY - minY) * 0.5f + Vector3.up * 2f,
            $"World Bounds\n{minX:F1} to {maxX:F1} (X)\n{minY:F1} to {maxY:F1} (Y)"
        );
    }
#endif

    [ContextMenu("Reset Camera to Top")]
    private void ResetCameraToTop()
    {
        if (cam == null) cam = GetComponent<Camera>();
        UpdateCameraFrustum();
        
        Vector3 pos = transform.position;
        pos.y = maxY - cameraHalfHeight;
        transform.position = pos;
        targetPosition = pos;
        
        Debug.Log("[CameraScrollController] Reset camera to top of bounds");
    }

    [ContextMenu("Debug: Print Current Bounds")]
    private void DebugPrintBounds()
    {
        UpdateCameraFrustum();
        Debug.Log($"[CameraScrollController] World Bounds: X({minX} to {maxX}), Y({minY} to {maxY})");
        Debug.Log($"[CameraScrollController] Camera Frustum: {cameraHalfWidth * 2f}w x {cameraHalfHeight * 2f}h");
        Debug.Log($"[CameraScrollController] Effective Bounds: X({minX + cameraHalfWidth} to {maxX - cameraHalfWidth}), Y({minY + cameraHalfHeight} to {maxY - cameraHalfHeight})");
    }
}