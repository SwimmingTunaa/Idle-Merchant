using UnityEngine;
using System.Collections.Generic;
using UnityEngine.U2D.Animation;

/// <summary>
/// Generates shop sprites from modular character configurations.
/// Uses off-screen rendering with object pooling and sprite caching.
/// </summary>
public static class CharacterSpriteGenerator
{
    private static Camera _renderCamera;
    private static GameObject _previewInstance;
    private static RenderTexture _renderTexture;
    private static Dictionary<int, Sprite> _spriteCache = new Dictionary<int, Sprite>();
    
    private const int RENDER_SIZE = 256;
    private const int SPRITE_PIXELS_PER_UNIT = 100;

    // Baked outline settings come from a Resources-loaded ScriptableObject so they're tunable in
    // the inspector (Assets/Resources/CharacterIconSettings). Falls back to sane defaults if the
    // asset is missing.
    private const int DEFAULT_ICON_OUTLINE_TEXELS = 2;   // art pixels (true-scale capture)
    private static CharacterIconSettings _iconSettings;
    private static CharacterIconSettings IconSettings =>
        _iconSettings != null ? _iconSettings : (_iconSettings = Resources.Load<CharacterIconSettings>("CharacterIconSettings"));

    private static int IconOutlineTexels => IconSettings != null ? IconSettings.outlineTexels : DEFAULT_ICON_OUTLINE_TEXELS;
    private static Color32 IconOutlineColor => IconSettings != null ? (Color32)IconSettings.outlineColor : new Color32(0, 0, 0, 255);
    private static OutlineShape IconOutlineShape => IconSettings != null ? IconSettings.outlineShape : OutlineShape.Cross;
    private static int IconCornerCut => IconSettings != null ? IconSettings.cornerCut : 1;
    private static readonly Vector3 PREVIEW_POSITION = new Vector3(9999f, 9999f, 0f); // Off-screen

    /// <summary>
    /// Generate shop sprite for a hiring candidate.
    /// Returns cached sprite if visual config already generated.
    /// </summary>
    public static Sprite GenerateSprite(HiringCandidate candidate)
    {
        int configHash = GetVisualConfigHash(candidate);
        if (_spriteCache.TryGetValue(configHash, out Sprite cachedSprite))
            return cachedSprite;
        return GenerateSpriteInternal(candidate.entityDef, candidate.appearanceIndices, configHash);
    }

    /// <summary>
    /// Generate sprite for a live entity using its EntityDef and current appearance indices.
    /// Returns cached sprite if visual config already generated.
    /// </summary>
    public static Sprite GenerateSprite(EntityDef def, CharacterAppearanceIndices indices)
    {
        int configHash = GetVisualConfigHash(def, indices);
        if (_spriteCache.TryGetValue(configHash, out Sprite cachedSprite))
            return cachedSprite;
        return GenerateSpriteInternal(def, indices, configHash);
    }
    
    /// <summary>
    /// Clear sprite cache to free memory.
    /// Call when changing scenes or during memory pressure.
    /// </summary>
    public static void ClearCache()
    {
        foreach (var sprite in _spriteCache.Values)
        {
            if (sprite != null)
                Object.Destroy(sprite.texture);
        }
        _spriteCache.Clear();
    }
    
    /// <summary>
    /// Clean up rendering resources.
    /// Call on application quit or scene unload.
    /// </summary>
    public static void Cleanup()
    {
        ClearCache();
        
        if (_renderCamera != null)
            Object.Destroy(_renderCamera.gameObject);
        
        if (_previewInstance != null)
            Object.Destroy(_previewInstance);
        
        if (_renderTexture != null)
            _renderTexture.Release();
        
        _renderCamera = null;
        _previewInstance = null;
        _renderTexture = null;
    }
    
