using System;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Battle;
using SongsOfConquest.Client.Battle.HUD;
using SongsOfConquest.Client.Battle.UI;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class BattleHudAdapter
    {
        private static readonly FieldInfo BattleHudSettingsField =
            AccessTools.Field(typeof(BattleHUDStateHandler), "_settings");
        private static readonly FieldInfo BattleEndTurnButtonField =
            AccessTools.Field(typeof(BattleEndTurnHUD), "_endTurnButton");

        private readonly BattleHUDStateHandler _stateHandler;
        private readonly BattleHUDStateHandler.Settings _settings;
        private readonly ILocalizationHandler _localization;

        public BattleHudAdapter(DiContainer container, IClientBattleFacade facade, ILocalizationHandler localization)
        {
            _localization = localization;
            _stateHandler = Resolve<BattleHUDStateHandler>(container);
            _settings = Resolve<BattleHUDStateHandler.Settings>(container)
                ?? GetField<BattleHUDStateHandler.Settings>(_stateHandler, BattleHudSettingsField);
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

        private UIButton GetEndTurnButton()
        {
            BattleEndTurnHUD hud = _settings != null && _settings.BattleEndTurnContainer != null
                ? _settings.BattleEndTurnContainer.GetComponentInChildren<BattleEndTurnHUD>(true)
                : null;
            return GetField<UIButton>(hud, BattleEndTurnButtonField);
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
    }
}
