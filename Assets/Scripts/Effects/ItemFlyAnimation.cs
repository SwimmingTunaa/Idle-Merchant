using UnityEngine;
using System.Collections;

/// <summary>
/// Animates a coin sprite flying from world position to UI screen position.
/// Flies in screen space (2D pixel coordinates) for accurate UI targeting.
/// Three phases: explosion → flight → arrival.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ItemFlyAnimation : MonoBehaviour
{
    [Header("Animation Timing")]
    [SerializeField] private float explosionDuration = 0.3f;
    [SerializeField] private float flightDuration = 0.7f;
    [SerializeField] private float arrivalDuration = 0.1f;
    
    [Header("Explosion")]
    [SerializeField] private float explosionForce = 1.5f;
    
    [Header("Flight")]
    [SerializeField] private float arcHeight = 0.8f;
    [SerializeField] private float rotationSpeed = 540f;
    
    [Header("Arrival VFX")]
    [SerializeField] private GameObject arrivalParticlePrefab;
    
    private Camera mainCamera;
    private SpriteRenderer spriteRenderer;
    private Coroutine flyCoroutine;
    
    private Vector2 targetScreenPosition;
    private Vector3 explosionVelocity;
    private System.Action onArrivalCallback;
    
    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        mainCamera = Camera.main;
    }
    
    void OnDisable()
    {
        if (flyCoroutine != null)
        {
            StopCoroutine(flyCoroutine);
            flyCoroutine = null;
        }
    }
    
    public void StartFlight(Vector2 screenTarget, Vector3 randomExplosionDir, System.Action onComplete = null)
    {
        targetScreenPosition = screenTarget;
        explosionVelocity = randomExplosionDir * explosionForce;
        onArrivalCallback = onComplete;
        
        if (flyCoroutine != null)
            StopCoroutine(flyCoroutine);
        
        flyCoroutine = StartCoroutine(FlySequence());
    }
    
    private IEnumerator FlySequence()
    {
        yield return StartCoroutine(ExplosionPhase());
        yield return StartCoroutine(FlightPhase());
        yield return StartCoroutine(ArrivalPhase());
        
        ObjectPoolManager.Instance.ReturnObjectToPool(gameObject);
    }
    
    private IEnumerator ExplosionPhase()
    {
        float elapsed = 0f;
        Vector3 startWorldPos = transform.position;
        
        while (elapsed < explosionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / explosionDuration;
            
            Vector3 offset = explosionVelocity * t;
            offset.y += Mathf.Sin(t * Mathf.PI) * 0.5f;
            
            transform.position = startWorldPos + offset;
            transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
            
            yield return null;
        }
    }
    
    private IEnumerator FlightPhase()
    {
        float elapsed = 0f;
        Vector2 startScreenPos = WorldToScreen(transform.position);
        
        while (elapsed < flightDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flightDuration;
            
            float easedT = Mathf.SmoothStep(0f, 1f, t);
            
            Vector2 currentScreenPos = Vector2.Lerp(startScreenPos, targetScreenPosition, easedT);
            
            float arcOffset = Mathf.Sin(easedT * Mathf.PI) * arcHeight * Screen.height * 0.1f;
            currentScreenPos.y += arcOffset;
            
            transform.position = ScreenToWorld(currentScreenPos);
            transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
            
            yield return null;
        }
        
        transform.position = ScreenToWorld(targetScreenPosition);
    }
    
    private IEnumerator ArrivalPhase()
    {
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        Color startColor = spriteRenderer.color;
        
        while (elapsed < arrivalDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / arrivalDuration;
            
            float scale = Mathf.Lerp(1f, 1.2f, t);
            if (t > 0.5f)
                scale = Mathf.Lerp(1.2f, 0f, (t - 0.5f) * 2f);
            
            transform.localScale = startScale * scale;
            
            Color color = startColor;
            color.a = 1f - t;
            spriteRenderer.color = color;
            
            yield return null;
        }
        
        // Spawn particle effect at arrival position
        if (arrivalParticlePrefab != null)
        {
            Vector3 worldPos = ScreenToWorld(targetScreenPosition);
            ObjectPoolManager.Instance.SpawnObject(
                arrivalParticlePrefab,
                worldPos,
                Quaternion.identity,
                ObjectPoolManager.PoolType.ParticleSystem
            );
        }
        
        // Invoke callback if provided
        onArrivalCallback?.Invoke();
        onArrivalCallback = null;
    }
    
    private Vector2 WorldToScreen(Vector3 worldPos)
    {
        if (mainCamera == null) mainCamera = Camera.main;
        return mainCamera.WorldToScreenPoint(worldPos);
    }
    
    private Vector3 ScreenToWorld(Vector2 screenPos)
    {
        if (mainCamera == null) mainCamera = Camera.main;
        
        Vector3 screenPoint = new Vector3(screenPos.x, screenPos.y, mainCamera.WorldToScreenPoint(transform.position).z);
        return mainCamera.ScreenToWorldPoint(screenPoint);
    }
}