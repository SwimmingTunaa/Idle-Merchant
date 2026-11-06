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
    private struct Card { public VisualElement fill; public Label effect, left; public System.Func<float> progress; public System.Func<string> note, count; }
    private readonly System.Collections.Generic.List<Card> cards = new();

    void Start()
    {
        root = uiDocument != null ? uiDocument.rootVisualElement?.Q("event-tracker") : null;
        list = root?.Q("ev-list");
        if (list == null) { Debug.LogWarning("[EventTracker] event-tracker / ev-list not found in MainUI"); enabled = false; return; }
        GameSignals.OnEventsChanged += Rebuild;
        GameSignals.OnCaravanChanged += Rebuild;
        Rebuild();
    }

    void OnDestroy() { GameSignals.OnEventsChanged -= Rebuild; GameSignals.OnCaravanChanged -= Rebuild; }

    private VisualElement AddCard(Sprite icon, string name, string effect, System.Action onClick, System.Func<float> progress, System.Func<string> note, System.Func<string> count)
    {
        var card = new Button { name = "ev-card" };
        card.AddToClassList("ev-card");
        card.clicked += onClick;
        var iconEl = new VisualElement(); iconEl.AddToClassList("ev-icon");
        if (icon != null) iconEl.style.backgroundImage = new StyleBackground(icon);
        card.Add(iconEl);
        var text = new VisualElement(); text.AddToClassList("ev-text");
        var nameL = new Label(name); nameL.AddToClassList("ev-name"); text.Add(nameL);
        var effectL = new Label(effect); effectL.AddToClassList("ev-effect"); text.Add(effectL);
        var track = new VisualElement(); track.AddToClassList("progress-track"); track.AddToClassList("progress-track--sm"); track.AddToClassList("ev-track");
        var fill = new VisualElement(); fill.AddToClassList("progress-fill"); fill.AddToClassList("ev-fill"); track.Add(fill);
        text.Add(track);
        card.Add(text);
        Label left = null;
        if (count != null) { left = new Label(); left.AddToClassList("ev-left"); card.Add(left); }
        list.Add(card);
        cards.Add(new Card { fill = fill, effect = effectL, left = left, progress = progress, note = note, count = count });
        return card;
    }

    private void Rebuild()
    {
        list.Clear(); cards.Clear();
        var em = EventManager.Instance;
        if (em == null) return;
        foreach (var e in em.Active)
        {
            var ev = e;
            AddCard(e.Icon, e.def.displayName, e.def.effectLine, () => em.FocusOn(ev),
                    () => 1f - ev.Progress01,
                    () => string.IsNullOrEmpty(ev.note) ? ev.def.effectLine : ev.note,
                    e.partySize > 0 ? () => $"{ev.PartyLeft} left" : null);
        }
        // The caravan keeps its own clock but rides the same list, so it's visible from down a depth
        var cv = CaravanManager.Instance;
        if (cv != null && cv.Present)
            AddCard(cv.Parked, "Caravan", $"{CaravanManager.SealReward} seals for {cv.Qty} {cv.Item.displayName}",
                    () => { if (CameraScrollController.Instance != null) CameraScrollController.Instance.LockOnto(cv.transform); },   // it drives: follow it
                    () => cv.LeaveIn / CaravanManager.StayFor, null, null);
        root.style.display = cards.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
    }

    void Update()
    {
        foreach (var c in cards)
        {
            c.fill.style.width = Length.Percent(Mathf.Clamp01(c.progress()) * 100f);
            if (c.note != null) { string n = c.note(); if (c.effect.text != n) c.effect.text = n; }
            if (c.left != null) c.left.text = c.count();
        }
    }
}
