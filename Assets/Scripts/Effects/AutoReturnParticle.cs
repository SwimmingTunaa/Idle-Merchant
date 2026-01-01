using UnityEngine;

/// <summary>
/// Automatically returns particle system to pool after it finishes playing.
/// Attach to particle system prefabs used with ObjectPoolManager.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class AutoReturnParticle : MonoBehaviour
{
    private ParticleSystem ps;
    
    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
    }
    
    void OnEnable()
    {
        if (ps != null)
        {
            ps.Play();
        }
    }
    
    void Update()
    {
        // Return to pool when particle finishes
        if (ps != null && !ps.IsAlive())
        {
            ObjectPoolManager.Instance.ReturnObjectToPool(gameObject);
        }
    }
}