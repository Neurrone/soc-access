using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest;
using SongsOfConquest.Client.Battle;
using SongsOfConquest.Client.Battle.HUD;
using SongsOfConquest.Client.Battle.UI;
using SongsOfConquest.Client.Battle.View;
using SongsOfConquest.Client.Logging;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Battle;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquest.Common.Spells;
using SongsOfConquestAccess.Speech;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class BattleHudAdapter
    {
        private static readonly FieldInfo BattleHudSettingsField =
            AccessTools.Field(typeof(BattleHUDStateHandler), "_settings");
        private static readonly FieldInfo BattleEndTurnButtonField =
            AccessTools.Field(typeof(BattleEndTurnHUD), "_endTurnButton");
        private static readonly FieldInfo SpellsHudSpellcastingContainerField =
            AccessTools.Field(typeof(SpellsHUD.Settings), "SpellcastingContainer");
        private static readonly FieldInfo SpellsHudCancelSpellButtonField =
            AccessTools.Field(typeof(SpellsHUD.Settings), "CancelSpellButton");
        private static readonly FieldInfo SpellsHudSettingsField =
            AccessTools.Field(typeof(SpellsHUD), "_settings");
        private static readonly FieldInfo SpellsHudTargetInstructionField =
            AccessTools.Field(typeof(SpellsHUD.Settings), "TargetInstruction");
        private static readonly FieldInfo QuickbarEntriesField =
            AccessTools.Field(typeof(Quickbar), "_entries");
        private static readonly FieldInfo QuickbarEntryButtonField =
            AccessTools.Field(typeof(QuickbarEntry), "_button");
        private static readonly FieldInfo QueueHudEntryPoolField =
            AccessTools.Field(typeof(QueueHUD), "_entryPool");
        private static readonly FieldInfo QueueEntryButtonField =
            AccessTools.Field(typeof(QueueHUDEntry), "_button");
        private static readonly FieldInfo SpellTargetInstructionSpellNameField =
            AccessTools.Field(typeof(BattleSpellTargetInstruction), "_spellName");
        private static readonly FieldInfo SpellTargetInstructionTextField =
            AccessTools.Field(typeof(BattleSpellTargetInstruction), "_targetInstruction");
        private static readonly FieldInfo TroopStatusPanelAbilityButtonField =
            AccessTools.Field(typeof(BattleTroopStatusPanel), "_abilityButton");
        private static readonly FieldInfo TroopStatusPanelCancelAbilityButtonField =
            AccessTools.Field(typeof(BattleTroopStatusPanel), "_cancelAbilityButton");
        private static readonly FieldInfo BattleViewManagerContainersField =
            AccessTools.Field(typeof(BattleViewManager), "_containers");

        private readonly BattleHUDStateHandler _stateHandler;
        private readonly BattleHUDStateHandler.Settings _settings;
        private readonly ILocalizationHandler _localization;
        private readonly IClientBattleFacade _facade;
        private readonly IGameLog _gameLog;
        private readonly ITroopAbilityUtility _abilityUtility;
        private readonly BattleViewManager _battleViewManager;
        private readonly ISpellsLookup _spellsLookup;
        private string _spellTargetInstructionText;
        private string _abilityTargetInstructionText;

        public BattleHudAdapter(DiContainer container, IClientBattleFacade facade, ILocalizationHandler localization)
        {
            _facade = facade;
            _localization = localization;
            _stateHandler = Resolve<BattleHUDStateHandler>(container);
            _settings = Resolve<BattleHUDStateHandler.Settings>(container)
                ?? GetField<BattleHUDStateHandler.Settings>(_stateHandler, BattleHudSettingsField);
            _gameLog = Resolve<IGameLog>(container);
            _abilityUtility = Resolve<ITroopAbilityUtility>(container);
            _battleViewManager = Resolve<BattleViewManager>(container);
            _spellsLookup = Resolve<ISpellsLookup>(container);
            Commanders = new BattleCommanderHudAdapter(_settings, facade, localization);
        }

        public BattleCommanderHudAdapter Commanders { get; private set; }

        public bool IsSpellbookButtonVisible()
        {
            UIButton button = GetSpellbookButton();
            return IsButtonVisible(button);
        }

        public bool IsSpellbookButtonEnabled()
        {
            SpellsHUD spellsHud = GetSpellsHud();
            return spellsHud != null && spellsHud.IsInteractable() && IsButtonInteractable(spellsHud.SpellbookButton);
        }

        public string SpellbookButtonLabel
        {
            get { return Localize("Common/HUD/SpellbookButton", "Spellbook"); }
        }

        public void FocusSpellbookButton()
        {
            NativeSelectionUtility.Select(GetSpellbookButton());
        }

        public bool ClickSpellbookButton()
        {
            return NativeSelectionUtility.Click(GetSpellbookButton());
        }

        public Tooltip SpellbookButtonTooltip
        {
            get { return Tooltip.ForComponent(GetSpellbookButton(), _localization); }
        }

        public bool IsEndTurnButtonVisible()
        {
            return IsButtonVisible(GetEndTurnButton());
        }

        public bool IsEndTurnButtonEnabled()
        {
            return IsButtonInteractable(GetEndTurnButton());
        }

        public string EndTurnButtonLabel
        {
            get { return Localize("Battle/Labels/EndTurn", "End turn"); }
        }

        public void FocusEndTurnButton()
        {
            NativeSelectionUtility.Select(GetEndTurnButton());
        }

        public bool ClickEndTurnButton()
        {
            return NativeSelectionUtility.Click(GetEndTurnButton());
        }

        public Tooltip EndTurnButtonTooltip
        {
            get { return Tooltip.ForComponent(GetEndTurnButton(), _localization); }
        }

        public bool IsTargetingInstructionVisible()
        {
            return !string.IsNullOrWhiteSpace(TargetingInstructionText);
        }

        public void SetAbilityTargetInstructionText(string text)
        {
            _abilityTargetInstructionText = SpeechTextSanitizer.Normalize(text);
        }

        public void ClearAbilityTargetInstructionText()
        {
            _abilityTargetInstructionText = null;
        }

        public void SetSpellTargetInstructionText(string text)
        {
            _spellTargetInstructionText = SpeechTextSanitizer.Normalize(text);
        }

        public void ClearSpellTargetInstructionText()
        {
            _spellTargetInstructionText = null;
        }

        public string TargetingInstructionText
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_abilityTargetInstructionText))
                {
                    return _abilityTargetInstructionText;
                }

                if (!string.IsNullOrWhiteSpace(_spellTargetInstructionText))
                {
                    return _spellTargetInstructionText;
                }

                return GetVisibleSpellInstructionText();
            }
        }

        public bool IsCancelSpellButtonVisible()
        {
            return IsButtonVisible(GetCancelSpellButton());
        }

        public bool IsCancelSpellButtonEnabled()
        {
            return IsButtonInteractable(GetCancelSpellButton());
        }

        public string CancelSpellButtonLabel
        {
            get { return "Cancel spell"; }
        }

        public void FocusCancelSpellButton()
        {
            NativeSelectionUtility.Select(GetCancelSpellButton());
        }

        public bool ClickCancelSpellButton()
        {
            return NativeSelectionUtility.Click(GetCancelSpellButton());
        }

        public Tooltip CancelSpellButtonTooltip
        {
            get { return Tooltip.ForComponent(GetCancelSpellButton(), _localization); }
        }

        public bool IsAbilityButtonVisible()
        {
            return IsButtonVisible(GetAbilityButton());
        }

        public bool IsAbilityButtonEnabled()
        {
            return IsButtonInteractable(GetAbilityButton());
        }

        public string AbilityButtonLabel
        {
            get
            {
                IBattleTroopState current = GetCurrentTroop();
                ITroopAbilityDefinition ability = current != null && _abilityUtility != null
                    ? _abilityUtility.GetAbilityDefinition(current)
                    : null;
                string label = ability != null ? Localize(ability.NameKey, null) : null;
                return !string.IsNullOrWhiteSpace(label) ? label : "Ability";
            }
        }

        public void FocusAbilityButton()
        {
            NativeSelectionUtility.Select(GetAbilityButton());
        }

        public bool ClickAbilityButton()
        {
            return NativeSelectionUtility.Click(GetAbilityButton());
        }

        public Tooltip AbilityButtonTooltip
        {
            get { return Tooltip.ForComponent(GetAbilityButton(), _localization); }
        }

        public bool IsCancelAbilityButtonVisible()
        {
            return IsButtonVisible(GetCancelAbilityButton());
        }

        public bool IsCancelAbilityButtonEnabled()
        {
            return IsButtonInteractable(GetCancelAbilityButton());
        }

        public string CancelAbilityButtonLabel
        {
            get { return "Cancel ability"; }
        }

        public void FocusCancelAbilityButton()
        {
            NativeSelectionUtility.Select(GetCancelAbilityButton());
        }

        public bool ClickCancelAbilityButton()
        {
            return NativeSelectionUtility.Click(GetCancelAbilityButton());
        }

        public Tooltip CancelAbilityButtonTooltip
        {
            get { return Tooltip.ForComponent(GetCancelAbilityButton(), _localization); }
        }

        public bool IsQuickbarMenuVisible()
        {
            return IsSpellcastingContainerVisible() && GetQuickbarItems().Count > 0;
        }

        public IReadOnlyList<QuickbarItem> GetQuickbarItems()
        {
            List<QuickbarItem> items = new List<QuickbarItem>();
            Quickbar quickbar = GetQuickbar();
            List<QuickbarEntry> entries = quickbar != null && QuickbarEntriesField != null
                ? QuickbarEntriesField.GetValue(quickbar) as List<QuickbarEntry>
                : null;
            if (entries == null)
            {
                return items;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                QuickbarEntry entry = entries[i];
                if (entry == null || entry.Spell == null || !IsGameObjectVisible(entry as Component))
                {
                    continue;
                }

                items.Add(new QuickbarItem(this, entry, i));
            }

            return items;
        }

        public int GetQuickbarSlotCount()
        {
            Quickbar quickbar = GetQuickbar();
            List<QuickbarEntry> entries = quickbar != null && QuickbarEntriesField != null
                ? QuickbarEntriesField.GetValue(quickbar) as List<QuickbarEntry>
                : null;
            return entries != null ? entries.Count : 0;
        }

        public QuickbarItem GetQuickbarItem(int index)
        {
            Quickbar quickbar = GetQuickbar();
            List<QuickbarEntry> entries = quickbar != null && QuickbarEntriesField != null
                ? QuickbarEntriesField.GetValue(quickbar) as List<QuickbarEntry>
                : null;
            if (entries == null || index < 0 || index >= entries.Count)
            {
                return null;
            }

            QuickbarEntry entry = entries[index];
            return entry != null ? new QuickbarItem(this, entry, index) : null;
        }

        public bool IsCurrentTroopIndicatorVisible()
        {
            return GetCurrentTroopId() >= 0;
        }

        public int GetCurrentTroopId()
        {
            try
            {
                return _facade != null && _facade.Troops != null && _facade.Troops.Current != null
                    ? _facade.Troops.Current.Id
                    : -1;
            }
            catch
            {
                return -1;
            }
        }

        public string CurrentTroopLabel
        {
            get { return "Current troop, " + GetTroopLabel(GetCurrentTroopId(), includePosition: false); }
        }

        public bool IsQueueMenuVisible()
        {
            return GetQueueItems().Count > 0;
        }

        public IReadOnlyList<QueueItem> GetQueueItems()
        {
            List<QueueItem> items = new List<QueueItem>();
            if (_facade == null || _facade.Queue == null)
            {
                return items;
            }

            IReadOnlyList<IQueueHUDEntry> nativeEntries = GetActiveQueueEntries();
            int insertedRound = -1;
            int turnsLeftInRound = GetTurnsLeftInRound();
            int nextRound = GetCurrentRound() + 2;
            for (int i = 1; i < _facade.Queue.Count; i++)
            {
                QueuedTroop queuedTroop = _facade.Queue[i];
                if (queuedTroop.Id < 0)
                {
                    continue;
                }

                if (insertedRound < 0 && turnsLeftInRound > 0 && i >= turnsLeftInRound)
                {
                    items.Add(QueueItem.RoundMarker("combat-queue-round-" + nextRound, "Round " + nextRound));
                    insertedRound = nextRound;
                }

                IQueueHUDEntry nativeEntry = FindNativeQueueEntry(nativeEntries, queuedTroop);
                items.Add(new QueueItem(this, queuedTroop, nativeEntry, items.Count + 1));
            }

            return items;
        }

        public int GetQueueItemCount()
        {
            return GetQueueItems().Count;
        }

        public QueueItem GetQueueItem(int index)
        {
            IReadOnlyList<QueueItem> items = GetQueueItems();
            return index >= 0 && index < items.Count ? items[index] : null;
        }

        public bool IsBattleLogMenuVisible()
        {
            return GetBattleLogEntries().Count > 0;
        }

        public IReadOnlyList<string> GetBattleLogEntries()
        {
            if (_gameLog == null)
            {
                return new string[0];
            }

            try
            {
                IList<string> entries = _gameLog.GetEntries(GameLogType.BattleOnly);
                if (entries == null)
                {
                    return new string[0];
                }

                List<string> result = new List<string>();
                for (int i = 0; i < entries.Count; i++)
                {
                    string text = SpeechTextSanitizer.Normalize(entries[i]);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        result.Add(text);
                    }
                }

                return result;
            }
            catch
            {
                return new string[0];
            }
        }

        public int GetBattleLogEntryCount()
        {
            return GetBattleLogEntries().Count;
        }

        public string GetBattleLogEntry(int index)
        {
            IReadOnlyList<string> entries = GetBattleLogEntries();
            return index >= 0 && index < entries.Count ? entries[index] : string.Empty;
        }

        public void FocusBattleLog()
        {
            GameLogHandleUI handle = GetGameLogHandle();
            if (handle != null)
            {
                handle.HandleMouseEnter();
            }
        }

        public void UnfocusBattleLog()
        {
            GameLogHandleUI handle = GetGameLogHandle();
            if (handle != null)
            {
                handle.HandleMouseExit();
            }
        }

        private SpellsHUD GetSpellsHud()
        {
            if (_stateHandler == null)
            {
                return null;
            }

            if (_stateHandler.AttackerSpellsHUD != null
                && _stateHandler.AttackerSpellsHUD.IsInteractable()
                && IsButtonVisible(_stateHandler.AttackerSpellsHUD.SpellbookButton))
            {
                return _stateHandler.AttackerSpellsHUD;
            }

            if (_stateHandler.DefenderSpellsHUD != null
                && _stateHandler.DefenderSpellsHUD.IsInteractable()
                && IsButtonVisible(_stateHandler.DefenderSpellsHUD.SpellbookButton))
            {
                return _stateHandler.DefenderSpellsHUD;
            }

            if (_stateHandler.AttackerSpellsHUD != null
                && IsButtonVisible(_stateHandler.AttackerSpellsHUD.SpellbookButton))
            {
                return _stateHandler.AttackerSpellsHUD;
            }

            if (_stateHandler.DefenderSpellsHUD != null
                && IsButtonVisible(_stateHandler.DefenderSpellsHUD.SpellbookButton))
            {
                return _stateHandler.DefenderSpellsHUD;
            }

            return null;
        }

        private UIButton GetSpellbookButton()
        {
            SpellsHUD spellsHud = GetSpellsHud();
            return spellsHud != null ? spellsHud.SpellbookButton : null;
        }

        private Quickbar GetQuickbar()
        {
            SpellsHUD spellsHud = GetSpellsHud();
            return spellsHud != null ? spellsHud.Quickbar : null;
        }

        private bool IsSpellcastingContainerVisible()
        {
            SpellsHUD spellsHud = GetSpellsHud();
            object settings = GetField<object>(spellsHud, SpellsHudSettingsField);
            UITransform container = settings != null && SpellsHudSpellcastingContainerField != null
                ? SpellsHudSpellcastingContainerField.GetValue(settings) as UITransform
                : null;
            return container != null && container.Active && IsGameObjectVisible(container as Component);
        }

        private UIButton GetCancelSpellButton()
        {
            SpellsHUD spellsHud = GetSpellsHudWithVisibleCancelSpell();
            return GetCancelSpellButton(spellsHud);
        }

        private SpellsHUD GetSpellsHudWithVisibleCancelSpell()
        {
            if (_stateHandler == null)
            {
                return null;
            }

            if (IsButtonVisible(GetCancelSpellButton(_stateHandler.AttackerSpellsHUD)))
            {
                return _stateHandler.AttackerSpellsHUD;
            }

            if (IsButtonVisible(GetCancelSpellButton(_stateHandler.DefenderSpellsHUD)))
            {
                return _stateHandler.DefenderSpellsHUD;
            }

            return GetSpellsHud();
        }

        private UIButton GetCancelSpellButton(SpellsHUD spellsHud)
        {
            object settings = GetField<object>(spellsHud, SpellsHudSettingsField);
            return settings != null && SpellsHudCancelSpellButtonField != null
                ? SpellsHudCancelSpellButtonField.GetValue(settings) as UIButton
                : null;
        }

        private UIButton GetAbilityButton()
        {
            BattleTroopStatusPanel panel = GetCurrentTroopStatusPanel();
            return GetField<UIButton>(panel, TroopStatusPanelAbilityButtonField);
        }

        private UIButton GetCancelAbilityButton()
        {
            BattleTroopStatusPanel panel = GetCurrentTroopStatusPanel();
            return GetField<UIButton>(panel, TroopStatusPanelCancelAbilityButtonField);
        }

        private BattleTroopStatusPanel GetCurrentTroopStatusPanel()
        {
            IBattleTroopState current = GetCurrentTroop();
            if (current == null)
            {
                return null;
            }

            object containers = _battleViewManager != null && BattleViewManagerContainersField != null
                ? BattleViewManagerContainersField.GetValue(_battleViewManager)
                : null;
            System.Collections.IDictionary dictionary = containers as System.Collections.IDictionary;
            if (dictionary == null || !dictionary.Contains(current.Id))
            {
                return null;
            }

            object container = dictionary[current.Id];
            FieldInfo viewStatusField = container != null ? AccessTools.Field(container.GetType(), "ViewStatus") : null;
            return viewStatusField != null ? viewStatusField.GetValue(container) as BattleTroopStatusPanel : null;
        }

        private IBattleTroopState GetCurrentTroop()
        {
            try
            {
                return _facade != null && _facade.Troops != null ? _facade.Troops.Current : null;
            }
            catch
            {
                return null;
            }
        }

        private UIButton GetEndTurnButton()
        {
            BattleEndTurnHUD hud = _settings != null && _settings.BattleEndTurnContainer != null
                ? _settings.BattleEndTurnContainer.GetComponentInChildren<BattleEndTurnHUD>(true)
                : null;
            return GetField<UIButton>(hud, BattleEndTurnButtonField);
        }

        private QueueHUD GetQueueHud()
        {
            return _stateHandler != null ? _stateHandler.QueueHUD : null;
        }

        private IReadOnlyList<IQueueHUDEntry> GetActiveQueueEntries()
        {
            object pool = GetQueueHud() != null && QueueHudEntryPoolField != null
                ? QueueHudEntryPoolField.GetValue(GetQueueHud())
                : null;
            if (pool == null)
            {
                return new IQueueHUDEntry[0];
            }

            PropertyInfo property = AccessTools.Property(pool.GetType(), "ActiveItems");
            object value = property != null ? property.GetValue(pool, null) : null;
            IList<IQueueHUDEntry> typed = value as IList<IQueueHUDEntry>;
            if (typed != null)
            {
                return new List<IQueueHUDEntry>(typed);
            }

            System.Collections.IEnumerable enumerable = value as System.Collections.IEnumerable;
            if (enumerable == null)
            {
                return new IQueueHUDEntry[0];
            }

            List<IQueueHUDEntry> result = new List<IQueueHUDEntry>();
            foreach (object item in enumerable)
            {
                IQueueHUDEntry entry = item as IQueueHUDEntry;
                if (entry != null)
                {
                    result.Add(entry);
                }
            }

            return result;
        }

        private static IQueueHUDEntry FindNativeQueueEntry(IReadOnlyList<IQueueHUDEntry> entries, QueuedTroop queuedTroop)
        {
            if (entries == null)
            {
                return null;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                IQueueHUDEntry entry = entries[i];
                if (entry != null
                    && entry.Troop.Id == queuedTroop.Id
                    && entry.Troop.Round == queuedTroop.Round
                    && IsGameObjectVisible(entry.Container))
                {
                    return entry;
                }
            }

            return null;
        }

        private int GetCurrentRound()
        {
            try
            {
                return _facade != null && _facade.Queue != null ? _facade.Queue.CurrentRound : 0;
            }
            catch
            {
                return 0;
            }
        }

        private int GetTurnsLeftInRound()
        {
            try
            {
                return _facade != null && _facade.Queue != null ? _facade.Queue.TurnsLeftInRound : 0;
            }
            catch
            {
                return 0;
            }
        }

        private string GetVisibleSpellInstructionText()
        {
            BattleSpellTargetInstruction instruction = GetSpellTargetInstruction();
            if (!IsGameObjectVisible(instruction))
            {
                return string.Empty;
            }

            string spellName = GetText(GetField<UITextMesh>(instruction, SpellTargetInstructionSpellNameField));
            string text = GetText(GetField<UITextMesh>(instruction, SpellTargetInstructionTextField));
            if (!string.IsNullOrWhiteSpace(spellName) && !string.IsNullOrWhiteSpace(text))
            {
                return spellName + ": " + text;
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            return string.Empty;
        }

        private BattleSpellTargetInstruction GetSpellTargetInstruction()
        {
            SpellsHUD spellsHud = GetSpellsHud();
            object settings = GetField<object>(spellsHud, SpellsHudSettingsField);
            return settings != null && SpellsHudTargetInstructionField != null
                ? SpellsHudTargetInstructionField.GetValue(settings) as BattleSpellTargetInstruction
                : null;
        }

        private GameLogHandleUI GetGameLogHandle()
        {
            GameObject container = _settings != null ? _settings.GameLogContainer : null;
            return container != null ? container.GetComponentInChildren<GameLogHandleUI>(true) : null;
        }

        private string GetTroopLabel(int troopId, bool includePosition)
        {
            try
            {
                IBattleTroopState troop = _facade != null && _facade.Troops != null ? _facade.Troops.Get(troopId) : null;
                if (troop == null)
                {
                    return "unknown troop";
                }

                int size = troop.Stats != null ? troop.Stats.Size : 0;
                string name = SpeechTextSanitizer.Normalize(_facade.Troops.GetName(troop.Id, size));
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = "troop";
                }

                string label = name;
                if (troop.Stats != null)
                {
                    label = troop.Stats.Size + " " + name;
                }

                if (includePosition)
                {
                    label += " at " + CombatAdapter.FormatPoint(troop.Position);
                }

                return label;
            }
            catch
            {
                return "unknown troop";
            }
        }

        private UIButton GetQuickbarEntryButton(QuickbarEntry entry)
        {
            return GetField<UIButton>(entry, QuickbarEntryButtonField);
        }

        private void FocusQuickbarEntry(QuickbarEntry entry)
        {
            NativeSelectionUtility.Select(entry != null ? entry.GetSelectable() : null);
            UIButton button = GetQuickbarEntryButton(entry);
            if (button != null)
            {
                button.OnPointerEnter(new PointerEventData(EventSystem.current));
            }
        }

        private void UnfocusQuickbarEntry(QuickbarEntry entry)
        {
            UIButton button = GetQuickbarEntryButton(entry);
            if (button != null)
            {
                button.OnPointerExit(new PointerEventData(EventSystem.current));
            }
        }

        private Tooltip GetQuickbarTooltip(QuickbarEntry entry)
        {
            if (entry == null || entry.Spell == null)
            {
                return null;
            }

            ISpellDefinition capturedSpell = entry.Spell;
            return new Tooltip(() => BuildSpellTooltipLines(capturedSpell), null);
        }

        private IReadOnlyList<string> BuildSpellTooltipLines(ISpellDefinition spell)
        {
            List<string> lines = new List<string>();
            if (spell == null)
            {
                return lines;
            }

            ICommanderState commander = _facade != null ? _facade.Commanders.Current : null;
            string name = Localize(spell.NameKey, "Spell");
            int tier = GetCurrentSpellTier(spell, commander);
            lines.Add(tier > 0 ? name + " tier " + tier : name);

            string lore = Localize(spell.DescriptionKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(lore))
            {
                lines.Add(lore);
            }

            if (_spellsLookup != null && commander != null && _localization != null)
            {
                SpellDetails details = _spellsLookup.GetDetails((SpellTypes)spell.Id, commander);
                if (details != null)
                {
                    string description = details.GetLocalizedTierDescription(details.CurrentTier, _localization);
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        string header = _localization.GetText("Spells/Spellbook/SpellDescriptionHeader")
                            + " ("
                            + _localization.GetText("Spells/Spellbook/SpellTierHeader", details.CurrentTier)
                            + ")";
                        lines.Add(header);
                        lines.Add(description);
                    }

                    string duration = details.GetLocalizedTierDurationDescription(details.CurrentTier, _localization);
                    if (!string.IsNullOrWhiteSpace(duration))
                    {
                        lines.Add(Localize("Spells/Spellbook/SpellDurationHeader", "Duration") + ": " + duration);
                    }
                }
            }

            string cost = FormatSpellCost(spell);
            if (!string.IsNullOrWhiteSpace(cost))
            {
                lines.Add(Localize("Spells/Spellbook/SpellCostHeader", "Cost") + ": " + cost);
            }

            string castText = BuildSpellCastText(spell, commander, tier);
            if (!string.IsNullOrWhiteSpace(castText))
            {
                lines.Add(castText);
            }

            return lines;
        }

        private string BuildSpellCastText(ISpellDefinition spell, ICommanderState commander, int tier)
        {
            if (spell == null || commander == null || _localization == null)
            {
                return string.Empty;
            }

            if (_facade != null && !_facade.Teams.IsCurrentLocal)
            {
                return _localization.GetText("Spells/Tooltip/Battle/UnavailableReasonNotMyTurn");
            }

            if (!commander.EssenceWallet.CanAffordToCast(spell))
            {
                return _localization.GetText("Spells/Tooltip/Battle/UnavailableReasonNoEssence");
            }

            bool hasTargets = _facade != null
                && SpellbookSpellEntry.HasAvailableTargets(_facade.Troops, commander, spell.GetTier(tier));
            if (!hasTargets)
            {
                return _localization.GetText("Spells/Spellbook/NoTarget");
            }

            return spell.GetHighestAvailableTier(commander).IsCastedInstantly()
                ? _localization.GetText("Spells/Tooltip/Battle/ClickToInstantCast")
                : _localization.GetText("Spells/Tooltip/Battle/ClickToBeginCast");
        }

        private int GetCurrentSpellTier(ISpellDefinition spell, ICommanderState commander)
        {
            if (spell == null || commander == null)
            {
                return 0;
            }

            try
            {
                return Math.Max(1, spell.GetHighestAvailableTier(commander).Tier);
            }
            catch
            {
                return 1;
            }
        }

        private string FormatSpellCost(ISpellDefinition spell)
        {
            if (spell == null || spell.Cost == null || spell.Cost.Count == 0)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < spell.Cost.Count; i++)
            {
                SpellCostEntry cost = spell.Cost[i];
                parts.Add(cost.Amount + " " + GetEssenceName(cost.Type));
            }

            return string.Join(", ", parts.ToArray());
        }

        private string GetEssenceName(EssenceType type)
        {
            string key = "Units/Types/" + type;
            string localized = Localize(key, string.Empty);
            if (!string.IsNullOrWhiteSpace(localized) && localized != key)
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

        private UIButton GetQueueEntryButton(IQueueHUDEntry entry)
        {
            return GetField<UIButton>(entry, QueueEntryButtonField);
        }

        private string Localize(string key, string fallback)
        {
            if (_localization == null || string.IsNullOrWhiteSpace(key))
            {
                return fallback;
            }

            string localized = _localization.GetText(key);
            return string.IsNullOrWhiteSpace(localized) || localized == key
                ? fallback
                : SpeechTextSanitizer.Normalize(localized);
        }

        private static bool IsButtonVisible(UIButton button)
        {
            if (button == null || !button.Active)
            {
                return false;
            }

            Component component = button as Component;
            return component == null || IsGameObjectVisible(component.gameObject);
        }

        private static bool IsButtonInteractable(UIButton button)
        {
            return IsButtonVisible(button) && button.Interactable;
        }

        private static bool IsGameObjectVisible(GameObject gameObject)
        {
            return gameObject != null && gameObject.activeInHierarchy;
        }

        private static bool IsGameObjectVisible(Component component)
        {
            return component != null && IsGameObjectVisible(component.gameObject);
        }

        private static string GetText(UITextMesh text)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(text));
        }

        private static T Resolve<T>(DiContainer container) where T : class
        {
            if (container == null)
            {
                return null;
            }

            try
            {
                return container.Resolve<T>();
            }
            catch
            {
                return null;
            }
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            if (owner == null || field == null)
            {
                return null;
            }

            try
            {
                return field.GetValue(owner) as T;
            }
            catch
            {
                return null;
            }
        }

        internal sealed class QuickbarItem
        {
            private readonly BattleHudAdapter _adapter;
            private readonly QuickbarEntry _entry;

            public QuickbarItem(BattleHudAdapter adapter, QuickbarEntry entry, int index)
            {
                _adapter = adapter;
                _entry = entry;
                Index = index;
            }

            public int Index { get; private set; }

            public string Id
            {
                get
                {
                    ISpellDefinition spell = _entry != null ? _entry.Spell : null;
                    return "combat-quickbar-" + Index + "-" + (spell != null ? spell.Id.ToString() : "empty");
                }
            }

            public string Label
            {
                get
                {
                    ISpellDefinition spell = _entry != null ? _entry.Spell : null;
                    if (spell == null)
                    {
                        return string.Empty;
                    }

                    string name = _adapter.Localize(spell.NameKey, "Spell");
                    int tier = 1;
                    try
                    {
                        tier = Math.Max(1, spell.GetHighestAvailableTier(_adapter._facade.Commanders.Current).Tier);
                    }
                    catch
                    {
                        tier = 1;
                    }

                    return name + ", tier " + tier;
                }
            }

            public bool IsVisible
            {
                get { return _entry != null && _entry.Spell != null && IsGameObjectVisible(_entry as Component); }
            }

            public bool IsEnabled
            {
                get { return _entry != null && _entry.CanCast; }
            }

            public void Focus()
            {
                _adapter.FocusQuickbarEntry(_entry);
            }

            public void Unfocus()
            {
                _adapter.UnfocusQuickbarEntry(_entry);
            }

            public bool Activate()
            {
                return NativeSelectionUtility.Click(_adapter.GetQuickbarEntryButton(_entry));
            }

            public Tooltip Tooltip
            {
                get { return _adapter.GetQuickbarTooltip(_entry); }
            }
        }

        internal sealed class QueueItem
        {
            private readonly BattleHudAdapter _adapter;
            private readonly QueuedTroop _queuedTroop;
            private readonly IQueueHUDEntry _entry;
            private readonly string _roundLabel;
            private readonly bool _isRoundMarker;

            public QueueItem(BattleHudAdapter adapter, QueuedTroop queuedTroop, IQueueHUDEntry entry, int index)
            {
                _adapter = adapter;
                _queuedTroop = queuedTroop;
                _entry = entry;
                Index = index;
            }

            private QueueItem(string id, string roundLabel)
            {
                Id = id;
                _roundLabel = roundLabel;
                _isRoundMarker = true;
            }

            public static QueueItem RoundMarker(string id, string label)
            {
                return new QueueItem(id, label);
            }

            public int Index { get; private set; }

            public string Id { get; private set; }

            public bool IsRoundMarker
            {
                get { return _isRoundMarker; }
            }

            public int TroopId
            {
                get { return IsRoundMarker ? -1 : _queuedTroop.Id; }
            }

            public bool HasNativeEntry
            {
                get { return _entry != null; }
            }

            public string Label
            {
                get
                {
                    if (IsRoundMarker)
                    {
                        return _roundLabel;
                    }

                    return _adapter.GetTroopLabel(TroopId, includePosition: false);
                }
            }

            public bool IsVisible
            {
                get { return IsRoundMarker || TroopId >= 0; }
            }

            public void Focus()
            {
                if (IsRoundMarker || _entry == null)
                {
                    return;
                }

                UIButton button = _adapter.GetQueueEntryButton(_entry);
                NativeSelectionUtility.Select(button);
                NativeSelectionUtility.PointerEnter(button);
            }

            public void Unfocus()
            {
                if (IsRoundMarker || _entry == null)
                {
                    return;
                }

                NativeSelectionUtility.PointerExit(_adapter.GetQueueEntryButton(_entry));
            }

            public Tooltip Tooltip
            {
                get { return IsRoundMarker || _entry == null ? null : Tooltip.ForComponent(_adapter.GetQueueEntryButton(_entry), _adapter._localization); }
            }
        }
    }
}
