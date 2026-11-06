using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Contracts page — offer board on the left; the active contract + seal shop on the right.
/// Implements IBookPage; lives inside BookPanelController as tab 6. Every card is cloned once from
/// ContractCard.uxml and bound to a slot through a closure; Refresh re-reads ContractManager.
/// </summary>
public class ContractsPage : MonoBehaviour, IBookPage
{
    [Tooltip("Card template (ContractCard.uxml): icon | ask, progress, reward | action button")]
    [SerializeField] private VisualTreeAsset cardTemplate;

    [Header("Icons")]
    [SerializeField] private Sprite iconHunt;
    [SerializeField] private Sprite iconPatronage;
    [SerializeField] private Sprite iconFavor;
    [SerializeField] private Sprite iconRush;

    public string PageTitle => "Contracts";

    private readonly List<Action> refreshers = new List<Action>();
    private Label rotationLabel, sealBalance, activeEmpty;
    private IVisualElementScheduledItem ticker;
    private MilestoneTracker coinShower;   // HUD tracker owns the UI coin flight to the gold counter

    void Awake() => hideFlags = HideFlags.HideInInspector;

    // ═════════════════════════════════════════════
    // IBOOK PAGE
    // ═════════════════════════════════════════════

    public void OnPageShown(VisualElement leftPage, VisualElement rightPage)
    {
        var cm = ContractManager.Instance;
        if (cm == null || cardTemplate == null) return;
        coinShower = FindAnyObjectByType<MilestoneTracker>();
        refreshers.Clear();

        rotationLabel = leftPage.Q<Label>("rotation-label");
        var offerList = leftPage.Q<VisualElement>("offer-list");
        offerList.Clear();
        for (int i = 0; i < ContractManager.OfferSlots; i++) BindOffer(offerList, i);

        var notices = leftPage.Q<VisualElement>("notice-list");
        if (notices != null && EventManager.Instance != null)
        {
            notices.Clear();
            foreach (var ad in EventManager.Instance.Ads) BindNotice(notices, ad);
        }

        activeEmpty = rightPage.Q<Label>("active-empty");
        var activeSlot = rightPage.Q<VisualElement>("active-slot");
        activeSlot.Clear();
        BindActive(activeSlot);

        sealBalance = rightPage.Q<Label>("seal-balance");
        var shop = rightPage.Q<VisualElement>("shop-list");
        shop.Clear();
        BindShopItem(shop, "Recruitment Favor", "A promoted recruit tops the hiring stack.", iconFavor, cm.FavorCost, cm.BuyRecruitmentFavor);
        BindShopItem(shop, "Rush Order", "Every hiring pool restocks now.", iconRush, cm.RushCost, cm.BuyRushOrder);

        GameSignals.OnContractAccepted += OnContractChanged;
        GameSignals.OnContractCompleted += OnContractChanged;
        GameSignals.OnSealsChanged += OnSealsChanged;
        // Live bits (stock for Fulfil, counters, rotation clock) — polled, not driven per item/kill
        ticker = leftPage.schedule.Execute(Refresh).Every(500);
        Refresh();
    }

    public void OnPageHidden()
    {
        GameSignals.OnContractAccepted -= OnContractChanged;
        GameSignals.OnContractCompleted -= OnContractChanged;
        GameSignals.OnSealsChanged -= OnSealsChanged;
        ticker?.Pause();
        ticker = null;
        refreshers.Clear();
        coinShower = null;
    }

    private void OnContractChanged(Contract _) => Refresh();
    private void OnSealsChanged(int _) => Refresh();

    private void Refresh()
    {
        var cm = ContractManager.Instance;
        if (cm == null) return;
        int secs = Mathf.Max(0, Mathf.CeilToInt(cm.RotationLeft));
        if (rotationLabel != null) rotationLabel.text = $"New offers in {secs / 60}:{secs % 60:00}";
        if (sealBalance != null) sealBalance.text = cm.Seals.ToString("N0");
        foreach (var r in refreshers) r();
    }

    // ═════════════════════════════════════════════
    // CARDS
    // ═════════════════════════════════════════════

    private VisualElement NewCard(VisualElement list)
    {
        var tc = cardTemplate.CloneTree();
        list.Add(tc);
        return tc.Q<VisualElement>("card-root");
    }

    private static void Show(VisualElement e, bool on) => e.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;

