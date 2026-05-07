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
using SongsOfConquestAccess.Speech;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class SpellbookAdapter
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
        private static readonly FieldInfo OrderTierValueField = AccessTools.Field(typeof(SpellBook), "_orderTierValue");
        private static readonly FieldInfo CreationTierValueField = AccessTools.Field(typeof(SpellBook), "_creationTierValue");
        private static readonly FieldInfo ChaosTierValueField = AccessTools.Field(typeof(SpellBook), "_chaosTierValue");
        private static readonly FieldInfo ArcanaTierValueField = AccessTools.Field(typeof(SpellBook), "_arcanaTierValue");
        private static readonly FieldInfo DestructionTierValueField = AccessTools.Field(typeof(SpellBook), "_destructionTierValue");
        private static readonly MethodInfo HandleEntryRightClickedMethod = AccessTools.Method(typeof(SpellBook), "HandleEntryRightClicked");
        private static readonly MethodInfo HandleCloseClickedMethod = AccessTools.Method(typeof(SpellBook), "HandleCloseClicked");
        private static readonly MethodInfo RefreshShownSpellMethod = AccessTools.Method(typeof(SpellBook), "RefreshShownSpell");
        private static readonly FieldInfo QuickbarEntriesField = AccessTools.Field(typeof(SpellbookQuickbar), "_entries");
        private static readonly FieldInfo QuickbarMovableSpellField = AccessTools.Field(typeof(SpellbookQuickbar), "_movableSpell");
        private static readonly FieldInfo QuickbarAutoPopulateToggleField = AccessTools.Field(typeof(SpellbookQuickbar), "_autoPopulateToggle");
        private static readonly MethodInfo QuickbarSetEmptyMethod = AccessTools.Method(typeof(SpellbookQuickbar), "SetEmpty", new[] { typeof(int) });
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

        public bool Close()
        {
            if (_spellbook == null)
            {
                return false;
            }

            if (HandleCloseClickedMethod != null)
            {
                HandleCloseClickedMethod.Invoke(_spellbook, null);
                return true;
            }

            _spellbook.Close();
            return true;
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
            return string.IsNullOrWhiteSpace(label) ? "Tutorial available" : label;
        }

        public bool ActivateTutorial()
        {
            return NativeSelectionUtility.Click(GetTutorialButton());
        }

        public IReadOnlyList<SchoolSummaryItem> GetSchoolSummary()
        {
            return new[]
            {
                BuildSchool("order", "Order", EssenceType.Order, OrderTierValueField, OrderTierAreaField),
                BuildSchool("chaos", "Chaos", EssenceType.Chaos, ChaosTierValueField, ChaosTierAreaField),
                BuildSchool("destruction", "Destruction", EssenceType.Destruction, DestructionTierValueField, DestructionTierAreaField),
                BuildSchool("creation", "Creation", EssenceType.Creation, CreationTierValueField, CreationTierAreaField),
                BuildSchool("arcana", "Arcana", EssenceType.Arcana, ArcanaTierValueField, ArcanaTierAreaField)
            };
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

        public bool IsAutoPopulateVisible()
        {
            UIToggle toggle = GetAutoPopulateToggle();
            return toggle != null && ((Component)toggle).gameObject.activeInHierarchy;
        }

        public string GetAutoPopulateLabel()
        {
            UIToggle toggle = GetAutoPopulateToggle();
            string label = SpeechTextSanitizer.Normalize(toggle != null ? toggle.Text : null);
            return string.IsNullOrWhiteSpace(label) ? "Auto-populate quickbar" : label;
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

        public bool AddSpellToQuickbar(SpellbookSpellEntry entry)
        {
            FocusSpell(entry);
            if (HandleEntryRightClickedMethod != null)
            {
                HandleEntryRightClickedMethod.Invoke(_spellbook, new object[] { entry });
                return true;
            }

            UIButton button = EntryButtonField != null ? EntryButtonField.GetValue(entry) as UIButton : null;
            return button != null && button.OnRightClicked != null && Invoke(button.OnRightClicked);
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

        public bool ActivateQuickbar(SpellbookQuickbarEntry entry)
        {
            FocusQuickbar(entry);
            Button button = QuickbarMainButtonField != null ? QuickbarMainButtonField.GetValue(entry) as Button : null;
            if (button == null)
            {
                return false;
            }

            button.onClick.Invoke();
            return true;
        }

        public bool ClearQuickbarSlot(int index)
        {
            SpellbookQuickbar quickbar = GetQuickbar();
            if (quickbar == null)
            {
                return false;
            }

            if (QuickbarSetEmptyMethod != null)
            {
                QuickbarSetEmptyMethod.Invoke(quickbar, new object[] { index });
                return true;
            }

            return false;
        }

        public bool DropQuickbarItem(QuickbarItem source, QuickbarItem target)
        {
            SpellbookQuickbarEntry sourceEntry = source != null ? source.Entry : null;
            SpellbookQuickbarEntry targetEntry = target != null ? target.Entry : null;
            if (sourceEntry == null || targetEntry == null || sourceEntry.Spell == null || IsAutoPopulateChecked())
            {
                return false;
            }

            SpellbookQuickbar quickbar = GetQuickbar();
            SpellbookMovableSpell movableSpell = quickbar != null && QuickbarMovableSpellField != null
                ? QuickbarMovableSpellField.GetValue(quickbar) as SpellbookMovableSpell
                : null;
            if (movableSpell == null || MovableSpellHoverQuickbarEntryField == null || MovableSpellEndDragMethod == null)
            {
                SoqAccessPlugin.Instance?.LogWarning("Spellbook quickbar drag failed because native movable spell members were not found");
                return false;
            }

            movableSpell.BeginDrag(sourceEntry);
            MovableSpellHoverQuickbarEntryField.SetValue(movableSpell, targetEntry);
            MovableSpellEndDragMethod.Invoke(movableSpell, null);
            return true;
        }

        public string GetSpellLabel(ISpellDefinition spell)
        {
            if (spell == null)
            {
                return "Unknown spell";
            }

            string name = Localize(spell.NameKey);
            int tier = GetCurrentTier(spell);
            string cost = FormatCost(spell);
            string result = name;
            if (tier > 0)
            {
                result += ", tier " + tier;
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
            IReadOnlyList<TooltipAction> actions = IsAutoPopulateChecked() || IsSpellOnQuickbar(capturedEntry.SpellDefinition)
                ? null
                : new[]
                {
                    new TooltipAction("Add to quickbar", () => AddSpellToQuickbar(capturedEntry))
                };
            return new Tooltip(
                () => BuildSpellTooltipLines(capturedEntry.SpellDefinition),
                null,
                actions);
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

        public Tooltip GetQuickbarTooltip(SpellbookQuickbarEntry entry, int index)
        {
            if (entry == null || entry.Spell == null)
            {
                return null;
            }

            SpellbookQuickbarEntry capturedEntry = entry;
            int capturedIndex = index;
            return new Tooltip(
                () => BuildSpellTooltipLines(capturedEntry.Spell),
                null,
                new[]
                {
                    new TooltipAction("Remove from quickbar", () => ClearQuickbarSlot(capturedIndex))
                });
        }

        public Tooltip GetTierTooltip(SchoolSummaryItem item)
        {
            return item != null && item.TierArea != null
                ? Tooltip.ForComponent(item.TierArea, GetLocalization())
                : null;
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
            lines.Add(tier > 0 ? name + " tier " + tier : name);

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

        private SchoolSummaryItem BuildSchool(string id, string label, EssenceType essence, FieldInfo tierField, FieldInfo tierAreaField)
        {
            int tier = tierField != null ? (int)tierField.GetValue(_spellbook) : 0;
            int essenceAmount = GetEssenceAmount(essence);
            string essenceLabel = IsInAdventure() ? "+" + essenceAmount + " per turn" : essenceAmount.ToString();
            return new SchoolSummaryItem(
                id,
                label + ": tier " + tier + ", essence " + essenceLabel,
                tierAreaField != null ? tierAreaField.GetValue(_spellbook) as Component : null);
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

        private UIButton GetTutorialButton()
        {
            return TutorialButtonField != null ? TutorialButtonField.GetValue(_spellbook) as UIButton : null;
        }

        private static bool Invoke(Action action)
        {
            if (action == null)
            {
                return false;
            }

            action();
            return true;
        }

        internal sealed class SchoolSummaryItem
        {
            public SchoolSummaryItem(string id, string label, Component tierArea)
            {
                Id = id;
                Label = label;
                TierArea = tierArea;
            }

            public string Id { get; private set; }

            public string Label { get; private set; }

            public Component TierArea { get; private set; }
        }

        internal sealed class SpellItem
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

            public string Label { get { return _adapter.GetSpellLabel(_entry.SpellDefinition); } }

            public bool Activate() { return _adapter.ActivateSpell(_entry); }

            public void Focus() { _adapter.FocusSpell(_entry); }

            public void Unfocus() { _adapter.UnfocusSpell(_entry); }

            public Tooltip Tooltip { get { return _adapter.GetSpellTooltip(_entry); } }
        }

        internal sealed class QuickbarItem
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

            public string Id { get { return "spellbook-quickbar-" + (Index + 1); } }

            public bool CanDrag { get { return _entry != null && _entry.Spell != null && !_adapter.IsAutoPopulateChecked(); } }

            public string Label
            {
                get
                {
                    return _entry != null && _entry.Spell != null
                        ? "Slot " + (Index + 1) + ": " + _adapter.GetSpellLabel(_entry.Spell)
                        : "Slot " + (Index + 1) + ": empty";
                }
            }

            public bool Activate() { return _entry != null && _adapter.ActivateQuickbar(_entry); }

            public bool DropTo(QuickbarItem target) { return _adapter.DropQuickbarItem(this, target); }

            public void Focus() { _adapter.FocusQuickbar(_entry); }

            public void Unfocus() { _adapter.UnfocusQuickbar(_entry); }

            public Tooltip Tooltip { get { return _adapter.GetQuickbarTooltip(_entry, Index); } }
        }
    }

    internal enum SpellbookSpellGroup
    {
        Order,
        Creation,
        Chaos,
        Arcana,
        Destruction,
        Multi
    }
}
