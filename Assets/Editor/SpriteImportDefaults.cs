using UnityEditor;
using UnityEngine;

/// <summary>
/// Pixel-art import defaults for anything new under Assets/Sprites: point filter, uncompressed, no mipmaps, Sprite type.
/// Only fires on a texture's first import (no .meta yet) so deliberate per-file settings (bilinear inscriptions,
/// per-folder PPU) are never clobbered on reimport. PPU is left alone — it varies by folder (8 world / 100 UI / ...).
/// </summary>
public class SpriteImportDefaults : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetImporter.importSettingsMissing) return;
        if (!assetPath.StartsWith("Assets/Sprites/")) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        Debug.Log($"[SpriteImportDefaults] Applied pixel-art defaults to {assetPath}");
    }
}