    private static void EnsureRenderSetup()
    {
        if (_renderCamera == null)
        {
            SetupRenderCamera();
        }
        
        if (_renderTexture == null)
        {
            _renderTexture = new RenderTexture(RENDER_SIZE, RENDER_SIZE, 16, RenderTextureFormat.ARGB32);
            _renderTexture.name = "CharacterPreviewRT";
            _renderCamera.targetTexture = _renderTexture;
        }
    }
    
    private static void SetupRenderCamera()
    {
        GameObject cameraObj = new GameObject("CharacterPreviewCamera");
        cameraObj.transform.position = PREVIEW_POSITION + (Vector3.back * 10f) + Vector3.up;
        
        _renderCamera = cameraObj.AddComponent<Camera>();
        _renderCamera.orthographic = true;
        // Capture at the sprite's TRUE scale: pixels-per-world-unit == sprite PPU, so 1 texel = 1
        // art-pixel. Outline widths are then real art-pixels and the icon is pixel-perfect.
        _renderCamera.orthographicSize = RENDER_SIZE / (2f * SPRITE_PIXELS_PER_UNIT);
        _renderCamera.clearFlags = CameraClearFlags.SolidColor;
        _renderCamera.backgroundColor = new Color(0, 0, 0, 0); // Transparent
        _renderCamera.cullingMask = 1 << LayerMask.NameToLayer("Default"); // Render only default layer
        _renderCamera.enabled = false; // Manual rendering only
        
        Object.DontDestroyOnLoad(cameraObj);
    }
    
    private static void ConfigurePreviewCharacter(EntityDef def, CharacterAppearanceIndices indices, bool withClothes = true)
    {
        if (_previewInstance == null)
        {
            _previewInstance = Object.Instantiate(def.prefab, PREVIEW_POSITION, Quaternion.identity);
            _previewInstance.name = "CharacterPreview";
            Object.DontDestroyOnLoad(_previewInstance);

            // Disable any gameplay components
            DisableGameplayComponents(_previewInstance);
        }

        _previewInstance.transform.position = PREVIEW_POSITION;

        ApplyVisualConfiguration(_previewInstance, def, indices, withClothes);
    }
    
    private static void DisableGameplayComponents(GameObject obj)
    {
        // Disable components that shouldn't run during preview
        var entityBase = obj.GetComponent<EntityBase>();
        if (entityBase != null)
            entityBase.enabled = false;

        var collider = obj.GetComponent<Collider2D>();
        if (collider != null)
            collider.enabled = false;

        var rigidbody = obj.GetComponent<Rigidbody2D>();
        if (rigidbody != null)
            rigidbody.simulated = false;

        // Stop the world black-outline feature from stroking the baked icon — we bake our own
        // outline at the right resolution below. Disable before Start so it never flags the bit.
        var permanentOutline = obj.GetComponent<PermanentOutline>();
        if (permanentOutline != null)
        {
            permanentOutline.enabled = false;
            permanentOutline.Clear();
        }
    }
    
    private static void ApplyVisualConfiguration(GameObject instance, EntityDef def, CharacterAppearanceIndices indices, bool withClothes = true)
    {
        var spriteLibraries = instance.GetComponentsInChildren<SpriteLibrary>(true);
        var spriteResolvers = instance.GetComponentsInChildren<SpriteResolver>(true);
        CharacterAppearanceManager appearanceManager = instance.GetComponentInChildren<CharacterAppearanceManager>();
        appearanceManager.SetEntityDef(def);
        appearanceManager.SetAppearanceIndices(indices);
        appearanceManager.ApplyAppearance();
        appearanceManager.SetClothingVisible(withClothes);   // false = base body only

        // Refresh sprite resolvers
        foreach (var resolver in spriteResolvers)
        {
            resolver.ResolveSpriteToSpriteRenderer();
        }
                
        // Force idle animation frame
        var animator = instance.GetComponent<Animator>();
        if (animator != null)
        {
            animator.Play("Idle", 0, 0f);
            animator.Update(0f);
        }
    }
    
