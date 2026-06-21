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

    // Baked black outline width in render-texture texels. Tuned to read like the world outline
    // at the small slot-icon size; the detail portrait shows the same sprite larger so its line
    // appears proportionally bolder (see RosterPanelController — same cached sprite, scaled).
    private const int ICON_OUTLINE_TEXELS = 6;
    private static readonly Color32 IconOutlineColor = new Color32(0, 0, 0, 255);
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
        _renderCamera.orthographicSize = 1f;
        _renderCamera.clearFlags = CameraClearFlags.SolidColor;
        _renderCamera.backgroundColor = new Color(0, 0, 0, 0); // Transparent
        _renderCamera.cullingMask = 1 << LayerMask.NameToLayer("Default"); // Render only default layer
        _renderCamera.enabled = false; // Manual rendering only
        
        Object.DontDestroyOnLoad(cameraObj);
    }
    
    private static void ConfigurePreviewCharacter(EntityDef def, CharacterAppearanceIndices indices)
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

        ApplyVisualConfiguration(_previewInstance, def, indices);
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
    
    private static void ApplyVisualConfiguration(GameObject instance, EntityDef def, CharacterAppearanceIndices indices)
    {
        var spriteLibraries = instance.GetComponentsInChildren<SpriteLibrary>(true);
        var spriteResolvers = instance.GetComponentsInChildren<SpriteResolver>(true);
        CharacterAppearanceManager appearanceManager = instance.GetComponentInChildren<CharacterAppearanceManager>();
        appearanceManager.SetEntityDef(def);
        appearanceManager.SetAppearanceIndices(indices);
        appearanceManager.ApplyAppearance();
        
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

    private static Sprite CaptureSprite()
    {
        // Render character to texture
        _renderCamera.Render();
        
        // Convert RenderTexture to Texture2D
        RenderTexture.active = _renderTexture;
        Texture2D texture = new Texture2D(RENDER_SIZE, RENDER_SIZE, TextureFormat.ARGB32, false);
        texture.ReadPixels(new Rect(0, 0, RENDER_SIZE, RENDER_SIZE), 0, 0);
        texture.Apply();
        RenderTexture.active = null;

        // Bake a black silhouette outline into the icon so it matches the world black outline.
        AddSilhouetteOutline(texture, ICON_OUTLINE_TEXELS, IconOutlineColor);

        // Create sprite from texture
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, RENDER_SIZE, RENDER_SIZE),
            new Vector2(0.5f, 0.5f),
            SPRITE_PIXELS_PER_UNIT
        );
        sprite.name = "GeneratedShopSprite";
        
        return sprite;
    }
    
    /// <summary>
    /// Dilates the silhouette's alpha outward by <paramref name="radius"/> texels and fills the
    /// new ring with <paramref name="color"/>, producing an outline that sits OUTSIDE the
    /// character (matching the world outline pass). Runs once per unique config — the result is
    /// cached with the sprite.
    /// </summary>
    private static void AddSilhouetteOutline(Texture2D tex, int radius, Color32 color)
    {
        if (radius <= 0) return;

        int w = tex.width, h = tex.height;
        Color32[] src = tex.GetPixels32();
        Color32[] dst = src.Clone() as Color32[];
        const byte alphaThreshold = 16;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (src[i].a > alphaThreshold) continue;   // inside the silhouette — keep the character pixel

                // Square (Chebyshev) dilation: outline if any opaque texel sits within the
                // [-radius, radius] box. A box, not a circle, gives crisp right-angle corners.
                bool nearSilhouette = false;
                for (int dy = -radius; dy <= radius && !nearSilhouette; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= h) continue;
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int nx = x + dx;
                        if (nx < 0 || nx >= w) continue;
                        if (src[ny * w + nx].a > alphaThreshold) { nearSilhouette = true; break; }
                    }
                }
                if (nearSilhouette) dst[i] = color;
            }
        }

        tex.SetPixels32(dst);
        tex.Apply();
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
