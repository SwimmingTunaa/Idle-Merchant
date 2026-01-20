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
    private static readonly Vector3 PREVIEW_POSITION = new Vector3(9999f, 9999f, 0f); // Off-screen

    /// <summary>
    /// Generate shop sprite for a hiring candidate.
    /// Returns cached sprite if visual config already generated.
    /// </summary>
    public static Sprite GenerateSprite(HiringCandidate candidate)
    {
        int configHash = GetVisualConfigHash(candidate);
        
        if (_spriteCache.TryGetValue(configHash, out Sprite cachedSprite))
        {
            return cachedSprite;
        }
        
        EnsureRenderSetup();
        ConfigurePreviewCharacter(candidate);
        
        Sprite generatedSprite = CaptureSprite();
        _spriteCache[configHash] = generatedSprite;
        
        return generatedSprite;
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
    
    private static void ConfigurePreviewCharacter(HiringCandidate candidate)
    {
        if (_previewInstance == null)
        {
            _previewInstance = Object.Instantiate(candidate.entityDef.prefab, PREVIEW_POSITION, Quaternion.identity);
            _previewInstance.name = "CharacterPreview";
            Object.DontDestroyOnLoad(_previewInstance);
            
            // Disable any gameplay components
            DisableGameplayComponents(_previewInstance);
        }
        
        _previewInstance.transform.position = PREVIEW_POSITION;
        
        ApplyVisualConfiguration(_previewInstance, candidate);
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
    }
    
    private static void ApplyVisualConfiguration(GameObject instance, HiringCandidate candidate)
    {
        var spriteLibraries = instance.GetComponentsInChildren<SpriteLibrary>(true);
        var spriteResolvers = instance.GetComponentsInChildren<SpriteResolver>(true);
        CharacterAppearanceManager appearanceManager = instance.GetComponentInChildren<CharacterAppearanceManager>();
        appearanceManager.SetEntityDef(candidate.entityDef);
        appearanceManager.SetAppearanceIndices(candidate.appearanceIndices);
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
    /// Generate hash from visual configuration for cache lookup.
    /// Collision-resistant enough for typical use cases.
    /// </summary>
    private static int GetVisualConfigHash(HiringCandidate candidate)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + candidate.entityDef.GetHashCode();
            hash = hash * 31 + candidate.appearanceIndices.pants;
            hash = hash * 31 + candidate.appearanceIndices.shirt;
            hash = hash * 31 + candidate.appearanceIndices.hairTop;
            hash = hash * 31 + candidate.appearanceIndices.hairBack;
            hash = hash * 31 + candidate.appearanceIndices.frontWeapon;
            hash = hash * 31 + candidate.appearanceIndices.backWeapon;
            hash = hash * 31 + candidate.appearanceIndices.skinColour.GetHashCode();
            hash = hash * 31 + candidate.appearanceIndices.shirtColour;
            hash = hash * 31 + candidate.appearanceIndices.pantsColour;
            hash = hash * 31 + candidate.appearanceIndices.hairColour;
            return hash;
        }
    }
}
