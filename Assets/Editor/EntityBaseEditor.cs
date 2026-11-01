using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EntityBase), true)]
public class EntityBaseEditor : Editor
{
    private bool showRuntimeStats = true;
    private int runtimeTab;
    private static readonly string[] Tabs = { "Base", "Final" };
    private static readonly string[] TabsWithClass = { "Base", "Final", "Class" };

    private static readonly FieldInfo PursuitCritField = typeof(AdventurerAgent).GetField("pursuitCrit", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo DashField = typeof(AdventurerAgent).GetField("dashCoroutine", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo TauntTimerField = typeof(AdventurerAgent).GetField("tauntTimer", BindingFlags.Instance | BindingFlags.NonPublic);

    // Live values (HP, dash, pending crit) change every frame — keep the panel current while playing.
    public override bool RequiresConstantRepaint() => Application.isPlaying;

    public override void OnInspectorGUI()
    {
        // Draw the normal inspector first (keeps everything you already have)
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        DrawRuntimeStatsPanel();
    }

    private void DrawRuntimeStatsPanel()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Runtime Stats", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see runtime stats (base → modified).", MessageType.Info);
                return;
            }

            showRuntimeStats = EditorGUILayout.Foldout(showRuntimeStats, "Show", true);
            if (!showRuntimeStats) return;

            var entity = target as EntityBase;
            if (entity == null) return;

            // Try find Stats anywhere on this object (field/property), public or private
            var stats = FindFirstMemberOfType<Stats>(entity);
            if (stats == null)
            {
                EditorGUILayout.HelpBox(
                    "No Stats instance found on this agent.\n\n" +
                    "Wire your stat system by adding a field/property of type Stats (public or private) on the agent " +
                    "or on a base class, and this panel will populate automatically.",
                    MessageType.Warning
                );
                return;
            }

            // A Stats deserialized by Unity (not constructed at runtime) has a non-null BaseStats
            // but a null Mediator/cache, which slips past the BaseStats guard below and NREs on
            // any modified-stat read. Bail before touching it.
            if (stats.Mediator == null)
            {
                EditorGUILayout.HelpBox(
                    "Stats found but not runtime-initialized (no Mediator) — this is a serialized/" +
                    "uninitialized instance, not a live entity. Select a spawned entity in Play Mode.",
                    MessageType.Warning
                );
                return;
            }

            // Buttons
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Force Refresh"))
                {
                    stats.MarkDirty();
                    _ = stats.MoveSpeed; // touch to rebuild cache immediately
                }

