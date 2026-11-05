using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// HUD list of running events (left edge, mirrors the milestone tracker): one plank card per
/// EventManager.ActiveEvent — icon, name, effect line, draining time bar, "N left" for parties.
/// Rebuilt on OnEventsChanged; bars tick every frame. Click = camera jump to the event.
/// </summary>
public class EventTracker : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private VisualElement root, list;
    private readonly System.Collections.Generic.List<(EventManager.ActiveEvent e, VisualElement fill, Label left)> cards = new();

    void Start()
    {
        root = uiDocument != null ? uiDocument.rootVisualElement?.Q("event-tracker") : null;
        list = root?.Q("ev-list");
        if (list == null) { Debug.LogWarning("[EventTracker] event-tracker / ev-list not found in MainUI"); enabled = false; return; }
        GameSignals.OnEventsChanged += Rebuild;
        Rebuild();
    }

    void OnDestroy() { GameSignals.OnEventsChanged -= Rebuild; }

    private void Rebuild()
    {
        list.Clear(); cards.Clear();
        var em = EventManager.Instance;
        if (em == null) return;
        foreach (var e in em.Active)
        {
            var card = new Button { name = "ev-card" };
            card.AddToClassList("ev-card");
            var ev = e;
            card.clicked += () => em.FocusOn(ev);

            var icon = new VisualElement(); icon.AddToClassList("ev-icon");
            if (e.def.icon != null) icon.style.backgroundImage = new StyleBackground(e.def.icon);
            card.Add(icon);

            var text = new VisualElement(); text.AddToClassList("ev-text");
            var name = new Label(e.def.displayName); name.AddToClassList("ev-name"); text.Add(name);
            var effect = new Label(e.def.effectLine); effect.AddToClassList("ev-effect"); text.Add(effect);
            var track = new VisualElement(); track.AddToClassList("progress-track"); track.AddToClassList("progress-track--sm"); track.AddToClassList("ev-track");
            var fill = new VisualElement(); fill.AddToClassList("progress-fill"); fill.AddToClassList("ev-fill"); track.Add(fill);
            text.Add(track);
            card.Add(text);

            Label left = null;
            if (e.partySize > 0) { left = new Label(); left.AddToClassList("ev-left"); card.Add(left); }

            list.Add(card);
            cards.Add((e, fill, left));
        }
        root.style.display = cards.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
    }

    void Update()
    {
        foreach (var (e, fill, left) in cards)
        {
            fill.style.width = Length.Percent(Mathf.Clamp01(1f - e.Progress01) * 100f);
            if (left != null) left.text = $"{e.PartyLeft} left";
        }
    }
}
