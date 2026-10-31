using UnityEngine;

/// <summary>
/// Cycles a SpriteRenderer through a fixed frame list on game time (pauses with the game).
/// Used by animated background sheets (the depth 5 lavafall + embers); the frames share
/// everything but the moving pixels, so this is cheaper than a particle rig inside the band mask.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFrameLoop : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float fps = 6f;
    [Tooltip("Offset in frames so copies of the same sheet don't tick in lockstep")]
    [SerializeField] private int phase;

    private SpriteRenderer sr;
    private int shown = -1;

    void Awake() => sr = GetComponent<SpriteRenderer>();

    void Update()
    {
        if (frames == null || frames.Length == 0) return;
        int i = (Mathf.FloorToInt(Time.time * fps) + phase) % frames.Length;
        if (i == shown) return;
        shown = i;
        sr.sprite = frames[i];
    }
}