                if (GUILayout.Button("Print To Console"))
                {
#if UNITY_EDITOR
                    stats.DebugPrintStats();
#endif
                }
            }

            EditorGUILayout.Space(4);

            var baseStats = stats.BaseStats;
            if (baseStats == null)
            {
                EditorGUILayout.HelpBox("Stats.BaseStats is null (entity may not be initialized yet).", MessageType.Warning);
                return;
            }

            bool hasClass = entity is AdventurerAgent a && a.ClassDef != null;
            var tabs = hasClass ? TabsWithClass : Tabs;
            runtimeTab = GUILayout.Toolbar(Mathf.Min(runtimeTab, tabs.Length - 1), tabs);
            EditorGUILayout.Space(4);

            switch (runtimeTab)
            {
                case 0:
                    DrawKeyValueGrid(
                        ("Move Speed", baseStats.moveSpeed),
                        ("Stop Distance", baseStats.stopDistance),

                        ("Attack Damage", baseStats.attackDamage),
                        ("Attack Interval", baseStats.attackSpeed),
                        ("Attack Range", baseStats.attackRange),
                        ("Chase Break Range", baseStats.chaseBreakRange),
                        ("Scan Range", baseStats.scanRange),

                        ("Carry Capacity", baseStats.carryCapacity),
                        ("Pickup Time", baseStats.pickupTime),
                        ("Deposit Time", baseStats.depositTime)
                    );
                    break;
                case 1:
                    DrawKeyValueGrid(
                        ("Move Speed", stats.MoveSpeed),
                        ("Attack Damage", stats.AttackDamage),
                        ("Attack Interval", stats.AttackInterval),
                        ("Attack Range", stats.AttackRange),
                        ("Chase Break Range", stats.ChaseBreakRange),
                        ("Scan Range", stats.ScanRange),
                        ("Carry Capacity", stats.CarryCapacity),
                        ("Pickup Time", stats.PickupTime),
                        ("Deposit Time", stats.DepositTime)
                    );
                    break;
                case 2:
                    DrawClassStats((AdventurerAgent)entity);
                    break;
            }
        }
    }

    // The tier def's class numbers plus each switched-on signature and its live state (AdventurerDef fields, read-only here).
    private static void DrawClassStats(AdventurerAgent adv)
    {
        var d = adv.ClassDef;
        var hp = adv.GetComponent<Health>();
        DrawTextGrid(("Tier Def", d.name), ("Class", $"{adv.ClassName} ({d.classLine})"), ("Rank", adv.RankDisplay));
        DrawKeyValueGrid(("Damage ×", d.damageMultiplier), ("Max HP ×", d.hpMultiplier),
            ("HP", hp != null ? hp.CurrentHP : 0f), ("Max HP", hp != null ? hp.MaxHP : 0f));

        if (d.cleaveFraction > 0f)
        {
            EditorGUILayout.LabelField("Cleave", EditorStyles.miniBoldLabel);
            DrawKeyValueGrid(("Splash Fraction", d.cleaveFraction), ("Splash Radius", d.cleaveRadius));
        }
        if (d.tauntRadius > 0f || d.damageReduction > 0f)
        {
            EditorGUILayout.LabelField("Hold the Line", EditorStyles.miniBoldLabel);
            DrawKeyValueGrid(("Taunt Radius", d.tauntRadius), ("Taunt Interval", d.tauntInterval),
                ("Next Taunt In", (float)TauntTimerField.GetValue(adv)), ("Damage Reduction", d.damageReduction));
        }
        if (d.critChance > 0f || d.pursuitRange > 0f)
        {
            EditorGUILayout.LabelField("Crits + Pursuit", EditorStyles.miniBoldLabel);
            DrawKeyValueGrid(("Crit Chance", d.critChance), ("Crit ×", d.critMultiplier),
                ("Dash Range", d.pursuitRange), ("Dash Time", d.pursuitDashTime), ("Hop Height", d.pursuitHopHeight));
            DrawTextGrid(("Next Hit Crit", (bool)PursuitCritField.GetValue(adv) ? "yes (pursuit)" : "no"),
                ("Dashing", DashField.GetValue(adv) != null ? "yes" : "no"));
        }
        if (d.maxHpDamage > 0f || d.projectilePrefab != null)
        {
            EditorGUILayout.LabelField("Arcane Weight + Fireball", EditorStyles.miniBoldLabel);
            DrawKeyValueGrid(("% Max-HP Damage", d.maxHpDamage));
            DrawTextGrid(("Projectile", d.projectilePrefab != null ? d.projectilePrefab.name : "none (melee)"));
        }
    }

    private static void DrawTextGrid(params (string label, string value)[] rows)
    {
        foreach (var (label, value) in rows)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.MinWidth(160));
                EditorGUILayout.SelectableLabel(value, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }
    }

    private static void DrawKeyValueGrid(params (string label, float value)[] rows)
    {
        using (new EditorGUILayout.VerticalScope())
        {
            foreach (var (label, value) in rows)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(label, GUILayout.MinWidth(160));
                    EditorGUILayout.SelectableLabel(value.ToString("0.###"), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                }
            }
        }
    }

    private static T FindFirstMemberOfType<T>(object obj) where T : class
    {
        if (obj == null) return null;

        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var type = obj.GetType();

        while (type != null)
        {
            // Fields
            var fields = type.GetFields(flags);
            foreach (var f in fields)
            {
                if (typeof(T).IsAssignableFrom(f.FieldType))
                {
                    return f.GetValue(obj) as T;
                }
            }

            // Properties
            var props = type.GetProperties(flags);
            foreach (var p in props)
            {
                if (!p.CanRead) continue;
                if (typeof(T).IsAssignableFrom(p.PropertyType))
                {
                    try { return p.GetValue(obj) as T; }
                    catch { /* ignored */ }
                }
            }

            type = type.BaseType;
        }

        return null;
    }
}
