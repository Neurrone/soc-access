using SongsOfConquest.Client.Adventure.Menu.Lobby;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The lobby's game settings window, made navigable as a graph. Two stops: the settings the page
    /// draws, and the Cancel and Confirm buttons under them.
    ///
    /// Measured 2026-09-06 at 1280x800 through `/gui/unity`: the window at [325,147,630,519], one
    /// scrolling column of rows at x 364 (539 wide, 861 px of rows in a 379 px viewport), each row
    /// drawing its label at x 367 and its control at x 570, and the buttons under it, Cancel at
    /// x 508 and Confirm at x 646. The page draws NO captions over its rows, so it declares no
    /// regions; a text the game does draw is a read-only row where it stands.
    ///
    /// The rows are whatever the menu's factory built, in the order the page draws them: text
    /// fields, toggles, dropdowns, the turn-timer time fields and the button that resets them.
    /// A dropdown is a combo box opening <see cref="DropListScreen"/> over the game's own popup.
    ///
    /// A TIME field is two edit fields, because that is what the game draws for a keyboard:
    /// <c>UITimeInputField</c> has a minutes field and a seconds field under its "Keyboard" header
    /// and a slider under a "Gamepad" one, and the slider is switched off outside gamepad mode
    /// (measured: `GamepadSlider` reads `visible=false` on every drawn row). Both nodes are named
    /// with the row's own label and say which half they are in the game's words.
    ///
    /// Escape is the game's (<see cref="ConsumesBack"/> false): <c>LobbyMapSettingsMenu.Show</c>
    /// registers <c>InputActions.UI.ExitMenu</c> outside its gamepad branch (decompiled, line 333),
    /// so the key already cancels the window.
    /// </summary>
    public sealed class AdventureLobbyGameSettingsScreen : LiveScreen<AdventureLobbyGameSettingsAdapter>
    {
        private const string RowsStop = "game-settings-rows";
        private const string ButtonsStop = "game-settings-buttons";

        // The rows as nodes: the same reader every settings form is drawn by, told that this
        // window draws no captions and that its node keys are the ones it already had.
        private readonly MenuFormNodes _rows = new MenuFormNodes("game-settings", "game-settings:", false);

        /// <summary>The popup the lobby page holds in <c>_mapSettingsMenu</c>
        /// (<see cref="LobbySources"/>).</summary>
        protected override object ResolveMenu()
        {
            return LobbySources.GameSettings.Current;
        }

        protected override AdventureLobbyGameSettingsAdapter Adapt(object menu)
        {
            return new AdventureLobbyGameSettingsAdapter((LobbyMapSettingsMenu)menu);
        }

        public override string Key
        {
            get { return "game-settings"; }
        }

        /// <summary>Layer 20: a lobby sub-page, over the lobby.</summary>
        public override int Layer
        {
            get { return 20; }
        }

        /// <summary>The window's own drawn title ("Game settings").</summary>
        public override string ScreenName
        {
            get { return Live != null ? Live.Title : null; }
        }

        public override object InitialFocusStop
        {
            get { return RowsStop; }
        }

        /// <summary>The page's own editor, over the page's text fields. GraphScreen takes the rest of its
        /// lifecycle: the raw-input and field-ownership answers, the per-frame update, and the
        /// abandon on leaving and on popping.</summary>
        public override GameTextEditor Editor
        {
            get { return _rows.Editor; }
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
            _rows.AddWindowButton(builder, Live.GetApplyButton());
        }
    }
}
