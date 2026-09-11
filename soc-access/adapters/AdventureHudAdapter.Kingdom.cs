using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.Map;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Client.Menu.Options;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Gamestate.Commander;
using SongsOfConquest.Common.Levels;
using SongsOfConquest.Common.Localization;
using SongsOfConquest.Common.Objectives;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// THE KINGDOM BAND: the game-menu button, the five kingdom overview buttons, and the bug
    /// report button drawn beside them.
    ///
    /// Split out of AdventureHudAdapter.cs as a pure move; nothing here changed with the split.
    /// </summary>
    public sealed partial class AdventureHudAdapter
    {
        public bool IsOptionsButtonVisible()
        {
            return IsAdventureHudVisible()
                && HudGroupVisible(HudStateSettings != null ? HudStateSettings.OptionsButtonsContainer : null)
                && MenuButtonAdapterBase.IsButtonDrawn(GetOptionsButton());
        }

        public bool IsOptionsButtonEnabled()
        {
            return MenuButtonAdapterBase.IsButtonEnabledAndDrawn(GetOptionsButton());
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
            get { return Tooltip.ForComponent(GetOptionsButton(), LocalizationHandler); }
        }

        public bool IsKingdomOverviewMenuVisible()
        {
            return HudGroupVisible(HudStateSettings != null ? HudStateSettings.KingdomOverviewContainer : null)
                && KingdomSettings != null;
        }

        public string GetKingdomOverviewLabel(int index)
        {
            return TooltipLines.First(GetKingdomOverviewTooltip(index));
        }

        public bool IsKingdomOverviewItemVisible(int index)
        {
            return MenuButtonAdapterBase.IsButtonDrawn(GetKingdomOverviewButton(index));
        }

        public bool IsKingdomOverviewItemEnabled(int index)
        {
            return MenuButtonAdapterBase.IsButtonEnabledAndDrawn(GetKingdomOverviewButton(index));
        }

        public void FocusKingdomOverviewItem(int index)
        {
            NativeSelectionUtility.Select(GetKingdomOverviewButton(index));
        }

        public bool ClickKingdomOverviewItem(int index)
        {
            return NativeSelectionUtility.Click(GetKingdomOverviewButton(index));
        }

        public Tooltip GetKingdomOverviewTooltip(int index)
        {
            return Tooltip.ForComponent(GetKingdomOverviewButton(index), LocalizationHandler);
        }

        public bool IsBugReportButtonVisible()
        {
            return MenuButtonAdapterBase.IsButtonDrawn(KingdomSettings != null ? KingdomSettings.BugReportButton : null);
        }

        public bool IsBugReportButtonEnabled()
        {
            return MenuButtonAdapterBase.IsButtonEnabledAndDrawn(KingdomSettings != null ? KingdomSettings.BugReportButton : null);
        }

        public string BugReportButtonLabel
        {
            get { return TooltipLines.First(BugReportButtonTooltip); }
        }

        public void FocusBugReportButton()
        {
            NativeSelectionUtility.Select(KingdomSettings != null ? KingdomSettings.BugReportButton : null);
        }

        public bool ClickBugReportButton()
        {
            return NativeSelectionUtility.Click(KingdomSettings != null ? KingdomSettings.BugReportButton : null);
        }

        public Tooltip BugReportButtonTooltip
        {
            get { return Tooltip.ForComponent(KingdomSettings != null ? KingdomSettings.BugReportButton : null, LocalizationHandler); }
        }

        // The map and combat builds both ask whether this button is drawn and for its tooltip, so
        // the walk was paid up to four times a frame. The button is instantiated with the HUD and
        // outlives every page, so it is found once per adapter and the miss is remembered too -
        // the same shape BattleHudAdapter.GetOptionsButton already uses.
        private UIButton GetOptionsButton()
        {
            if (_optionsButton != null || _optionsButtonProbed)
            {
                return _optionsButton;
            }

            _optionsButtonProbed = true;
            GameObject container = HudStateSettings != null ? HudStateSettings.OptionsButtonsContainer : null;
            OptionsButtonInstaller installer = container != null ? container.GetComponentInChildren<OptionsButtonInstaller>(false) : null;
            _optionsButton = installer != null
                ? installer.GetComponent<UIButton>()
                : (container != null ? container.GetComponentInChildren<UIButton>(false) : null);
            return _optionsButton;
        }

        private UIButton GetKingdomOverviewButton(int index)
        {
            KingdomInformationHUD.Settings settings = KingdomSettings;
            if (settings == null)
            {
                return null;
            }

            switch (index)
            {
                case 0:
                    return settings.OwnedEntitiesButton;
                case 1:
                    return settings.TroopIncomeButton;
                case 2:
                    return settings.ResearchButton;
                case 3:
                    return settings.MarketplaceButton;
                case 4:
                    return settings.PlayerButton;
                default:
                    return null;
            }
        }
    }
}
