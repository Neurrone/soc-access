using SongsOfConquest.Client.Adventure.Menu.Lobby;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The lobby's per-player settings popup, made navigable as a graph. Two stops: the settings the
    /// popup draws, and the Cancel and Confirm buttons under them.
    ///
    /// Measured 2026-09-06 at 1280x800 through `/gui/unity`: the popup at [333,226,613,348] with its
    /// title at y 252, then one column of rows at x 450 - Income multiplier (y 298), Troop production
    /// multiplier (y 337), Start with Marketplace (y 382), XP bonus multiplier (y 421) and the
    /// "Reset to default" button (y 453) - and Cancel (x 508) and Confirm (x 646) at y 514. No
    /// caption is drawn over any of it, so the screen declares no regions.
    ///
    /// Each slider draws a value box over its number (an `EditButton` at x 792), so each slider row
    /// is ACTIVATED by opening the game's own "Provide a number" popup, exactly as the options
    /// window's sliders are; the arrows remain the way the value is moved.
    ///
    /// Escape is the game's (<see cref="ConsumesBack"/> false): <c>LobbyPlayerSettingsMenu.Show</c>
    /// registers <c>InputActions.UI.ExitMenu</c> outside its gamepad branch (decompiled, line 146),
    /// so the key already cancels the popup.
    /// </summary>
    public sealed class AdventureLobbyPlayerSettingsScreen : LiveScreen<AdventureLobbyPlayerSettingsAdapter>
    {
        private const string RowsStop = "player-settings-rows";
        private const string ButtonsStop = "player-settings-buttons";

        // The rows as nodes: the same reader every settings form is drawn by, told that this popup
        // draws no captions and that its node keys are the ones it already had.
        private readonly MenuFormNodes _rows = new MenuFormNodes("player-settings", "player-settings:", false);

        /// <summary>The popup the player menu holds in its settings
        /// (<see cref="LobbySources"/>).</summary>
        protected override object ResolveMenu()
        {
            return LobbySources.PlayerSettings.Current;
        }

        protected override AdventureLobbyPlayerSettingsAdapter Adapt(object menu)
        {
            return new AdventureLobbyPlayerSettingsAdapter((LobbyPlayerSettingsMenu)menu);
        }

        public override string Key
        {
            get { return "player-settings"; }
        }

        /// <summary>Layer 20: a lobby sub-page, over the lobby.</summary>
        public override int Layer
        {
            get { return 20; }
        }

        /// <summary>The popup's own drawn title ("Player settings"), and after it the player it is
        /// about: one popup serves every row and its title says so for none of them, so arrival
        /// would otherwise never say whose settings these are.</summary>
        public override string ScreenName
        {
            get
            {
                if (Live == null)
                {
                    return null;
                }

                string title = Live.Title;
                string player = Live.PlayerName;
                return string.IsNullOrWhiteSpace(player) || string.IsNullOrWhiteSpace(title)
                    ? title
                    : ModText.Get(ModStrings.Common.ListSeparator, title, player);
            }
        }

        public override object InitialFocusStop
        {
            get { return RowsStop; }
        }


        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            builder.BeginStop(RowsStop);
            _rows.BuildRows(builder, Live.GetContentControls());

            builder.BeginStop(ButtonsStop);
            _rows.AddWindowButton(builder, Live.GetCancelButton());
            _rows.AddWindowButton(builder, Live.GetConfirmButton());
        }
    }
}
