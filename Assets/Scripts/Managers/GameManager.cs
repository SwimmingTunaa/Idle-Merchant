using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central game state manager.
/// Reserved for future game-wide state and flow control.
/// 
/// Gold is managed by Inventory.cs - use Inventory.Instance for all gold operations.
/// </summary>
public class GameManager : PersistentSingleton<GameManager>
{
    // Time management
    private float previousTimeScale = 1f;
    private bool IsPaused {get; set;}

    private void OnDestroy()
    {
        CharacterSpriteGenerator.Cleanup();
    }

    public void PauseGame()
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        IsPaused = true;
    }

    public void UnpauseGame()
    {
        if (IsPaused)
        {
            Time.timeScale = previousTimeScale;
            IsPaused = false;
        }
    }

    public void LoadScene(string sceneName, bool unpauseFirst = true)
    {
         // Unpause before scene transition
        if(unpauseFirst)
            UnpauseGame();

        // Check if scene exists before loading
        if (SceneManager.GetSceneByName(sceneName).IsValid())
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogWarning($"'{sceneName}' not found. Create scene or update sceneName field.");
        }
    }

    public static void ClearSpriteCache()
    {
        CharacterSpriteGenerator.ClearCache();
    }

     private void OnApplicationQuit()
    {
        CharacterSpriteGenerator.Cleanup();
    }

    // Future: Game state, pause, save/load, scene management, etc.
}