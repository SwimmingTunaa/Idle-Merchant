using System.Collections;
using UnityEngine;

/// <summary>
/// Click-hit feedback: pixel spark burst on every damaging click, crack-star +
/// debris on the kill blow, mob squash, and a short global hit-stop on kills.
/// Particles are two code-configured ParticleSystems (square quads, no per-particle
/// GameObjects, nothing to pool). Add once to the scene.
/// </summary>
public class ClickHitVFX : MonoBehaviour
{
    public static ClickHitVFX Instance { get; private set; }

    [Header("Feel")]
    [Tooltip("Freeze the game ~45ms on a kill blow. Kills only — per-click hit-stop at 10 cps would freeze half of real time.")]
    [SerializeField] private bool killHitStop = true;
    [SerializeField] private float hitStopSeconds = 0.045f;
    [SerializeField] private bool squashOnHit = true;

    [Header("Colours")]
    [SerializeField] private Color sparkGold = new Color(0.941f, 0.706f, 0.161f);
    [SerializeField] private Color sparkWhite = new Color(0.988f, 0.965f, 0.910f);
    [SerializeField] private Color starRed = new Color(0.847f, 0.392f, 0.235f);
    // ponytail: fixed debris palette; per-mob colours via MobDef when mobs diversify
    [SerializeField] private Color debrisLight = new Color(0.392f, 0.627f, 0.314f);
    [SerializeField] private Color debrisDark = new Color(0.247f, 0.439f, 0.188f);

    private ParticleSystem sparks;   // no gravity: hit sparks + crack star + absorb pops
    private ParticleSystem debris;   // gravity: kill chunks
    private ParticleSystem ring;      // no gravity, own system so a new click can Clear() just the ring
    private ParticleSystem shockRing; // expanding damage-AOE ring, white -> red fade over its own lifetime
    private bool stopping;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        sparks = CreateSystem("Sparks", gravity: 0f);
        debris = CreateSystem("Debris", gravity: 1.6f);
        ring = CreateSystem("VacuumRing", gravity: 0f);

        shockRing = CreateSystem("ShockRing", gravity: 0f);
        var col = shockRing.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(sparkWhite, 0f), new GradientColorKey(starRed, 0.45f), new GradientColorKey(starRed, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private ParticleSystem CreateSystem(string name, float gravity)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = gravity;
        main.maxParticles = 256;
        main.startSize = 0.14f;

        var emission = ps.emission;
        emission.enabled = false;   // Emit() only

        var shape = ps.shape;
        shape.enabled = false;

        // Square pixel quads: default-sprite shader on a solid white texture
        var psr = ps.GetComponent<ParticleSystemRenderer>();
        psr.material = new Material(Shader.Find("Sprites/Default")) { mainTexture = Texture2D.whiteTexture };
        psr.sortingOrder = 10000;   // above EntitySortingManager's ranges (2000-8000), under UI

        ps.Play();
        return ps;
    }

    private void Emit(ParticleSystem ps, Vector3 pos, Vector3 velocity, Color color, float life, float size)
    {
        var p = new ParticleSystem.EmitParams
        {
            position = pos,
            velocity = velocity,
            startColor = color,
            startLifetime = life,
            startSize = size,
        };
        ps.Emit(p, 1);
    }

    /// <summary>Spark burst at the click point + squash on the struck mob. Every damaging click.</summary>
    public void PlayHit(Vector3 hitPos, Transform mob)
    {
        hitPos.z = 0f;   // cursor world pos arrives at raw camera z — pin to the gameplay plane
        SfxManager.Instance?.Play(SfxId.ClickHit);
        for (int i = 0; i < 6; i++)
        {
            float a = Mathf.PI * 2f * i / 6f + Random.Range(-0.25f, 0.25f);
            var v = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Random.Range(1.8f, 2.6f);
            Emit(sparks, hitPos, v, i % 2 == 0 ? sparkGold : sparkWhite, Random.Range(0.16f, 0.24f), 0.22f);
        }

        if (squashOnHit && mob != null)
        {
            var squash = mob.GetComponent<HitSquash>();
            if (squash == null) squash = mob.gameObject.AddComponent<HitSquash>();
            squash.Play();
        }
    }