    private static Sprite GenerateSpriteInternal(EntityDef def, CharacterAppearanceIndices indices, int configHash)
    {
        EnsureRenderSetup();
        ConfigurePreviewCharacter(def, indices);
        Sprite generatedSprite = CaptureSprite();
        _spriteCache[configHash] = generatedSprite;
        return generatedSprite;
    }

    /// <summary>
    /// Bakes every frame of an animation clip into a sprite strip (each frame outlined, like the
    /// single-icon bake). NOT cached — the caller owns the returned sprites and must destroy their
    /// textures when done. Set <paramref name="withClothes"/> false to preview the base body only.
    /// </summary>
    public static Sprite[] GenerateAnimationStrip(EntityDef def, CharacterAppearanceIndices indices, AnimationClip clip, int maxFrames, bool withClothes = true)
    {
        if (def == null || clip == null) return System.Array.Empty<Sprite>();

        EnsureRenderSetup();
        ConfigurePreviewCharacter(def, indices, withClothes);

        // Clip paths are relative to the body Animator's GameObject (a child, not the root).
        var bodyAnimator = FindBodyAnimator(_previewInstance);
        GameObject sampleTarget = bodyAnimator != null ? bodyAnimator.gameObject : _previewInstance;
        if (bodyAnimator != null) bodyAnimator.enabled = false;   // we pose it via SampleAnimation

        // One capture per DISTINCT animation frame (the artist's keyframes), not per 60fps tick —
        // so every frame shows up exactly once with no duplicates or truncation.
        float[] times = GetFrameSampleTimes(clip, maxFrames);
        var resolvers = _previewInstance.GetComponentsInChildren<SpriteResolver>(true);

        var frames = new Sprite[times.Length];
        for (int f = 0; f < times.Length; f++)
        {
            clip.SampleAnimation(sampleTarget, times[f]);

            // SampleAnimation sets the SpriteResolver keys; push them to the renderers before capture.
            foreach (var resolver in resolvers)
                resolver.ResolveSpriteToSpriteRenderer();

            frames[f] = CaptureSprite();
        }
        return frames;
    }

    /// <summary>The modular character's body Animator (a child, e.g. "BaseCharacterRoot") — the
    /// one with the most clips, so it wins over small effect animators like the soul sprite.</summary>
    public static Animator FindBodyAnimator(GameObject instance)
    {
        Animator best = null;
        int bestClips = -1;
        foreach (var a in instance.GetComponentsInChildren<Animator>(true))
        {
            int n = a.runtimeAnimatorController != null ? a.runtimeAnimatorController.animationClips.Length : 0;
            if (n > bestClips) { bestClips = n; best = a; }
        }
        return best;
    }

    /// <summary>
    /// Times to sample for "every frame" of a clip. Sprite-swap clips run at a high frame rate
    /// but only change sprite on a handful of keyframes, so we sample the MIDDLE of each held
    /// keyframe interval — one time per distinct frame, in order, capped at <paramref name="maxFrames"/>.
    /// In a build (no AnimationUtility), falls back to uniform sampling at the clip's frame rate.
    /// </summary>
    public static float[] GetFrameSampleTimes(AnimationClip clip, int maxFrames)
    {
        if (clip == null) return System.Array.Empty<float>();
        maxFrames = Mathf.Max(1, maxFrames);

#if UNITY_EDITOR
        var keyTimes = new System.Collections.Generic.SortedSet<float>();
        foreach (var binding in UnityEditor.AnimationUtility.GetCurveBindings(clip))
        {
            var curve = UnityEditor.AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null) continue;
            foreach (var key in curve.keys) keyTimes.Add(key.time);
        }

