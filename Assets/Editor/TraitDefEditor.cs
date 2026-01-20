#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom editor for TraitDef with validation.
/// </summary>
[CustomEditor(typeof(TraitDef))]
public class TraitDefEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        var traitDef = (TraitDef)target;
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
        
        // Validate traitId
        if (string.IsNullOrWhiteSpace(traitDef.traitId))
        {
            EditorGUILayout.HelpBox("traitId is required!", MessageType.Error);
        }
        else if (traitDef.traitId.Contains(" "))
        {
            EditorGUILayout.HelpBox("traitId should not contain spaces (use underscores)", MessageType.Warning);
        }
        
        // Validate displayName
        if (string.IsNullOrWhiteSpace(traitDef.displayName))
        {
            EditorGUILayout.HelpBox("displayName is required!", MessageType.Warning);
        }
        
        // Validate tier count
        if (traitDef.tiers == null || traitDef.tiers.Length == 0)
        {
            EditorGUILayout.HelpBox("No tiers defined!", MessageType.Error);
        }
        else if (traitDef.tiers.Length != traitDef.maxTier)
        {
            EditorGUILayout.HelpBox(
                $"Tier array length ({traitDef.tiers.Length}) doesn't match maxTier ({traitDef.maxTier})", 
                MessageType.Warning
            );
        }
        
        // Validate tier modifiers
        for (int i = 0; i < traitDef.tiers.Length; i++)
        {
            if (traitDef.tiers[i].modifiers == null || traitDef.tiers[i].modifiers.Length == 0)
            {
                EditorGUILayout.HelpBox($"Tier {i + 1} has no modifiers!", MessageType.Warning);
            }
        }
        
        // Validate conflicts symmetry
        if (traitDef.conflictsWith != null)
        {
            foreach (var conflict in traitDef.conflictsWith)
            {
                if (conflict == null) continue;
                
                if (conflict == traitDef)
                {
                    EditorGUILayout.HelpBox("Trait cannot conflict with itself!", MessageType.Error);
                }
                else if (conflict.conflictsWith != null && 
                         System.Array.IndexOf(conflict.conflictsWith, traitDef) < 0)
                {
                    EditorGUILayout.HelpBox(
                        $"Asymmetric conflict: {conflict.displayName} doesn't list this trait back", 
                        MessageType.Warning
                    );
                }
            }
        }
        
        // Show preview
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        
        if (!string.IsNullOrEmpty(traitDef.traitId))
        {
            EditorGUILayout.LabelField($"ID: {traitDef.traitId}");
            EditorGUILayout.LabelField($"Display: {traitDef.displayName}");
            EditorGUILayout.LabelField($"Role: {traitDef.role}");
            EditorGUILayout.LabelField($"Max Tier: {traitDef.maxTier}");
            
            if (traitDef.tiers != null)
            {
                for (int i = 0; i < traitDef.tiers.Length; i++)
                {
                    EditorGUILayout.LabelField($"Tier {i + 1}: {traitDef.tiers[i].modifiers?.Length ?? 0} modifiers");
                }
            }
        }
    }
}

/// <summary>
/// Custom editor for CursedComboDef with validation.
/// </summary>
[CustomEditor(typeof(CursedComboDef))]
public class CursedComboDefEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        var combo = (CursedComboDef)target;
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
        
        // Validate traits exist
        if (combo.traitA == null || combo.traitB == null)
        {
            EditorGUILayout.HelpBox("Both traits must be assigned!", MessageType.Error);
            return;
        }
        
        // Validate same role
        if (combo.traitA.role != combo.traitB.role)
        {
            EditorGUILayout.HelpBox(
                $"Traits must be same role! {combo.traitA.displayName} is {combo.traitA.role}, " +
                $"{combo.traitB.displayName} is {combo.traitB.role}", 
                MessageType.Error
            );
        }
        
        // Validate not same trait
        if (combo.traitA == combo.traitB)
        {
            EditorGUILayout.HelpBox("Cannot use the same trait twice!", MessageType.Error);
        }
        
        // Show preview
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Combo: {combo.traitA.displayName} + {combo.traitB.displayName}");
        EditorGUILayout.LabelField($"Role: {combo.traitA.role}");
        EditorGUILayout.LabelField($"Spawn Chance: {combo.spawnChance * 100f:F1}%");
        EditorGUILayout.LabelField($"Cost Multiplier: +{(combo.costMultiplier - 1f) * 100f:F0}%");
    }
}
#endif
