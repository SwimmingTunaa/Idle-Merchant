using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EntityBase), true)]
public class EntityBaseEditor : Editor
{
    private bool showRuntimeStats = true;
    private bool showBaseStats = true;
    private bool showModifiedStats = true;

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

            showBaseStats = EditorGUILayout.Foldout(showBaseStats, "Base Stats", true);
            if (showBaseStats)
            {
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
            }

            EditorGUILayout.Space(6);

            showModifiedStats = EditorGUILayout.Foldout(showModifiedStats, "Modified Stats (Final)", true);
            if (showModifiedStats)
            {
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
