using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Custom editor for EntityDef and all subclasses (AdventurerDef, MobDef, PorterDef, CustomerDef)
/// Provides tabbed interface, auto-calculate collider utility, and validation warnings.
/// Supports all entity types with type-specific behavior tabs.
/// </summary>
[CustomEditor(typeof(EntityDef), true)]
public class EntityDefEditor : Editor
{
    private int selectedTab = 0;
    private readonly string[] tabNames = { "Core", "Stats", "Visual", "Behaviour" };
    
    private int selectedLibraryTab = 0;
    private readonly string[] libraryTabNames = { "Base Body", "Shirt", "Pants", "Hair Top", "Hair Back", "Front Weapon", "Back Weapon" };

    // Core
    private SerializedProperty prefabProp;
    private SerializedProperty idProp;
    private SerializedProperty displayNameProp;
    private SerializedProperty descriptionProp;
    private SerializedProperty hireCostProp;
    private SerializedProperty assignedLayerProp;
    private SerializedProperty startingSkillsProp;
    private SerializedProperty sortingTypeProp;
    private SerializedProperty spawnWeightProp;

    // Stats
    private SerializedProperty healthProp;
    private SerializedProperty moveSpeedProp;
    private SerializedProperty stopDistanceProp;
    private SerializedProperty idleTimeRangeProp;
    private SerializedProperty deathAnimationDurationProp;
    private SerializedProperty colliderSizeProp;
    private SerializedProperty colliderOffsetProp;
    
    // Combat Stats (EntityDef base class - all entities)
    private SerializedProperty attackDamageProp;
    private SerializedProperty attackIntervalProp;
    private SerializedProperty attackRangeProp;
    private SerializedProperty chaseBreakRangeProp;
    private SerializedProperty scanRangeProp;

    // Visual
    private SerializedProperty useModularCharacterProp;
    private SerializedProperty spriteDropProp;
    private SerializedProperty shopSpriteProp;
    private SerializedProperty animatorOverrideProp;
    private SerializedProperty baseBodySpriteLibrariesProp;
    private SerializedProperty shirtSpriteLibrariesProp;
    private SerializedProperty pantsSpriteLibrariesProp;
    private SerializedProperty hairTopSpriteLibrariesProp;
    private SerializedProperty hairBackSpriteLibrariesProp;
    private SerializedProperty frontWeaponSpriteLibrariesProp;
    private SerializedProperty backWeaponSpriteLibrariesProp;
    private SerializedProperty skinColourPaletteProp;
    private SerializedProperty shirtColourPaletteProp;
    private SerializedProperty pantsColourPaletteProp;
    private SerializedProperty hairColourPaletteProp;

    // Combat - AdventurerDef specific
    private SerializedProperty adventurerTypeProp;
    private SerializedProperty adventurerStartingStateProp;
    private SerializedProperty adventurerWanderTimeRangeProp;
    private SerializedProperty adventurerReturnToSpawnProp;
    private SerializedProperty leashRangeProp;
    
    // Combat - MobDef specific
    private SerializedProperty mobStartingStateProp;
    private SerializedProperty isBossProp;
    private SerializedProperty hpMultiplierProp;
    private SerializedProperty stunTimeProp;
    private SerializedProperty combatConfigProp;
    private SerializedProperty maxSimultaneousAttackersProp;
    private SerializedProperty lootProp;
    private SerializedProperty lootDropAmountProp;

    // Porter-specific
    private SerializedProperty porterTypeProp;
    private SerializedProperty carryCapacityProp;
    private SerializedProperty pickupTimeProp;
    private SerializedProperty depositTimeProp;
    private SerializedProperty porterStartingStateProp;
    private SerializedProperty porterWanderTimeRangeProp;
    private SerializedProperty porterReturnToSpawnProp;
    private SerializedProperty idleColorProp;
    private SerializedProperty wanderColorProp;
    private SerializedProperty seekColorProp;
    private SerializedProperty carryingColorProp;
    private SerializedProperty travelColorProp;

