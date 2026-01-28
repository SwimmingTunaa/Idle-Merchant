using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class ClickerManager : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private float clickDamage = 1f;
    [SerializeField] private int clicksPerSecondCap = 10;

    [Header("AOE Settings")]
    [Tooltip("Radius for damage AOE (0 = single target only)")]
    [SerializeField] private float damageRadius = 0f;
    
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

    void Update()
    {
        secondTimer += Time.deltaTime;
        if (secondTimer >= 1f)
        {
            secondTimer = 0f;
            clicksThisSecond = 0;
        }
    }

    public void OnPlayerClick()
    {
        if (clicksThisSecond >= clicksPerSecondCap) return;
        clicksThisSecond++;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Vector3 world = Camera.main.ScreenToWorldPoint(screenPos);
        lastClickPosition = world;

        var directHit = Physics2D.OverlapPoint(world);
        
        // Priority 1: Direct loot click
        if (directHit && directHit.TryGetComponent(out IClickableLoot directLoot))
        {
            if (lootPickupRadius > 0f)
            {
                VacuumLootToMouse(world, lootPickupRadius);
            }
            else
            {
                StartCoroutine(MoveLootToMouse(directHit.gameObject));
            }
            return;
        }

        // Priority 2: Direct mob click
        if (directHit && directHit.TryGetComponent(out IDamageable enemy))
        {
            // Don't damage dying entities
            if (directHit.TryGetComponent(out EntityBase entity) && entity.IsDying)
                return;

            if (damageRadius > 0f)
            {
                // AOE shockwave centered on the mob you clicked
                if (activeShockwave != null)
                {
                    StopCoroutine(activeShockwave);
                }
                activeShockwave = StartCoroutine(DamageShockwave(directHit.transform.position, damageRadius, shockwaveDuration));
            }
            else
            {
                // Single target damage
                float applied = enemy.OnDamage(clickDamage);
                if (applied > 0f)
                {
                    if (DamageNumberManager.Instance != null)
                    {
                        DamageNumberManager.Instance.ShowGoldGain(applied, directHit.transform);
                    }
                    Debug.Log(applied);
                    Inventory.Instance.AddGoldFloat(applied);
                }
            }
            return;
        }

        // Click on empty space = no action
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

                    float applied = enemy.OnDamage(clickDamage);
                    if (applied > 0f)
                    {
                        // Show gold number
                        if (DamageNumberManager.Instance != null)
                        {
                            DamageNumberManager.Instance.ShowGoldGain(applied, hit.transform);
                        }
                        
                        Inventory.Instance.AddGoldFloat(applied);
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
            
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
            mouseWorldPos.z = 0f;

            float t = elapsed / lootVacuumSpeed;
            lootObj.transform.position = Vector3.Lerp(startPos, mouseWorldPos, t);

            if (Vector3.Distance(lootObj.transform.position, mouseWorldPos) < 0.1f)
            {
                if (lootObj.TryGetComponent(out IClickableLoot loot))
                {
                    loot.OnManualCollect();
                }
                yield break;
            }

            yield return null;
        }

        if (lootObj != null && lootObj.TryGetComponent(out IClickableLoot finalLoot))
        {
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

        if (damageRadius > 0f)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
            Gizmos.DrawWireSphere(lastClickPosition, damageRadius);
        }

        if (lootPickupRadius > 0f)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(lastClickPosition, lootPickupRadius);
        }
    }
#endif
}