    // Ask, client line, icon and reward preview shared by offers and the active card
    private void BindContract(VisualElement card, Contract c)
    {
        card.Q<Label>("card-name").text = c.Title;
        card.Q<Label>("card-desc").text = c.Client;
        card.Q<Label>("reward-gold").text = AnimatedNumberUITK.Abbrev(c.gold);
        card.Q<Label>("reward-seals").text = c.seals.ToString();
        var icon = c.kind == ContractKind.Deliver ? c.itemDef?.icon : c.kind == ContractKind.Hunt ? iconHunt : iconPatronage;
        card.Q<VisualElement>("card-icon").style.backgroundImage = icon != null ? new StyleBackground(icon) : new StyleBackground();
    }

    private void BindOffer(VisualElement list, int slot)
    {
        var cm = ContractManager.Instance;
        var card = NewCard(list);
        Show(card.Q<VisualElement>("card-bar"), false);
        Show(card.Q<VisualElement>("card-action-seal"), false);
        var button = card.Q<Button>("card-action");
        card.Q<Label>("card-action-label").text = "Accept";
        button.clicked += () => { SfxManager.Instance?.Play(SfxId.UiTick); cm.Accept(slot); Refresh(); };

        refreshers.Add(() =>
        {
            var c = cm.GetOffer(slot);
            Show(card.parent, c != null);   // the TemplateContainer — taken slots refill on rotation
            if (c == null) return;
            BindContract(card, c);
            button.SetEnabled(cm.Active == null);   // one active contract at a time
        });
    }

    private void BindActive(VisualElement slot)
    {
        var cm = ContractManager.Instance;
        var card = NewCard(slot);
        Show(card.Q<VisualElement>("card-action-seal"), false);
        var fill = card.Q<VisualElement>("progress-fill");
        var progressLabel = card.Q<Label>("progress-label");
        var button = card.Q<Button>("card-action");
        var buttonLabel = card.Q<Label>("card-action-label");

        button.clicked += () =>
        {
            var a = cm.Active;
            if (a == null) return;
            SfxManager.Instance?.Play(SfxId.UiTick);
            if (a.complete)
            {
                button.SetEnabled(false);   // no double-claims; the card hides once Active clears
                cm.Claim(MilestoneTracker.BurstOrigin(button),
                    (count, land) => coinShower != null && coinShower.LaunchCoins(button, count, land));
            }
            else cm.Fulfil();
            Refresh();
        };

        refreshers.Add(() =>
        {
            var a = cm.Active;
            Show(card.parent, a != null);
            if (activeEmpty != null) Show(activeEmpty, a == null);
            if (a == null) return;

            BindContract(card, a);
            int p = cm.ProgressOf(a);
            fill.style.width = Length.Percent(100f * p / Mathf.Max(1, a.target));
            progressLabel.text = $"{AnimatedNumberUITK.Abbrev(p)} / {AnimatedNumberUITK.Abbrev(a.target)}";
            card.EnableInClassList("contract-card--claimable", a.complete || cm.CanFulfil);
            // Deliver: Fulfil (enabled once stock covers it) → Claim. Counters: button only once complete.
            Show(button, a.complete || a.kind == ContractKind.Deliver);
            buttonLabel.text = a.complete ? "Claim" : "Fulfil";
            button.SetEnabled(a.complete || cm.CanFulfil);
        });
    }

    // ═════════════════════════════════════════════
    // NOTICES — player-posted ads (EventManager.Ads): ◀ pick ▶ an audience / an item, Post for gold
    // ═════════════════════════════════════════════

