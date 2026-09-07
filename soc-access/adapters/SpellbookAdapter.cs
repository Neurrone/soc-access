using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest;
using SongsOfConquest.Common;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Battle;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Settings;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquest.Common.Spells;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class SpellbookAdapter
    {
        private static readonly FieldInfo EntriesField = AccessTools.Field(typeof(SpellBook), "_entries");
        private static readonly FieldInfo CommanderStateField = AccessTools.Field(typeof(SpellBook), "_commanderState");
        private static readonly FieldInfo IsInAdventureField = AccessTools.Field(typeof(SpellBook), "_isInAdventure");
        private static readonly FieldInfo IsCurrentTeamsTurnField = AccessTools.Field(typeof(SpellBook), "_isCurrentTeamsTurn");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(SpellBook), "_localizationHandler");
        private static readonly FieldInfo SpellsLookupField = AccessTools.Field(typeof(SpellBook), "_spellsLookup");
        private static readonly FieldInfo BattleFacadeField = AccessTools.Field(typeof(SpellBook), "_clientBattleFacade");
        private static readonly FieldInfo EntryAdventureFacadeField = AccessTools.Field(typeof(SpellbookSpellEntry), "_adventureFacade");
        private static readonly FieldInfo ClientSettingsField = AccessTools.Field(typeof(SpellBook), "_clientSettings");
        private static readonly FieldInfo SpellDetailsField = AccessTools.Field(typeof(SpellBook), "_spellDetails");
        private static readonly FieldInfo QuickbarField = AccessTools.Field(typeof(SpellBook), "_quickbar");
        private static readonly FieldInfo TutorialButtonField = AccessTools.Field(typeof(SpellBook), "_tutorialButton");
        private static readonly FieldInfo CloseButtonField = AccessTools.Field(typeof(SpellBook), "_closeButton");
        private static readonly FieldInfo OrderTierAreaField = AccessTools.Field(typeof(SpellBook), "_orderTierArea");
        private static readonly FieldInfo CreationTierAreaField = AccessTools.Field(typeof(SpellBook), "_creationTierArea");
        private static readonly FieldInfo ChaosTierAreaField = AccessTools.Field(typeof(SpellBook), "_chaosTierArea");
        private static readonly FieldInfo ArcanaTierAreaField = AccessTools.Field(typeof(SpellBook), "_arcanaTierArea");
        private static readonly FieldInfo DestructionTierAreaField = AccessTools.Field(typeof(SpellBook), "_destructionTierArea");
        private static readonly FieldInfo OrderTierTextField = AccessTools.Field(typeof(SpellBook), "_orderTier");
        private static readonly FieldInfo CreationTierTextField = AccessTools.Field(typeof(SpellBook), "_creationTier");
        private static readonly FieldInfo ChaosTierTextField = AccessTools.Field(typeof(SpellBook), "_chaosTier");
        private static readonly FieldInfo ArcanaTierTextField = AccessTools.Field(typeof(SpellBook), "_arcanaTier");
        private static readonly FieldInfo DestructionTierTextField = AccessTools.Field(typeof(SpellBook), "_destructionTier");
        private static readonly FieldInfo EssenceControllerField = AccessTools.Field(typeof(SpellBook), "_spellbookEssenceController");
        private static readonly FieldInfo EssenceOrderTextField = AccessTools.Field(typeof(SpellBookEssenceController), "_orderText");
        private static readonly FieldInfo EssenceCreationTextField = AccessTools.Field(typeof(SpellBookEssenceController), "_creationText");
        private static readonly FieldInfo EssenceChaosTextField = AccessTools.Field(typeof(SpellBookEssenceController), "_chaosText");
        private static readonly FieldInfo EssenceArcanaTextField = AccessTools.Field(typeof(SpellBookEssenceController), "_arcanaText");
        private static readonly FieldInfo EssenceDestructionTextField = AccessTools.Field(typeof(SpellBookEssenceController), "_destructionText");
        private static readonly FieldInfo EssenceOrderImageField = AccessTools.Field(typeof(SpellBookEssenceController), "_orderImageNonActive");
        private static readonly FieldInfo EssenceCreationImageField = AccessTools.Field(typeof(SpellBookEssenceController), "_creationImageNonActive");
        private static readonly FieldInfo EssenceChaosImageField = AccessTools.Field(typeof(SpellBookEssenceController), "_chaosImageNonActive");
        private static readonly FieldInfo EssenceArcanaImageField = AccessTools.Field(typeof(SpellBookEssenceController), "_arcanaImageNonActive");
        private static readonly FieldInfo EssenceDestructionImageField = AccessTools.Field(typeof(SpellBookEssenceController), "_destructionImageNonActive");
        private static readonly MethodInfo RefreshShownSpellMethod = AccessTools.Method(typeof(SpellBook), "RefreshShownSpell");
        private static readonly FieldInfo QuickbarEntriesField = AccessTools.Field(typeof(SpellbookQuickbar), "_entries");
        private static readonly FieldInfo QuickbarMovableSpellField = AccessTools.Field(typeof(SpellbookQuickbar), "_movableSpell");
        private static readonly FieldInfo QuickbarAutoPopulateToggleField = AccessTools.Field(typeof(SpellbookQuickbar), "_autoPopulateToggle");
        private static readonly FieldInfo QuickbarMainButtonField = AccessTools.Field(typeof(SpellbookQuickbarEntry), "_mainButton");
        private static readonly FieldInfo QuickbarDeleteButtonField = AccessTools.Field(typeof(SpellbookQuickbarEntry), "_deleteButton");
        private static readonly FieldInfo MovableSpellHoverQuickbarEntryField = AccessTools.Field(typeof(SpellbookMovableSpell), "_hoverQuickbarEntry");
        private static readonly MethodInfo MovableSpellEndDragMethod = AccessTools.Method(typeof(SpellbookMovableSpell), "EndDrag");
        private static readonly FieldInfo EntryButtonField = AccessTools.Field(typeof(SpellbookSpellEntry), "_button");

        private readonly SpellBook _spellbook;
        private SpellbookSpellEntry _hoveredEntry;

        public SpellbookAdapter(SpellBook spellbook)
        {
            _spellbook = spellbook;
        }

        public bool IsPresent()
        {
            return _spellbook != null
                && _spellbook.IsOpen
                && ((Component)_spellbook).gameObject.activeInHierarchy;
        }

        /// <summary>The drawn close cross.</summary>
        public UIButton CloseButton
        {
            get { return CloseButtonField != null ? CloseButtonField.GetValue(_spellbook) as UIButton : null; }
        }

        public bool IsCloseVisible()
        {
            UIButton button = CloseButton;
            return button != null && ((Component)button).gameObject.activeInHierarchy;
        }

        public bool ActivateClose()
        {
            return NativeSelectionUtility.Click(CloseButton);
        }

        /// <summary>The tutorial button, drawn only until the tutorial has been seen.</summary>
        public UIButton TutorialButton
        {
            get { return GetTutorialButton(); }
        }

        public bool IsTutorialButtonVisible()
        {
            UIButton button = GetTutorialButton();
            return button != null && ((Component)button).gameObject.activeInHierarchy;
        }

        public string GetTutorialButtonLabel()
        {
            UIButton button = GetTutorialButton();
            string label = MenuButtonTextUtility.GetAllVisibleText(button);
            return string.IsNullOrWhiteSpace(label)
                ? GameText.Get(GetLocalization(), "Tutorial/CodexCategory/Tutorials", "Tutorials")
                : label;
        }

        public bool ActivateTutorial()
        {
            return NativeSelectionUtility.Click(GetTutorialButton());
        }

        /// <summary>The five single-essence columns the window draws, in the order it draws them.
        /// </summary>
        public IReadOnlyList<SchoolItem> GetSchools()
        {
            return new[]
            {
                BuildSchool(SpellbookSpellGroup.Order, EssenceType.Order, OrderTierTextField, OrderTierAreaField, EssenceOrderTextField, EssenceOrderImageField),
                BuildSchool(SpellbookSpellGroup.Chaos, EssenceType.Chaos, ChaosTierTextField, ChaosTierAreaField, EssenceChaosTextField, EssenceChaosImageField),
                BuildSchool(SpellbookSpellGroup.Destruction, EssenceType.Destruction, DestructionTierTextField, DestructionTierAreaField, EssenceDestructionTextField, EssenceDestructionImageField),
                BuildSchool(SpellbookSpellGroup.Creation, EssenceType.Creation, CreationTierTextField, CreationTierAreaField, EssenceCreationTextField, EssenceCreationImageField),
                BuildSchool(SpellbookSpellGroup.Arcana, EssenceType.Arcana, ArcanaTierTextField, ArcanaTierAreaField, EssenceArcanaTextField, EssenceArcanaImageField)
            };
        }

        /// <summary>The drawn header over the quick bar.</summary>
        public string GetQuickbarHeaderText()
        {
            SpellbookQuickbar quickbar = GetQuickbar();
            if (quickbar == null)
            {
                return string.Empty;
            }

            List<SpellbookQuickbarEntry> entries = QuickbarEntriesField != null
                ? QuickbarEntriesField.GetValue(quickbar) as List<SpellbookQuickbarEntry>
                : null;
            UITextMesh[] texts = quickbar.GetComponentsInChildren<UITextMesh>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                UITextMesh text = texts[i];
                if (text == null || IsUnderEntry(((Component)text).transform, entries))
                {
                    continue;
                }

                string value = UITextMeshTextUtility.GetEffectiveText(text);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        // The cost numbers a slot draws are text meshes of the quick bar too; only the header sits
        // outside every entry.
        private static bool IsUnderEntry(Transform transform, List<SpellbookQuickbarEntry> entries)
        {
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                SpellbookQuickbarEntry entry = entries[i];
                if (entry != null && transform != null && transform.IsChildOf(((Component)entry).transform))
                {
                    return true;
                }
            }

            return false;
        }

        public IReadOnlyList<SpellItem> GetSpells(SpellbookSpellGroup group)
        {
            List<SpellItem> items = new List<SpellItem>();
            IReadOnlyList<SpellbookSpellEntry> entries = GetEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                SpellbookSpellEntry entry = entries[i];
                if (entry == null || entry.SpellDefinition == null || !((Component)entry).gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (GetGroup(entry.SpellDefinition) == group)
                {
                    items.Add(new SpellItem(this, entry, group.ToString().ToLowerInvariant() + "-" + entry.SpellDefinition.Id));
                }
            }

            return items;
        }

        public IReadOnlyList<QuickbarItem> GetQuickbarItems()
        {
            List<QuickbarItem> items = new List<QuickbarItem>();
            SpellbookQuickbar quickbar = GetQuickbar();
            List<SpellbookQuickbarEntry> entries = quickbar != null ? QuickbarEntriesField.GetValue(quickbar) as List<SpellbookQuickbarEntry> : null;
            if (entries == null)
            {
                return items;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                items.Add(new QuickbarItem(this, entries[i], i));
            }

            return items;
        }

        /// <summary>The game's own auto-fill toggle.</summary>
        public UIToggle AutoPopulateToggle
        {
            get { return GetAutoPopulateToggle(); }
        }

        public bool IsAutoPopulateVisible()
        {
            UIToggle toggle = GetAutoPopulateToggle();
            return toggle != null && ((Component)toggle).gameObject.activeInHierarchy;
        }

        public string GetAutoPopulateLabel()
        {
            UIToggle toggle = GetAutoPopulateToggle();
            return GetFirstTooltipLabel(toggle);
        }

        public bool IsAutoPopulateChecked()
        {
            UIToggle toggle = GetAutoPopulateToggle();
            return toggle != null && toggle.ToggleValue;
        }

        public void ToggleAutoPopulate()
        {
            UIToggle toggle = GetAutoPopulateToggle();
            if (toggle != null)
            {
                toggle.ToggleValue = !toggle.ToggleValue;
            }
        }

        public void FocusSpell(SpellbookSpellEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            if (_hoveredEntry != null && !ReferenceEquals(_hoveredEntry, entry))
            {
                _hoveredEntry.OnPointerExit(new PointerEventData(EventSystem.current));
            }

            _hoveredEntry = entry;
            NativeSelectionUtility.Select((Component)entry);
            entry.OnPointerEnter(new PointerEventData(EventSystem.current));
            RefreshShownSpellMethod?.Invoke(_spellbook, null);
        }

        public void UnfocusSpell(SpellbookSpellEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            entry.OnPointerExit(new PointerEventData(EventSystem.current));
            if (ReferenceEquals(_hoveredEntry, entry))
            {
                _hoveredEntry = null;
            }
        }

        public bool ActivateSpell(SpellbookSpellEntry entry)
        {
            FocusSpell(entry);
            UIButton button = EntryButtonField != null ? EntryButtonField.GetValue(entry) as UIButton : null;
            if (button != null)
            {
                return NativeSelectionUtility.Click(button);
            }

            return NativeSelectionUtility.PointerClick(entry);
        }

        /// <summary>The entry's own RIGHT click, which the game answers by adding the spell to the
        /// first free quick bar slot under its own guards
        /// (<c>SpellBook.AddEntryToFirstAvailableQuickbarSlot</c>).</summary>
        public bool RightClickSpell(SpellbookSpellEntry entry)
        {
            FocusSpell(entry);
            UIButton button = EntryButtonField != null ? EntryButtonField.GetValue(entry) as UIButton : null;
            return NativeSelectionUtility.RightClick(button);
        }

        /// <summary>Whether the game's own right-click handler would do anything with this spell: the
        /// two conditions <c>AddEntryToFirstAvailableQuickbarSlot</c> tests.</summary>
        public bool CanAddSpellToQuickbar(SpellbookSpellEntry entry)
        {
            return entry != null
                && entry.SpellDefinition != null
                && !IsAutoPopulateChecked()
                && !IsSpellOnQuickbar(entry.SpellDefinition);
        }

        public void FocusQuickbar(SpellbookQuickbarEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            entry.OnPointerEnter(new PointerEventData(EventSystem.current));
        }

        public void UnfocusQuickbar(SpellbookQuickbarEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            entry.OnPointerExit(new PointerEventData(EventSystem.current));
        }

        /// <summary>
        /// The slot's own main button, clicked the way the pointer clicks it, so the game's own
        /// dispatch decides what a click on a quick bar spell means.
        ///
        /// Guarded because on the ADVENTURE map the game's own listener throws:
        /// <c>SpellbookQuickbar.HandleClickedSpell</c> reads <c>_battleFacade.Teams</c>, and outside a
        /// battle there is no battle facade (seen 2026-09-07 through <c>/log</c>). Nothing is put in
        /// its place - the click simply does what the mouse's does, which is nothing - but the
        /// exception must not travel up through the mod's own update.
        /// </summary>
        public bool ActivateQuickbar(SpellbookQuickbarEntry entry)
        {
            FocusQuickbar(entry);
            Button button = QuickbarMainButtonField != null ? QuickbarMainButtonField.GetValue(entry) as Button : null;
            if (button == null)
            {
                return false;
            }

            try
            {
                return NativeSelectionUtility.Click(button);
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("Spellbook quickbar click threw inside the game's own handler: " + exception);
                return false;
            }
        }


        /// <summary>The delete button the game reveals when the pointer rests on a filled slot, pressed
        /// the way the pointer presses it. Focusing the slot first is what makes the game draw the
        /// button at all (<c>SpellbookQuickbarEntry.ShowDeleteButton</c>).</summary>
        public bool ActivateQuickbarDelete(SpellbookQuickbarEntry entry)
        {
            FocusQuickbar(entry);
            UIButton button = entry != null && QuickbarDeleteButtonField != null
                ? QuickbarDeleteButtonField.GetValue(entry) as UIButton
                : null;
            return NativeSelectionUtility.Click(button);
        }

        /// <summary>
        /// The mouse's whole drag of a spell already on the bar, replayed in one call: the source
        /// slot's own <c>OnBeginDrag</c> (which hands the spell to the movable and empties the slot
        /// up front, as the pointer's press does), then the movable's hover and its
        /// <c>EndDrag</c>. A null <paramref name="target"/> is the release over nothing, which the
        /// game answers with its cancel sound and puts the spell nowhere.
        ///
        /// One call, because the movable's own <c>LateUpdate</c> re-reads the hovered slot from the
        /// real pointer and ends the drag the moment the mouse button is up.
        /// </summary>
        public bool DragQuickbarSpell(SpellbookQuickbarEntry source, SpellbookQuickbarEntry target)
        {
            if (source == null || source.Spell == null || IsAutoPopulateChecked())
            {
                return false;
            }

            SpellbookMovableSpell movableSpell = GetMovableSpell();
            if (movableSpell == null)
            {
                return false;
            }

            source.OnBeginDrag(LeftDrag());
            return EndDrag(movableSpell, target);
        }

        /// <summary>The same replay for a spell dragged out of a column: the entry's own
        /// <c>OnBeginDrag</c> (<c>SpellBook.HandleBeginDragSpell</c> to
        /// <c>SpellbookQuickbar.HandleBeginDragSpell</c> to the movable), then hover and
        /// <c>EndDrag</c>, which overwrites whatever the slot held.</summary>
        public bool DragSpellToQuickbar(SpellbookSpellEntry source, SpellbookQuickbarEntry target)
        {
            if (source == null || source.SpellDefinition == null || target == null || IsAutoPopulateChecked())
            {
                return false;
            }

            SpellbookMovableSpell movableSpell = GetMovableSpell();
            if (movableSpell == null)
            {
                return false;
            }

            source.OnBeginDrag(LeftDrag());
            return EndDrag(movableSpell, target);
        }

        private bool EndDrag(SpellbookMovableSpell movableSpell, SpellbookQuickbarEntry target)
        {
            MovableSpellHoverQuickbarEntryField.SetValue(movableSpell, target);
            MovableSpellEndDragMethod.Invoke(movableSpell, null);
            return true;
        }

        private SpellbookMovableSpell GetMovableSpell()
        {
            SpellbookQuickbar quickbar = GetQuickbar();
            SpellbookMovableSpell movableSpell = quickbar != null && QuickbarMovableSpellField != null
                ? QuickbarMovableSpellField.GetValue(quickbar) as SpellbookMovableSpell
                : null;
            if (movableSpell == null || MovableSpellHoverQuickbarEntryField == null || MovableSpellEndDragMethod == null)
            {
                SocAccessMod.Instance?.LogWarning("Spellbook quickbar drag failed because native movable spell members were not found");
                return null;
            }

            return movableSpell;
        }

        private static PointerEventData LeftDrag()
        {
            return new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        }

        public string GetSpellLabel(ISpellDefinition spell)
        {
            if (spell == null)
            {
                return ModText.Get(ModStrings.Screens.UnknownSpell);
            }

            string name = Localize(spell.NameKey);
            int tier = GetCurrentTier(spell);
            string cost = FormatCost(spell);
            string result = name;
            if (tier > 0)
            {
                result += ", " + GetTierLabel(tier);
            }

            if (!string.IsNullOrWhiteSpace(cost))
            {
                result += ", " + cost;
            }

            return result;
        }

        public Tooltip GetSpellTooltip(SpellbookSpellEntry entry)
        {
            if (entry == null || entry.SpellDefinition == null)
            {
                return null;
            }

            SpellbookSpellEntry capturedEntry = entry;
            return new Tooltip(() => BuildSpellTooltipLines(capturedEntry.SpellDefinition), null);
        }

        private bool IsSpellOnQuickbar(ISpellDefinition spell)
        {
            if (spell == null)
            {
                return false;
            }

            SpellbookQuickbar quickbar = GetQuickbar();
            List<SpellbookQuickbarEntry> entries = quickbar != null ? QuickbarEntriesField.GetValue(quickbar) as List<SpellbookQuickbarEntry> : null;
            if (entries == null)
            {
                return false;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                ISpellDefinition quickbarSpell = entries[i] != null ? entries[i].Spell : null;
                if (quickbarSpell != null && quickbarSpell.Id == spell.Id)
                {
                    return true;
                }
            }

            return false;
        }

        public Tooltip GetQuickbarTooltip(SpellbookQuickbarEntry entry)
        {
            if (entry == null || entry.Spell == null)
            {
                return null;
            }

            SpellbookQuickbarEntry capturedEntry = entry;
            return new Tooltip(() => BuildSpellTooltipLines(capturedEntry.Spell), null);
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        private IReadOnlyList<string> BuildSpellTooltipLines(ISpellDefinition spell)
        {
            List<string> lines = new List<string>();
            if (spell == null)
            {
                return lines;
            }

            ICommanderState commander = GetCommander();
            ISpellsLookup lookup = GetSpellsLookup();
            ILocalizationHandler localization = GetLocalization();
            string name = Localize(spell.NameKey);
            int tier = GetCurrentTier(spell);
            lines.Add(tier > 0 ? name + ", " + GetTierLabel(tier) : name);

            string lore = Localize(spell.DescriptionKey);
            if (!string.IsNullOrWhiteSpace(lore))
            {
                lines.Add(lore);
            }

            if (lookup != null && commander != null && localization != null)
            {
                SpellDetails details = lookup.GetDetails((SpellTypes)spell.Id, commander);
                if (details != null)
                {
                    string description = details.GetLocalizedTierDescription(details.CurrentTier, localization);
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        string header = localization.GetText("Spells/Spellbook/SpellDescriptionHeader")
                            + " ("
                            + localization.GetText("Spells/Spellbook/SpellTierHeader", details.CurrentTier)
                            + ")";
                        lines.Add(header);
                        lines.Add(description);
                    }

                    string duration = details.GetLocalizedTierDurationDescription(details.CurrentTier, localization);
                    if (!string.IsNullOrWhiteSpace(duration))
                    {
                        lines.Add(localization.GetText("Spells/Spellbook/SpellDurationHeader") + ": " + duration);
                    }
                }
            }

            string cost = FormatCost(spell);
            if (!string.IsNullOrWhiteSpace(cost))
            {
                lines.Add(Localize("Spells/Spellbook/SpellCostHeader") + ": " + cost);
            }

            string castText = BuildCastText(spell, tier);
            if (!string.IsNullOrWhiteSpace(castText))
            {
                lines.Add(castText);
            }

            return lines;
        }

        private string BuildCastText(ISpellDefinition spell, int tier)
        {
            ICommanderState commander = GetCommander();
            ILocalizationHandler localization = GetLocalization();
            if (spell == null || commander == null || localization == null)
            {
                return string.Empty;
            }

            if (IsInAdventure())
            {
                return HasAdventureEssence(spell, commander)
                    ? localization.GetText("Spells/Tooltip/Adventure/AvailableInTurns", CastInTurns(spell, commander))
                    : localization.GetText("Spells/Tooltip/Adventure/NotAbleToCast");
            }

            if (!IsCurrentTeamsTurn())
            {
                return localization.GetText("Spells/Tooltip/Battle/UnavailableReasonNotMyTurn");
            }

            if (!commander.EssenceWallet.CanAffordToCast(spell))
            {
                return localization.GetText("Spells/Tooltip/Battle/UnavailableReasonNoEssence");
            }

            IClientBattleFacade battleFacade = GetBattleFacade();
            bool hasTargets = battleFacade != null
                && SpellbookSpellEntry.HasAvailableTargets(battleFacade.Troops, commander, spell.GetTier(tier));
            if (!hasTargets)
            {
                return localization.GetText("Spells/Spellbook/NoTarget");
            }

            return spell.GetHighestAvailableTier(commander).IsCastedInstantly()
                ? localization.GetText("Spells/Tooltip/Battle/ClickToInstantCast")
                : localization.GetText("Spells/Tooltip/Battle/ClickToBeginCast");
        }

        private SchoolItem BuildSchool(
            SpellbookSpellGroup group,
            EssenceType essence,
            FieldInfo tierTextField,
            FieldInfo tierAreaField,
            FieldInfo essenceTextField,
            FieldInfo essenceImageField)
        {
            SpellBookEssenceController controller = GetEssenceController();
            UITextMesh tierText = tierTextField != null ? tierTextField.GetValue(_spellbook) as UITextMesh : null;
            Component tierArea = tierAreaField != null ? tierAreaField.GetValue(_spellbook) as Component : null;
            UITextMesh essenceText = controller != null && essenceTextField != null
                ? essenceTextField.GetValue(controller) as UITextMesh
                : null;
            Component essenceImage = controller != null && essenceImageField != null
                ? essenceImageField.GetValue(controller) as Component
                : null;
            if (essenceImage != null)
            {
                // The details the essence icon carries are declared on its UIImage, not on the raw
                // Image the controller keeps (SpellBookEssenceController.UpdateTooltip).
                UIImage details = essenceImage.GetComponent<UIImage>();
                if (details != null)
                {
                    essenceImage = details;
                }
            }

            return new SchoolItem(
                group,
                GameText.Get(GetLocalization(), "Spells/Spellbook/SelectedEssenceTitle", string.Empty, GetEssenceName(essence)),
                GetEssenceName(essence),
                UITextMeshTextUtility.GetEffectiveText(essenceText),
                essenceImage,
                Tooltip.ForComponent(essenceImage, GetLocalization()),
                UITextMeshTextUtility.GetEffectiveText(tierText),
                tierArea,
                Tooltip.ForComponent(tierArea, GetLocalization()));
        }

        private SpellBookEssenceController GetEssenceController()
        {
            return EssenceControllerField != null
                ? EssenceControllerField.GetValue(_spellbook) as SpellBookEssenceController
                : null;
        }

        private string GetTierLabel(int tier)
        {
            return GameText.Get(GetLocalization(), "Spells/Spellbook/SpellTierHeader", "tier " + tier, tier).Trim();
        }

        private int GetEssenceAmount(EssenceType essence)
        {
            ICommanderState commander = GetCommander();
            if (commander == null)
            {
                return 0;
            }

            if (!IsInAdventure())
            {
                return commander.EssenceWallet.Amount(essence);
            }

            IClientAdventureFacade adventureFacade = GetAdventureFacade();
            return adventureFacade != null ? adventureFacade.Commanders.GetTotalEssenceIncome(commander.Id, essence) : 0;
        }

        private bool HasAdventureEssence(ISpellDefinition spell, ICommanderState commander)
        {
            if (spell == null || commander == null)
            {
                return false;
            }

            for (int i = 0; i < spell.Cost.Count; i++)
            {
                if (GetEssenceAmount(spell.Cost[i].Type) < 1)
                {
                    return false;
                }
            }

            return true;
        }

        private float CastInTurns(ISpellDefinition spell, ICommanderState commander)
        {
            float result = 1f;
            if (spell == null || commander == null)
            {
                return result;
            }

            for (int i = 0; i < spell.Cost.Count; i++)
            {
                int income = Math.Max(1, GetEssenceAmount(spell.Cost[i].Type));
                result = Mathf.Max(result, (float)Math.Ceiling((double)spell.Cost[i].Amount / income));
            }

            return result;
        }

        private int GetCurrentTier(ISpellDefinition spell)
        {
            ICommanderState commander = GetCommander();
            return spell != null && commander != null ? spell.GetHighestAvailableTier(commander).Tier : 0;
        }

        private string FormatCost(ISpellDefinition spell)
        {
            if (spell == null || spell.Cost == null || spell.Cost.Count == 0)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < spell.Cost.Count; i++)
            {
                SpellCostEntry cost = spell.Cost[i];
                parts.Add(FormatCostEntry(cost));
            }

            return string.Join(", ", parts.ToArray());
        }

        private string FormatCostEntry(SpellCostEntry cost)
        {
            return cost.Amount + " " + GetEssenceName(cost.Type);
        }

        private string GetEssenceName(EssenceType type)
        {
            string localized = Localize("Units/Types/" + type);
            if (!string.IsNullOrWhiteSpace(localized) && localized != "Units/Types/" + type)
            {
                return localized;
            }

            switch (type)
            {
                case EssenceType.Order:
                    return "Order";
                case EssenceType.Creation:
                    return "Creation";
                case EssenceType.Chaos:
                    return "Chaos";
                case EssenceType.Arcana:
                    return "Arcana";
                case EssenceType.Destruction:
                    return "Destruction";
                default:
                    return type.ToString();
            }
        }

        private string Localize(string key)
        {
            ILocalizationHandler localization = GetLocalization();
            if (localization == null || string.IsNullOrWhiteSpace(key))
            {
                return key ?? string.Empty;
            }

            return localization.GetText(key) ?? key;
        }

        private SpellbookSpellGroup GetGroup(ISpellDefinition spell)
        {
            if (spell == null || spell.Cost == null || spell.Cost.Count != 1)
            {
                return SpellbookSpellGroup.Multi;
            }

            switch (spell.Cost[0].Type)
            {
                case EssenceType.Order:
                    return SpellbookSpellGroup.Order;
                case EssenceType.Creation:
                    return SpellbookSpellGroup.Creation;
                case EssenceType.Chaos:
                    return SpellbookSpellGroup.Chaos;
                case EssenceType.Arcana:
                    return SpellbookSpellGroup.Arcana;
                case EssenceType.Destruction:
                    return SpellbookSpellGroup.Destruction;
                default:
                    return SpellbookSpellGroup.Multi;
            }
        }

        private IReadOnlyList<SpellbookSpellEntry> GetEntries()
        {
            return EntriesField != null
                ? EntriesField.GetValue(_spellbook) as List<SpellbookSpellEntry> ?? new List<SpellbookSpellEntry>()
                : new List<SpellbookSpellEntry>();
        }

        private ICommanderState GetCommander()
        {
            return CommanderStateField != null ? CommanderStateField.GetValue(_spellbook) as ICommanderState : null;
        }

        private bool IsInAdventure()
        {
            return IsInAdventureField != null && (bool)IsInAdventureField.GetValue(_spellbook);
        }

        private bool IsCurrentTeamsTurn()
        {
            return IsCurrentTeamsTurnField == null || (bool)IsCurrentTeamsTurnField.GetValue(_spellbook);
        }

        private ILocalizationHandler GetLocalization()
        {
            return LocalizationField != null ? LocalizationField.GetValue(_spellbook) as ILocalizationHandler : null;
        }

        private ISpellsLookup GetSpellsLookup()
        {
            return SpellsLookupField != null ? SpellsLookupField.GetValue(_spellbook) as ISpellsLookup : null;
        }

        private IClientBattleFacade GetBattleFacade()
        {
            return BattleFacadeField != null ? BattleFacadeField.GetValue(_spellbook) as IClientBattleFacade : null;
        }

        private IClientAdventureFacade GetAdventureFacade()
        {
            if (EntryAdventureFacadeField == null)
            {
                return null;
            }

            IReadOnlyList<SpellbookSpellEntry> entries = GetEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                IClientAdventureFacade facade = EntryAdventureFacadeField.GetValue(entries[i]) as IClientAdventureFacade;
                if (facade != null)
                {
                    return facade;
                }
            }

            return null;
        }

        private SpellbookQuickbar GetQuickbar()
        {
            return QuickbarField != null ? QuickbarField.GetValue(_spellbook) as SpellbookQuickbar : null;
        }

        private UIToggle GetAutoPopulateToggle()
        {
            SpellbookQuickbar quickbar = GetQuickbar();
            return quickbar != null && QuickbarAutoPopulateToggleField != null
                ? QuickbarAutoPopulateToggleField.GetValue(quickbar) as UIToggle
                : null;
        }

        private string GetTooltipLabel(Component component)
        {
            IReadOnlyList<string> lines = NativeTooltipUtility.GetTooltipLinesForComponent(component, GetLocalization());
            if (lines == null || lines.Count == 0)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < lines.Count; i++)
            {
                string part = SpeechTextSanitizer.Normalize(lines[i]);
                if (!string.IsNullOrWhiteSpace(part))
                {
                    parts.Add(part);
                }
            }

            return parts.Count == 0 ? string.Empty : string.Join(". ", parts.ToArray());
        }

        private string GetFirstTooltipLabel(Component root)
        {
            if (root == null)
            {
                return string.Empty;
            }

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null || ReferenceEquals(component, root))
                {
                    continue;
                }

                string label = GetTooltipLabel(component);
                if (!string.IsNullOrWhiteSpace(label))
                {
                    return label;
                }
            }

            return string.Empty;
        }

        private UIButton GetTutorialButton()
        {
            return TutorialButtonField != null ? TutorialButtonField.GetValue(_spellbook) as UIButton : null;
        }

        /// <summary>One drawn essence column: the game's own name for its spells, the essence income
        /// it heads and the tier it grants, each with the details the game hangs on it.</summary>
        public sealed class SchoolItem
        {
            public SchoolItem(
                SpellbookSpellGroup group,
                string title,
                string essenceName,
                string essenceAmountText,
                Component essenceComponent,
                Tooltip essenceTooltip,
                string tierTitle,
                Component tierComponent,
                Tooltip tierTooltip)
            {
                Group = group;
                Title = title;
                EssenceName = essenceName;
                EssenceAmountText = essenceAmountText;
                EssenceComponent = essenceComponent;
                EssenceTooltip = essenceTooltip;
                TierTitle = tierTitle;
                TierComponent = tierComponent;
                TierTooltip = tierTooltip;
            }

            public SpellbookSpellGroup Group { get; private set; }

            /// <summary>What the game calls this column's spells ("Order spells").</summary>
            public string Title { get; private set; }

            public string EssenceName { get; private set; }

            /// <summary>The income the column draws beside its essence icon ("(+11)").</summary>
            public string EssenceAmountText { get; private set; }

            public Component EssenceComponent { get; private set; }

            public Tooltip EssenceTooltip { get; private set; }

            /// <summary>The tier heading the column draws ("Tier 3").</summary>
            public string TierTitle { get; private set; }

            public Component TierComponent { get; private set; }

            public Tooltip TierTooltip { get; private set; }
        }

        public sealed class SpellItem
        {
            private readonly SpellbookAdapter _adapter;
            private readonly SpellbookSpellEntry _entry;

            public SpellItem(SpellbookAdapter adapter, SpellbookSpellEntry entry, string id)
            {
                _adapter = adapter;
                _entry = entry;
                Id = id;
            }

            public string Id { get; private set; }

            public SpellbookSpellEntry Entry { get { return _entry; } }

            public string Label { get { return _adapter.GetSpellLabel(_entry.SpellDefinition); } }

            /// <summary>Whether the game reads the spell as castable; it greys the ones it does not.
            /// </summary>
            public bool CanCast { get { return _entry != null && _entry.CanCast; } }

            /// <summary>Whether the game's own right click would add this spell to the quick bar.
            /// </summary>
            public bool CanAddToQuickbar { get { return _adapter.CanAddSpellToQuickbar(_entry); } }

            public bool Activate() { return _adapter.ActivateSpell(_entry); }

            public bool RightClick() { return _adapter.RightClickSpell(_entry); }

            public void Focus() { _adapter.FocusSpell(_entry); }

            public void Unfocus() { _adapter.UnfocusSpell(_entry); }

            public Tooltip Tooltip { get { return _adapter.GetSpellTooltip(_entry); } }
        }

        public sealed class QuickbarItem
        {
            private readonly SpellbookAdapter _adapter;
            private readonly SpellbookQuickbarEntry _entry;

            public QuickbarItem(SpellbookAdapter adapter, SpellbookQuickbarEntry entry, int index)
            {
                _adapter = adapter;
                _entry = entry;
                Index = index;
            }

            public int Index { get; private set; }

            public SpellbookQuickbarEntry Entry { get { return _entry; } }

            public bool CanDrag { get { return _entry != null && _entry.Spell != null && !_adapter.IsAutoPopulateChecked(); } }

            /// <summary>Whether the game would take a spell dropped here at all.</summary>
            public bool AcceptsDrop { get { return _entry != null && !_adapter.IsAutoPopulateChecked(); } }

            public bool HasSpell
            {
                get { return _entry != null && _entry.Spell != null; }
            }

            public string SpellName
            {
                get { return HasSpell ? _adapter.GetSpellLabel(_entry.Spell) : string.Empty; }
            }

            public bool Activate() { return _entry != null && _adapter.ActivateQuickbar(_entry); }

            public bool Delete() { return _adapter.ActivateQuickbarDelete(_entry); }

            public void Focus() { _adapter.FocusQuickbar(_entry); }

            public void Unfocus() { _adapter.UnfocusQuickbar(_entry); }

            public Tooltip Tooltip { get { return _adapter.GetQuickbarTooltip(_entry); } }
        }
    }

    public enum SpellbookSpellGroup
    {
        Order,
        Creation,
        Chaos,
        Arcana,
        Destruction,
        Multi
    }
}
