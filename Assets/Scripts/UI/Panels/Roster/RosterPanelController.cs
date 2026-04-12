using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Roster page — shows all hired adventurers with XP progress, rank badge, and a promote button.
/// Implements IBookPage; lives inside BookPanelController as tab 0.
/// </summary>
public class RosterPanelController : MonoBehaviour, IBookPage
{
    // agent → card mapping for O(1) targeted refresh
    private readonly Dictionary<AdventurerAgent, VisualElement> agentCards = new();
    private VisualElement rosterList;

    public string PageTitle => "Roster";

    void Awake() => hideFlags = HideFlags.HideInInspector;

    // ═════════════════════════════════════════════
    // IBOOK PAGE
    // ═════════════════════════════════════════════

    public void OnPageShown(VisualElement leftPage, VisualElement rightPage)
    {
        rosterList = leftPage.Q<VisualElement>("roster-list");

        GameSignals.OnAdventurerPromoted += OnAdventurerPromoted;

        RebuildRoster();
    }

    public void OnPageHidden()
    {
        GameSignals.OnAdventurerPromoted -= OnAdventurerPromoted;
        rosterList = null;
        agentCards.Clear();
    }

    // ═════════════════════════════════════════════
    // ROSTER
    // ═════════════════════════════════════════════

    private void RebuildRoster()
    {
        if (rosterList == null) return;
        rosterList.Clear();
        agentCards.Clear();

        var managers = FindObjectsByType<AdventurerManager>(FindObjectsSortMode.None);
        foreach (var manager in managers)
        {
            foreach (var agent in manager.GetAllAdventurers())
            {
                if (agent == null) continue;
                var card = BuildCard(agent);
                rosterList.Add(card);
                agentCards[agent] = card;
            }
        }
    }

    private VisualElement BuildCard(AdventurerAgent agent)
    {
        var card = new VisualElement();
        card.AddToClassList("adventurer-card");

        var nameLabel = new Label();
        nameLabel.name = "adventurer-name";
        nameLabel.AddToClassList("adventurer-name");
        card.Add(nameLabel);

        var rankLabel = new Label();
        rankLabel.name = "adventurer-rank";
        rankLabel.AddToClassList("adventurer-rank");
        card.Add(rankLabel);

        var xpRow = new VisualElement();
        xpRow.AddToClassList("xp-row");

        var xpBarBg = new VisualElement();
        xpBarBg.AddToClassList("xp-bar-bg");
        var xpFill = new VisualElement();
        xpFill.name = "xp-bar-fill";
        xpFill.AddToClassList("xp-bar-fill");
        xpBarBg.Add(xpFill);
        xpRow.Add(xpBarBg);

        var xpLabel = new Label();
        xpLabel.name = "xp-label";
        xpLabel.AddToClassList("xp-label");
        xpRow.Add(xpLabel);
        card.Add(xpRow);

        var promoteBtn = new Button();
        promoteBtn.name = "promote-button";
        promoteBtn.text = "Promote";
        promoteBtn.AddToClassList("promote-button");
        promoteBtn.clicked += () => TryPromote(agent, card);
        card.Add(promoteBtn);

        RefreshCard(card, agent);
        return card;
    }

    private void RefreshCard(VisualElement card, AdventurerAgent agent)
    {
        var nameLabel = card.Q<Label>("adventurer-name");
        if (nameLabel != null)
        {
            var id = agent.GetComponent<IdentityComponent>();
            nameLabel.text = (id != null && id.Identity != null)
                ? id.Identity.DisplayName
                : agent.gameObject.name;
        }

        var rankLabel = card.Q<Label>("adventurer-rank");
        if (rankLabel != null)
            rankLabel.text = $"Rank {agent.CurrentRank}";

        bool atMax = agent.XPForNextRank == float.MaxValue;
        float xpRatio = atMax ? 1f : Mathf.Clamp01(agent.CurrentXP / agent.XPForNextRank);

        var xpFill = card.Q<VisualElement>("xp-bar-fill");
        if (xpFill != null)
            xpFill.style.width = Length.Percent(xpRatio * 100f);

        var xpLabel = card.Q<Label>("xp-label");
        if (xpLabel != null)
            xpLabel.text = atMax ? "Max Rank" : $"{agent.CurrentXP:F0} / {agent.XPForNextRank:F0} XP";

        var promoteBtn = card.Q<Button>("promote-button");
        if (promoteBtn != null)
            promoteBtn.style.display = agent.CanPromote ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void TryPromote(AdventurerAgent agent, VisualElement card)
    {
        if (!agent.CanPromote) return;
        agent.Promote();
        RefreshCard(card, agent);
    }

    // ═════════════════════════════════════════════
    // SIGNALS
    // ═════════════════════════════════════════════

    private void OnAdventurerPromoted(EntityBase entity, string oldRole, string newRole)
    {
        if (entity is AdventurerAgent agent && agentCards.TryGetValue(agent, out var card))
            RefreshCard(card, agent);
    }
}
