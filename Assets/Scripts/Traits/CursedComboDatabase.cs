using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Database for cursed combo lookups.
/// </summary>
public static class CursedComboDatabase
{
    private static List<CursedComboDef> allCombos;
    private static bool isInitialized = false;
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        Initialize();
    }
    
    public static void Initialize()
    {
        if (isInitialized)
        {
            Debug.LogWarning("[CursedComboDatabase] Already initialized, skipping");
            return;
        }
        
        allCombos = new List<CursedComboDef>(
            Resources.LoadAll<CursedComboDef>("CursedCombos")
        );
        
        isInitialized = true;
        Debug.Log($"[CursedComboDatabase] Initialized with {allCombos.Count} cursed combos");
    }
    
    public static CursedComboDef GetCombo(TraitDef trait1, TraitDef trait2)
    {
        if (!isInitialized) Initialize();
        
        if (trait1 == null || trait2 == null)
            return null;
        
        foreach (var combo in allCombos)
        {
            if ((combo.traitA == trait1 && combo.traitB == trait2) ||
                (combo.traitA == trait2 && combo.traitB == trait1))
            {
                return combo;
            }
        }
        
        return null;
    }
    
    public static bool IsCursedCombo(TraitDef trait1, TraitDef trait2)
    {
        return GetCombo(trait1, trait2) != null;
    }
    
    public static List<CursedComboDef> GetAllCombos()
    {
        if (!isInitialized) Initialize();
        return new List<CursedComboDef>(allCombos);
    }
    
    public static List<CursedComboDef> GetCombosForRole(TraitRole role)
    {
        if (!isInitialized) Initialize();
        
        var result = new List<CursedComboDef>();
        foreach (var combo in allCombos)
        {
            if (combo.traitA.role == role && combo.traitB.role == role)
            {
                result.Add(combo);
            }
        }
        return result;
    }
    
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Traits/Reload Cursed Combo Database")]
    public static void ReloadDatabase()
    {
        isInitialized = false;
        allCombos?.Clear();
        Initialize();
    }
    
    [UnityEditor.MenuItem("Tools/Traits/Print Cursed Combos")]
    public static void PrintDatabase()
    {
        if (!isInitialized) Initialize();
        
        Debug.Log("=== CURSED COMBO DATABASE ===");
        Debug.Log($"Total Combos: {allCombos.Count}");
        
        foreach (var role in System.Enum.GetValues(typeof(TraitRole)))
        {
            var combos = GetCombosForRole((TraitRole)role);
            Debug.Log($"\n{role} Cursed Combos ({combos.Count}):");
            foreach (var combo in combos)
            {
                Debug.Log($"  - {combo.comboName}: {combo.traitA.displayName} + {combo.traitB.displayName} " +
                         $"({combo.spawnChance * 100f:F1}% spawn, {combo.costMultiplier}x cost)");
            }
        }
    }
#endif
}
