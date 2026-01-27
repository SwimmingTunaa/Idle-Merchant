using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.U2D.Animation;

[CreateAssetMenu(menuName = "Data/Entity")]
public class EntityDef : ScriptableObject
{
    public GameObject prefab;
    public bool useModularCharacter = false;
    public AnimatorOverrideController[] animatorOverrideController;
    public Sprite spriteDrop; 
    public Sprite shopSprite;
    public string id;
    public string displayName;
    public string description;
    public int hireCost = 50;
    public int assignedLayer = 1;

    [Header("Skills")]
    [Tooltip("Skills automatically applied when entity spawns")]
    public SkillDef[] startingSkills = new SkillDef[0];

    [Header("Sorting")]
    [Tooltip("Entity type for sorting order assignment. Prevents z-fighting.")]
    public EntitySortingType sortingType = EntitySortingType.Mob;

    [Header("Spawn")]
    public float spawnWeight = 1f;
    
    [Header("Stats")]
    public float moveSpeed;
    public float stopDistance = 0.05f;
    public Vector2 idleTimeRange;

    [Header("Combat Stats")]
    [Tooltip("Damage dealt per attack")]
    public float attackDamage = 1f;
    
    [Tooltip("Time between attacks (lower = faster)")]
    public float attackInterval = 1f;
    
    [Tooltip("How close entity must be to attack")]
    public float attackRange = 1.5f;
    
    [Tooltip("Distance at which to stop chasing (should be > attackRange)")]
    public float chaseBreakRange = 2.5f;
    
    [Tooltip("How far entity scans for targets")]
    public float scanRange = 10f;

    [Header("Death Animation")]
    [Tooltip("Duration of death animation before despawning (0 = instant)")]
    [Range(0f, 3f)]
    public float deathAnimationDuration = 0.5f;

    [Header("Collider")]
    [Tooltip("Collider size (leave at zero to auto-calculate from sprite)")]
    public Vector2 colliderSize = Vector2.zero;
    
    [Tooltip("Collider offset from entity center")]
    public Vector2 colliderOffset = Vector2.zero;

    [Header("Sprite Libraries")]
    public SpriteLibraryAsset[] baseBodySpriteLibraries;
    public SpriteLibraryAsset[] shirtSpriteLibraries;
    public SpriteLibraryAsset[] pantsSpriteLibraries;
    public SpriteLibraryAsset[] hairTopSpriteLibraries;
    public SpriteLibraryAsset[] hairBackSpriteLibraries;
    public SpriteLibraryAsset[] frontWeaponSpriteLibraries;
    public SpriteLibraryAsset[] backWeaponSpriteLibraries;

    [Header("Colour palettes")]
    public ColourPalette skinColourPalette;
    public ColourPalette ShirtColourPalette;
    public ColourPalette PantsColourPalette;
    public ColourPalette HairColourPalette;
}