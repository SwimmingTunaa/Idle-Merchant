using Unity.VisualScripting;
using UnityEditor.Animations;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/Entity")]
public class EntityDef : ScriptableObject
{
    public GameObject prefab;
    //this controls the visual
    public AnimatorOverrideController animatorOverrideController;
    public Sprite spriteVisual;
    public Sprite icon;
    public string id;         // unique key
    public string displayName;
    public string description;
    public int hireCost = 50;
    public int assignedLayer = 1;


    [Header("Sorting")]
    [Tooltip("Entity type for sorting order assignment. Prevents z-fighting.")]
    public EntitySortingType sortingType = EntitySortingType.Mob;


    [Header("Spawn")]
    public float spawnWeight = 1f;     // used by weighted RNG
    
    [Header("Stats")]
    public float moveSpeed;
    public float stopDistance = 0.05f;
    public Vector2 idleTimeRange;

    [Header("Death Animation")]
    [Tooltip("Duration of death animation before despawning (0 = instant)")]
    [Range(0f, 3f)]
    public float deathAnimationDuration = 0.5f;

    [Header("Collider")]
    [Tooltip("Collider size (leave at zero to auto-calculate from sprite)")]
    public Vector2 colliderSize = Vector2.zero;
    
    [Tooltip("Collider offset from entity center")]
    public Vector2 colliderOffset = Vector2.zero;

#if UNITY_EDITOR
    [ContextMenu("Auto-Calculate Collider Size From Sprite")]
    private void AutoCalculateColliderSize()
    {
        if (spriteVisual == null)
        {
            UnityEngine.Debug.LogWarning($"[{name}] No animatorOverrideController assigned!");
            return;
        }
        //change this to GetIdleSpriteFromAnimator(), to get from animator.
        Sprite sprite = spriteVisual;
        
        if (sprite != null)
        {
            Rect spriteRect = sprite.rect;
            float pixelsPerUnit = sprite.pixelsPerUnit;
            
            // Calculate size in world units
            colliderSize = new Vector2(
                spriteRect.width / pixelsPerUnit,
                spriteRect.height / pixelsPerUnit
            );
            
            // Calculate offset (sprite pivot to sprite center)
            Vector2 spritePivot = sprite.pivot;
            Vector2 spriteCenter = new Vector2(spriteRect.width / 2f, spriteRect.height / 2f);
            Vector2 pivotOffset = (spriteCenter - spritePivot) / pixelsPerUnit;
            colliderOffset = pivotOffset;
            
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEngine.Debug.Log($"[{name}] Collider size set to {colliderSize}, offset {colliderOffset} from sprite '{sprite.name}'");
        }
        else
        {
            UnityEngine.Debug.LogWarning($"[{name}] Could not extract idle sprite from animator!");
        }
    }

    private Sprite GetIdleSpriteFromAnimator()
    {
        if (animatorOverrideController == null) return null;

        AnimationClip[] clips = animatorOverrideController.animationClips;
        if (clips == null || clips.Length == 0) return null;

        // Search for idle animation by name (case-insensitive)
        AnimationClip idleClip = null;
        foreach (var clip in clips)
        {
            if (clip.name.ToLower().Contains("idle"))
            {
                idleClip = clip;
                break;
            }
        }
        
        // Fallback to first clip if no idle found
        if (idleClip == null)
        {
            idleClip = clips[0];
            UnityEngine.Debug.LogWarning($"[{name}] No 'idle' animation found, using first clip '{idleClip.name}'");
        }
        
        // Extract sprite from clip
        var bindings = UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(idleClip);
        foreach (var binding in bindings)
        {
            if (binding.type == typeof(SpriteRenderer) && binding.propertyName == "m_Sprite")
            {
                var keyframes = UnityEditor.AnimationUtility.GetObjectReferenceCurve(idleClip, binding);
                if (keyframes != null && keyframes.Length > 0)
                {
                    return keyframes[0].value as Sprite;
                }
            }
        }

        return null;
    }
#endif

}