using System;
using UnityEngine;

/// <summary>
/// Example save data structure including identity.
/// Extend your existing unit save data.
/// </summary>
[Serializable]
public class AdventurerSaveData
{
    // Identity data
    public Gender gender;
    public string firstName;
    public string epithet;
    public string description;
    public int appearanceSeed;
    
    // Existing trait data
    public TraitInstance[] traits;
    
    // Other existing data...
    public int level;
    public int layerIndex;
    // etc.
}

/// <summary>
/// Save/Load helper for Identity.
/// </summary>
public static class IdentitySaveHelper
{
    /// <summary>
    /// Serialize identity to save data.
    /// </summary>
    public static void SaveIdentity(Identity identity, AdventurerSaveData saveData)
    {
        if (identity == null)
        {
            Debug.LogWarning("Cannot save null identity");
            return;
        }
        
        saveData.gender = identity.gender;
        saveData.firstName = identity.firstName;
        saveData.epithet = identity.epithet;
        saveData.description = identity.description;
        saveData.appearanceSeed = identity.appearanceSeed;
    }
    
    /// <summary>
    /// Deserialize identity from save data.
    /// </summary>
    public static Identity LoadIdentity(AdventurerSaveData saveData)
    {
        return new Identity(
            saveData.gender,
            saveData.firstName,
            saveData.epithet,
            saveData.description,
            saveData.appearanceSeed
        );
    }
}

/*
// Usage in your save system:

// SAVE:
var saveData = new AdventurerSaveData();
var identityComp = adventurer.GetComponent<IdentityComponent>();
IdentitySaveHelper.SaveIdentity(identityComp.Identity, saveData);

// Also save traits
var traitComp = adventurer.GetComponent<TraitComponent>();
saveData.traits = traitComp.GetTraitsForSave();

// Serialize saveData to JSON...

// LOAD:
// Deserialize JSON to saveData...

// Restore identity
var identity = IdentitySaveHelper.LoadIdentity(saveData);
identityComp.LoadIdentityFromSave(identity);

// Restore traits
traitComp.LoadTraitsFromSave(saveData.traits);
*/
