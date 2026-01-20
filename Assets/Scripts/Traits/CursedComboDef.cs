using UnityEngine;

/// <summary>
/// ScriptableObject definition for cursed trait combos.
/// Cursed combos are rare (2-5%) but extremely powerful/risky combinations.
/// </summary>
[CreateAssetMenu(fileName = "NewCursedCombo", menuName = "Traits/Cursed Combo Definition")]
public class CursedComboDef : ScriptableObject
{
    [Header("Combo Identity")]
    [Tooltip("Display name (e.g., 'Glass Cannon Berserker')")]
    public string comboName = "Cursed Combo";
    
    [TextArea(2, 3)]
    public string description = "Extremely risky combination";
    
    [Header("Required Traits")]
    [Tooltip("Both traits must be present for this cursed combo")]
    public TraitDef traitA;
    public TraitDef traitB;
    
    [Header("Rarity")]
    [Tooltip("Chance this combo appears in hiring pool (0.02 = 2%)")]
    [Range(0f, 0.1f)]
    public float spawnChance = 0.03f;
    
    [Header("Cost Modifier")]
    [Tooltip("Multiplier for hire cost (1.5 = +50%)")]
    [Range(1f, 3f)]
    public float costMultiplier = 1.5f;
    
    [Header("Visual")]
    public Sprite icon;
    public Color highlightColor = Color.red;
    
    void OnValidate()
    {
        // Validate traits exist
        if (traitA == null || traitB == null)
        {
            Debug.LogWarning($"[{name}] Both traits must be assigned");
            return;
        }
        
        // Validate same role
        if (traitA.role != traitB.role)
        {
            Debug.LogError($"[{name}] Traits must be same role! {traitA.displayName} is {traitA.role}, {traitB.displayName} is {traitB.role}");
        }
        
        // Validate not same trait
        if (traitA == traitB)
        {
            Debug.LogError($"[{name}] Cannot use the same trait twice!");
        }
    }
}