        if (keyTimes.Count > 0)
        {
            var starts = new System.Collections.Generic.List<float>(keyTimes);
            var mids = new System.Collections.Generic.List<float>(starts.Count);
            for (int i = 0; i < starts.Count; i++)
            {
                float end = (i + 1 < starts.Count) ? starts[i + 1] : clip.length;
                mids.Add((starts[i] + end) * 0.5f);   // middle of each held frame → robust against step boundaries
            }

            if (mids.Count <= maxFrames) return mids.ToArray();

            var capped = new float[maxFrames];   // too many frames → evenly subsample
            for (int i = 0; i < maxFrames; i++)
                capped[i] = mids[Mathf.RoundToInt(i * (mids.Count - 1) / (float)(maxFrames - 1))];
            return capped;
        }
#endif

        int n = Mathf.Clamp(Mathf.RoundToInt(clip.length * clip.frameRate), 1, maxFrames);
        var times = new float[n];
        for (int i = 0; i < n; i++) times[i] = (i / (float)n) * clip.length;
        return times;
    }

    private static Sprite CaptureSprite()
    {
        // Render character to texture
        _renderCamera.Render();
        
        // Convert RenderTexture to Texture2D
        RenderTexture.active = _renderTexture;
        Texture2D full = new Texture2D(RENDER_SIZE, RENDER_SIZE, TextureFormat.ARGB32, false);
        full.ReadPixels(new Rect(0, 0, RENDER_SIZE, RENDER_SIZE), 0, 0);
        full.Apply();
        RenderTexture.active = null;

        // Bake a black silhouette outline into the icon so it matches the world black outline.
        AddSilhouetteOutline(full, IconOutlineTexels, IconOutlineColor, IconOutlineShape, IconCornerCut);

        // Crop to the character (incl. its outline) so the icon is a tight, native-resolution
        // sprite — captured at true scale, it'd otherwise sit small in a big transparent canvas.
        Texture2D texture = CropToContent(full, padding: 1);
        if (texture != full) Object.Destroy(full);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            SPRITE_PIXELS_PER_UNIT
        );
        sprite.name = "GeneratedShopSprite";

        return sprite;
    }

    /// <summary>Returns a new texture cropped to the non-transparent content (plus `padding` px of
    /// transparent border), point-filtered for crisp pixel-art upscaling. Returns the input
    /// unchanged if it's fully transparent.</summary>
    private static Texture2D CropToContent(Texture2D tex, int padding)
    {
        int w = tex.width, h = tex.height;
        Color32[] px = tex.GetPixels32();

        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                if (px[row + x].a == 0) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }
        if (maxX < 0) return tex;   // nothing drawn

        minX = Mathf.Max(0, minX - padding);
        minY = Mathf.Max(0, minY - padding);
        maxX = Mathf.Min(w - 1, maxX + padding);
        maxY = Mathf.Min(h - 1, maxY + padding);
        int cw = maxX - minX + 1, ch = maxY - minY + 1;

        var outTex = new Texture2D(cw, ch, TextureFormat.ARGB32, false) { filterMode = FilterMode.Point };
        outTex.SetPixels(tex.GetPixels(minX, minY, cw, ch));
        outTex.Apply();
        return outTex;
    }
    
    /// <summary>
    /// Dilates the silhouette's alpha outward by <paramref name="radius"/> texels and fills the
    /// new ring with <paramref name="color"/>, producing an outline that sits OUTSIDE the
    /// character (matching the world outline pass). Runs once per unique config — the result is
    /// cached with the sprite.
    /// </summary>
    private static void AddSilhouetteOutline(Texture2D tex, int radius, Color32 color, OutlineShape shape, int cornerCut)
    {
        if (radius <= 0) return;

        int w = tex.width, h = tex.height;
        Color32[] src = tex.GetPixels32();
        const byte alphaThreshold = 16;
        bool[] solid = new bool[w * h];
        for (int i = 0; i < src.Length; i++) solid[i] = src[i].a > alphaThreshold;

        // Distance transforms (two-pass chamfer, O(n) at any width — a full box kernel was
        // O(radius^2) and crashed the editor at thick widths).
        //   Square : Chebyshev (diag 1) <= r            → full 90° corners
        //   Circle : Euclidean (diag 1.414) <= r        → rounded corners
        //   Cross  : Chebyshev <= r AND Manhattan <= 2r-cornerCut → 90° corners with the tips
        //            notched by `cornerCut` px (Aseprite "+"/non-diagonal look)
        float[] primary = ChamferDistance(solid, w, h, shape == OutlineShape.Circle ? 1.41421356f : 1f);
        float[] manhattan = shape == OutlineShape.Cross ? ChamferDistance(solid, w, h, 2f) : null;
        float crossLimit = 2f * radius - Mathf.Max(0, cornerCut);

        Color32[] dst = src.Clone() as Color32[];
        for (int i = 0; i < src.Length; i++)
        {
            if (solid[i]) continue;                       // keep the character pixel
            if (primary[i] <= 0f || primary[i] > radius) continue;
            if (shape == OutlineShape.Cross && manhattan[i] > crossLimit) continue;   // notch the corner tips
            dst[i] = color;
        }

        tex.SetPixels32(dst);
        tex.Apply();
    }

    /// <summary>Two-pass chamfer distance transform: distance (in `diag`-weighted steps) from each
    /// empty texel to the nearest solid texel. diag 1 = Chebyshev, 1.414 = Euclidean, 2 = Manhattan.</summary>
    private static float[] ChamferDistance(bool[] solid, int w, int h, float diag)
    {
        const float INF = 1e9f, ortho = 1f;
        float[] d = new float[w * h];
        for (int i = 0; i < solid.Length; i++) d[i] = solid[i] ? 0f : INF;

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                float v = d[i];
                if (x > 0)              v = Mathf.Min(v, d[i - 1] + ortho);
                if (y > 0)              v = Mathf.Min(v, d[i - w] + ortho);
                if (x > 0 && y > 0)     v = Mathf.Min(v, d[i - w - 1] + diag);
                if (x < w - 1 && y > 0) v = Mathf.Min(v, d[i - w + 1] + diag);
                d[i] = v;
            }

        for (int y = h - 1; y >= 0; y--)
            for (int x = w - 1; x >= 0; x--)
            {
                int i = y * w + x;
                float v = d[i];
                if (x < w - 1)              v = Mathf.Min(v, d[i + 1] + ortho);
                if (y < h - 1)              v = Mathf.Min(v, d[i + w] + ortho);
                if (x < w - 1 && y < h - 1) v = Mathf.Min(v, d[i + w + 1] + diag);
                if (x > 0 && y < h - 1)     v = Mathf.Min(v, d[i + w - 1] + diag);
                d[i] = v;
            }

        return d;
    }

    /// <summary>
    /// Generate hash from visual configuration for cache lookup.
    /// Collision-resistant enough for typical use cases.
    /// </summary>
    private static int GetVisualConfigHash(HiringCandidate candidate)
    {
        return GetVisualConfigHash(candidate.entityDef, candidate.appearanceIndices);
    }

    private static int GetVisualConfigHash(EntityDef def, CharacterAppearanceIndices indices)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + def.GetHashCode();
            hash = hash * 31 + IconOutlineTexels;          // re-bake when the outline width changes
            hash = hash * 31 + (int)IconOutlineShape;      // ...or the corner shape
            hash = hash * 31 + IconCornerCut;              // ...or the corner-cut amount
            hash = hash * 31 + indices.pants;
            hash = hash * 31 + indices.shirt;
            hash = hash * 31 + indices.hairTop;
            hash = hash * 31 + indices.hairBack;
            hash = hash * 31 + indices.frontWeapon;
            hash = hash * 31 + indices.backWeapon;
            hash = hash * 31 + indices.skinColour.GetHashCode();
            hash = hash * 31 + indices.shirtColour;
            hash = hash * 31 + indices.pantsColour;
            hash = hash * 31 + indices.hairColour;
            return hash;
        }
    }
}
