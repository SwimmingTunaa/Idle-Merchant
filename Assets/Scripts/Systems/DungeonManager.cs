using UnityEngine;

public class DungeonManager : MonoBehaviour, IProducer, ITickable {
    [Header("Generation")]
    [SerializeField] private string lootId = "Ore";
    [SerializeField] private float lootEverySeconds = 2f;

    private float t;
    public event System.Action<ResourceStack> OnProduced;

    public void Tick(float dt) {
        t += dt;
        if (t >= lootEverySeconds) {
            t = 0f;
            // var drop = new ResourceStack(lootId, 1);
            // OnProduced?.Invoke(drop);
        }
    }

    void Update() => Tick(Time.deltaTime);
}
