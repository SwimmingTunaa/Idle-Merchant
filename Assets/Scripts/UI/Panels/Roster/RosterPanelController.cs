using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Roster page — adventurers grouped by dungeon layer on the left, full detail panel on the right.
/// Implements IBookPage; lives inside BookPanelController as tab 0.
/// Live XP/HP updates via GameSignals.OnAdventurerXPChanged.
/// </summary>
public class RosterPanelController : MonoBehaviour, IBookPage
{
    [SerializeField] private VisualTreeAsset slotTemplate;
    [SerializeField] private HireController hireController;

    [Tooltip("Optional per-rank gem sprites (index 0 = Wood … 4 = Gold). Falls back to the slot's placeholder when empty.")]
    [SerializeField] private Sprite[] rankBadgeSprites;

    private static readonly string[] LayerNames =
    {
        "",                 // index 0 unused
        "Abandoned Mines",
        "Deep Caverns",
        "Crystal Hollows",
        "Cursed Crypts",
        "Infernal Depths",
    };

    // agent → slot wrapper for O(1) targeted refresh
    private readonly Dictionary<AdventurerAgent, VisualElement> agentSlots = new();
    private AdventurerAgent selectedAgent;
    private VisualElement leftContent;
    private VisualElement rightContent;

    public string PageTitle => "Roster";

    // ═════════════════════════════════════════════
    // IBOOK PAGE
    // ═════════════════════════════════════════════

    public void OnPageShown(VisualElement leftPage, VisualElement rightPage)
    {
        leftContent = leftPage;
        rightContent = rightPage;
        GameSignals.OnAdventurerXPChanged += OnAdventurerXPChanged;
        GameSignals.OnAdventurerPromoted += OnAdventurerPromoted;
        GameSignals.OnUnitHired += OnUnitHired;
        RebuildRoster(leftPage);
        ShowDetail(null);
    }

    public void OnPageHidden()
    {
        GameSignals.OnAdventurerXPChanged -= OnAdventurerXPChanged;
        GameSignals.OnAdventurerPromoted -= OnAdventurerPromoted;
        GameSignals.OnUnitHired -= OnUnitHired;
        agentSlots.Clear();
        selectedAgent = null;
        leftContent = null;
        rightContent = null;
    }

    // ═════════════════════════════════════════════
    // ROSTER BUILD
    // ═════════════════════════════════════════════

    private void RebuildRoster(VisualElement leftPage)
    {
        var layersContainer = leftPage.Q<VisualElement>("layers-container");
        if (layersContainer == null) return;

        layersContainer.Clear();
        agentSlots.Clear();

        var managers = FindObjectsByType<AdventurerManager>(FindObjectsSortMode.None)
            .OrderBy(m => m.LayerIndex)
            .ToList();

        foreach (var manager in managers)
            layersContainer.Add(BuildLayerSection(manager));
    }

    private VisualElement BuildLayerSection(AdventurerManager manager)
    {
        var section = new VisualElement();
        section.AddToClassList("layer-section");

        // Header: flourish line · centered layer name · mirrored flourish line
        var header = new VisualElement();
        header.AddToClassList("layer-header");

        var lineLeft = new VisualElement();
        lineLeft.AddToClassList("layer-deco-line");

        var nameLabel = new Label(GetLayerName(manager.LayerIndex));
        nameLabel.AddToClassList("layer-name");

        var lineRight = new VisualElement();
        lineRight.AddToClassList("layer-deco-line");
        lineRight.AddToClassList("layer-deco-line--right");

        header.Add(lineLeft);
        header.Add(nameLabel);
        header.Add(lineRight);
        section.Add(header);

        var adventurers = manager.GetAllAdventurers().Where(a => a != null).ToList();

        // Slot row: filled slots then empty slots
        var slotRow = new VisualElement();
        slotRow.AddToClassList("slot-row");

        foreach (var agent in adventurers)
        {
            var slot = BuildPortraitSlot(agent);
            slotRow.Add(slot);
            agentSlots[agent] = slot;
        }

        int emptyCount = manager.MaxUnits - adventurers.Count;
        for (int i = 0; i < emptyCount; i++)
            slotRow.Add(BuildEmptySlot(manager.LayerIndex));

        // No `gap` in UI Toolkit — slots carry margin-right; clear it on the last
        // slot so the trailing gap doesn't eat width (keeps slots at full size).
        if (slotRow.childCount > 0)
            slotRow[slotRow.childCount - 1].style.marginRight = 0;

        section.Add(slotRow);
        return section;
    }

