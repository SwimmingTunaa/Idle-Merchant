using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Every customer look is an AnimatorOverrideController cloned from an older one. A clone that keeps pairs keyed on
/// clips the base controller no longer has is silently dropped by Unity in play mode whenever the stale override side
/// resolves to a real clip, and the look plays the base (Miner) sheet instead. Found 2026-11-06: every Humans /
/// Dwarves / Elves / Pirates / Royals look. The check reads the raw serialized pair list: GetOverrides() returns one
/// entry per base clip and never shows a stale pair, and an edit-mode Animator plays the override fine, so neither
/// catches it.
/// </summary>
public class CustomerLookTests
{
    private const string LooksFolder = "Assets/Prefabs/Entities/Customers";

    [Test]
    public void EveryCustomerLookKeysOnlyOnBaseControllerClips()
    {
        var guids = AssetDatabase.FindAssets("t:AnimatorOverrideController", new[] { LooksFolder });
        Assert.That(guids, Is.Not.Empty, $"no override controllers under {LooksFolder}");

        var failures = new List<string>();
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var look = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (look == null || look.runtimeAnimatorController == null)
            {
                failures.Add($"{path}: no base controller");
                continue;
            }

            var baseClips = new HashSet<AnimationClip>(look.runtimeAnimatorController.animationClips);
            var clips = new SerializedObject(look).FindProperty("m_Clips");
            int overrides = 0;
            for (int i = 0; i < clips.arraySize; i++)
            {
                var pair = clips.GetArrayElementAtIndex(i);
                var original = pair.FindPropertyRelative("m_OriginalClip").objectReferenceValue as AnimationClip;
                var replacement = pair.FindPropertyRelative("m_OverrideClip").objectReferenceValue as AnimationClip;
                if (original == null || !baseClips.Contains(original))
                    failures.Add($"{look.name}: pair {i} keyed on '{(original ? original.name : "missing")}', not a clip of {look.runtimeAnimatorController.name}");
                else if (replacement != null && AssetDatabase.GetAssetPath(replacement) != AssetDatabase.GetAssetPath(original))
                    overrides++;
            }
            if (overrides == 0) failures.Add($"{look.name}: overrides nothing (plays the base sheet)");
        }

        Assert.That(failures, Is.Empty, string.Join("\n", failures));
    }
}
