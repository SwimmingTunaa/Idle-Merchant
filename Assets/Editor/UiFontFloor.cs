using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Guards the body-font floor from GlobalVariables.uss: no font-size under 18px in any USS/UXML under Scripts/UI
/// (px or a var() token resolved from GlobalVariables). Runs on every USS/UXML import and from Tools/UI/Check Font Floor.
/// Legacy files still on the 16px BrightLinger scale are allow-listed; drop a file from the list once it's migrated.
/// </summary>
public class UiFontFloor : AssetPostprocessor
{
    public const int FloorPx = 18;
    private const string Root = "Assets/Scripts/UI";
    private const string Tokens = Root + "/GlobalVariables.uss";

    // ponytail: pending migration to --font-size-* (CLAUDE.md "New pages use…"); shrink as pages are migrated
    private static readonly HashSet<string> Legacy = new()
    {
        Root + "/Components/Badge.uss", Root + "/Components/Checkbox.uss", Root + "/Components/Divider.uss",
        Root + "/Components/ProgressBar.uss", Root + "/MainUI.uss", Root + "/Panels/Book/BookPanel.uss",
        Root + "/Panels/Book/Settings_Page.uxml", Root + "/Panels/Book/Settings_Right.uxml",
        Root + "/Panels/Contracts/ContractsPageStyle.uss", Root + "/Panels/Cursor/CursorPageStyle.uss",
        Root + "/Panels/Guild Panel/MilestoneCard.uss", Root + "/Panels/Inventory/InventoryPanel.uss",
        Root + "/Panels/Inventory/ItemSlot.uxml", Root + "/Panels/Roster/RosterCard.uxml", Root + "/Panels/Roster/RosterPanel.uss",
    };

    private static readonly Regex Size = new(@"font-size:\s*(?:(\d+)px|var\(--([\w-]+)\))");
    private static readonly Regex Token = new(@"--([\w-]+):\s*(\d+)px");

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        var styles = imported.Where(p => p.StartsWith(Root) && (p.EndsWith(".uss") || p.EndsWith(".uxml"))).ToList();
        if (styles.Count > 0) Check(styles);
    }

    [MenuItem("Tools/UI/Check Font Floor")]
    public static void CheckAll() =>
        Check(Directory.GetFiles(Root, "*.*", SearchOption.AllDirectories)
                       .Where(p => p.EndsWith(".uss") || p.EndsWith(".uxml")).Select(p => p.Replace('\\', '/')).ToList());

    private static void Check(List<string> files)
    {
        var tokens = Token.Matches(File.ReadAllText(Tokens)).ToDictionary(m => m.Groups[1].Value, m => int.Parse(m.Groups[2].Value));
        int bad = 0;
        foreach (var file in files)
        {
            var sizes = Size.Matches(File.ReadAllText(file))
                .Select(m => m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : tokens.GetValueOrDefault(m.Groups[2].Value, 99))
                .Where(px => px < FloorPx).ToList();
            if (sizes.Count == 0)
            {
                if (Legacy.Contains(file)) Debug.Log($"[UiFontFloor] {file} is clean — remove it from UiFontFloor.Legacy");
                continue;
            }
            if (Legacy.Contains(file)) continue;
            bad++;
            Debug.LogError($"[UiFontFloor] {file}: font-size {string.Join(", ", sizes)}px is under the {FloorPx}px floor — use --font-size-caption/-small/-body", AssetDatabase.LoadMainAssetAtPath(file));
        }
        if (files.Count > 1) Debug.Log($"[UiFontFloor] checked {files.Count} files, {bad} under the floor");
    }
}