    private VisualElement BuildPortraitSlot(AdventurerAgent agent)
    {
        if (slotTemplate == null)
        {
            Debug.LogError("[RosterPanelController] slotTemplate is not assigned — drag AdventurerSlot.uxml into the Slot Template field on the RosterPanelController component.");
            return new VisualElement();
        }

        var slot = slotTemplate.Instantiate();
        slot.AddToClassList("adv-slot-container");

        // Apply selected state to the inner frame
        if (agent == selectedAgent)
            slot.Q<VisualElement>("slot-frame")?.AddToClassList("slot-selected");

        // Sprite
        var unitIcon = slot.Q<VisualElement>("unit-icon");
        if (unitIcon != null && agent.appearanceManager != null && agent.def != null && agent.def.useModularCharacter)
        {
            var sprite = CharacterSpriteGenerator.GenerateSprite(agent.def, agent.appearanceManager.GetCurrentIndices());
            if (sprite != null)
                unitIcon.style.backgroundImage = new StyleBackground(sprite);
        }

        RefreshSlot(slot, agent);

        slot.RegisterCallback<ClickEvent>(_ => SelectAgent(agent));
        return slot;
    }

    private VisualElement BuildEmptySlot(int layerIndex)
    {
        var slot = new VisualElement();
        slot.AddToClassList("empty-slot");

        var icon = new Label("+");
        icon.AddToClassList("empty-slot-icon");
        slot.Add(icon);

        slot.RegisterCallback<ClickEvent>(_ =>
        {
            if (hireController != null)
                hireController.OpenAtLayer(layerIndex);
            else
                UIManager.Instance.OpenPanel("HirePanel");
        });

        return slot;
    }

    private static string GetLayerName(int layerIndex) =>
        layerIndex >= 1 && layerIndex < LayerNames.Length
            ? LayerNames[layerIndex]
            : $"Layer {layerIndex}";

    // ═════════════════════════════════════════════
    // SELECTION
    // ═════════════════════════════════════════════

    private void SelectAgent(AdventurerAgent agent)
    {
        if (selectedAgent != null && agentSlots.TryGetValue(selectedAgent, out var prevSlot))
            prevSlot.Q<VisualElement>("slot-frame")?.RemoveFromClassList("slot-selected");

        selectedAgent = agent;

        if (agentSlots.TryGetValue(agent, out var newSlot))
            newSlot.Q<VisualElement>("slot-frame")?.AddToClassList("slot-selected");

        ShowDetail(agent);
    }

    // ═════════════════════════════════════════════
    // DETAIL PANEL
    // ═════════════════════════════════════════════

