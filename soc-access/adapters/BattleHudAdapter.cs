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
using SongsOfConquest.Client.Menu.Options;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Battle;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquest.Common.Spells;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class BattleHudAdapter
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

        // The three child lookups the HUD's own containers answer once for the life of a battle, and
        // the queue pool's ActiveItems property: resolved on demand and remembered, MISSES INCLUDED
        // (the probed flags), so an absent panel costs one lookup rather than one per frame. The
        // combat screen is a graph screen and rebuilds every frame.
        private BattleEndTurnHUD _endTurnHud;
        private bool _endTurnHudProbed;
        private UIButton _optionsButton;
        private bool _optionsButtonProbed;
        private GameLogHandleUI _gameLogHandle;
        private bool _gameLogHandleProbed;
        private PropertyInfo _queuePoolActiveItemsProperty;
        private Type _queuePoolType;
        private FieldInfo _troopViewStatusField;
        private Type _troopContainerType;

        public BattleHudAdapter(DiContainer container, IClientBattleFacade facade, ILocalizationHandler localization)
        {
            _facade = facade;
            _localization = localization;
            _stateHandler = ResolveHud<BattleHUDStateHandler, BattleHUDStateHandlerInstaller>(container);
            _settings = Reflect.Resolve<BattleHUDStateHandler.Settings>(container)
                ?? Reflect.Get<BattleHUDStateHandler.Settings>(_stateHandler, BattleHudSettingsField);
            _gameLog = Reflect.Resolve<IGameLog>(container);
            _abilityUtility = Reflect.Resolve<ITroopAbilityUtility>(container);
            _battleViewManager = ResolveHud<BattleViewManager, BattleViewInstaller>(container);
            _spellsLookup = Reflect.Resolve<ISpellsLookup>(container);
            Commanders = new BattleCommanderHudAdapter(_settings, facade, localization);
        }

        public BattleCommanderHudAdapter Commanders { get; private set; }

        public bool IsSpellbookButtonVisible()
        {
            UIButton button = GetSpellbookButton();
            return IsButtonVisible(button);
        }

        /// <summary>Whether the Spells button the game is drawing belongs to this side. There is one
        /// spellbook button on the screen at a time - the game draws it in the panel of whichever side
        /// owns it - so the side's own panel is the only place it should be declared.</summary>
        public bool IsSpellbookButtonVisible(CombatHudSide side)
        {
            return IsSpellbookButtonVisible() && GetSide(GetSpellsHud()) == side;
        }

        public bool IsSpellbookButtonEnabled()
        {
            SpellsHUD spellsHud = GetSpellsHud();
            return spellsHud != null && spellsHud.IsInteractable() && IsButtonInteractable(spellsHud.SpellbookButton);
        }

        public string SpellbookButtonLabel
        {
            // The button's tooltip carries the same word with the hotkey ("Spells (V)"); label from it
            // so the readout is not "Spells, button, Spells (V)", the tooltip's first line then
            // dropping as a duplicate. Fall back to the plain game label when no tooltip is drawn.
            get
            {
                string label = TooltipLines.First(SpellbookButtonTooltip);
                return string.IsNullOrWhiteSpace(label)
                    ? SpokenText.Get(_localization, "Common/HUD/SpellbookButton", "Spellbook")
                    : label;
            }
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
            // As the adventure end-turn button already does: take the label from the tooltip so its
            // hotkey-bearing first line is not read twice.
            get
            {
                string label = TooltipLines.First(EndTurnButtonTooltip);
                return string.IsNullOrWhiteSpace(label)
                    ? SpokenText.Get(_localization, "Battle/Labels/EndTurn", "End turn")
                    : label;
            }
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

        public bool IsOptionsButtonVisible()
        {
            return HudGroupVisible(_settings != null ? _settings.OptionsButtonsContainer : null)
                && IsButtonVisible(GetOptionsButton());
        }

        public bool IsOptionsButtonEnabled()
        {
            return IsButtonInteractable(GetOptionsButton());
        }

        public string OptionsButtonLabel
        {
            get { return TooltipLines.First(OptionsButtonTooltip); }
        }

        public void FocusOptionsButton()
        {
            NativeSelectionUtility.Select(GetOptionsButton());
        }

        public bool ClickOptionsButton()
        {
            return NativeSelectionUtility.Click(GetOptionsButton());
        }

        public Tooltip OptionsButtonTooltip
        {
            get { return Tooltip.ForComponent(GetOptionsButton(), _localization); }
        }

        public bool IsTargetingInstructionVisible()
        {
            return !string.IsNullOrWhiteSpace(TargetingInstructionText);
        }

        public void SetAbilityTargetInstructionText(string text)
        {
            _abilityTargetInstructionText = SpokenLines.Clean(text);
        }

        public void ClearAbilityTargetInstructionText()
        {
            _abilityTargetInstructionText = null;
        }

        public void SetSpellTargetInstructionText(string text)
        {
            _spellTargetInstructionText = SpokenLines.Clean(text);
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

        /// <summary>Whether the Cancel spell button, which the game draws in the Spells spot while a
        /// spell is being aimed, belongs to this side.</summary>
        public bool IsCancelSpellButtonVisible(CombatHudSide side)
        {
            return IsCancelSpellButtonVisible() && GetSide(GetSpellsHudWithVisibleCancelSpell()) == side;
        }

        public bool IsCancelSpellButtonEnabled()
        {
            return IsButtonInteractable(GetCancelSpellButton());
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
                string label = ability != null ? SpokenText.Get(_localization, ability.NameKey, null) : null;
                return !string.IsNullOrWhiteSpace(label) ? label : string.Empty;
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
            return BattleFacadeState.CurrentTroopId(_facade);
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
                    items.Add(QueueItem.RoundMarker(nextRound));
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
                    string text = SpokenLines.Clean(entries[i]);
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

        /// <summary>Which side's panel a spells HUD is drawn in, or null when it is neither.</summary>
        private CombatHudSide? GetSide(SpellsHUD spellsHud)
        {
            if (spellsHud == null || _stateHandler == null)
            {
                return null;
            }

            if (ReferenceEquals(spellsHud, _stateHandler.AttackerSpellsHUD))
            {
                return CombatHudSide.Attacker;
            }

            return ReferenceEquals(spellsHud, _stateHandler.DefenderSpellsHUD)
                ? CombatHudSide.Defender
                : (CombatHudSide?)null;
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
            object settings = Reflect.Get<object>(spellsHud, SpellsHudSettingsField);
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
            object settings = Reflect.Get<object>(spellsHud, SpellsHudSettingsField);
            return settings != null && SpellsHudCancelSpellButtonField != null
                ? SpellsHudCancelSpellButtonField.GetValue(settings) as UIButton
                : null;
        }

        private UIButton GetAbilityButton()
        {
            BattleTroopStatusPanel panel = GetCurrentTroopStatusPanel();
            return Reflect.Get<UIButton>(panel, TroopStatusPanelAbilityButtonField);
        }

        private UIButton GetCancelAbilityButton()
        {
            BattleTroopStatusPanel panel = GetCurrentTroopStatusPanel();
            return Reflect.Get<UIButton>(panel, TroopStatusPanelCancelAbilityButtonField);
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
            if (container == null)
            {
                return null;
            }

            Type containerType = container.GetType();
            if (_troopContainerType != containerType)
            {
                _troopContainerType = containerType;
                _troopViewStatusField = AccessTools.Field(containerType, "ViewStatus");
            }

            return _troopViewStatusField != null ? _troopViewStatusField.GetValue(container) as BattleTroopStatusPanel : null;
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

        // ONCE PER ADAPTER, miss included: the end-turn HUD is instantiated with the battle HUD and
        // outlives every page, so a session whose container is absent costs one walk and not one a
        // frame.
        private UIButton GetEndTurnButton()
        {
            if (_endTurnHud == null && !_endTurnHudProbed)
            {
                _endTurnHudProbed = true;
                _endTurnHud = _settings != null && _settings.BattleEndTurnContainer != null
                    ? _settings.BattleEndTurnContainer.GetComponentInChildren<BattleEndTurnHUD>(true)
                    : null;
            }

            return Reflect.Get<UIButton>(_endTurnHud, BattleEndTurnButtonField);
        }

        // ONCE PER ADAPTER, miss included: the options button is instantiated with the HUD and
        // outlives every page, and the build asks whether it is drawn and for its tooltip.
        private UIButton GetOptionsButton()
        {
            if (_optionsButton != null || _optionsButtonProbed)
            {
                return _optionsButton;
            }

            _optionsButtonProbed = true;
            GameObject container = _settings != null ? _settings.OptionsButtonsContainer : null;
            OptionsButtonInstaller installer = container != null ? container.GetComponentInChildren<OptionsButtonInstaller>(false) : null;
            _optionsButton = installer != null
                ? installer.GetComponent<UIButton>()
                : (container != null ? container.GetComponentInChildren<UIButton>(false) : null);
            return _optionsButton;
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

            Type poolType = pool.GetType();
            if (_queuePoolType != poolType)
            {
                _queuePoolType = poolType;
                _queuePoolActiveItemsProperty = AccessTools.Property(poolType, "ActiveItems");
            }

            PropertyInfo property = _queuePoolActiveItemsProperty;
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
            return BattleFacadeState.CurrentRound(_facade);
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

            string spellName = UITextMeshTextUtility.Spoken(Reflect.Get<UITextMesh>(instruction, SpellTargetInstructionSpellNameField));
            string text = UITextMeshTextUtility.Spoken(Reflect.Get<UITextMesh>(instruction, SpellTargetInstructionTextField));
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
            object settings = Reflect.Get<object>(spellsHud, SpellsHudSettingsField);
            return settings != null && SpellsHudTargetInstructionField != null
                ? SpellsHudTargetInstructionField.GetValue(settings) as BattleSpellTargetInstruction
                : null;
        }

        // ONCE PER ADAPTER, miss included, and only from the focus and unfocus actions: the game log
        // handle is instantiated with the battle HUD and never replaced under it.
        private GameLogHandleUI GetGameLogHandle()
        {
            if (_gameLogHandle != null || _gameLogHandleProbed)
            {
                return _gameLogHandle;
            }

            _gameLogHandleProbed = true;
            GameObject container = _settings != null ? _settings.GameLogContainer : null;
            _gameLogHandle = container != null ? container.GetComponentInChildren<GameLogHandleUI>(true) : null;
            return _gameLogHandle;
        }

        public TroopInfo GetCurrentTroopInfo()
        {
            return GetTroopInfo(GetCurrentTroopId());
        }

        public TroopInfo GetTroopInfo(int troopId)
        {
            try
            {
                IBattleTroopState troop = _facade != null && _facade.Troops != null ? _facade.Troops.Get(troopId) : null;
                if (troop == null)
                {
                    return TroopInfo.Unknown();
                }

                int size = troop.Stats != null ? troop.Stats.Size : 0;
                string name = SpokenLines.Clean(_facade.Troops.GetName(troop.Id, size));
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = string.Empty;
                }

                int localTeamId = GetLocalTeamId();
                bool isEnemy = localTeamId >= 0 && troop.TeamId != localTeamId;
                return new TroopInfo(name, size, troop.Stats != null, isEnemy, troop.Position);
            }
            catch
            {
                return TroopInfo.Unknown();
            }
        }

        private int GetLocalTeamId()
        {
            return BattleFacadeState.LocalTeamId(_facade);
        }

        private UIButton GetQuickbarEntryButton(QuickbarEntry entry)
        {
            return Reflect.Get<UIButton>(entry, QuickbarEntryButtonField);
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
            string name = SpokenText.Get(_localization, spell.NameKey, "Spell");
            int tier = GetCurrentSpellTier(spell, commander);
            lines.Add(tier > 0 ? name + ", " + GetTierLabel(tier) : name);

            string lore = SpokenText.Get(_localization, spell.DescriptionKey, string.Empty);
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
                        lines.Add(SpokenText.Get(_localization, "Spells/Spellbook/SpellDurationHeader", "Duration") + ": " + duration);
                    }
                }
            }

            string cost = FormatSpellCost(spell);
            if (!string.IsNullOrWhiteSpace(cost))
            {
                lines.Add(SpokenText.Get(_localization, "Spells/Spellbook/SpellCostHeader", "Cost") + ": " + cost);
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

        public string GetTierLabel(int tier)
        {
            return SpokenText.Get(_localization, "Spells/Spellbook/SpellTierHeader", "tier " + tier, tier);
        }

        private string GetEssenceName(EssenceType type)
        {
            string key = "Units/Types/" + type;
            string localized = SpokenText.Get(_localization, key, string.Empty);
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
            return Reflect.Get<UIButton>(entry, QueueEntryButtonField);
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

        private static bool HudGroupVisible(GameObject gameObject)
        {
            if (gameObject == null || !gameObject.activeInHierarchy)
            {
                return false;
            }

            CanvasGroup canvasGroup = gameObject.GetComponent<CanvasGroup>();
            return canvasGroup == null || canvasGroup.alpha > 0.01f;
        }

        /// <summary>
        /// A HUD bound in its own MonoInstaller's container is invisible to the scene container the
        /// battle installer hands out (the adventure map's town list was lost that way), so a miss
        /// there is retried on the installer's own container. Constructor-time, once per battle.
        /// </summary>
        private static T ResolveHud<T, TInstaller>(DiContainer container)
            where T : class
            where TInstaller : MonoInstallerBase
        {
            T found = Reflect.Resolve<T>(container);
            if (found != null)
            {
                return found;
            }

            TInstaller[] installers = Resources.FindObjectsOfTypeAll<TInstaller>();
            for (int i = 0; i < installers.Length && found == null; i++)
            {
                TInstaller installer = installers[i];
                if (installer != null && installer.gameObject.scene.IsValid())
                {
                    found = Reflect.Resolve<T>(Reflect.InstallerContainer(installer));
                }
            }

            return found;
        }

        public sealed class QuickbarItem
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

            public bool HasSpell
            {
                get
                {
                    ISpellDefinition spell = _entry != null ? _entry.Spell : null;
                    return spell != null;
                }
            }

            public string SpellName
            {
                get
                {
                    ISpellDefinition spell = _entry != null ? _entry.Spell : null;
                    if (spell == null)
                    {
                        return string.Empty;
                    }

                    return SpokenText.Get(_adapter._localization, spell.NameKey, "Spell");
                }
            }

            public int SpellTier
            {
                get
                {
                    ISpellDefinition spell = _entry != null ? _entry.Spell : null;
                    if (spell == null)
                    {
                        return 0;
                    }

                    int tier = 1;
                    try
                    {
                        tier = Math.Max(1, spell.GetHighestAvailableTier(_adapter._facade.Commanders.Current).Tier);
                    }
                    catch
                    {
                        tier = 1;
                    }

                    return tier;
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

        public sealed class QueueItem
        {
            private readonly BattleHudAdapter _adapter;
            private readonly QueuedTroop _queuedTroop;
            private readonly IQueueHUDEntry _entry;
            private readonly int _roundNumber;
            private readonly bool _isRoundMarker;

            public QueueItem(BattleHudAdapter adapter, QueuedTroop queuedTroop, IQueueHUDEntry entry, int index)
            {
                _adapter = adapter;
                _queuedTroop = queuedTroop;
                _entry = entry;
                Index = index;
            }

            private QueueItem(int roundNumber)
            {
                _roundNumber = roundNumber;
                _isRoundMarker = true;
            }

            public static QueueItem RoundMarker(int roundNumber)
            {
                return new QueueItem(roundNumber);
            }

            public int Index { get; private set; }

            public bool IsRoundMarker
            {
                get { return _isRoundMarker; }
            }

            public int RoundNumber
            {
                get { return _roundNumber; }
            }

            public int TroopId
            {
                get { return IsRoundMarker ? -1 : _queuedTroop.Id; }
            }

            public bool HasNativeEntry
            {
                get { return _entry != null; }
            }

            public TroopInfo Troop
            {
                get { return IsRoundMarker ? TroopInfo.Unknown() : _adapter.GetTroopInfo(TroopId); }
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

        public sealed class TroopInfo
        {
            public TroopInfo(string name, int size, bool hasSize, bool isEnemy, Vector2Int position)
            {
                Name = name ?? string.Empty;
                Size = size;
                HasSize = hasSize;
                IsEnemy = isEnemy;
                Position = position;
                HasPosition = true;
                IsKnown = true;
            }

            private TroopInfo()
            {
                Name = string.Empty;
            }

            public string Name { get; private set; }
            public int Size { get; private set; }
            public bool HasSize { get; private set; }
            public bool IsEnemy { get; private set; }
            public Vector2Int Position { get; private set; }
            public bool HasPosition { get; private set; }
            public bool IsKnown { get; private set; }

            public static TroopInfo Unknown()
            {
                return new TroopInfo();
            }
        }
    }
}
