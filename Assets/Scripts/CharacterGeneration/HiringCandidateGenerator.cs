using UnityEngine;
using System.Collections.Generic;
using UnityEngine.U2D.Animation;


public static class HiringCandidateGenerator
{
    private static NamePoolDef namePool; // Cached reference
    
    /// <summary>
    /// Initialize name pool (call once at game start or lazy-load).
    /// </summary>
    public static void Initialize(NamePoolDef pool)
    {
        namePool = pool;
    }
    
  
    /// Generate candidates with identities.
    /// traitChance: probability that a candidate has traits (0.0 - 1.0)
    /// Default 0.7 means 70% have traits, 30% have no traits
    /// </summary>
    public static HiringCandidate[] GenerateCandidates(
        EntityDef entityDef, 
        int count, 
        int baseCost,
        float traitChance = 0.7f)
    {
        // Lazy-load name pool if not initialized
        if (namePool == null)
        {
            namePool = Resources.Load<NamePoolDef>("NamePool");
            if (namePool == null)
            {
                Debug.LogWarning("[HiringCandidateGenerator] NamePool not found in Resources, using fallback names");
            }
        }
        
        var candidates = new HiringCandidate[count];
        var role = GetTraitRole(entityDef);
        
        var possibleCursedCombos = CursedComboDatabase.GetCombosForRole(role);
        
        for (int i = 0; i < count; i++)
        {
            // Roll for trait presence
            bool shouldHaveTraits = Random.value < traitChance;
            TraitInstance[] traits;
            CursedComboDef cursedCombo = null;
            
            if (shouldHaveTraits)
            {
                cursedCombo = TryRollCursedCombo(possibleCursedCombos);
                
                if (cursedCombo != null)
                {
                    traits = new[]
                    {
                        new TraitInstance { traitId = cursedCombo.traitA.traitId, tier = 1 },
                        new TraitInstance { traitId = cursedCombo.traitB.traitId, tier = 1 }
                    };
                }
                else
                {
                    var trait = GenerateRandomTrait(role, tier: 1);
                    traits = new[] { trait };
                }
            }
            else
            {
                // No traits - empty array
                traits = new TraitInstance[0];
            }
            
            // Get primary trait for identity generation (if any)
            TraitDef primaryTrait = traits.Length > 0 
                ? TraitDatabase.GetTrait(traits[0].traitId) 
                : null;
            
            // Generate identity (pass null if no traits)
            Identity identity = IdentityGenerator.Generate(namePool, primaryTrait);
            
            candidates[i] = new HiringCandidate
            {
                entityDef = entityDef,
                identity = identity,
                traits = traits,
                cursedCombo = cursedCombo,
                hireCost = CalculateHireCost(baseCost, cursedCombo),
                appearanceIndices = entityDef.useModularCharacter ? new CharacterAppearanceIndices
                {
                    // Visual customization indices
                    pants = RandomSpriteLibraryIndex(entityDef.pantsSpriteLibraries),
                    shirt = RandomSpriteLibraryIndex(entityDef.shirtSpriteLibraries),
                    hairTop = RandomSpriteLibraryIndex(entityDef.hairTopSpriteLibraries),
                    hairBack = RandomSpriteLibraryIndex(entityDef.hairBackSpriteLibraries),
                    frontWeapon = RandomSpriteLibraryIndex(entityDef.frontWeaponSpriteLibraries),
                    backWeapon = RandomSpriteLibraryIndex(entityDef.backWeaponSpriteLibraries), 

                    // Colour customization indices
                    skinColour = entityDef.skinColourPalette?.GetRandomGradientValue() ?? 0f,
                    shirtColour = entityDef.ShirtColourPalette?.GetRandomPaletteIndex() ?? 0,
                    pantsColour = entityDef.PantsColourPalette?.GetRandomPaletteIndex() ?? 0,
                    hairColour = entityDef.HairColourPalette?.GetRandomPaletteIndex() ?? 0
                } : default,
            };
        }
        
        return candidates;
    }
        
    private static TraitRole GetTraitRole(EntityDef def)
    {
        if (def is AdventurerDef)
            return TraitRole.Adventurer;
        
        if (def is PorterDef)
            return TraitRole.Porter;
        
        Debug.LogWarning($"[HiringCandidateGenerator] Unknown EntityDef type: {def.GetType().Name}, defaulting to Adventurer");
        return TraitRole.Adventurer;
    }
    
    private static CursedComboDef TryRollCursedCombo(List<CursedComboDef> possibleCombos)
    {
        if (possibleCombos == null || possibleCombos.Count == 0)
            return null;
        
        foreach (var combo in possibleCombos)
        {
            if (Random.value < combo.spawnChance)
            {
                return combo;
            }
        }
        
        return null;
    }
    
    private static TraitInstance GenerateRandomTrait(TraitRole role, int tier)
    {
        var availableTraits = TraitDatabase.GetTraitsForRole(role);
        
        if (availableTraits.Count == 0)
        {
            Debug.LogError($"[HiringCandidateGenerator] No traits found for role {role}");
            return default;
        }
        
        var randomTrait = availableTraits[Random.Range(0, availableTraits.Count)];
        
        return new TraitInstance
        {
            traitId = randomTrait.traitId,
            tier = tier
        };
    }
    
    private static int CalculateHireCost(int baseCost, CursedComboDef cursedCombo)
    {
        if (cursedCombo != null)
        {
            return Mathf.RoundToInt(baseCost * cursedCombo.costMultiplier);
        }
        
        return baseCost;
    }

    private static int RandomSpriteLibraryIndex(SpriteLibraryAsset[] spriteLibrary)
    {
        return Random.Range(0, spriteLibrary.Length);
    }

}