using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject containing name pools for identity generation.
/// </summary>
[CreateAssetMenu(fileName = "NamePool", menuName = "Identity/Name Pool")]
public class NamePoolDef : ScriptableObject
{
    [Header("Male Names")]
    public List<string> maleNames = new List<string>();
    
    [Header("Female Names")]
    public List<string> femaleNames = new List<string>();
    
    [Header("Fallback Descriptions (No Trait)")]
    [Tooltip("Used when unit has no traits - just a description, no epithet")]
    public List<string> genericDescriptions = new List<string>();
    
    public string GetRandomName(Gender gender)
    {
        var pool = gender == Gender.Male ? maleNames : femaleNames;
        
        if (pool == null || pool.Count == 0)
        {
            return GetFallbackName(gender);
        }
        
        return pool[Random.Range(0, pool.Count)];
    }
    
    private string GetFallbackName(Gender gender)
    {
        return gender == Gender.Male ? "Adventurer" : "Adventurer";
    }
}