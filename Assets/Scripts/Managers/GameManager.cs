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

    // Future: Game state, pause, save/load, scene management, etc.
}