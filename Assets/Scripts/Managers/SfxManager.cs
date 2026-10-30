using UnityEngine;
using UnityEngine.Audio;

public enum SfxId { ClickHit, Kill, Coin, Kaching, Hire, MilestoneDing, StarFanfare, Vacuum, UiTick, PageFlip, Stamp, PaperTear, PaperSlide, Pin } // serialized ints: append only

/// <summary>
/// One-shot 2D SFX. A small round-robin AudioSource pool so each play gets its own pitch jitter
/// (oldest voice is stolen when all are busy). Per-id throttle keeps bursts (auto-click + AOE,
/// 36-coin showers) from machine-gunning. Signal-driven sounds subscribe here; VFX/UI seams call Play.
/// Volumes (master / sfx / music) persist in SaveData.settings and drive the Main mixer's exposed
/// params (MasterVol / SfxVol / MusicVol, dB); the pool routes to the SFX group, which sidechain-ducks Music.
/// </summary>
public class SfxManager : PersistentSingleton<SfxManager>
{
    [System.Serializable]
    public class SfxEntry
    {
        public SfxId id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Random pitch offset ± this (frequent sounds, so repeats don't sound identical)")]
        public float pitchJitter;
        [Tooltip("Min seconds (unscaled) between plays of this id; extra plays inside the window are dropped")]
        public float minInterval;

        public SfxEntry(SfxId id, float volume, float pitchJitter, float minInterval)
        {
            this.id = id; this.volume = volume; this.pitchJitter = pitchJitter; this.minInterval = minInterval;
        }
    }

    [Header("Sounds (one entry per SfxId)")]
    [SerializeField] private SfxEntry[] sounds =
    {
        new SfxEntry(SfxId.ClickHit,      0.7f, 0.06f, 0.05f),
        new SfxEntry(SfxId.Kill,          0.9f, 0.05f, 0.05f),
        new SfxEntry(SfxId.Coin,          0.5f, 0.06f, 0.05f),   // ≤ ~20/s through a coin shower
        new SfxEntry(SfxId.Kaching,       0.8f, 0.03f, 0.10f),
        new SfxEntry(SfxId.Hire,          0.9f, 0.03f, 0.05f),
        new SfxEntry(SfxId.MilestoneDing, 0.8f, 0f,    0.20f),   // several reached in one frame → one ding
        new SfxEntry(SfxId.StarFanfare,   0.9f, 0f,    0.50f),
        new SfxEntry(SfxId.Vacuum,        0.6f, 0.05f, 0.08f),
        new SfxEntry(SfxId.UiTick,        0.6f, 0.04f, 0.03f),
        new SfxEntry(SfxId.PageFlip,      0.7f, 0.06f, 0.05f),
        // Hiring board (ElevenLabs-generated, Audio/SFX/sfx_stamp / paper_tear / paper_slide / pin)
        new SfxEntry(SfxId.Stamp,         0.9f, 0.03f, 0.10f),
        new SfxEntry(SfxId.PaperTear,     0.8f, 0.05f, 0.10f),
        new SfxEntry(SfxId.PaperSlide,    0.6f, 0.06f, 0.05f),
        new SfxEntry(SfxId.Pin,           0.6f, 0.05f, 0.05f),
    };

    [Header("Mix")]
    [SerializeField] private AudioMixer mixer;
    [SerializeField, Range(0f, 1f)] private float masterVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 1f;
    [SerializeField] private int voices = 8;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    private static readonly int IdCount = System.Enum.GetValues(typeof(SfxId)).Length;
    private readonly SfxEntry[] byId = new SfxEntry[IdCount];
    private readonly float[] lastPlayTime = new float[IdCount];
    private readonly int[] playCounts = new int[IdCount];
    private AudioSource[] pool;
    private int nextVoice;

    private bool mixDirty = true;   // flushed in Update: AudioMixer.SetFloat is a no-op in Awake, and save restore may land before Start
    public float MasterVolume { get => masterVolume; set { masterVolume = Mathf.Clamp01(value); mixDirty = true; } }
    public float SfxVolume { get => sfxVolume; set { sfxVolume = Mathf.Clamp01(value); mixDirty = true; } }
    public float MusicVolume { get => musicVolume; set { musicVolume = Mathf.Clamp01(value); mixDirty = true; } }
    /// <summary>Last id that actually played (after throttle) — for the debugger / play-mode checks.</summary>
    public SfxId? LastPlayed { get; private set; }
    public int PlayCount(SfxId id) => playCounts[(int)id];

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;

        foreach (var e in sounds) byId[(int)e.id] = e;
        for (int i = 0; i < IdCount; i++) lastPlayTime[i] = float.NegativeInfinity;

        var sfxGroup = mixer != null ? mixer.FindMatchingGroups("SFX") : null;
        pool = new AudioSource[Mathf.Max(1, voices)];
        for (int i = 0; i < pool.Length; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            if (sfxGroup != null && sfxGroup.Length > 0) src.outputAudioMixerGroup = sfxGroup[0];
            pool[i] = src;
        }

        GameSignals.OnItemSold += OnItemSold;
        GameSignals.OnMilestoneReached += OnMilestoneReached;
        GameSignals.OnStarEarned += OnStarEarned;
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        GameSignals.OnItemSold -= OnItemSold;
        GameSignals.OnMilestoneReached -= OnMilestoneReached;
        GameSignals.OnStarEarned -= OnStarEarned;
    }

    void Update()
    {
        if (!mixDirty || mixer == null) return;
        mixDirty = false;
        mixer.SetFloat("MasterVol", ToDb(masterVolume));
        mixer.SetFloat("SfxVol", ToDb(sfxVolume));
        mixer.SetFloat("MusicVol", ToDb(musicVolume));
    }

    private static float ToDb(float v) => v <= 0.0001f ? -80f : 20f * Mathf.Log10(v);

    private void OnItemSold(ResourceStack _) => Play(SfxId.Kaching);
    private void OnMilestoneReached(StarMilestoneDef _) => Play(SfxId.MilestoneDing);
    private void OnStarEarned(int _) => Play(SfxId.StarFanfare);

    public void Play(SfxId id)
    {
        var e = byId[(int)id];
        if (e == null || e.clip == null || pool == null) return;

        // Unscaled: kill hit-stop sets timeScale 0 and pause must not unthrottle anything
        float now = Time.unscaledTime;
        if (now - lastPlayTime[(int)id] < e.minInterval) return;
        lastPlayTime[(int)id] = now;

        var src = pool[nextVoice];
        nextVoice = (nextVoice + 1) % pool.Length;
        src.clip = e.clip;
        src.volume = e.volume;
        src.pitch = 1f + Random.Range(-e.pitchJitter, e.pitchJitter);
        src.Play();   // restarts the voice: round-robin steals the oldest when all are busy

        playCounts[(int)id]++;
        LastPlayed = id;
        if (showDebugLogs) Debug.Log($"[SfxManager] {id} (pitch {src.pitch:F2})");
    }
}