    private void BindNotice(VisualElement list, EventDef ad)
    {
        var em = EventManager.Instance;
        // Compact ledger row (the page has no height left for two more cards): icon | name | ◀ pick ▶ | Post
        var row = new VisualElement(); row.AddToClassList("contracts-notice");
        var iconEl = new VisualElement { pickingMode = PickingMode.Ignore }; iconEl.AddToClassList("contracts-notice-icon");
        var name = new Label(ad.displayName) { pickingMode = PickingMode.Ignore }; name.AddToClassList("contracts-notice-name");
        var prev = new Button { text = "<" }; prev.AddToClassList("btn-secondary"); prev.AddToClassList("contracts-picker-btn");
        var pick = new Label { pickingMode = PickingMode.Ignore }; pick.AddToClassList("contracts-picker-label");
        var next = new Button { text = ">" }; next.AddToClassList("btn-secondary"); next.AddToClassList("contracts-picker-btn");
        var button = new Button(); button.AddToClassList("btn-success"); button.AddToClassList("contracts-notice-post");
        var buttonLabel = new Label { pickingMode = PickingMode.Ignore }; buttonLabel.AddToClassList("btn-label"); buttonLabel.AddToClassList("contracts-notice-post-label");
        var coin = new VisualElement { pickingMode = PickingMode.Ignore }; coin.AddToClassList("btn-coin"); coin.AddToClassList("contracts-post-coin");
        button.Add(buttonLabel); button.Add(coin);
        row.Add(iconEl); row.Add(name); row.Add(prev); row.Add(pick); row.Add(next); row.Add(button);
        list.Add(row);

        bool crowd = ad.kind == EventKind.Advertise;
        var customers = new List<CustomerDef>();
        var items = new List<ItemDef>();
        int index = 0;
        int Count() => crowd ? customers.Count : items.Count;
        prev.clicked += () => { if (Count() > 0) { index = (index - 1 + Count()) % Count(); Refresh(); } };
        next.clicked += () => { if (Count() > 0) { index = (index + 1) % Count(); Refresh(); } };
        button.clicked += () =>
        {
            if (Count() == 0) return;
            SfxManager.Instance?.Play(SfxId.UiTick);
            bool ok = crowd ? em.TryPost(ad, customer: customers[index]) : em.TryPost(ad, item: items[index]);
            if (ok) EventManager.Announce(QueueController.Instance != null ? QueueController.Instance.counterPoint : null, "Notice posted!");
            Refresh();
        };

        refreshers.Add(() =>
        {
            // Options: walk-in defs past their star gate / sellable items in stock (re-read: stars, stock and sale marks move)
            if (crowd)
            {
                var spawner = FindAnyObjectByType<CustomerSpawner>();
                customers.Clear(); if (spawner != null) customers.AddRange(spawner.AdvertisableDefs());
            }
            else
            {
                items.Clear();
                var sm = SalesManager.Instance;
                if (sm != null && Inventory.Instance != null)
                    foreach (var r in Inventory.Instance.SnapshotAll())
                        if (sm.CanSell(r.item) && sm.GetAvailableForSale(r.item) > 0) items.Add(r.item);
            }
            int n = Count();
            if (n == 0) { index = 0; pick.text = crowd ? "Nobody to call" : "Nothing for sale"; iconEl.style.backgroundImage = new StyleBackground(); }
            else
            {
                index = Mathf.Clamp(index, 0, n - 1);
                Sprite icon = crowd ? customers[index].portrait : items[index].icon;
                iconEl.style.backgroundImage = icon != null ? new StyleBackground(icon) : new StyleBackground();
                pick.text = crowd ? customers[index].displayName
                                  : $"{items[index].displayName} ({SalesManager.Instance.GetAvailableForSale(items[index])})";
            }

            var running = em.ActiveOf(ad);
            float cd = em.CooldownLeft(ad);
            bool canPick = running == null && n > 1;
            prev.SetEnabled(canPick); next.SetEnabled(canPick);
            if (running != null)
            {
                int secs = Mathf.CeilToInt(running.remaining);
                pick.text = running.note;
                iconEl.style.backgroundImage = running.Icon != null ? new StyleBackground(running.Icon) : new StyleBackground();
                buttonLabel.text = $"{secs / 60}:{secs % 60:00}"; Show(coin, false); button.SetEnabled(false);
            }
            else if (cd > 0f)
            {
                int secs = Mathf.CeilToInt(cd);
                buttonLabel.text = $"{secs / 60}:{secs % 60:00}"; Show(coin, false); button.SetEnabled(false);
            }
            else
            {
                int cost = em.CostOf(ad);
                buttonLabel.text = AnimatedNumberUITK.Abbrev(cost); Show(coin, true);
                button.SetEnabled(n > 0 && Inventory.Instance != null && Inventory.Instance.CanAfford(cost));
            }
        });
    }

    private void BindShopItem(VisualElement list, string title, string desc, Sprite icon, int cost, Func<bool> buy)
    {
        var cm = ContractManager.Instance;
        var card = NewCard(list);
        card.Q<Label>("card-name").text = title;
        card.Q<Label>("card-desc").text = desc;
        if (icon != null) card.Q<VisualElement>("card-icon").style.backgroundImage = new StyleBackground(icon);
        Show(card.Q<VisualElement>("card-bar"), false);
        Show(card.Q<VisualElement>("card-reward"), false);
        card.Q<Label>("card-action-label").text = cost.ToString();
        var button = card.Q<Button>("card-action");
        button.clicked += () => { SfxManager.Instance?.Play(SfxId.UiTick); buy(); Refresh(); };
        refreshers.Add(() => button.SetEnabled(cm.Seals >= cost));
    }
}
