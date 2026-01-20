using UnityEngine;

[CreateAssetMenu(menuName = "Data/Category Shelf")]
public class CategoryShelfDef : ScriptableObject {
    public ItemCategory category;
    public int brandingLevel;      // 0..4
    public int trafficLevel;       // 0..3
    public int ambienceLevel;      // 0..3
    // Curves for tuning per level:
    public AnimationCurve brandingBonus;   // e.g., 0→0,1→0.1,2→0.2,3→0.35,4→0.5
    public AnimationCurve trafficBonus;    // 0→0,1→0.1,2→0.25,3→0.4
    public AnimationCurve ambienceGps;     // 0→0,1→0.5,2→1.0,3→2.0

    public float PriceBonus()  => brandingBonus.Evaluate(brandingLevel);
    public float TrafficBonus()=> trafficBonus.Evaluate(trafficLevel);
    public float PassiveGps()  => ambienceGps.Evaluate(ambienceLevel);
}
