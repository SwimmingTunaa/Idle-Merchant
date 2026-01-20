using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Custom editor for EntityDef and all subclasses (AdventurerDef, MobDef, etc.)
/// Provides tabbed interface, auto-calculate collider utility, and validation warnings.
/// Now supports CombatConfig for both adventurers and mobs.
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
    private SerializedProperty startingStateProp;
    private SerializedProperty wanderTimeRangeProp;
    private SerializedProperty returnToSpawnProp;
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

    private bool isAdventurer;
    private bool isMob;

    private void OnEnable()
    {
        isAdventurer = target is AdventurerDef;
        isMob = target is MobDef;

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
            startingStateProp = serializedObject.FindProperty("startingState");
            wanderTimeRangeProp = serializedObject.FindProperty("wanderTimeRange");
            returnToSpawnProp = serializedObject.FindProperty("returnToSpawn");
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
            case 3: DrawCombatTab(); break;
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
        EditorGUILayout.LabelField("Systems", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(sortingTypeProp);
        EditorGUILayout.PropertyField(startingSkillsProp, true);
        EditorGUILayout.EndVertical();
    }

    private void DrawStatsTab()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Health", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(healthProp);
        
        // Mob-specific: HP multiplier curve
        if (isMob && hpMultiplierProp != null)
        {
            EditorGUILayout.PropertyField(hpMultiplierProp, new GUIContent("HP Multiplier by Layer"));
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Movement", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(moveSpeedProp);
        EditorGUILayout.PropertyField(stopDistanceProp);
        EditorGUILayout.PropertyField(idleTimeRangeProp);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(deathAnimationDurationProp);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Collider", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(colliderSizeProp);
        EditorGUILayout.PropertyField(colliderOffsetProp);
        
        if (GUILayout.Button("Auto-Calculate Collider From Sprite"))
        {
            AutoCalculateCollider();
        }
        EditorGUILayout.EndVertical();
        
        // Mob-specific: damage response
        if (isMob && stunTimeProp != null)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Damage Response", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(stunTimeProp, new GUIContent("Stun Time (when hit)"));
            EditorGUILayout.EndVertical();
        }
        
        EditorGUILayout.Space(5);
        
        // Combat Stats (all entities have these, but not all use them)
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Combat Stats", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(attackDamageProp);
        EditorGUILayout.PropertyField(attackIntervalProp);
        EditorGUILayout.PropertyField(attackRangeProp);
        EditorGUILayout.PropertyField(chaseBreakRangeProp);
        EditorGUILayout.PropertyField(scanRangeProp);
        
        if (attackDamageProp.floatValue > 0 && attackIntervalProp.floatValue > 0)
        {
            float dps = attackDamageProp.floatValue / attackIntervalProp.floatValue;
            EditorGUILayout.LabelField($"DPS: {dps:F2}");
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawVisualTab()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Visual System", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(useModularCharacterProp);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        bool useModular = useModularCharacterProp.boolValue;

        if (!useModular)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Simple Visual", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(spriteDropProp);
            EditorGUILayout.PropertyField(shopSpriteProp);
            EditorGUILayout.PropertyField(animatorOverrideProp);
            EditorGUILayout.EndVertical();
        }
        else
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Modular Character", EditorStyles.boldLabel);
            
            selectedLibraryTab = GUILayout.Toolbar(selectedLibraryTab, libraryTabNames);
            
            EditorGUILayout.Space(5);

            switch (selectedLibraryTab)
            {
                case 0:
                    EditorGUILayout.PropertyField(baseBodySpriteLibrariesProp, new GUIContent("Base Body Libraries"), true);
                    EditorGUILayout.PropertyField(skinColourPaletteProp, new GUIContent("Skin Colour Palette"), true);
                    break;
                case 1:
                    EditorGUILayout.PropertyField(shirtSpriteLibrariesProp, new GUIContent("Shirt Libraries"), true);
                    EditorGUILayout.PropertyField(shirtColourPaletteProp, new GUIContent("Shirt Colour Palette"), true);
                    break;
                case 2:
                    EditorGUILayout.PropertyField(pantsSpriteLibrariesProp, new GUIContent("Pants Libraries"), true);
                    EditorGUILayout.PropertyField(pantsColourPaletteProp, new GUIContent("Pants Colour Palette"), true);
                    break;
                case 3:
                    EditorGUILayout.PropertyField(hairTopSpriteLibrariesProp, new GUIContent("Hair Top Libraries"), true);
                    EditorGUILayout.PropertyField(hairColourPaletteProp, new GUIContent("Hair Colour Palette"), true);
                    break;
                case 4:
                    EditorGUILayout.PropertyField(hairBackSpriteLibrariesProp, new GUIContent("Hair Back Libraries"), true);
                    break;
                case 5:
                    EditorGUILayout.PropertyField(frontWeaponSpriteLibrariesProp, new GUIContent("Front Weapon Libraries"), true);
                    break;
                case 6:
                    EditorGUILayout.PropertyField(backWeaponSpriteLibrariesProp, new GUIContent("Back Weapon Libraries"), true);
                    break;
            }
            
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawCombatTab()
    {
        if (isAdventurer)
        {
            DrawAdventurerCombatTab();
        }
        else if (isMob)
        {
            DrawMobCombatTab();
        }
        else
        {
            EditorGUILayout.HelpBox("Combat stats only available for AdventurerDef and MobDef.", MessageType.Info);
        }
    }

    private void DrawAdventurerCombatTab()
    {
        AdventurerDef adventurer = target as AdventurerDef;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Calculated Stats", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"DPS: {adventurer.DPS:F2} ({attackDamageProp.floatValue} dmg / {attackIntervalProp.floatValue}s)");
        EditorGUILayout.HelpBox("Combat stats (damage, range, etc.) are now in the Stats tab.", MessageType.Info);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Starting State", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(startingStateProp);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Behavior", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(wanderTimeRangeProp);
        EditorGUILayout.PropertyField(returnToSpawnProp);
        EditorGUILayout.PropertyField(leashRangeProp);
        EditorGUILayout.EndVertical();
    }

    private void DrawMobCombatTab()
    {
        MobDef mob = target as MobDef;

        // Quick toggle buttons
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Make Passive", GUILayout.Height(30)))
        {
            SetMobPassive();
        }
        if (GUILayout.Button("Make Aggressive", GUILayout.Height(30)))
        {
            SetMobAggressive();
        }
        if (GUILayout.Button("Make Defensive", GUILayout.Height(30)))
        {
            SetMobDefensive();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Starting State", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(mobStartingStateProp);
        EditorGUILayout.PropertyField(isBossProp);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);
        
        EditorGUILayout.HelpBox("Combat stats (damage, range, etc.) are in the Stats tab.", MessageType.Info);
        
        EditorGUILayout.Space(5);

        // Combat Configuration
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Combat Configuration", EditorStyles.boldLabel);
        
        if (combatConfigProp != null)
        {
            SerializedProperty canAttackProp = combatConfigProp.FindPropertyRelative("canAttack");
            SerializedProperty behaviorTypeProp = combatConfigProp.FindPropertyRelative("behaviorType");
            SerializedProperty hostileToProp = combatConfigProp.FindPropertyRelative("hostileTo");
            SerializedProperty territorialRadiusProp = combatConfigProp.FindPropertyRelative("territorialRadius");
            
            EditorGUILayout.PropertyField(canAttackProp, new GUIContent("Can Attack"));
            
            if (canAttackProp.boolValue)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.PropertyField(behaviorTypeProp, new GUIContent("Behavior Type"));
                EditorGUILayout.PropertyField(hostileToProp, new GUIContent("Hostile To"));
                
                // Show territorial radius only if behavior is Territorial
                if (behaviorTypeProp.enumValueIndex == (int)CombatBehaviorType.Territorial)
                {
                    EditorGUILayout.PropertyField(territorialRadiusProp, new GUIContent("Territorial Radius"));
                }
                
                EditorGUI.indentLevel--;
                
                // Combat guidance
                EditorGUILayout.Space(3);
                string behaviorDesc = GetBehaviorDescription((CombatBehaviorType)behaviorTypeProp.enumValueIndex);
                EditorGUILayout.HelpBox(behaviorDesc, MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("This mob will not attack. It will only Wander/Idle/Damaged.", MessageType.Info);
            }
        }
        
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        // Targeting
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Targeting (Adventurer → Mob)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(maxSimultaneousAttackersProp, new GUIContent("Max Simultaneous Attackers"));
        EditorGUILayout.HelpBox("How many adventurers can target this mob at once. 0 = use MobManager default (3).", MessageType.Info);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        // Loot
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Loot", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(lootProp, true);
        EditorGUILayout.PropertyField(lootDropAmountProp);
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
        combatConfigProp.FindPropertyRelative("hostileTo").enumValueFlag = 0;
        combatConfigProp.FindPropertyRelative("territorialRadius").floatValue = 0f;
        
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private void SetMobAggressive()
    {
        if (combatConfigProp == null) return;
        
        combatConfigProp.FindPropertyRelative("canAttack").boolValue = true;
        combatConfigProp.FindPropertyRelative("behaviorType").enumValueIndex = (int)CombatBehaviorType.Aggressive;
        combatConfigProp.FindPropertyRelative("hostileTo").enumValueFlag = (int)HostilityTargets.Adventurers;
        combatConfigProp.FindPropertyRelative("territorialRadius").floatValue = 0f;
        
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private void SetMobDefensive()
    {
        if (combatConfigProp == null) return;
        
        combatConfigProp.FindPropertyRelative("canAttack").boolValue = true;
        combatConfigProp.FindPropertyRelative("behaviorType").enumValueIndex = (int)CombatBehaviorType.Defensive;
        combatConfigProp.FindPropertyRelative("hostileTo").enumValueFlag = (int)HostilityTargets.Adventurers;
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

        if (entityDef.animatorOverrideController != null)
        {
            return ExtractIdleSpriteFromAnimator(entityDef.animatorOverrideController);
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