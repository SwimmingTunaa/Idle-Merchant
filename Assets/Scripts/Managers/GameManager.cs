using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Central game state manager.
/// Reserved for future game-wide state and flow control.
/// 
/// Gold is managed by Inventory.cs - use Inventory.Instance for all gold operations.
/// </summary>
public class GameManager : PersistentSingleton<GameManager>
{

    [Header("Scene Management")]
    public string mainMenuSceneName = "MainMenu";
    // Time management
    private float previousTimeScale = 1f;
    private bool IsPaused {get; set;}
    public float CurrentTimeScale => Time.timeScale;

    private void OnDestroy()
    {
        CharacterSpriteGenerator.Cleanup();
    }

    // Number keys 1/2/3 set game speed (1x/2x/3x). Ignored while paused (SetTimeScale
    // stores the speed and PauseGame/UnpauseGame restore it).
    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.digit1Key.wasPressedThisFrame) SetTimeScale(1f);
        else if (kb.digit2Key.wasPressedThisFrame) SetTimeScale(2f);
        else if (kb.digit3Key.wasPressedThisFrame) SetTimeScale(3f);
    }


    public void SetTimeScale(float scale)
    {
        scale = Mathf.Clamp(scale, 0.1f, 10f);

        if (!IsPaused)
        {
            Time.timeScale = scale;
            previousTimeScale = scale;
        }
        else
        {
            previousTimeScale = scale;
        }

        Debug.Log($"[GameManager] Time scale set to {scale}x");
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
    

    public void QuitGame()
    {
        UnpauseGame();
    #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
    #else
        Application.Quit();
    #endif
    }

    // Future: Game state, pause, save/load, scene management, etc.
}