    // Customer-specific
    private SerializedProperty customerStartingStateProp;
    private SerializedProperty itemPreferenceProp;
    private SerializedProperty budgetProp;
    private SerializedProperty batchRangeProp;

    private bool isAdventurer;
    private bool isMob;
    private bool isPorter;
    private bool isCustomer;

    private void OnEnable()
    {
        isAdventurer = target is AdventurerDef;
        isMob = target is MobDef;
        isPorter = target is PorterDef;
        isCustomer = target is CustomerDef;

        // Debug: Log what type we detected
        Debug.Log($"[EntityDefEditor] OnEnable for {target.name} - Type: {target.GetType().Name} | isAdventurer: {isAdventurer}, isMob: {isMob}, isPorter: {isPorter}, isCustomer: {isCustomer}");

        // Core
        prefabProp = serializedObject.FindProperty("prefab");
        idProp = serializedObject.FindProperty("id");
        displayNameProp = serializedObject.FindProperty("displayName");
        descriptionProp = serializedObject.FindProperty("description");
        hireCostProp = serializedObject.FindProperty("hireCost");
        assignedLayerProp = serializedObject.FindProperty("assignedLayer");
        startingSkillsProp = serializedObject.FindProperty("startingSkills");
        sortingTypeProp = serializedObject.FindProperty("sortingType");
        spawnWeightProp = serializedObject.FindProperty("spawnWeight");

        // Stats
        healthProp = serializedObject.FindProperty("baseHealth");
        moveSpeedProp = serializedObject.FindProperty("moveSpeed");
        stopDistanceProp = serializedObject.FindProperty("stopDistance");
        idleTimeRangeProp = serializedObject.FindProperty("idleTimeRange");
        deathAnimationDurationProp = serializedObject.FindProperty("deathAnimationDuration");
        colliderSizeProp = serializedObject.FindProperty("colliderSize");
        colliderOffsetProp = serializedObject.FindProperty("colliderOffset");
        
        // Combat Stats (EntityDef base class)
        attackDamageProp = serializedObject.FindProperty("attackDamage");
        attackIntervalProp = serializedObject.FindProperty("attackInterval");
        attackRangeProp = serializedObject.FindProperty("attackRange");
        chaseBreakRangeProp = serializedObject.FindProperty("chaseBreakRange");
        scanRangeProp = serializedObject.FindProperty("scanRange");

        // Visual
        useModularCharacterProp = serializedObject.FindProperty("useModularCharacter");
        spriteDropProp = serializedObject.FindProperty("spriteDrop");
        shopSpriteProp = serializedObject.FindProperty("shopSprite");
        animatorOverrideProp = serializedObject.FindProperty("animatorOverrideController");
        baseBodySpriteLibrariesProp = serializedObject.FindProperty("baseBodySpriteLibraries");
        shirtSpriteLibrariesProp = serializedObject.FindProperty("shirtSpriteLibraries");
        pantsSpriteLibrariesProp = serializedObject.FindProperty("pantsSpriteLibraries");
        hairTopSpriteLibrariesProp = serializedObject.FindProperty("hairTopSpriteLibraries");
        hairBackSpriteLibrariesProp = serializedObject.FindProperty("hairBackSpriteLibraries");
        frontWeaponSpriteLibrariesProp = serializedObject.FindProperty("frontWeaponSpriteLibraries");
        backWeaponSpriteLibrariesProp = serializedObject.FindProperty("backWeaponSpriteLibraries");
        skinColourPaletteProp = serializedObject.FindProperty("skinColourPalette");
        shirtColourPaletteProp = serializedObject.FindProperty("ShirtColourPalette");
        pantsColourPaletteProp = serializedObject.FindProperty("PantsColourPalette");
        hairColourPaletteProp = serializedObject.FindProperty("HairColourPalette");

        // Combat (AdventurerDef)
        if (isAdventurer)
        {
            adventurerTypeProp = serializedObject.FindProperty("adventurerType");
            adventurerStartingStateProp = serializedObject.FindProperty("startingState");
            adventurerWanderTimeRangeProp = serializedObject.FindProperty("wanderTimeRange");
            adventurerReturnToSpawnProp = serializedObject.FindProperty("returnToSpawn");
            leashRangeProp = serializedObject.FindProperty("leashRange");
        }
        
        // Combat (MobDef)
        if (isMob)
        {
            mobStartingStateProp = serializedObject.FindProperty("startingState");
            isBossProp = serializedObject.FindProperty("isBoss");
            hpMultiplierProp = serializedObject.FindProperty("hpMultiplierByLayer");
            stunTimeProp = serializedObject.FindProperty("stunTime");
            combatConfigProp = serializedObject.FindProperty("combatConfig");
            maxSimultaneousAttackersProp = serializedObject.FindProperty("maxSimultaneousAttackers");
            lootProp = serializedObject.FindProperty("loot");
            lootDropAmountProp = serializedObject.FindProperty("lootDropAmount");
        }

        // Porter-specific
        if (isPorter)
        {
            try
            {
                porterTypeProp = serializedObject.FindProperty("porterType");
                carryCapacityProp = serializedObject.FindProperty("carryCapacity");
                pickupTimeProp = serializedObject.FindProperty("pickupTime");
                depositTimeProp = serializedObject.FindProperty("depositTime");
                porterStartingStateProp = serializedObject.FindProperty("startingState");
                porterWanderTimeRangeProp = serializedObject.FindProperty("wanderTimeRange");
                porterReturnToSpawnProp = serializedObject.FindProperty("returnToSpawn");
                idleColorProp = serializedObject.FindProperty("idleColor");
                wanderColorProp = serializedObject.FindProperty("wanderColor");
                seekColorProp = serializedObject.FindProperty("seekColor");
                carryingColorProp = serializedObject.FindProperty("carryingColor");
                travelColorProp = serializedObject.FindProperty("travelColor");
                
                Debug.Log($"[EntityDefEditor] Successfully bound {(porterStartingStateProp != null ? "all" : "SOME")} Porter properties");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[EntityDefEditor] Failed to bind Porter properties: {e.Message}");
            }
        }

        // Customer-specific
        if (isCustomer)
        {
            try
            {
                customerStartingStateProp = serializedObject.FindProperty("startingState");
                itemPreferenceProp = serializedObject.FindProperty("itemPreferance"); // Note: typo in CustomerDef
                budgetProp = serializedObject.FindProperty("budget");
                batchRangeProp = serializedObject.FindProperty("batchRange");
                
                Debug.Log($"[EntityDefEditor] Successfully bound {(customerStartingStateProp != null ? "all" : "SOME")} Customer properties");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[EntityDefEditor] Failed to bind Customer properties: {e.Message}");
            }
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawValidationWarnings();
        
        EditorGUILayout.Space(10);

        // Tab selection
        selectedTab = GUILayout.Toolbar(selectedTab, tabNames);
        
        EditorGUILayout.Space(10);

        switch (selectedTab)
        {
            case 0: DrawCoreTab(); break;
            case 1: DrawStatsTab(); break;
            case 2: DrawVisualTab(); break;
            case 3: DrawBehaviourTab(); break;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawCoreTab()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(idProp);
        EditorGUILayout.PropertyField(displayNameProp);
        EditorGUILayout.PropertyField(descriptionProp);
        
        if (isAdventurer)
        {
            EditorGUILayout.PropertyField(adventurerTypeProp);
        }
        if (isPorter)
        {
            EditorGUILayout.PropertyField(porterTypeProp);
        }
        
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Prefab & Spawning", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(prefabProp);
        EditorGUILayout.PropertyField(hireCostProp);
        EditorGUILayout.PropertyField(assignedLayerProp);
        EditorGUILayout.PropertyField(spawnWeightProp);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Skills", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(startingSkillsProp, true);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Rendering", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(sortingTypeProp);
        EditorGUILayout.EndVertical();
    }

    private void DrawStatsTab()
    {
        // Only show health for entities that have it (Adventurer and Mob)
        if (healthProp != null && (isAdventurer || isMob))
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Health", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(healthProp);
            
            if (isMob)
            {
                EditorGUILayout.PropertyField(hpMultiplierProp, new GUIContent("HP Multiplier By Layer"));
                
                MobDef mobDef = target as MobDef;
                if (mobDef != null)
                {
                    EditorGUILayout.Space(3);
                    EditorGUILayout.LabelField("HP Preview:", EditorStyles.miniLabel);
                    for (int layer = 1; layer <= 10; layer++)
                    {
                        float hp = mobDef.baseHealth * mobDef.hpMultiplierByLayer.Evaluate(layer);
                        EditorGUILayout.LabelField($"  Layer {layer}: {hp:F1} HP", EditorStyles.miniLabel);
                    }
                }
            }
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);
        }

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Movement", EditorStyles.boldLabel);
        if (moveSpeedProp != null) EditorGUILayout.PropertyField(moveSpeedProp);
        if (stopDistanceProp != null) EditorGUILayout.PropertyField(stopDistanceProp);
        if (idleTimeRangeProp != null) EditorGUILayout.PropertyField(idleTimeRangeProp);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Combat Stats", EditorStyles.boldLabel);
        if (attackDamageProp != null) EditorGUILayout.PropertyField(attackDamageProp);
        if (attackIntervalProp != null) EditorGUILayout.PropertyField(attackIntervalProp);
        if (attackRangeProp != null) EditorGUILayout.PropertyField(attackRangeProp);
        if (chaseBreakRangeProp != null) EditorGUILayout.PropertyField(chaseBreakRangeProp);
        if (scanRangeProp != null) EditorGUILayout.PropertyField(scanRangeProp);
        
        if (isAdventurer)
        {
            AdventurerDef advDef = target as AdventurerDef;
            if (advDef != null)
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField($"DPS: {advDef.DPS:F2}", EditorStyles.boldLabel);
            }
        }
        
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);
        if (deathAnimationDurationProp != null) EditorGUILayout.PropertyField(deathAnimationDurationProp);
        
        if (isMob && stunTimeProp != null)
        {
            EditorGUILayout.PropertyField(stunTimeProp);
        }
        
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Collider", EditorStyles.boldLabel);
        if (colliderSizeProp != null) EditorGUILayout.PropertyField(colliderSizeProp);
        if (colliderOffsetProp != null) EditorGUILayout.PropertyField(colliderOffsetProp);
        
        if (GUILayout.Button("Auto-Calculate Collider From Sprite", GUILayout.Height(30)))
        {
            AutoCalculateCollider();
        }
        
        EditorGUILayout.HelpBox(
            "Right-click this ScriptableObject → 'Auto-Calculate Collider Size From Sprite' to set collider based on sprite bounds.",
            MessageType.Info
        );
        
        EditorGUILayout.EndVertical();
    }

    private void DrawVisualTab()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Character Type", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(useModularCharacterProp);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        bool useModular = useModularCharacterProp.boolValue;

        if (!useModular)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Simple Sprite (Non-Modular)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(spriteDropProp);
            EditorGUILayout.PropertyField(shopSpriteProp);
            EditorGUILayout.PropertyField(animatorOverrideProp);
            EditorGUILayout.EndVertical();
        }
        else
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Modular Character System", EditorStyles.boldLabel);
            
            selectedLibraryTab = GUILayout.Toolbar(selectedLibraryTab, libraryTabNames);
            EditorGUILayout.Space(5);

            switch (selectedLibraryTab)
            {
                case 0: EditorGUILayout.PropertyField(baseBodySpriteLibrariesProp, true); break;
                case 1: EditorGUILayout.PropertyField(shirtSpriteLibrariesProp, true); break;
                case 2: EditorGUILayout.PropertyField(pantsSpriteLibrariesProp, true); break;
                case 3: EditorGUILayout.PropertyField(hairTopSpriteLibrariesProp, true); break;
                case 4: EditorGUILayout.PropertyField(hairBackSpriteLibrariesProp, true); break;
                case 5: EditorGUILayout.PropertyField(frontWeaponSpriteLibrariesProp, true); break;
                case 6: EditorGUILayout.PropertyField(backWeaponSpriteLibrariesProp, true); break;
            }
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Color Palettes", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(skinColourPaletteProp);
            EditorGUILayout.PropertyField(shirtColourPaletteProp);
            EditorGUILayout.PropertyField(pantsColourPaletteProp);
            EditorGUILayout.PropertyField(hairColourPaletteProp);
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawBehaviourTab()
    {
        // Debug header
        EditorGUILayout.HelpBox($"Entity Type: {target.GetType().Name} | Porter: {isPorter} | Customer: {isCustomer} | Adventurer: {isAdventurer} | Mob: {isMob}", MessageType.Info);
        
        if (isAdventurer)
        {
            DrawAdventurerBehaviourTab();
        }
        else if (isMob)
        {
            DrawMobBehaviourTab();
        }
        else if (isPorter)
        {
            DrawPorterBehaviourTab();
        }
        else if (isCustomer)
        {
            DrawCustomerBehaviourTab();
        }
        else
        {
            EditorGUILayout.HelpBox("Base EntityDef has no behavior-specific settings", MessageType.Info);
        }
    }

    private void DrawAdventurerBehaviourTab()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Adventurer Behavior", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(adventurerStartingStateProp, new GUIContent("Starting State"));
        EditorGUILayout.PropertyField(adventurerWanderTimeRangeProp, new GUIContent("Wander Time Range"));
        EditorGUILayout.PropertyField(adventurerReturnToSpawnProp, new GUIContent("Return to Spawn"));
        EditorGUILayout.PropertyField(leashRangeProp, new GUIContent("Leash Range"));
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Combat Configuration", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Adventurers use EntityDef base combat stats (see Stats tab)", MessageType.Info);
        EditorGUILayout.EndVertical();
    }

    private void DrawMobBehaviourTab()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Mob Behavior", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(mobStartingStateProp, new GUIContent("Starting State"));
        EditorGUILayout.PropertyField(isBossProp);
        EditorGUILayout.PropertyField(maxSimultaneousAttackersProp, new GUIContent("Max Attackers (0 = default)"));
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Combat Configuration", EditorStyles.boldLabel);
        
        EditorGUILayout.PropertyField(combatConfigProp, new GUIContent("Combat Config"));
        
        if (combatConfigProp != null)
        {
            SerializedProperty canAttackProp = combatConfigProp.FindPropertyRelative("canAttack");
            SerializedProperty behaviorTypeProp = combatConfigProp.FindPropertyRelative("behaviorType");
            SerializedProperty hostileToProp = combatConfigProp.FindPropertyRelative("hostileTo");
            SerializedProperty territorialRadiusProp = combatConfigProp.FindPropertyRelative("territorialRadius");

            EditorGUILayout.Space(5);
            
            CombatBehaviorType behaviorType = (CombatBehaviorType)behaviorTypeProp.enumValueIndex;
            EditorGUILayout.HelpBox(GetBehaviorDescription(behaviorType), MessageType.Info);
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Quick Presets:", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Passive"))
            {
                SetMobPassive();
            }
            if (GUILayout.Button("Defensive"))
            {
                SetMobDefensive();
            }
            if (GUILayout.Button("Aggressive"))
            {
                SetMobAggressive();
            }
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Loot Table", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(lootProp, true);
        EditorGUILayout.PropertyField(lootDropAmountProp);
        EditorGUILayout.EndVertical();
    }

    private void DrawPorterBehaviourTab()
    {
        // Debug: Check if properties were found
        if (porterStartingStateProp == null)
        {
            EditorGUILayout.HelpBox("ERROR: porterStartingStateProp is null! Property binding failed.", MessageType.Error);
            return;
        }
        
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Porter Behavior", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(porterStartingStateProp, new GUIContent("Starting State"));
        EditorGUILayout.PropertyField(porterWanderTimeRangeProp, new GUIContent("Wander Time Range"));
        EditorGUILayout.PropertyField(porterReturnToSpawnProp, new GUIContent("Return to Spawn"));
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Porter Stats", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(carryCapacityProp);
        EditorGUILayout.PropertyField(pickupTimeProp);
        EditorGUILayout.PropertyField(depositTimeProp);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("State Colors (Visual Debug)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(idleColorProp, new GUIContent("Idle Color"));
        EditorGUILayout.PropertyField(wanderColorProp, new GUIContent("Wander Color"));
        EditorGUILayout.PropertyField(seekColorProp, new GUIContent("Seek Color"));
        EditorGUILayout.PropertyField(carryingColorProp, new GUIContent("Carrying Color"));
        EditorGUILayout.PropertyField(travelColorProp, new GUIContent("Travel Color"));
        EditorGUILayout.EndVertical();
    }

    private void DrawCustomerBehaviourTab()
    {
        // Debug: Check if properties were found
        if (customerStartingStateProp == null)
        {
            EditorGUILayout.HelpBox("ERROR: customerStartingStateProp is null! Property binding failed.", MessageType.Error);
            return;
        }
        
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Customer Behavior", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(customerStartingStateProp, new GUIContent("Starting State"));
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Shopping Preferences", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(itemPreferenceProp, new GUIContent("Item Preference"));
        EditorGUILayout.PropertyField(budgetProp);
        EditorGUILayout.PropertyField(batchRangeProp, new GUIContent("Items Per Visit"));
        EditorGUILayout.EndVertical();
    }

    private string GetBehaviorDescription(CombatBehaviorType type)
    {
        return type switch
        {
            CombatBehaviorType.Passive => "Never attacks, even when damaged.",
            CombatBehaviorType.Defensive => "Only attacks entities that damage it first (revenge).",
            CombatBehaviorType.Aggressive => "Actively scans for and attacks targets within scan range.",
            CombatBehaviorType.Territorial => "Attacks targets that enter its territorial radius.",
            _ => ""
        };
    }

    private void SetMobPassive()
    {
        if (combatConfigProp == null) return;
        
        combatConfigProp.FindPropertyRelative("canAttack").boolValue = false;
        combatConfigProp.FindPropertyRelative("behaviorType").enumValueIndex = (int)CombatBehaviorType.Passive;
        combatConfigProp.FindPropertyRelative("hostileTo").intValue = 0;
        combatConfigProp.FindPropertyRelative("territorialRadius").floatValue = 0f;
        
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private void SetMobAggressive()
    {
        if (combatConfigProp == null) return;
        
        combatConfigProp.FindPropertyRelative("canAttack").boolValue = true;
        combatConfigProp.FindPropertyRelative("behaviorType").enumValueIndex = (int)CombatBehaviorType.Aggressive;
        combatConfigProp.FindPropertyRelative("hostileTo").intValue = (int)HostilityTargets.Adventurers;
        combatConfigProp.FindPropertyRelative("territorialRadius").floatValue = 0f;
        
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private void SetMobDefensive()
    {
        if (combatConfigProp == null) return;
        
        combatConfigProp.FindPropertyRelative("canAttack").boolValue = true;
        combatConfigProp.FindPropertyRelative("behaviorType").enumValueIndex = (int)CombatBehaviorType.Defensive;
        combatConfigProp.FindPropertyRelative("hostileTo").intValue = (int)HostilityTargets.Adventurers;
        combatConfigProp.FindPropertyRelative("territorialRadius").floatValue = 0f;
        
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private void DrawValidationWarnings()
    {
        bool hasWarnings = false;

        if (string.IsNullOrWhiteSpace(idProp.stringValue))
        {
            EditorGUILayout.HelpBox("ID is empty. This entity cannot be referenced by systems.", MessageType.Error);
            hasWarnings = true;
        }

        bool useModular = useModularCharacterProp.boolValue;
        
        if (!useModular && spriteDropProp.objectReferenceValue == null && animatorOverrideProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("No visual assigned. Assign either spriteDrop or animatorOverrideController, or enable 'Use Modular Character'.", MessageType.Warning);
            hasWarnings = true;
        }

        if (colliderSizeProp.vector2Value == Vector2.zero)
        {
            EditorGUILayout.HelpBox("Collider size is zero. Use 'Auto-Calculate Collider' or set manually.", MessageType.Warning);
            hasWarnings = true;
        }

        if (moveSpeedProp.floatValue == 0f && target.GetType() != typeof(EntityDef))
        {
            EditorGUILayout.HelpBox("Move speed is zero. This entity will not move.", MessageType.Info);
            hasWarnings = true;
        }

        if (hasWarnings)
        {
            EditorGUILayout.Space(5);
        }
    }

    private void AutoCalculateCollider()
    {
        EntityDef entityDef = target as EntityDef;
        
        Sprite sprite = GetSpriteForColliderCalculation(entityDef);
        
        if (sprite == null)
        {
            EditorUtility.DisplayDialog(
                "Cannot Calculate Collider",
                "No sprite found. Assign either:\n- spriteDrop, or\n- animatorOverrideController with an idle animation",
                "OK"
            );
            return;
        }

        CalculateColliderFromSprite(sprite);
        
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        
        Debug.Log($"[{target.name}] Collider calculated: size={colliderSizeProp.vector2Value}, offset={colliderOffsetProp.vector2Value} from sprite '{sprite.name}'");
    }

    private Sprite GetSpriteForColliderCalculation(EntityDef entityDef)
    {
        if (entityDef.spriteDrop != null)
        {
            return entityDef.spriteDrop;
        }

       

        return null;
    }

    private void CalculateColliderFromSprite(Sprite sprite)
    {
        Rect spriteRect = sprite.rect;
        float pixelsPerUnit = sprite.pixelsPerUnit;
        
        Vector2 size = new Vector2(
            spriteRect.width / pixelsPerUnit,
            spriteRect.height / pixelsPerUnit
        );
        
        Vector2 spritePivot = sprite.pivot;
        Vector2 spriteCenter = new Vector2(spriteRect.width / 2f, spriteRect.height / 2f);
        Vector2 pivotOffset = (spriteCenter - spritePivot) / pixelsPerUnit;
        
        colliderSizeProp.vector2Value = size;
        colliderOffsetProp.vector2Value = pivotOffset;
    }

    /// <summary>
    /// Extracts the first sprite from the idle animation clip in an AnimatorOverrideController.
    /// Searches for clips containing "idle" (case-insensitive), falls back to first clip.
    /// </summary>
    public static Sprite ExtractIdleSpriteFromAnimator(AnimatorOverrideController animator)
    {
        if (animator == null) return null;

        AnimationClip[] clips = animator.animationClips;
        if (clips == null || clips.Length == 0) return null;

        AnimationClip idleClip = FindIdleClip(clips);
        if (idleClip == null)
        {
            idleClip = clips[0];
        }

        return ExtractSpriteFromClip(idleClip);
    }

    private static AnimationClip FindIdleClip(AnimationClip[] clips)
    {
        foreach (var clip in clips)
        {
            if (clip.name.IndexOf("idle", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return clip;
            }
        }
        return null;
    }

    private static Sprite ExtractSpriteFromClip(AnimationClip clip)
    {
        var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        
        foreach (var binding in bindings)
        {
            if (binding.type == typeof(SpriteRenderer) && binding.propertyName == "m_Sprite")
            {
                var keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                if (keyframes != null && keyframes.Length > 0)
                {
                    return keyframes[0].value as Sprite;
                }
            }
        }

        return null;
    }
}