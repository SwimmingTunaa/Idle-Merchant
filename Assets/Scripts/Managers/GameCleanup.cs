using UnityEngine;

/// <summary>
/// Handles cleanup of static resources during scene transitions and app quit.
/// Attach to a persistent GameObject or initialize from GameManager.
/// </summary>
public class GameCleanup : MonoBehaviour
{
    private static GameCleanup _instance;
    
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void OnApplicationQuit()
    {
        CharacterSpriteGenerator.Cleanup();
    }
    
    private void OnDestroy()
    {
        if (_instance == this)
        {
            CharacterSpriteGenerator.Cleanup();
        }
    }
    
    /// <summary>
    /// Call this when changing scenes if you want to clear sprite cache.
    /// Optional - cache persists across scenes by default.
    /// </summary>
    public static void ClearSpriteCache()
    {
        CharacterSpriteGenerator.ClearCache();
    }
}
