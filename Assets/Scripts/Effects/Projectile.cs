using UnityEngine;

/// <summary>
/// A fired shot (the Mage's fireball). Homes on its target every frame, but lands on the first hostile its path touches,
/// so a mob stepping in front takes the hit. The hit goes through the caster's <see cref="CombatBehavior.LandHit"/>, so
/// procs, damage numbers and kill XP behave exactly like a melee hit. A target that dies mid-flight leaves the shot
/// flying to its last spot, where it bursts harmlessly. Pooled (prefab on <c>AdventurerDef.projectilePrefab</c>);
/// game time, so it pauses with the game. Sprites flip rather than rotate to stay pixel-crisp — shots fly along a floor.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Projectile : MonoBehaviour
{
    [SerializeField] private Sprite[] flightFrames;
    [SerializeField] private Sprite[] impactFrames;
    [SerializeField] private float fps = 12f;
    [SerializeField] private float speed = 14f;
    [Tooltip("Radius swept along the path for the first-hostile check")]
    [SerializeField] private float hitRadius = 0.35f;
    [Tooltip("Spawn point from the caster's feet; x is mirrored toward the target")]
    [SerializeField] private Vector2 launchOffset = new(0.9f, 1.4f);
    [Tooltip("Seconds into the attack anim the shot leaves (the cast's point-forward frame)")]
    public float releaseDelay = 0.45f;
    [Tooltip("Bursts after this long regardless (target left the floor)")]
    [SerializeField] private float maxLifetime = 3f;

    private SpriteRenderer sr;
    private CombatBehavior owner;
    private Transform target;
    private Vector3 aim;
    private float age, frameTime;
    private bool impacting;
    private static readonly RaycastHit2D[] hits = new RaycastHit2D[16];
    private static ContactFilter2D everything;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        sr.sortingOrder = 10000;     // over EntitySortingManager's 2000–8000
        sr.renderingLayerMask = 1;   // no outline bits
        everything = new ContactFilter2D().NoFilter();
    }

    public void Fire(CombatBehavior from, GameObject at, float facing)
    {
        owner = from; target = at.transform;
        age = frameTime = 0f; impacting = false;
        Vector3 p = from.transform.position;
        transform.SetPositionAndRotation(new Vector3(p.x + launchOffset.x * facing, p.y + launchOffset.y, p.z), Quaternion.identity);
        aim = CenterOf(target);
        sr.flipX = facing < 0f;
        sr.sprite = flightFrames[0];
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt; frameTime += dt;
        int frame = (int)(frameTime * fps);

        if (impacting)
        {
            if (frame >= impactFrames.Length) { ObjectPoolManager.Instance.ReturnObjectToPool(gameObject); return; }
            sr.sprite = impactFrames[frame];
            return;
        }
        sr.sprite = flightFrames[frame % flightFrames.Length];

        if (target != null && target.gameObject.activeInHierarchy) aim = CenterOf(target);   // homing
        Vector3 pos = transform.position;
        Vector2 to = aim - pos;
        float dist = to.magnitude;
        Vector2 dir = dist > 0.0001f ? to / dist : (sr.flipX ? Vector2.left : Vector2.right);
        float travel = Mathf.Min(speed * dt, dist);
        if (Mathf.Abs(dir.x) > 0.01f) sr.flipX = dir.x < 0f;

        // First hostile along this frame's segment (cast results aren't guaranteed sorted — take the nearest valid)
        if (owner != null)
        {
            int n = Physics2D.CircleCast(pos, hitRadius, dir, everything, hits, travel);
            Health first = null; float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var go = hits[i].collider.gameObject;
                if (hits[i].distance >= best || !owner.IsValidTarget(go) || !go.TryGetComponent<Health>(out var h)) continue;
                first = h; best = hits[i].distance;
            }
            if (first != null)
            {
                transform.position = pos + (Vector3)(dir * best);
                owner.LandHit(first);
                Burst();
                return;
            }
        }

        transform.position = pos + (Vector3)(dir * travel);
        if (travel >= dist || age >= maxLifetime || owner == null) Burst();   // reached an empty spot, or the caster is gone
    }

    private void Burst()
    {
        impacting = true;
        frameTime = 0f;
        sr.sprite = impactFrames.Length > 0 ? impactFrames[0] : null;
    }

    private static Vector3 CenterOf(Transform t) =>
        t.TryGetComponent<Collider2D>(out var c) ? c.bounds.center : t.position;
}
