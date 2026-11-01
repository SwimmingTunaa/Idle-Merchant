using UnityEngine;

/// <summary>
/// Persistent marker bobbing above an adventurer: the next rank's badge plus an up arrow while it
/// CanPromote, and a thought bubble with its rank-fit face (RankFit) while its rank doesn't fit its
/// depth — strong moods keep it up, mild ones pop it for a few seconds and on hover / Roster selection;
/// the bubble sits left of the badge when both show. Builds its own child renderers at runtime — only sprites need
/// assigning on the prefab (faces come from Resources/RankFit).
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
    [Tooltip("Rank-fit thought bubble scale (the bubble art is world-native at 8 PPU; 0.5 = half size)")]
    [SerializeField] private float bubbleScale = 0.6f;
    [Tooltip("Face texels per bubble texel: 2 = the 16 px face sits in the 18 px bubble body with a white margin")]
    [SerializeField] private float faceTexelsPerBubbleTexel = 2f;
    [Tooltip("Face centre inside the full bubble frame, in bubble pixels from its pivot")]
    [SerializeField] private Vector2 faceOffsetPx = new Vector2(1f, 18f);
    [Tooltip("Seconds per bubble pop-in frame")]
    [SerializeField] private float popFrameTime = 0.07f;
    [SerializeField] private Color arrowColor = new Color(0.45f, 0.85f, 0.3f);

    private AdventurerAgent agent;
    private Transform root;
    private SpriteRenderer badgeRenderer;
    private SpriteRenderer arrowRenderer;
    private Transform bubbleGroup;
    private SpriteRenderer bubbleRenderer, faceRenderer;
    private bool promoteShown, bubbleShown;
    private RankFit.Mood lastMood = RankFit.Mood.Fit;
    private float mildTimer, popT;
    private static Sprite fallbackArrow;   // shared runtime-generated pixel arrow

    void Awake()
    {
        agent = GetComponent<AdventurerAgent>();

        root = new GameObject("PromoteIndicator").transform;
        root.SetParent(transform, false);
        // The adventurer's root SortingGroup would cap the marker at its own entity order: sort it at the root instead
        var group = root.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
        group.sortAtRoot = true;
        group.sortingOrder = 32000;

        badgeRenderer = NewChildRenderer("Badge", Vector3.zero);

        arrowRenderer = NewChildRenderer("Arrow", new Vector3(0.32f, 0f, 0f));
        arrowRenderer.sprite = arrowSprite != null ? arrowSprite : FallbackArrow();
        arrowRenderer.color = arrowColor;

        // Rank-fit thought bubble with the face inside; it pops in, then holds (mild moods time out)
        bubbleGroup = new GameObject("Fit Bubble").transform;
        bubbleGroup.SetParent(root, false);
        bubbleRenderer = NewChildRenderer("Bubble", Vector3.zero);
        bubbleRenderer.transform.SetParent(bubbleGroup, false);
        bubbleRenderer.transform.localScale = Vector3.one * bubbleScale;
        faceRenderer = NewChildRenderer("Face", Vector3.zero);
        faceRenderer.transform.SetParent(bubbleGroup, false);
        faceRenderer.sortingOrder = 32001;

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
        sr.renderingLayerMask |= HoverOutlineFeature.OverlayRenderingLayer;   // and above the outlines
        return sr;
    }

    void LateUpdate()
    {
        bool promote = agent.CanPromote && agent.IsAlive;
        var rules = RankFit.Instance;
        var mood = RankFit.MoodOf(agent.Fit);
        if (mood != lastMood) { lastMood = mood; mildTimer = rules != null ? rules.mildShowSeconds : 0f; }
        if (mildTimer > 0f) mildTimer -= Time.deltaTime;
        Sprite face = agent.IsAlive && mood != RankFit.Mood.Fit && rules != null ? rules.Face(mood) : null;
        // Strong moods keep their bubble; mild ones only after a change and while hovered / selected
        bool bubble = face != null && (!RankFit.IsMild(mood) || mildTimer > 0f || agent.IsHighlighted);
        bool show = promote || bubble;

        if (root.gameObject.activeSelf != show)
        {
            root.gameObject.SetActive(show);
            // PermanentOutline flags every child SpriteRenderer for the black outline pass on activation —
            // strip that bit so the marker isn't stroked, keep the overlay bit so it hides the outlines under it
            if (show)
                foreach (var sr in new[] { badgeRenderer, arrowRenderer, bubbleRenderer, faceRenderer })
                    sr.renderingLayerMask = 1 | HoverOutlineFeature.OverlayRenderingLayer;
        }
        if (!show) { promoteShown = false; bubbleShown = false; return; }

        // Badge + arrow follow promote every frame (the marker can be up for the bubble alone)
        badgeRenderer.gameObject.SetActive(promote);
        arrowRenderer.gameObject.SetActive(promote);
        if (promote != promoteShown)
        {
            promoteShown = promote;
            if (promote)
            {
                if (rankBadges != null && rankBadges.Length > 0)
                    badgeRenderer.sprite = rankBadges[Mathf.Clamp(agent.CurrentRank - 1, 0, rankBadges.Length - 1)];
                // Normalise badge to a fixed world height — badge art PPU varies
                if (badgeRenderer.sprite != null && badgeRenderer.sprite.bounds.size.y > 0f)
                    badgeRenderer.transform.localScale = Vector3.one * (badgeWorldHeight / badgeRenderer.sprite.bounds.size.y);
            }
        }

        bubbleGroup.gameObject.SetActive(bubble);
        if (bubble)
        {
            if (!bubbleShown) popT = 0f;   // pop in each time it appears
            popT += Time.deltaTime;
            var frames = rules.bubbleFrames;
            int last = frames != null ? frames.Length - 1 : -1;
            int frame = last < 0 ? -1 : Mathf.Min((int)(popT / popFrameTime), last);
            bubbleRenderer.sprite = frame >= 0 ? frames[frame] : null;
            faceRenderer.enabled = frame == last;   // the face appears with the full bubble
            if (faceRenderer.sprite != face && last >= 0 && frames[last] != null)
            {
                faceRenderer.sprite = face;
                // The face drawn at a fixed fraction of the bubble's texel, centred in its round body
                float px = bubbleScale / frames[last].pixelsPerUnit;
                faceRenderer.transform.localScale = Vector3.one * (px / faceTexelsPerBubbleTexel * face.pixelsPerUnit);
                faceRenderer.transform.localPosition = new Vector3(faceOffsetPx.x * px, faceOffsetPx.y * px, 0f);
            }
            bubbleGroup.localPosition = new Vector3(promote ? -0.75f : 0f, 0f, 0f);
        }
        bubbleShown = bubble;

        // Sit above the collider top, whatever the unit's size
        float top = 1f;
        if (TryGetComponent(out BoxCollider2D col)) top = col.offset.y + col.size.y * 0.5f;
        root.localPosition = new Vector3(0f, top + clearance + Mathf.Sin(Time.time * bobSpeed) * bobAmplitude, 0f);
        // Counter the agent's facing flip so the marker never mirrors
        root.localScale = new Vector3(Mathf.Sign(transform.localScale.x), 1f, 1f);
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
