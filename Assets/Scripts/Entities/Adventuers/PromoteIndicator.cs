using UnityEngine;

/// <summary>
/// Persistent "ready to promote" marker bobbing above an adventurer: the next rank's
/// badge plus an up arrow, shown whenever the adventurer CanPromote and hidden again
/// after promotion. Builds its own child renderers at runtime — only sprites need
/// assigning on the prefab.
/// </summary>
[RequireComponent(typeof(AdventurerAgent))]
public class PromoteIndicator : MonoBehaviour
{
    [Header("Sprites")]
    [Tooltip("Badge per rank the adventurer can promote INTO (index 0 = rank 2's badge). " +
             "Clamps to the last entry while badge art is missing.")]
    [SerializeField] private Sprite[] rankBadges;

    [Tooltip("Up-arrow sprite. Leave empty to use a generated pixel arrow until art exists.")]
    [SerializeField] private Sprite arrowSprite;

    [Header("Layout")]
    [Tooltip("Gap between the top of the adventurer's collider and the marker.")]
    [SerializeField] private float clearance = 0.45f;
    [SerializeField] private float bobAmplitude = 0.06f;
    [SerializeField] private float bobSpeed = 3f;
    [Tooltip("Badge height in world units — renderer is scaled to this regardless of the sprite's import PPU.")]
    [SerializeField] private float badgeWorldHeight = 0.45f;
    [SerializeField] private Color arrowColor = new Color(0.45f, 0.85f, 0.3f);

    private AdventurerAgent agent;
    private Transform root;
    private SpriteRenderer badgeRenderer;
    private SpriteRenderer arrowRenderer;
    private static Sprite fallbackArrow;   // shared runtime-generated pixel arrow

    void Awake()
    {
        agent = GetComponent<AdventurerAgent>();

        root = new GameObject("PromoteIndicator").transform;
        root.SetParent(transform, false);

        badgeRenderer = NewChildRenderer("Badge", Vector3.zero);

        arrowRenderer = NewChildRenderer("Arrow", new Vector3(0.32f, 0f, 0f));
        arrowRenderer.sprite = arrowSprite != null ? arrowSprite : FallbackArrow();
        arrowRenderer.color = arrowColor;

        root.gameObject.SetActive(false);
    }

    private SpriteRenderer NewChildRenderer(string name, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.localPosition = localPos;
        var sr = go.AddComponent<SpriteRenderer>();
        // World-space marker: always on top of entities and scenery
        sr.sortingOrder = 32000;
        return sr;
    }

    void LateUpdate()
    {
        bool show = agent.CanPromote && agent.IsAlive;

        if (root.gameObject.activeSelf != show)
        {
            root.gameObject.SetActive(show);
            if (show)
            {
                if (rankBadges != null && rankBadges.Length > 0)
                    badgeRenderer.sprite = rankBadges[Mathf.Clamp(agent.CurrentRank - 1, 0, rankBadges.Length - 1)];

                // Normalise badge to a fixed world height — badge art PPU varies
                if (badgeRenderer.sprite != null && badgeRenderer.sprite.bounds.size.y > 0f)
                    badgeRenderer.transform.localScale = Vector3.one * (badgeWorldHeight / badgeRenderer.sprite.bounds.size.y);

                // PermanentOutline flags every child SpriteRenderer for the black outline
                // pass on activation — strip the bit so the marker isn't stroked.
                badgeRenderer.renderingLayerMask = 1;
                arrowRenderer.renderingLayerMask = 1;
            }
        }

        if (show)
        {
            // Sit above the collider top, whatever the unit's size
            float top = 1f;
            if (TryGetComponent(out BoxCollider2D col)) top = col.offset.y + col.size.y * 0.5f;
            root.localPosition = new Vector3(0f, top + clearance + Mathf.Sin(Time.time * bobSpeed) * bobAmplitude, 0f);
            // Counter the agent's facing flip so the marker never mirrors
            root.localScale = new Vector3(Mathf.Sign(transform.localScale.x), 1f, 1f);
        }
    }

    /// <summary>7x9 pixel up arrow, generated once. Replaced by assigning arrowSprite.</summary>
    private static Sprite FallbackArrow()
    {
        if (fallbackArrow != null) return fallbackArrow;

        string[] rows =
        {
            "...X...",
            "..XXX..",
            ".XXXXX.",
            "XXXXXXX",
            "..XXX..",
            "..XXX..",
            "..XXX..",
            "..XXX..",
            "..XXX..",
        };
        int w = rows[0].Length, h = rows.Length;
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false) { filterMode = FilterMode.Point };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, h - 1 - y, rows[y][x] == 'X' ? Color.white : Color.clear);
        tex.Apply();

        fallbackArrow = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
        fallbackArrow.name = "GeneratedPromoteArrow";
        return fallbackArrow;
    }
}
