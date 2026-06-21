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

        // Right-click is a separate channel from the left-click clicker.
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            OnPlayerInspect();
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
                if (lootPickupRadius > 0f)
                    VacuumLootToMouse(world, lootPickupRadius);
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

            if (damageRadius > 0f)
            {
                // AOE shockwave centered on the mob you clicked
                if (activeShockwave != null) StopCoroutine(activeShockwave);
                activeShockwave = StartCoroutine(DamageShockwave(hit.transform.position, damageRadius, shockwaveDuration));
            }
            else
            {
                // Single target damage
                float applied = enemy.OnDamage(clickDamage);
                if (applied > 0f)
                {
                    if (DamageNumberManager.Instance != null)
                        DamageNumberManager.Instance.ShowGoldGain(applied, hit.transform);
                    Inventory.Instance.AddGoldFloat(applied);
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
            
            Vector3 mouseWorldPos = GetCursorWorldPos();
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