    private void ShowDetail(AdventurerAgent agent)
    {
        if (rightContent == null) return;

        var placeholder = rightContent.Q<Label>("roster-placeholder");
        var detailContainer = rightContent.Q<VisualElement>("detail-container");

        if (agent == null)
        {
            if (placeholder != null) placeholder.style.display = DisplayStyle.Flex;
            if (detailContainer != null) detailContainer.style.display = DisplayStyle.None;
            return;
        }

        if (placeholder != null) placeholder.style.display = DisplayStyle.None;
        if (detailContainer != null) detailContainer.style.display = DisplayStyle.Flex;

        // Sprite
        var detailSprite = rightContent.Q<VisualElement>("detail-sprite");
        if (detailSprite != null && agent.appearanceManager != null && agent.def != null && agent.def.useModularCharacter)
        {
            var sprite = CharacterSpriteGenerator.GenerateSprite(agent.def, agent.appearanceManager.GetCurrentIndices());
            if (sprite != null)
                detailSprite.style.backgroundImage = new StyleBackground(sprite);
        }

        // Rank badge (roman level), tier name, and Promote button
        RefreshDetailRank(agent);

        // Description
        var descLabel = rightContent.Q<Label>("detail-description");
        if (descLabel != null)
        {
            string desc = agent.Identity?.description;
            if (string.IsNullOrEmpty(desc)) desc = "Ready for work. Probably.";
            descLabel.text = desc;
            descLabel.style.display = DisplayStyle.Flex;
            Debug.Log($"[RosterPanel] description label found. Identity={(agent.Identity != null ? "set" : "NULL")} desc=\"{desc}\"");
        }
        else
        {
            Debug.LogWarning("[RosterPanel] detail-description label NOT FOUND in rightContent.");
        }

        // Name + epithet
        var nameLabel = rightContent.Q<Label>("detail-name");
        if (nameLabel != null)
            nameLabel.text = agent.Identity?.DisplayName ?? agent.gameObject.name;


        // Class + layer
        var classLabel = rightContent.Q<Label>("detail-class");
        if (classLabel != null)
            classLabel.text = "Novice";

        var layerLabel = rightContent.Q<Label>("detail-layer");
        if (layerLabel != null)
            layerLabel.text = GetLayerName(agent.layerIndex);

        // Stats
        if (agent.Stats != null)
        {
            var atkLabel = rightContent.Q<Label>("stat-atk");
            if (atkLabel != null)
                atkLabel.text = agent.Stats.AttackDamage.ToString("F1");

            float interval = agent.Stats.AttackInterval;
            float aps = interval > 0f ? 1f / interval : 0f;

            var spdLabel = rightContent.Q<Label>("stat-spd");
            if (spdLabel != null)
                spdLabel.text = aps > 0f ? $"{aps:F1}/s" : "0/s";

            var dpsLabel = rightContent.Q<Label>("stat-dps");
            if (dpsLabel != null)
                dpsLabel.text = (agent.Stats.AttackDamage * aps).ToString("F1");
        }

        // XP bar
        RefreshDetailXP(agent);

        // HP bar
        var health = agent.GetComponent<Health>();
        var hpFill = rightContent.Q<VisualElement>("detail-hp-fill");
        var hpValueLabel = rightContent.Q<Label>("hp-label-value");
        if (health != null)
        {
            if (hpFill != null) hpFill.style.width = Length.Percent(health.HealthPercent * 100f);
            if (hpValueLabel != null) hpValueLabel.text = $"{health.CurrentHP:F0}/{health.MaxHP:F0}";
        }
        else
        {
            if (hpFill != null) hpFill.style.width = Length.Percent(0f);
            if (hpValueLabel != null) hpValueLabel.text = "?/?";
        }

        // Traits
        var traitsContainer = rightContent.Q<VisualElement>("detail-traits");
        if (traitsContainer != null)
        {
            traitsContainer.Clear();
            var traitComponent = agent.GetComponent<TraitComponent>();
            if (traitComponent != null)
            {
                foreach (var ti in traitComponent.GetTraits())
                {
                    var traitDef = TraitDatabase.GetTrait(ti.traitId);
                    if (traitDef == null) continue;
                    var chip = new Label(traitDef.displayName);
                    chip.AddToClassList("trait");
                    traitsContainer.Add(chip);
                }
            }
        }

        // Mods
        var modsContainer = rightContent.Q<VisualElement>("detail-mods");
        if (modsContainer != null)
        {
            modsContainer.Clear();
            var traitComponent = agent.GetComponent<TraitComponent>();
            var traits = traitComponent?.GetTraits();
            if (traits != null)
            {
                foreach (var ti in traits)
                {
                    var traitDef = TraitDatabase.GetTrait(ti.traitId);
                    if (traitDef == null || ti.tier < 1 || ti.tier > traitDef.tiers.Length) continue;
                    var tierData = traitDef.tiers[ti.tier - 1];
                    if (tierData.modifiers == null) continue;
                    foreach (var mod in tierData.modifiers)
                    {
                        var modLabel = new Label(FormatStatModifier(mod));
                        modLabel.AddToClassList("mod");
                        modsContainer.Add(modLabel);
                    }
                }
            }
        }

    }

    private void OnPromoteClicked()
    {
        if (selectedAgent == null || !selectedAgent.CanPromote) return;
        selectedAgent.Promote();
        ShowDetail(selectedAgent);
        if (agentSlots.TryGetValue(selectedAgent, out var slot))
            RefreshSlot(slot, selectedAgent);
    }

    // ═════════════════════════════════════════════
    // REFRESH HELPERS
    // ═════════════════════════════════════════════

