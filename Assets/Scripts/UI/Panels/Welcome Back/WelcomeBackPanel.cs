using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// "While you were away" parchment (WelcomeBack.uxml, instanced hidden in MainUI.uxml). Display only — SaveManager
/// owns the award and banks it in the onCollect callback, which gets the Collect button as the coin-shower source.
/// </summary>
public static class WelcomeBackPanel
{
    private const int ShownItems = 3;

    /// <summary>False when no HUD document carries the panel (caller banks directly).</summary>
    public static bool Show(TimeSpan away, int gold, List<(ItemDef item, int qty)> items, Action<VisualElement> onCollect)
    {
        VisualElement root = null;
        foreach (var doc in UnityEngine.Object.FindObjectsByType<UIDocument>())
            if ((root = doc.rootVisualElement?.Q("welcome-back")) != null) break;
        if (root == null) return false;
        var host = root.parent ?? root;   // the hidden full-screen TemplateContainer in MainUI.uxml

        root.Q<Label>("wb-away").text = $"You were gone {(away.TotalHours >= 1 ? $"{(int)away.TotalHours}h {away.Minutes}m" : $"{away.Minutes}m")}";

        root.Q("wb-gold").style.display = gold > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        root.Q<Label>("wb-gold-amount").text = $"+{gold:N0} gold";

        var list = root.Q("wb-items");
        list.Clear();
        for (int i = 0; i < Mathf.Min(ShownItems, items.Count); i++)
        {
            var (item, qty) = items[i];
            var row = new VisualElement();
            row.AddToClassList("wb-row");
            var frame = new VisualElement();
            frame.AddToClassList("wb-icon-frame");
            var icon = new VisualElement();
            icon.AddToClassList("wb-icon");
            icon.style.backgroundImage = new StyleBackground(item.icon != null ? item.icon : item.spriteDrop);
            frame.Add(icon);
            row.Add(frame);
            var label = new Label($"+{qty:N0}  {item.displayName}");
            label.AddToClassList("wb-amount");
            row.Add(label);
            list.Add(row);
        }
        var more = root.Q<Label>("wb-more");
        more.text = $"+ {items.Count - ShownItems} more";
        more.style.display = items.Count > ShownItems ? DisplayStyle.Flex : DisplayStyle.None;

        var button = root.Q<Button>("wb-collect");
        button.SetEnabled(true);
        button.clickable = new Clickable(() =>
        {
            button.SetEnabled(false);
            onCollect(button);                         // coins launch from the button's live bound…
            host.style.display = DisplayStyle.None;    // …then the panel goes
        });

        host.style.display = DisplayStyle.Flex;
        return true;
    }
}
