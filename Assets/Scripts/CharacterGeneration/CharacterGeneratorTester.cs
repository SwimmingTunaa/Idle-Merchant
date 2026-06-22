using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.U2D.Animation;

/// <summary>
/// Dev sandbox for <see cref="CharacterSpriteGenerator"/> and the modular character animations.
/// Shows two things side by side:
///   • a LIVE animated character (left) playing the selected clip, driven by direct clip
///     sampling so any clip can be previewed regardless of the controller's transitions, and
///   • a baked SPRITE SHEET (right) of every frame of that clip, each baked with the outline
///     exactly as the generator produces icons.
///
/// Options: pick the EntityDef, toggle clothes vs base-body-only, and choose the animation.
///
/// Controls (in Play Mode):
///   SPACE  re-roll appearance       C  toggle clothes / base body
///   [  ]   previous / next animation
/// Or use the inspector fields + the "Rebuild" context-menu button.
/// </summary>
public class CharacterGeneratorTester : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Modular EntityDef to preview (e.g. an AdventurerDef like 'Lvl 1 Novice').")]
    [SerializeField] private EntityDef entityDef;

    [Header("Options")]
    [Tooltip("Show clothing/hair/weapons. Off = base body only.")]
    [SerializeField] private bool withClothes = true;
    [Tooltip("Which animation clip (index into the character's controller clips).")]
    [SerializeField] private int animationIndex = 0;
    [Tooltip("Max frames to bake into the sheet (caps very long clips).")]
    [SerializeField, Min(1)] private int maxFrames = 24;

    [Header("Sprite Sheet Layout")]
    [SerializeField, Min(1)] private int sheetColumns = 8;
    [SerializeField] private float sheetSpacing = 1.3f;
    [SerializeField] private Vector2 sheetOrigin = new Vector2(-4.5f, 3f);

    [Header("Live Preview")]
    [SerializeField] private Vector2 livePosition = new Vector2(-8f, 0f);
    [Tooltip("Multiplier on the prefab's in-game scale. 1 = exactly as it appears in game.")]
    [SerializeField] private float liveScale = 1f;
    [SerializeField] private float playbackSpeed = 1f;

    // ── runtime state ─────────────────────────────────────────────────────────
    private GameObject liveInstance;
    private GameObject liveAnimRoot;   // the body Animator's GameObject — clip paths are relative to it
    private SpriteResolver[] liveResolvers;
    private AnimationClip[] clips = System.Array.Empty<AnimationClip>();
    private AnimationClip currentClip;
    private CharacterAppearanceIndices indices;
    private bool hasIndices;

    // Distinct-frame playback: step through the clip's real frames so every frame is visible.
    private float[] frameTimes = System.Array.Empty<float>();
    private int frameIndex;
    private float frameTimer;

    private readonly List<GameObject> sheetCells = new();
    private readonly List<Sprite> stripSprites = new();
    private Material unlitSpriteMaterial;

    void Start() => Rebuild(rerollAppearance: true);

    void Update()
    {
        HandleInput();
        DriveLivePlayback();
    }

    private void HandleInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.spaceKey.wasPressedThisFrame) Rebuild(rerollAppearance: true);
        else if (kb.cKey.wasPressedThisFrame) { withClothes = !withClothes; Rebuild(rerollAppearance: false); }
        else if (kb.rightBracketKey.wasPressedThisFrame) CycleAnimation(+1);
        else if (kb.leftBracketKey.wasPressedThisFrame) CycleAnimation(-1);
    }

    private void DriveLivePlayback()
    {
        if (liveInstance == null || currentClip == null || frameTimes.Length == 0) return;

        // Hold each distinct frame for its share of the clip duration, then step — so every
        // frame is actually shown (not skipped past by continuous 60fps sampling).
        float frameDuration = (currentClip.length / frameTimes.Length) / Mathf.Max(0.01f, playbackSpeed);
        frameTimer += Time.deltaTime;
        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            frameIndex = (frameIndex + 1) % frameTimes.Length;
        }

        currentClip.SampleAnimation(liveAnimRoot != null ? liveAnimRoot : liveInstance, frameTimes[frameIndex]);

        // SampleAnimation sets the SpriteResolver keys; push them to the renderers.
        if (liveResolvers != null)
            foreach (var r in liveResolvers)
                if (r != null) r.ResolveSpriteToSpriteRenderer();
    }

    private void RefreshFrameTimes()
    {
        frameTimes = currentClip != null
            ? CharacterSpriteGenerator.GetFrameSampleTimes(currentClip, maxFrames)
            : System.Array.Empty<float>();
        frameIndex = 0;
        frameTimer = 0f;
    }

    // ── build ──────────────────────────────────────────────────────────────────

    [ContextMenu("Rebuild")]
    public void RebuildContextMenu() => Rebuild(rerollAppearance: true);

    private void Rebuild(bool rerollAppearance)
    {
        if (entityDef == null) { Debug.LogError("[CharacterGeneratorTester] No EntityDef assigned."); return; }
        if (!entityDef.useModularCharacter) { Debug.LogError($"[CharacterGeneratorTester] '{entityDef.name}' is not a modular character."); return; }

        if (rerollAppearance || !hasIndices)
        {
            indices = RandomIndices(entityDef);
            hasIndices = true;
        }

        BuildLive();
        BuildSheet();
    }

    private void BuildLive()
    {
        StopAllCoroutines();   // cancel any pending re-apply from a previous build
        if (liveInstance != null) Destroy(liveInstance);

        liveInstance = Instantiate(entityDef.prefab, transform);
        liveInstance.name = "LivePreview";
        liveInstance.transform.localPosition = livePosition;
        liveInstance.transform.localScale *= liveScale;   // keep the prefab's in-game scale, times the multiplier

        DisableGameplay(liveInstance);

        var appearance = liveInstance.GetComponentInChildren<CharacterAppearanceManager>();
        ApplyAppearanceTo(appearance);

        liveResolvers = liveInstance.GetComponentsInChildren<SpriteResolver>(true);
        foreach (var r in liveResolvers) r.ResolveSpriteToSpriteRenderer();

        // CharacterAppearanceManager.Start() may re-randomise the whole appearance
        // (autoChangeOnStart) at end of this frame, clobbering what we just set — so re-apply
        // our chosen appearance once Start has run.
        if (appearance != null) StartCoroutine(ReapplyAppearanceAfterStart(appearance));

        // The body Animator lives on a child ("BaseCharacterRoot"); clip paths are relative to it.
        var animator = CharacterSpriteGenerator.FindBodyAnimator(liveInstance);
        liveAnimRoot = animator != null ? animator.gameObject : liveInstance;
        clips = animator != null && animator.runtimeAnimatorController != null
            ? animator.runtimeAnimatorController.animationClips
            : System.Array.Empty<AnimationClip>();

        // Disable every animator so our manual SampleAnimation fully controls the pose.
        foreach (var a in liveInstance.GetComponentsInChildren<Animator>(true)) a.enabled = false;

        if (clips.Length > 0)
        {
            animationIndex = Mathf.Clamp(animationIndex, 0, clips.Length - 1);
            currentClip = clips[animationIndex];
        }
        else currentClip = null;

        RefreshFrameTimes();
    }

    private void ApplyAppearanceTo(CharacterAppearanceManager appearance)
    {
        if (appearance == null) return;
        appearance.SetEntityDef(entityDef);
        appearance.SetAppearanceIndices(indices);
        appearance.ApplyAppearance();
        appearance.SetClothingVisible(withClothes);
    }

    private System.Collections.IEnumerator ReapplyAppearanceAfterStart(CharacterAppearanceManager appearance)
    {
        yield return null;   // let CharacterAppearanceManager.Start()'s auto-randomise run first
        if (appearance == null) yield break;   // instance was rebuilt/destroyed
        ApplyAppearanceTo(appearance);
        if (liveResolvers != null)
            foreach (var r in liveResolvers)
                if (r != null) r.ResolveSpriteToSpriteRenderer();
    }

    private void BuildSheet()
    {
        ClearSheet();
        if (currentClip == null) return;

        unlitSpriteMaterial ??= new Material(Shader.Find("Sprites/Default"));

        var frames = CharacterSpriteGenerator.GenerateAnimationStrip(entityDef, indices, currentClip, maxFrames, withClothes);

        for (int i = 0; i < frames.Length; i++)
        {
            int cx = i % sheetColumns;
            int cy = i / sheetColumns;

            var go = new GameObject($"Frame_{i}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(sheetOrigin.x + cx * sheetSpacing, sheetOrigin.y - cy * sheetSpacing, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[i];
            sr.sharedMaterial = unlitSpriteMaterial;

            sheetCells.Add(go);
            stripSprites.Add(frames[i]);
        }

        Debug.Log($"[CharacterGeneratorTester] '{entityDef.name}' | anim '{currentClip.name}' | {frames.Length} frames | clothes={withClothes}");
    }

    private void ClearSheet()
    {
        foreach (var go in sheetCells) if (go != null) Destroy(go);
        sheetCells.Clear();

        // Baked strip sprites aren't cached by the generator — destroy their textures to avoid leaks.
        foreach (var sprite in stripSprites)
            if (sprite != null) { if (sprite.texture != null) Destroy(sprite.texture); Destroy(sprite); }
        stripSprites.Clear();
    }

    private void CycleAnimation(int dir)
    {
        if (clips.Length == 0) return;
        animationIndex = ((animationIndex + dir) % clips.Length + clips.Length) % clips.Length;
        currentClip = clips[animationIndex];
        RefreshFrameTimes();
        BuildSheet();
    }

    // Mirror CharacterSpriteGenerator's preview cleanup, but KEEP PermanentOutline so the live
    // character still gets its world outline.
    private static void DisableGameplay(GameObject obj)
    {
        if (obj.TryGetComponent(out EntityBase entity)) entity.enabled = false;
        if (obj.TryGetComponent(out Collider2D col)) col.enabled = false;
        if (obj.TryGetComponent(out Rigidbody2D rb)) rb.simulated = false;
    }

    private static CharacterAppearanceIndices RandomIndices(EntityDef def)
    {
        return new CharacterAppearanceIndices
        {
            pants       = RandLibraryIndex(def.pantsSpriteLibraries),
            shirt       = RandLibraryIndex(def.shirtSpriteLibraries),
            hairTop     = RandLibraryIndex(def.hairTopSpriteLibraries),
            hairBack    = RandLibraryIndex(def.hairBackSpriteLibraries),
            frontWeapon = RandLibraryIndex(def.frontWeaponSpriteLibraries),
            backWeapon  = RandLibraryIndex(def.backWeaponSpriteLibraries),

            skinColour  = def.skinColourPalette?.GetRandomGradientValue() ?? 0f,
            shirtColour = def.ShirtColourPalette?.GetRandomPaletteIndex() ?? 0,
            pantsColour = def.PantsColourPalette?.GetRandomPaletteIndex() ?? 0,
            hairColour  = def.HairColourPalette?.GetRandomPaletteIndex() ?? 0,
        };
    }

    private static int RandLibraryIndex(SpriteLibraryAsset[] libraries)
        => (libraries == null || libraries.Length == 0) ? 0 : Random.Range(0, libraries.Length);

    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
        string clip = currentClip != null ? currentClip.name : "(none)";
        GUI.Label(new Rect(10, 10, 900, 24),
            $"Anim [{animationIndex + 1}/{Mathf.Max(clips.Length, 1)}]: {clip}    Clothes: {(withClothes ? "ON" : "base only")}", style);
        GUI.Label(new Rect(10, 32, 900, 24),
            "SPACE re-roll appearance   C toggle clothes   [ ] change animation", style);
    }
}
