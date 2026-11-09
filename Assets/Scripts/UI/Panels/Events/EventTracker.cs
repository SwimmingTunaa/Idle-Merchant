using System.Linq;
using System.Collections.Generic;
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
        BuildRush();
        Rebuild();
    }

    void OnDestroy() { GameSignals.OnEventsChanged -= Rebuild; GameSignals.OnCaravanChanged -= Rebuild; }

    private VisualElement AddCard(Sprite icon, string name, string effect, System.Action onClick, System.Func<float> progress, System.Func<string> note, System.Func<string> count)
    {
        var card = new Button { name = "ev-card" };
        card.AddToClassList("ev-card");
        card.clicked += () => { TutorialManager.Flag("evcard"); onClick(); };
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

    private readonly HashSet<EventManager.ActiveEvent> seen = new();   // cards already shown: a new event's card pops in

    private void Rebuild()
    {
        list.Clear(); cards.Clear();
        var em = EventManager.Instance;
        if (em == null) return;
        foreach (var e in em.Active)
        {
            var ev = e;
            var card = AddCard(e.Icon, e.def.displayName, e.def.effectLine, () => em.FocusOn(ev),
                    () => 1f - ev.Progress01,
                    () => string.IsNullOrEmpty(ev.note) ? ev.def.effectLine : ev.note,
                    e.partySize > 0 ? () => $"{ev.PartyLeft} left" : null);
            if (seen.Add(ev))
            {
                card.AddToClassList("ev-card--new");   // held big + bright for a beat, then the .ev-card transition eases it down
                card.schedule.Execute(() => card.RemoveFromClassList("ev-card--new")).ExecuteLater(220);
            }
        }
        seen.RemoveWhere(x => !em.Active.Contains(x));
        // The caravan keeps its own clock but rides the same list, so it's visible from down a depth
        var cv = CaravanManager.Instance;
        if (cv != null && cv.Present)
            AddCard(cv.Parked, "Caravan", $"{CaravanManager.SealReward} seals for {cv.Qty} {cv.Item.displayName}",
                    () => { if (CameraScrollController.Instance != null) CameraScrollController.Instance.LockOnto(cv.transform); },   // it drives: follow it
                    () => cv.LeaveIn / CaravanManager.StayFor, null, null);
        root.style.display = cards.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        rushOn = em.RushHour;
    }

    void Update()
    {
        foreach (var c in cards)
        {
            c.fill.style.width = Length.Percent(Mathf.Clamp01(c.progress()) * 100f);
            if (c.note != null) { string n = c.note(); if (c.effect.text != n) c.effect.text = n; }
            if (c.left != null) c.left.text = c.count();
        }
        TickRush();
    }

    // ═════════════════════════════════════════════
    // RUSH HOUR — gold frame + coins raining down both screen edges (centre stays clear, nothing picks)
    // ═════════════════════════════════════════════

    private const int RushCoins = 36;
    private const float RushBand = 0.13f, RushFade = 0.5f, CoinSize = 26f;
    private VisualElement rushHost, rushFrame;
    private struct RushCoin { public VisualElement el; public float x, y, speed, spin, phase; }
    private readonly List<RushCoin> rushCoins = new();
    private bool rushOn;
    private float rushAlpha;

    private void BuildRush()
    {
        var docRoot = uiDocument.rootVisualElement;
        var coin = docRoot.Q("coin-icon");
        rushHost = new VisualElement { name = "rush-hour", pickingMode = PickingMode.Ignore };
        rushHost.AddToClassList("rush-host");
        rushFrame = new VisualElement { pickingMode = PickingMode.Ignore };
        rushFrame.AddToClassList("rush-frame");
        rushHost.Add(rushFrame);
        var inner = new VisualElement { pickingMode = PickingMode.Ignore };
        inner.AddToClassList("rush-frame--inner");
        rushHost.Add(inner);
        for (int i = 0; i < RushCoins; i++)
        {
            var el = new VisualElement { pickingMode = PickingMode.Ignore };
            el.AddToClassList("rush-coin");
            if (coin != null) el.style.backgroundImage = coin.resolvedStyle.backgroundImage;
            rushHost.Add(el);
            rushCoins.Add(new RushCoin { el = el, x = Random.value, y = Random.value, speed = Random.Range(0.18f, 0.34f), spin = Random.Range(-240f, 240f), phase = Random.value * 6.28f });
        }
        rushHost.style.display = DisplayStyle.None;
        docRoot.Insert(0, rushHost);   // first child: over the world, under every other HUD element
    }

    private void TickRush()
    {
        if (rushHost == null) return;
        float target = rushOn ? 1f : 0f;
        if (rushAlpha == target && !rushOn) return;
        rushAlpha = Mathf.MoveTowards(rushAlpha, target, Time.unscaledDeltaTime / RushFade);
        rushHost.style.display = rushAlpha > 0f ? DisplayStyle.Flex : DisplayStyle.None;
        if (rushAlpha <= 0f) return;
        rushHost.style.opacity = rushAlpha;
        float now = Time.unscaledTime;
        rushFrame.style.opacity = 0.7f + 0.3f * Mathf.Sin(now * 5f);   // the frame breathes
        var size = rushHost.layout.size;
        if (size.x <= 0f) return;
        float band = size.x * RushBand;
        for (int i = 0; i < rushCoins.Count; i++)
        {
            var c = rushCoins[i];
            c.y += c.speed * Time.unscaledDeltaTime;
            if (c.y > 1.05f) { c.y = -0.05f; c.x = Random.value; }
            rushCoins[i] = c;
            bool left = i % 2 == 0;
            float x = left ? c.x * band : size.x - band + c.x * band;
            x += Mathf.Sin(now * 2f + c.phase) * 8f;   // a little drift
            c.el.style.translate = new Translate(x - CoinSize * 0.5f, c.y * size.y - CoinSize * 0.5f);
            c.el.style.rotate = new Rotate(now * c.spin + c.phase * 57f);
        }
    }
}
