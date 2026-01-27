using UnityEngine;

/// <summary>
/// Central game state manager.
/// Reserved for future game-wide state and flow control.
/// 
/// Gold is managed by Inventory.cs - use Inventory.Instance for all gold operations.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // Time management
    private float previousTimeScale = 1f;
    private bool didPauseTime = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void PauseGame()
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        didPauseTime = true;
    }

    public void UnpauseGame()
    {
        if (didPauseTime)
        {
            Time.timeScale = previousTimeScale;
            didPauseTime = false;
        }
    }


    // Future: Game state, pause, save/load, scene management, etc.
}