    /// <summary>Crack star + heavy debris + optional hit-stop. Kill blows only.</summary>
    public void PlayKill(Vector3 mobPos)
    {
        mobPos.z = 0f;
        SfxManager.Instance?.Play(SfxId.Kill);
        // crack star: 8 fast rays, white core aging handled by two waves (white then red)
        for (int i = 0; i < 8; i++)
        {
            float a = Mathf.PI * 2f * i / 8f;
            var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            Emit(sparks, mobPos, dir * 4.2f, sparkWhite, 0.10f, 0.26f);
            Emit(sparks, mobPos, dir * 2.6f, starRed, 0.16f, 0.22f);
        }
        Emit(sparks, mobPos, Vector3.zero, sparkWhite, 0.07f, 0.7f);   // centre flash

        // big debris: mob-coloured chunks with gravity
        for (int i = 0; i < 7; i++)
        {
            var v = new Vector3(Random.Range(-2.2f, 2.2f), Random.Range(2.2f, 4.2f), 0f);
            Emit(debris, mobPos, v, i % 2 == 0 ? debrisLight : debrisDark, Random.Range(0.55f, 0.85f), Random.Range(0.22f, 0.38f));
        }

        if (killHitStop) StartCoroutine(HitStop());
    }

    /// <summary>White pickup-radius ring collapsing to the click point. One ring at a time:
    /// a new click clears any ring mid-collapse. Particles converge and die at ~25% radius
    /// so they never clump into a blob at the centre.</summary>
    public void PlayVacuumRing(Vector3 center, float radius)
    {
        center.z = 0f;   // cursor world pos arrives at raw camera z — pin to the gameplay plane
        ring.Clear();
        SfxManager.Instance?.Play(SfxId.Vacuum);

        const float duration = 0.18f;
        int count = Mathf.Clamp(Mathf.RoundToInt(radius * 40f), 20, 64);
        float travel = radius * 0.75f;
        for (int i = 0; i < count; i++)
        {
            float a = Mathf.PI * 2f * i / count;
            var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            Emit(ring, center + dir * radius, -dir * (travel / duration), sparkWhite, duration, 0.18f);
        }
    }

    /// <summary>Expanding ring matching the damage-AOE shockwave: grows from the click point
    /// to the full radius over the shockwave's own duration, white fading to red. One ring at
    /// a time — a new shockwave replaces any ring still expanding.</summary>
    public void PlayShockRing(Vector3 center, float radius, float duration)
    {
        center.z = 0f;
        shockRing.Clear();

        int count = Mathf.Clamp(Mathf.RoundToInt(radius * 40f), 24, 72);
        const float startR = 0.15f;
        float speed = (radius - startR) / Mathf.Max(duration, 0.05f);
        for (int i = 0; i < count; i++)
        {
            float a = Mathf.PI * 2f * i / count;
            var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            Emit(shockRing, center + dir * startR, dir * speed, sparkWhite, duration * 1.15f, 0.2f);
        }
    }

    /// <summary>Tiny 4-spark pop where a vacuumed loot piece reaches the cursor.</summary>
    public void PlayAbsorb(Vector3 pos)
    {
        pos.z = 0f;
        for (int i = 0; i < 4; i++)
        {
            float a = Mathf.PI * 0.5f * i + Mathf.PI * 0.25f;
            var v = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 1.4f;
            Emit(sparks, pos, v, sparkWhite, 0.13f, 0.18f);
        }
    }

    private IEnumerator HitStop()
    {
        // Respect pause (timescale 0) and the 1/2/3 speed keys: restore whatever was set.
        if (stopping || Time.timeScale <= 0f) yield break;
        stopping = true;
        float prev = Time.timeScale;
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(hitStopSeconds);
        // Only restore if nothing else (pause menu) grabbed the timescale meanwhile
        if (Time.timeScale == 0f) Time.timeScale = prev;
        stopping = false;
    }
}

/// <summary>
/// Tiny squash-and-stretch tween on an entity's sprite child. Added on demand by
/// ClickHitVFX; restores scale on disable so pooled mobs never respawn squashed.
/// </summary>
public class HitSquash : MonoBehaviour
{
    private Transform target;
    private Vector3 baseScale;
    private float t = 1f;

    public void Play()
    {
        if (target == null)
        {
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr == null) return;
            target = sr.transform;
            baseScale = target.localScale;
        }
        t = 0f;
    }

    void LateUpdate()
    {
        if (target == null || t >= 1f) return;
        t = Mathf.Min(1f, t + Time.deltaTime / 0.09f);
        float k = 1f - t;
        target.localScale = new Vector3(baseScale.x * (1f + 0.15f * k), baseScale.y * (1f - 0.22f * k), baseScale.z);
    }

    void OnDisable()
    {
        if (target != null) target.localScale = baseScale;
        t = 1f;
    }
}
