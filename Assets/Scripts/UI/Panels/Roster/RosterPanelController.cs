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
    private AdventurerAgent outlinedAgent;  // hero outlined + camera-locked in the world while the Roster is open
    private AdventurerAgent pendingFocus;    // set by FocusAgent (world right-click) until the page is built
    private VisualElement leftContent;
    private VisualElement rightContent;

    // Drag a portrait onto another depth (move) or another adventurer (swap); see MOVE / SWAP below
    private const float DragStartPx = 8f;
    private const float AutoScrollEdge = 48f, AutoScrollStep = 14f;
    private AdventurerAgent pressAgent;
    private VisualElement pressSlot, ghost, hoverTarget;
    private Vector2 pressPos;
    private bool dragging;
    private VisualElement moveDrop;    // "Move to depth" picker on the right page
    private Label moveLink;

    private const int SlotsPerRow = 5;
    private const long PulseIntervalMs = 700;   // matches .slot-rank transition-duration
    private bool goldDirty;                      // GoldChanged → re-check promote affordability once per frame
    private IVisualElementScheduledItem pulseItem;

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
        GameSignals.GoldChanged += OnGoldChanged;
        RebuildRoster(leftPage);

        // One page-level ticker drives every affordable gem's pulse via .roster-pulse (USS transitions the scale)
        pulseItem = leftPage.schedule.Execute(() => leftContent?.ToggleInClassList("roster-pulse")).Every(PulseIntervalMs);

        var dismissBtn = rightPage.Q<Button>("detail-dismiss-btn");
        if (dismissBtn != null)
        {
            dismissBtn.clicked -= OnDismissClicked;
            dismissBtn.clicked += OnDismissClicked;
        }

        SetupMovePicker(rightPage);
        ShowDetail(null);
        ApplyPendingFocus();
    }

    public void OnPageHidden()
    {
        GameSignals.OnAdventurerXPChanged -= OnAdventurerXPChanged;
        GameSignals.OnAdventurerPromoted -= OnAdventurerPromoted;
        GameSignals.OnUnitHired -= OnUnitHired;
        GameSignals.GoldChanged -= OnGoldChanged;
        pulseItem?.Pause();
        pulseItem = null;
        leftContent?.RemoveFromClassList("roster-pulse");
        // Roster page is gone (book closed or tab switched away) → drop the world outline
        // and release the camera lock; they only live while you're viewing the Roster.
        SetWorldFocus(null);
        EndDrag();
        CloseMoveDrop();
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

        var managers = FindObjectsByType<AdventurerManager>()
            .OrderBy(m => m.LayerIndex)
            .ToList();

        foreach (var manager in managers)
            layersContainer.Add(BuildLayerSection(manager));
    }

    private VisualElement BuildLayerSection(AdventurerManager manager)
    {
        var section = new VisualElement { userData = manager };   // drop target: move onto this depth
        section.AddToClassList("layer-section");

        // Header: flourish line · centered layer name · mirrored flourish line
        var header = new VisualElement();
        header.AddToClassList("book-subhead");

        var lineLeft = new VisualElement();
        lineLeft.AddToClassList("book-subhead__rule");

        var nameLabel = new Label(GetLayerName(manager.LayerIndex));
        nameLabel.AddToClassList("book-subhead__label");

        var lineRight = new VisualElement();
        lineRight.AddToClassList("book-subhead__rule");
        lineRight.AddToClassList("book-subhead__rule--right");

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

        // No `gap` / :nth-child in UI Toolkit — slots carry margin-right; clear it on every
        // 5th slot so exactly 5 fit per wrapped row.
        for (int i = SlotsPerRow - 1; i < slotRow.childCount; i += SlotsPerRow)
            slotRow[i].style.marginRight = 0;

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

        // Press + release without moving = select; press + move past DragStartPx = drag (move / swap)
        slot.userData = agent;   // drop target: swap with this adventurer
        slot.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0) return;
            pressAgent = agent; pressSlot = slot; pressPos = e.position; dragging = false;
            slot.CapturePointer(e.pointerId);
        });
        slot.RegisterCallback<PointerMoveEvent>(e => { if (pressSlot == slot && slot.HasPointerCapture(e.pointerId)) DragMove(e.position); });
        slot.RegisterCallback<PointerUpEvent>(e =>
        {
            if (pressSlot != slot || !slot.HasPointerCapture(e.pointerId)) return;
            slot.ReleasePointer(e.pointerId);
            if (dragging) Drop(e.position); else SelectAgent(agent);
            EndDrag();
        });
        slot.RegisterCallback<PointerCaptureOutEvent>(_ => { if (pressSlot == slot) EndDrag(); });
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

    public static string GetLayerName(int layerIndex) =>
        layerIndex >= 1 && layerIndex < LayerNames.Length
            ? LayerNames[layerIndex]
            : $"Depth {layerIndex}";

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

        SetWorldFocus(agent);
        ShowDetail(agent);
    }

    /// <summary>Outline the hero in the world and lock the camera onto them, moving both off
    /// any previously-focused hero. Pass null to clear. Lives only while the Roster is open —
    /// selecting another hero moves it; Dismiss or closing the Roster clears it.</summary>
    private void SetWorldFocus(AdventurerAgent agent)
    {
        if (outlinedAgent != agent)
        {
            if (outlinedAgent != null) outlinedAgent.SetOutlined(false);
            outlinedAgent = agent;
            if (outlinedAgent != null) outlinedAgent.SetOutlined(true);
        }

        if (CameraScrollController.Instance != null)
        {
            if (agent != null) CameraScrollController.Instance.LockOnto(agent.transform);
            else CameraScrollController.Instance.ClearLock();
        }
    }

    /// <summary>Open the page focused on a specific adventurer (from a world right-click).
    /// Selects immediately if the page is already built, otherwise after the next rebuild.</summary>
    public void FocusAgent(AdventurerAgent agent)
    {
        pendingFocus = agent;
        if (leftContent != null) ApplyPendingFocus();
    }

    private void ApplyPendingFocus()
    {
        if (pendingFocus == null) return;
        var agent = pendingFocus;
        pendingFocus = null;
        SelectAgent(agent);
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
            // Class role line (its signature mechanic) leads the flavour text — runtime UITK has no tooltips
            string role = agent.ClassDef != null ? agent.ClassDef.description : null;
            descLabel.text = string.IsNullOrEmpty(role) ? desc : $"<b>{role}.</b> {desc}";
            descLabel.style.display = DisplayStyle.Flex;
            Debug.Log($"[RosterPanel] description label found. Identity={(agent.Identity != null ? "set" : "NULL")} desc=\"{desc}\"");
        }
        else
        {
            Debug.LogWarning("[RosterPanel] detail-description label NOT FOUND in rightContent.");
        }

        // Name + epithet (separate banners)
        var nameLabel = rightContent.Q<Label>("detail-name");
        if (nameLabel != null)
            nameLabel.text = !string.IsNullOrEmpty(agent.Identity?.firstName)
                ? agent.Identity.firstName
                : agent.gameObject.name;

        var epithetLabel = rightContent.Q<Label>("detail-epithet");
        if (epithetLabel != null)
        {
            string ep = agent.Identity?.epithet;
            epithetLabel.text = ep ?? "";
            var epithetBanner = rightContent.Q("epithet-wrapper") ?? epithetLabel;   // the plank too, not just its text
            epithetBanner.style.display = string.IsNullOrEmpty(ep) ? DisplayStyle.None : DisplayStyle.Flex;
        }


        // Class + layer
        var classLabel = rightContent.Q<Label>("detail-class");
        if (classLabel != null)
            classLabel.text = agent.ClassName;

        var classIcon = rightContent.Q<VisualElement>("detail-class-icon");
        if (classIcon != null && agent.ClassDef != null && agent.ClassDef.classIcon != null)
            classIcon.style.backgroundImage = new StyleBackground(agent.ClassDef.classIcon);

        var layerLabel = rightContent.Q<Label>("detail-layer");
        if (layerLabel != null)
            layerLabel.text = GetLayerName(agent.layerIndex);
        CloseMoveDrop();

        // Rank vs depth (RankFit): the fit face, with its line (outmatched below, too easy above) as a hover tooltip
        var rankWarning = rightContent.Q<Label>("detail-rank-warning");
        if (rankWarning != null)
        {
            string fitText = RankFit.Describe(agent.Fit);
            rankWarning.text = fitText.Length > 0 ? fitText : "Good fit for this depth";
            rankWarning.style.display = DisplayStyle.None;
            rankWarning.EnableInClassList("detail-rank-warning--fit", fitText.Length == 0);

            // The fit face — every state, the content smile included
            var fitFace = rankWarning.parent.Q("detail-fit-face");
            if (fitFace == null)
            {
                fitFace = new VisualElement { name = "detail-fit-face" };
                fitFace.AddToClassList("detail-fit-face");
                rankWarning.parent.Add(fitFace);
                fitFace.RegisterCallback<PointerEnterEvent>(_ => { rankWarning.style.display = DisplayStyle.Flex; rankWarning.BringToFront(); });
                fitFace.RegisterCallback<PointerLeaveEvent>(_ => rankWarning.style.display = DisplayStyle.None);
            }
            var faceSprite = RankFit.Instance != null ? RankFit.Instance.Face(RankFit.MoodOf(agent.Fit)) : null;
            fitFace.style.backgroundImage = faceSprite != null ? new StyleBackground(faceSprite) : StyleKeyword.None;
            fitFace.style.display = faceSprite != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

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

        // Stat bars, same fixed scale as the hiring poster (class tier + traits)
        var bars = rightContent.Q("detail-bars");
        if (bars != null && agent.ClassDef != null)
            CandidateUIMapper.PopulateBars(bars,
                new HiringCandidate { entityDef = agent.ClassDef, traits = agent.traitComponent != null ? agent.traitComponent.GetTraitsForSave() : null },
                true, BaseStats.FromEntityDef(agent.ClassDef));

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

        // Traits & Skills — box-splice badges (trait name + its first stat-mod, green)
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

                    string value = "";
                    if (ti.tier >= 1 && ti.tier <= traitDef.tiers.Length)
                    {
                        var tierData = traitDef.tiers[ti.tier - 1];
                        if (tierData.modifiers != null)
                        {
                            foreach (var mod in tierData.modifiers)
                            {
                                value = FormatStatModifier(mod);
                                break;
                            }
                        }
                    }
                    traitsContainer.Add(BuildDetailBadge(traitDef.displayName, value, "badge-value--trait"));
                }
            }
        }

        // Hide the whole Traits & Skills section (heading included) when there are none.
        var traitsSection = rightContent.Q<VisualElement>("traits-skills-section");
        if (traitsSection != null)
            traitsSection.style.display =
                (traitsContainer != null && traitsContainer.childCount > 0) ? DisplayStyle.Flex : DisplayStyle.None;

    }

    private void OnPromoteClicked()
    {
        if (selectedAgent == null || !selectedAgent.CanPromote) return;

        // A Novice's promotion is the class pick
        if (selectedAgent.Line == ClassLine.Novice)
        {
            OpenClassPick(selectedAgent);
            return;
        }

        var before = CurrentPortrait();
        if (!AdventurerManager.PromoteAndFollow(selectedAgent, ClassLine.Novice, out var unit, out bool moved)) return;
        if (moved) { AfterMove(unit); FlashMoved(unit); }
        else
        {
            ShowDetail(selectedAgent);
            if (agentSlots.TryGetValue(selectedAgent, out var slot))
                RefreshSlot(slot, selectedAgent);
        }
        PlayPromoteFx(unit, before);
    }

    // Dismiss = clear the selection and return to the placeholder.
    // NOTE: this only deselects; it does NOT fire/remove the adventurer.
    private void OnDismissClicked()
    {
        if (selectedAgent != null && agentSlots.TryGetValue(selectedAgent, out var slot))
            slot.Q<VisualElement>("slot-frame")?.RemoveFromClassList("slot-selected");
        selectedAgent = null;
        SetWorldFocus(null);   // dismiss un-pins the world outline + camera lock
        ShowDetail(null);
    }

    // ═════════════════════════════════════════════
    // MOVE / SWAP — drag on the left page, "Move to depth" picker on the right
    // ═════════════════════════════════════════════

    private void DragMove(Vector2 pos)
    {
        if (pressAgent == null) return;
        if (!dragging)
        {
            if ((pos - pressPos).sqrMagnitude < DragStartPx * DragStartPx) return;
            dragging = true;
            CloseMoveDrop();
            pressSlot.AddToClassList("drag-source");
            // Inline styles: the panel root is outside the subtree RosterPanel.uss is attached to
            ghost = new VisualElement { pickingMode = PickingMode.Ignore };
            ghost.style.position = Position.Absolute;
            ghost.style.width = 88f; ghost.style.height = 88f;
            ghost.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            ghost.style.opacity = 0.9f;
            ghost.style.scale = new Scale(new Vector2(1.1f, 1.1f));
            var icon = pressSlot.Q<VisualElement>("unit-icon");
            if (icon != null) ghost.style.backgroundImage = icon.style.backgroundImage;
            leftContent.panel.visualTree.Add(ghost);   // top level: not clipped by the scroll view
        }

        ghost.style.left = pos.x - 44f;
        ghost.style.top = pos.y - 44f;

        // Long roster: hovering near the scroll view's top/bottom edge scrolls it
        var scroll = leftContent.Q<ScrollView>("roster-scroll");
        if (scroll != null)
        {
            var r = scroll.worldBound;
            if (pos.y < r.yMin + AutoScrollEdge) scroll.scrollOffset -= new Vector2(0, AutoScrollStep);
            else if (pos.y > r.yMax - AutoScrollEdge) scroll.scrollOffset += new Vector2(0, AutoScrollStep);
        }

        SetHover(TargetAt(pos, out var cls), cls);
    }

    // What's under the pointer: another adventurer (swap) wins over the depth section (move).
    // cls = the highlight class: drop-ok / drop-bad, or null for no highlight (own depth, nothing there).
    private VisualElement TargetAt(Vector2 pos, out string cls)
    {
        cls = null;
        for (var e = leftContent.panel.Pick(pos); e != null; e = e.parent)
        {
            if (e.userData is AdventurerAgent other)
            {
                if (other == pressAgent || other.layerIndex == pressAgent.layerIndex) return null;
                cls = AdventurerManager.SwapBlocker(pressAgent, other) == null ? "drop-ok" : "drop-bad";
                return e;
            }
            if (e.userData is AdventurerManager m)
            {
                if (m.LayerIndex == pressAgent.layerIndex) return null;
                cls = AdventurerManager.MoveBlocker(pressAgent, m) == null ? "drop-ok" : "drop-bad";
                return e;
            }
        }
        return null;
    }

    private void SetHover(VisualElement target, string cls)
    {
        if (hoverTarget != null) { hoverTarget.RemoveFromClassList("drop-ok"); hoverTarget.RemoveFromClassList("drop-bad"); }
        hoverTarget = target;
        if (hoverTarget != null && cls != null) hoverTarget.AddToClassList(cls);
    }

    private void Drop(Vector2 pos)
    {
        var target = TargetAt(pos, out var cls);
        if (target == null || cls != "drop-ok") return;
        var agent = pressAgent;

        if (target.userData is AdventurerAgent other)
        {
            if (AdventurerManager.Swap(agent, other, out var movedA, out var movedB))
                AfterMove(selectedAgent == other ? movedB : movedA);
        }
        else if (target.userData is AdventurerManager m)
        {
            var moved = AdventurerManager.Move(agent, m);
            if (moved != null) AfterMove(moved);
        }
    }

    private void EndDrag()
    {
        SetHover(null, null);
        ghost?.RemoveFromHierarchy();
        ghost = null;
        pressSlot?.RemoveFromClassList("drag-source");
        pressSlot = null;
        pressAgent = null;
        dragging = false;
    }

    // Moves respawn the unit: rebuild the roster (keeping its scroll) and select the new instance.
    /// <summary>After a promotion moved the unit: the depth line reads "Moved to <depth>" for a few seconds.</summary>
    private void FlashMoved(AdventurerAgent unit)
    {
        var layerLabel = rightContent?.Q<Label>("detail-layer");
        if (layerLabel == null || unit != selectedAgent) return;
        layerLabel.text = "Moved to " + GetLayerName(unit.layerIndex);
        layerLabel.schedule.Execute(() => { if (selectedAgent == unit) layerLabel.text = GetLayerName(unit.layerIndex); }).StartingIn(3000);
    }

    private void AfterMove(AdventurerAgent select)
    {
        if (leftContent == null) return;
        var scroll = leftContent.Q<ScrollView>("roster-scroll");
        var offset = scroll != null ? scroll.scrollOffset : Vector2.zero;
        selectedAgent = null;
        RebuildRoster(leftContent);
        if (scroll != null) scroll.schedule.Execute(() => scroll.scrollOffset = offset);
        if (select != null) SelectAgent(select);
    }

    // The depth name is the "Move to depth" dropdown (caret added in code) + its picker, built on the right page.
    private void SetupMovePicker(VisualElement rightPage)
    {
        moveLink = rightPage.Q<Label>("detail-layer");
        if (moveLink != null && moveLink.parent.Q("detail-layer-caret") == null)
        {
            // A sibling, not a child: a Label with children stops measuring its text (Yoga measures leaves only)
            var caret = new VisualElement { name = "detail-layer-caret", pickingMode = PickingMode.Ignore };
            caret.AddToClassList("detail-layer__caret");
            moveLink.parent.Add(caret);
            moveLink.AddToClassList("detail-layer--drop");
            moveLink.RegisterCallback<ClickEvent>(_ => ToggleMoveDrop());
        }

        moveDrop = rightPage.Q<VisualElement>("roster-move-drop");
        if (moveDrop == null)
        {
            moveDrop = new VisualElement { name = "roster-move-drop" };
            moveDrop.AddToClassList("roster-drop");
            rightPage.Add(moveDrop);
            // Any press outside the open picker (and not on the link, which toggles it) closes it
            rightPage.RegisterCallback<PointerDownEvent>(e =>
            {
                if (moveDrop.style.display != DisplayStyle.Flex || e.target is not VisualElement t) return;
                if (moveDrop.Contains(t) || (moveLink != null && moveLink.Contains(t))) return;
                CloseMoveDrop();
            }, TrickleDown.TrickleDown);
        }
        CloseMoveDrop();
    }

    private void ToggleMoveDrop()
    {
        if (moveDrop == null || selectedAgent == null) return;
        if (moveDrop.style.display == DisplayStyle.Flex) { CloseMoveDrop(); return; }

        moveDrop.Clear();
        var head = new Label("MOVE TO");
        head.AddToClassList("roster-drop__head");
        moveDrop.Add(head);

        var agent = selectedAgent;
        foreach (var m in FindObjectsByType<AdventurerManager>().OrderBy(x => x.LayerIndex))
        {
            string blocker = AdventurerManager.MoveBlocker(agent, m);
            if (blocker == "Locked") continue;   // locked depths aren't offered
            var row = new VisualElement();
            row.AddToClassList("roster-drop__row");
            var name = new Label(GetLayerName(m.LayerIndex)) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("roster-drop__name");
            var state = new Label(blocker == "Here" ? "here" : blocker == "Full" ? "full · drag to swap" : blocker == "Down" ? "down" : $"{m.GetTotalCount()}/{m.MaxUnits}")
                { pickingMode = PickingMode.Ignore };
            state.AddToClassList("roster-drop__state");
            row.Add(name);
            row.Add(state);
            if (blocker == "Here") row.AddToClassList("roster-drop__row--cur");
            else if (blocker != null) row.AddToClassList("roster-drop__row--off");
            else
            {
                var target = m;
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    CloseMoveDrop();
                    var moved = AdventurerManager.Move(agent, target);
                    if (moved != null) AfterMove(moved);
                });
            }
            moveDrop.Add(row);
        }

        // Hang it under the link, right-aligned to the page (right-content local coordinates)
        var pos = moveLink.ChangeCoordinatesTo(rightContent, Vector2.zero);
        moveDrop.style.top = pos.y + moveLink.layout.height + 4f;
        moveDrop.style.display = DisplayStyle.Flex;
        SetMoveDropOpen(true);
    }

    private void CloseMoveDrop()
    {
        if (moveDrop != null) moveDrop.style.display = DisplayStyle.None;
        SetMoveDropOpen(false);
    }

    private void SetMoveDropOpen(bool open)
    {
        if (moveLink == null) return;
        moveLink.EnableInClassList("detail-layer--open", open);
        moveLink.parent?.Q("detail-layer-caret")?.EnableInClassList("detail-layer__caret--open", open);
    }

    /// <summary>Box-splice badge (label on top, coloured value below) for the Stats / Traits rows.</summary>
    private static VisualElement BuildDetailBadge(string label, string value, string valueModifier = null)
    {
        var badge = new VisualElement();
        badge.AddToClassList("badge");

        var l = new Label(label);
        l.AddToClassList("badge-label");
        badge.Add(l);

        var v = new Label(value);
        v.AddToClassList("badge-value");
        if (!string.IsNullOrEmpty(valueModifier)) v.AddToClassList(valueModifier);
        badge.Add(v);

        return badge;
    }

    // ═════════════════════════════════════════════
    // REFRESH HELPERS
    // ═════════════════════════════════════════════

    private void RefreshSlot(VisualElement slot, AdventurerAgent agent)
    {
        // Rank-fit face on the portrait's corner — misfits only, so the badge means "move me"
        var frame = slot.Q("slot-frame");
        if (frame != null)
        {
            var badge = frame.Q("slot-fit-badge");
            if (badge == null)
            {
                badge = new VisualElement { name = "slot-fit-badge", pickingMode = PickingMode.Ignore };
                badge.AddToClassList("slot-fit-badge");
                frame.Add(badge);
            }
            var mood = RankFit.MoodOf(agent.Fit);
            var face = mood != RankFit.Mood.Fit && RankFit.Instance != null ? RankFit.Instance.Face(mood) : null;
            badge.style.backgroundImage = face != null ? new StyleBackground(face) : StyleKeyword.None;
            badge.style.display = face != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

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

        ApplyPromoteState(slot, agent);
    }

    /// <summary>Gem state: ready → gold tint + arrow; ready AND affordable → also pulses.</summary>
    private static void ApplyPromoteState(VisualElement slot, AdventurerAgent agent)
    {
        var gem = slot.Q<VisualElement>("rank-container");
        if (gem == null) return;
        bool ready = agent.CanPromote;
        gem.EnableInClassList("slot-rank--ready", ready);
        gem.EnableInClassList("slot-rank--affordable",
            ready && Inventory.Instance != null && Inventory.Instance.CanAfford(agent.PromoteCost));
    }

    // Gold ticks often — flag it and re-check affordability once per frame while the page is open.
    private void OnGoldChanged(int _) => goldDirty = true;

    void Update()
    {
        if (!goldDirty) return;
        goldDirty = false;
        if (leftContent == null) return;

        foreach (var kv in agentSlots)
            if (kv.Key != null) ApplyPromoteState(kv.Value, kv.Key);
        if (selectedAgent != null)
            RefreshDetailRank(selectedAgent);   // Promote button enable tracks gold too
    }

    /// <summary>Per-rank badge (1-based rank) from the ClassCatalog; null falls back to the slot placeholder.</summary>
    private static Sprite RankSprite(int rank) => ClassCatalog.Instance != null ? ClassCatalog.Instance.RankBadge(rank) : null;

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
            else if (agent.NeedsPathPick) xpLabel.text = "Choose a class";
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
        var rankShield = rightContent.Q("Rank");
        var rankSprite = RankSprite(agent.CurrentRank);
        if (rankShield != null && rankSprite != null) rankShield.style.backgroundImage = new StyleBackground(rankSprite);

        var rankName = rightContent.Q<Label>("detail-rank-name");
        if (rankName != null) rankName.text = agent.RankName;

        var promoteBtn = rightContent.Q<Button>("detail-promote-btn");
        if (promoteBtn != null)
        {
            promoteBtn.style.display = agent.CanPromote ? DisplayStyle.Flex : DisplayStyle.None;
            var promoteLabel = promoteBtn.Q<Label>("detail-promote-label");
            var next = agent.NextClassDef;
            if (promoteLabel != null)
                promoteLabel.text = agent.NeedsPathPick ? "Choose a class"
                    : next != null ? $"Promote » {next.displayName} {agent.PromoteCost:N0}"   // Ithaca has no →
                    : $"Promote {agent.PromoteCost:N0}";
            var coin = promoteBtn.Q<VisualElement>("detail-promote-coin");
            if (coin != null) coin.style.display = agent.NeedsPathPick ? DisplayStyle.None : DisplayStyle.Flex;
            promoteBtn.SetEnabled(Inventory.Instance != null && Inventory.Instance.CanAfford(agent.PromoteCost));
            promoteBtn.clicked -= OnPromoteClicked;
            promoteBtn.clicked += OnPromoteClicked;
        }
    }

    // ═════════════════════════════════════════════
    // CLASS PICK (first promotion / free pick) — the "Choose a Class" modal
    // ═════════════════════════════════════════════

    private void OpenClassPick(AdventurerAgent agent)
    {
        var before = CurrentPortrait();
        FindAnyObjectByType<ClassPickPanelController>()?.OpenFor(agent, unit => OnClassPicked(agent, unit, before));
    }

    // Promote also raises OnAdventurerPromoted; ChoosePath doesn't, so refresh here either way.
    // A first promotion that followed the fit to the next depth hands back the respawned unit.
    private void OnClassPicked(AdventurerAgent agent, AdventurerAgent unit, Sprite before)
    {
        if (rightContent == null) return;   // Roster closed meanwhile
        if (unit != null && unit != agent) { AfterMove(unit); FlashMoved(unit); }
        else
        {
            if (agent == selectedAgent) ShowDetail(agent);
            if (agentSlots.TryGetValue(agent, out var slot)) RefreshSlot(slot, agent);
        }
        PlayPromoteFx(unit ?? agent, before);
    }

    // ═════════════════════════════════════════════
    // PROMOTION FX — the detail page's transformation beat: the old look whites out, the new one is
    // swapped in under the flash and settles with a squash, the rank badge slams on with sparks, the class
    // name punches. ~0.5 s, never blocks input. Runs after ShowDetail has drawn the new look.
    // ═════════════════════════════════════════════

    private Sprite CurrentPortrait() => rightContent?.Q("detail-sprite")?.style.backgroundImage.value.sprite;

    private void PlayPromoteFx(AdventurerAgent unit, Sprite before)
    {
        var portrait = rightContent?.Q("detail-sprite");
        if (portrait == null || unit == null || unit != selectedAgent) return;
        var after = portrait.style.backgroundImage.value.sprite;
        SfxManager.Instance?.Play(SfxId.MilestoneDing);   // ponytail: placeholder until a promotion chime exists

        var whites = new List<Texture2D>();
        var flash = new VisualElement { pickingMode = PickingMode.Ignore };
        flash.style.position = Position.Absolute;
        flash.style.left = flash.style.top = flash.style.right = flash.style.bottom = 0;
        flash.style.backgroundSize = portrait.resolvedStyle.backgroundSize;
        flash.style.opacity = 0f;
        portrait.Add(flash);
        void Whiteout(Sprite s)
        {
            var t = WhiteSilhouette(s);
            if (t != null) whites.Add(t);
            flash.style.backgroundImage = t != null ? new StyleBackground(t) : StyleKeyword.None;
        }
        void Cleanup()
        {
            flash.RemoveFromHierarchy();
            foreach (var t in whites) Destroy(t);
        }

        if (before != null) portrait.style.backgroundImage = new StyleBackground(before);
        Whiteout(before != null ? before : after);
        var badge = rightContent.Q("Rank");
        if (badge != null) badge.style.opacity = 0f;   // the new badge waits for its slam
        flash.experimental.animation.Start(0f, 1f, 150, (e, v) => e.style.opacity = v).OnCompleted(() =>
        {
            portrait.style.backgroundImage = new StyleBackground(after);   // the new look, under the flash
            if (unit != selectedAgent || portrait.panel == null)
            {
                if (badge != null) badge.style.opacity = 1f;
                Cleanup();
                return;
            }
            Whiteout(after);
            flash.experimental.animation.Start(1f, 0f, 350, (e, v) => e.style.opacity = v).OnCompleted(Cleanup);

            portrait.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(100));
            portrait.experimental.animation.Start(0f, 1f, 280, (e, v) =>
                e.style.scale = new Scale(new Vector2(Mathf.LerpUnclamped(1.18f, 1f, v), Mathf.LerpUnclamped(0.84f, 1f, v))))
                .Ease(UnityEngine.UIElements.Experimental.Easing.OutBack);

            if (badge != null)
            {
                badge.experimental.animation.Start(0f, 1f, 220, (e, v) =>
                {
                    e.style.scale = new Scale(Vector2.one * Mathf.LerpUnclamped(2.2f, 1f, v));
                    e.style.opacity = Mathf.Clamp01(v * 3f);
                }).Ease(UnityEngine.UIElements.Experimental.Easing.OutBack).OnCompleted(() => Sparks(badge));
            }
            rightContent.Q("detail-class")?.experimental.animation.Start(0f, 1f, 300, (e, v) =>
                e.style.scale = new Scale(Vector2.one * Mathf.LerpUnclamped(1.4f, 1f, v)))
                .Ease(UnityEngine.UIElements.Experimental.Easing.OutBack);
        });
    }

    // A white copy of the sprite's pixels (alpha kept) for the flash; null if its texture isn't readable.
    private static Texture2D WhiteSilhouette(Sprite s)
    {
        if (s == null || !s.texture.isReadable) return null;
        var r = s.textureRect;
        var px = s.texture.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
        for (int i = 0; i < px.Length; i++) px[i] = new Color(1f, 1f, 1f, px[i].a);
        var t = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    // A handful of pixel sparks bursting off the badge on impact.
    private void Sparks(VisualElement badge)
    {
        if (rightContent == null || badge.panel == null) return;
        Vector2 c = badge.ChangeCoordinatesTo(rightContent, badge.layout.size * 0.5f);
        var cols = new[] { new Color(0.94f, 0.71f, 0.16f), new Color(0.9f, 0.45f, 0.15f) };   // gold / ember: reads on parchment
        for (int i = 0; i < 12; i++)
        {
            var spark = new VisualElement { pickingMode = PickingMode.Ignore };
            float size = i % 3 == 0 ? 10f : 6f;
            spark.style.position = Position.Absolute;
            spark.style.width = spark.style.height = size;
            spark.style.backgroundColor = cols[i % 2];
            rightContent.Add(spark);
            float ang = (i + Random.Range(-0.3f, 0.3f)) * Mathf.PI * 2f / 12f;
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(80f, 130f);
            spark.experimental.animation.Start(0f, 1f, 550, (e, v) =>
            {
                e.style.left = c.x + dir.x * v - size * 0.5f;
                e.style.top = c.y + dir.y * v - size * 0.5f;
                e.style.opacity = 1f - v * v * v;
            }).Ease(UnityEngine.UIElements.Experimental.Easing.OutCubic).OnCompleted(spark.RemoveFromHierarchy);
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
        return $"{valueStr} {TraitUIHelper.GetStatShortName(mod.stat)}";
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
