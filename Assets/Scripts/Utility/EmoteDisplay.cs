using System;
using UnityEngine;

/// <summary>
/// Generic sprite emote popup above any character.
/// Define emote types in the EmoteType enum and assign their sprites in the inspector.
/// Drive it from any code via Show(EmoteType) / Hide() — no entity coupling.
/// Pool-safe: resets in OnDisable.
/// </summary>
public class EmoteDisplay : MonoBehaviour
{
    public enum EmoteType
    {
        Frustrated,
        Impatient,
        // Add more types here as needed
    }

    [Serializable]
    public struct EmoteEntry
    {
        public EmoteType type;
        public Sprite    sprite;
    }

    [Header("Refs")]
    [SerializeField] private SpriteRenderer iconRenderer;  // child ref

    [Header("Emotes")]
    [SerializeField] private EmoteEntry[] emotes;

    [Header("Positioning")]
    public Vector3 worldOffset = new Vector3(0f, 1.8f, 0f);

    void OnDisable() => Hide();

    public void Show(EmoteType type)
    {
        if (iconRenderer == null) return;

        Sprite sprite = null;
        foreach (var entry in emotes)
        {
            if (entry.type == type) { sprite = entry.sprite; break; }
        }

        if (sprite == null) { Hide(); return; }

        iconRenderer.sprite  = sprite;
        iconRenderer.enabled = true;
    }

    public void Hide()
    {
        if (iconRenderer != null)
            iconRenderer.enabled = false;
    }
}
