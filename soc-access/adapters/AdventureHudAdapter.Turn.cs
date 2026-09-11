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
    /// THE TURN: the end-turn button, whose title the game only recomposes on hover, and the round
    /// counter drawn beside it.
    ///
    /// Split out of AdventureHudAdapter.cs as a pure move; nothing here changed with the split.
    /// </summary>
    public sealed partial class AdventureHudAdapter
    {
        public bool IsEndTurnButtonVisible()
        {
            return IsAdventureHudVisible()
                && HudGroupVisible(HudStateSettings != null ? HudStateSettings.EndTurnContainer : null)
                && MenuButtonAdapterBase.IsButtonDrawn(EndTurnSettings != null ? EndTurnSettings.EndTurnButton : null);
        }

        public bool IsEndTurnButtonEnabled()
        {
            return MenuButtonAdapterBase.IsButtonEnabledAndDrawn(EndTurnSettings != null ? EndTurnSettings.EndTurnButton : null);
        }

        public string EndTurnButtonLabel
        {
            get { return TooltipLines.First(EndTurnButtonTooltip) ?? string.Empty; }
        }

        /// <summary>
        /// Recompose the end-turn button's tooltip the way the game does when the mouse arrives on it.
        /// <c>EndTurnHUD.Tick</c> refreshes the button's interactable state every frame, but its title
        /// ("End turn" against "Hold on...") is only recomposed by <c>UpdateTooltip</c> on mouse-over,
        /// on a click and on round events, so a mouse user never sees it stale while a reader that
        /// names the button by that title and selects it without hovering does (seen 2026-09-08: the
        /// check mark drawn, the tooltip still saying "Hold on..."). This runs the same private
        /// refresh the hover handler runs.
        /// </summary>
        private void RefreshEndTurnTooltip()
        {
            EndTurnHUD hud = EndTurnHud;
            if (hud == null || EndTurnUpdateTooltipMethod == null)
            {
                return;
            }

            try
            {
                EndTurnUpdateTooltipMethod.Invoke(hud, null);
            }
            catch (Exception ex)
            {
                LogFailureOnce("refreshing the end-turn tooltip", ex);
            }
        }

        public void FocusEndTurnButton()
        {
            NativeSelectionUtility.Select(EndTurnSettings != null ? EndTurnSettings.EndTurnButton : null);
        }

        public bool ClickEndTurnButton()
        {
            return NativeSelectionUtility.Click(EndTurnSettings != null ? EndTurnSettings.EndTurnButton : null);
        }

        /// <summary>The refresh runs when the LINES are read, never when a build asks whether the
        /// button has a tooltip: the map's build passes this tooltip by value every frame, and the
        /// game's private UpdateTooltip is too expensive to run for a tooltip nobody is reading.</summary>
        public Tooltip EndTurnButtonTooltip
        {
            get
            {
                Component button = EndTurnSettings != null ? EndTurnSettings.EndTurnButton : null;
                if (button == null)
                {
                    return null;
                }

                return new Tooltip(
                    () =>
                    {
                        RefreshEndTurnTooltip();
                        return NativeTooltipUtility.GetTooltipLinesForComponent(button, LocalizationHandler);
                    },
                    VisualTooltipMetadata.ForComponent(button),
                    // No composition provoked here, unlike the wielder rows: what this button
                    // composes is PLAIN TEXT, which never lands in _overriddenDetails and so would
                    // be recomposed on every build for an answer that is short whatever it says.
                    isLong: () => NativeTooltipUtility.IsLongForComponent(button));
            }
        }

        public bool IsRoundTextVisible()
        {
            return IsAdventureHudVisible()
                && HudGroupVisible(HudStateSettings != null ? HudStateSettings.TeamQueueContainer : null)
                && !string.IsNullOrWhiteSpace(RoundTextLabel);
        }

        public string RoundTextLabel
        {
            get { return GetRoundTextLabel(); }
        }

        private string GetRoundTextLabel()
        {
            UITextMesh[] texts = Reflect.Get<UITextMesh[]>(TeamQueueHud, TeamQueueRoundTextsField);
            if (texts == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < texts.Length; i++)
            {
                UITextMesh text = texts[i];
                if (text == null || !GameObjects.IsLive(text))
                {
                    continue;
                }

                string label = UITextMeshTextUtility.Spoken(text);
                if (!string.IsNullOrWhiteSpace(label))
                {
                    return label;
                }
            }

            return string.Empty;
        }
    }
}