    private void RefreshSlot(VisualElement slot, AdventurerAgent agent)
    {
        float xpRatio = agent.XPForNextLevel >= float.MaxValue
            ? 1f
            : Mathf.Clamp01(agent.CurrentXP / agent.XPForNextLevel);

        var xpFill = slot.Q<VisualElement>("slot-xp-fill");
        if (xpFill != null)
            xpFill.style.width = Length.Percent(xpRatio * 100f);

        // Badge shows the level as a roman numeral (I..V); gem sprite by rank when provided.
        var rankLabel = slot.Q<Label>("rank-level");
        if (rankLabel != null)
            rankLabel.text = AdventurerRank.Roman(agent.CurrentLevel);

        var rankBadge = slot.Q<VisualElement>("rank-container");
        var badgeSprite = RankSprite(agent.CurrentRank);
        if (rankBadge != null && badgeSprite != null)
            rankBadge.style.backgroundImage = new StyleBackground(badgeSprite);

        var health = agent.GetComponent<Health>();
        var hpFill = slot.Q<VisualElement>("slot-hp-fill");
        if (hpFill != null)
            hpFill.style.width = Length.Percent(health != null ? health.HealthPercent * 100f : 0f);
    }

    /// <summary>Optional per-rank gem sprite (1-based rank); null falls back to the slot placeholder.</summary>
    private Sprite RankSprite(int rank)
    {
        int i = rank - 1;
        if (rankBadgeSprites != null && i >= 0 && i < rankBadgeSprites.Length)
            return rankBadgeSprites[i];
        return null;
    }

    private void RefreshDetailXP(AdventurerAgent agent)
    {
        if (rightContent == null) return;

        float xpRatio = agent.XPForNextLevel >= float.MaxValue
            ? 1f
            : Mathf.Clamp01(agent.CurrentXP / agent.XPForNextLevel);

        var xpFill = rightContent.Q<VisualElement>("detail-xp-fill");
        if (xpFill != null)
            xpFill.style.width = Length.Percent(xpRatio * 100f);

        var xpLabel = rightContent.Q<Label>("xp-label-value");
        if (xpLabel != null)
        {
            if (agent.IsMaxLevel) xpLabel.text = "Max";
            else if (agent.CanPromote) xpLabel.text = "Ready to Promote";
            else xpLabel.text = $"{agent.CurrentXP:F0} / {agent.XPForNextLevel:F0}";
        }
    }

    /// <summary>Updates the detail rank badge (roman level), tier name, and the Promote button.</summary>
    private void RefreshDetailRank(AdventurerAgent agent)
    {
        if (rightContent == null) return;

        var rankNum = rightContent.Q<Label>("detail-rank-number");
        if (rankNum != null) rankNum.text = AdventurerRank.Roman(agent.CurrentLevel);

        var rankName = rightContent.Q<Label>("detail-rank-name");
        if (rankName != null) rankName.text = agent.RankName;

        var promoteBtn = rightContent.Q<Button>("detail-promote-btn");
        if (promoteBtn != null)
        {
            promoteBtn.style.display = agent.CanPromote ? DisplayStyle.Flex : DisplayStyle.None;
            promoteBtn.text = agent.CanPromote ? $"Promote ({agent.PromoteCost}g)" : "Promote";
            promoteBtn.SetEnabled(Inventory.Instance != null && Inventory.Instance.CanAfford(agent.PromoteCost));
            promoteBtn.clicked -= OnPromoteClicked;
            promoteBtn.clicked += OnPromoteClicked;
        }
    }

    // ═════════════════════════════════════════════
    // MOD FORMATTING
    // ═════════════════════════════════════════════

    private static string FormatStatModifier(TraitStatModifier mod)
    {
        string sign = mod.value >= 0 ? "+" : "";
        string valueStr;
        if (mod.operation == ModifierOp.Mult)
        {
            float pct = (mod.value - 1f) * 100f;
            sign = pct >= 0 ? "+" : "";
            valueStr = $"{sign}{pct:F0}%";
        }
        else
        {
            valueStr = $"{sign}{mod.value:F0}";
        }
        return $"{valueStr} {mod.stat}";
    }

    // ═════════════════════════════════════════════
    // SIGNALS
    // ═════════════════════════════════════════════

    private void OnAdventurerXPChanged(AdventurerAgent agent, float newXP)
    {
        if (agentSlots.TryGetValue(agent, out var slot))
            RefreshSlot(slot, agent);

        if (agent == selectedAgent)
        {
            RefreshDetailXP(agent);
            RefreshDetailRank(agent);
        }
    }

    private void OnUnitHired(EntityDef unitDef, HireRole role)
    {
        if (leftContent == null) return;
        RebuildRoster(leftContent);
    }

    private void OnAdventurerPromoted(EntityBase entity, string oldRole, string newRole)
    {
        if (!(entity is AdventurerAgent agent)) return;

        if (agentSlots.TryGetValue(agent, out var slot))
            RefreshSlot(slot, agent);

        if (agent == selectedAgent)
            ShowDetail(agent);
    }
}
