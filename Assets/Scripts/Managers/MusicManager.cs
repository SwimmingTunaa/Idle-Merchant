using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Background music + crowd ambience. Three looping stems (bed always on, shop vs depths
/// blended by camera Y) and a crowd murmur whose level follows the shop headcount. All
/// sources start phase-locked on one dsp time and route through the Main mixer (Music /
/// Ambience groups); the sliders in SfxManager drive the mixer, not these sources.
/// </summary>
public class MusicManager : MonoBehaviour
{
    [Header("Stems (same tempo / key / bar count)")]
    [SerializeField] private AudioClip bed;
    [SerializeField] private AudioClip shop;
    [SerializeField] private AudioClip depths;
    [SerializeField] private AudioClip crowd;
    [SerializeField] private AudioMixer mixer;

    [Header("Blend")]
    [Tooltip("Camera Y at or above which only the shop stem plays")]
    [SerializeField] private float shopY = 0f;
    [Tooltip("Camera Y at or below which only the depths stem plays")]
    [SerializeField] private float depthsY = -10f;
    [Tooltip("How fast the blend follows the camera (per second, unscaled)")]
    [SerializeField] private float smoothing = 4f;

    [Header("Levels")]
    [SerializeField, Range(0f, 1f)] private float bedLevel = 0.8f;
    [SerializeField, Range(0f, 1f)] private float shopLevel = 0.9f;
    [SerializeField, Range(0f, 1f)] private float depthsLevel = 0.9f;
    [SerializeField, Range(0f, 1f)] private float crowdLevel = 0.7f;
    [Tooltip("Customers in the shop at which the crowd loop reaches full level")]
    [SerializeField] private int fullCrowd = 8;

    private AudioSource bedSrc, shopSrc, depthsSrc, crowdSrc;
    private Transform cam;
    private float depthWeight;   // 0 = shop, 1 = depths
    private float crowd01;

    /// <summary>Current shop→depths blend, 0..1 — for the debugger.</summary>
    public float DepthWeight => depthWeight;
    public float Crowd01 => crowd01;

    void Start()
    {
        cam = Camera.main != null ? Camera.main.transform : null;
        var music = Group("Music");
        var ambience = Group("Ambience");
        bedSrc = Make(bed, music);
        shopSrc = Make(shop, music);
        depthsSrc = Make(depths, music);
        crowdSrc = Make(crowd, ambience);

        depthWeight = TargetWeight();
        ApplyVolumes();

        double at = AudioSettings.dspTime + 0.1;   // one start time → stems stay in phase
        foreach (var s in new[] { bedSrc, shopSrc, depthsSrc, crowdSrc })
            if (s.clip != null) s.PlayScheduled(at);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime * smoothing;
        depthWeight = Mathf.MoveTowards(depthWeight, TargetWeight(), dt);
        crowd01 = Mathf.MoveTowards(crowd01, Mathf.Clamp01(CustomerCount() / (float)Mathf.Max(1, fullCrowd)), dt * 0.25f);
        ApplyVolumes();
    }

    private float TargetWeight()
    {
        if (cam == null) return 0f;
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(shopY, depthsY, cam.position.y));
    }

    private static int CustomerCount()
    {
        int n = QueueController.Instance != null ? QueueController.Instance.Count : 0;
        if (ShopManager.Instance != null) n += ShopManager.Instance.NonQueueingCustomers.Count;
        return n;
    }

    private void ApplyVolumes()
    {
        float shopW = 1f - depthWeight;
        bedSrc.volume = bedLevel;
        shopSrc.volume = shopLevel * shopW;
        depthsSrc.volume = depthsLevel * depthWeight;
        crowdSrc.volume = crowdLevel * crowd01 * shopW;   // silent underground
    }

    private AudioMixerGroup Group(string name)
    {
        if (mixer == null) return null;
        var g = mixer.FindMatchingGroups(name);
        return g.Length > 0 ? g[0] : null;
    }

    private AudioSource Make(AudioClip clip, AudioMixerGroup group)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.clip = clip; s.loop = true; s.playOnAwake = false; s.spatialBlend = 0f;
        s.outputAudioMixerGroup = group;
        return s;
    }
}
