using System.Collections.Generic;
using SongsOfConquest.Client.Menu.Options;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The game's options window, made navigable as a graph. One window serves both the main menu and
    /// a game in progress, so making it navigable once covers every route to it.
    ///
    /// Three places to be, and Tab moves between them: the column of category tabs down the left, the
    /// settings of the category currently showing, and the button along the bottom. Measured
    /// 2026-09-06 at 1280x800: the tabs at x 270, seven of them 30 px apart; the content column at
    /// x 489, 486 wide, scrolling (904 px of rows inside a 519 px panel), each row with its label at
    /// the left and its control at the right; the OK button under it.
    ///
    /// The tab bar switches ON FOCUS rather than on Enter. A tab whose page you cannot see is not a
    /// thing a player wants to stand on, and the game's own switch is instant and costs nothing, so
    /// arriving at a tab and arriving at its page are the same event. Enter does it too, for the
    /// player who expects to have to ask.
    ///
    /// The page draws captions over groups of rows ("General", "Battle", "Adventure"). A caption is
    /// the REGION its rows belong to - Alt+Up and Alt+Down jump between them and the name is spoken on
    /// the way in - and not a row of its own, because there is nothing there to operate. WITH ONE
    /// EXCEPTION, measured on the Controls page: the key binding rows are drawn by
    /// <c>MenuFactoryController.AddKeyBinding</c>, which the adapter does not read, so the category
    /// captions there head nothing at all. A caption with no rows under it stays a read-only row, so
    /// the page still reads what it draws.
    ///
    /// Scrolling into view is the game's: the content panel's own <c>AutoScrollToSelected</c> follows
    /// the natively selected row, so every row's focus visual is its adapter <c>Focus</c>.
    ///
    /// Escape is the game's: <c>OptionsMenu.ReregisterInput</c> registers <c>UI.ExitMenu</c> on its
    /// keyboard branch (decompiled, lines 532 to 544), so the key already closes the window.
    /// </summary>
    public sealed class OptionsScreen : LiveScreen<OptionsMenuAdapter>
    {
        private const string TabsStop = "options-tabs";
        private const string RowsStop = "options-rows";
        private const string ButtonsStop = "options-buttons";

        /// <summary>The rows of the form, declared the way every settings form the game draws is
        /// declared - shared with the mod's own options dialog, which is a copy of this panel.</summary>
        private readonly MenuFormNodes _rows = new MenuFormNodes("options");

        /// <summary>The one options window the project container holds for the whole game; the main
        /// menu and a game in progress open the same one.</summary>
        private readonly ScreenSource<OptionsMenu> _source = ScreenSource<OptionsMenu>.FromProject();

        protected override object ResolveMenu()
        {
            return _source.Current;
        }

        protected override OptionsMenuAdapter Adapt(object menu)
        {
            return new OptionsMenuAdapter((OptionsMenu)menu);
        }

        public override string Key
        {
            get { return "options"; }
        }

        /// <summary>Layer 42: over the pause menu or the main menu that opens it.</summary>
        public override int Layer
        {
            get { return 42; }
        }

        public override string ScreenName
        {
            get { return ModText.Get(ModStrings.Screens.Options); }
        }

        /// <summary>The tab bar, so arrival lands on the category actually showing rather than on the
        /// first tab - which, with the switch-on-focus above, would change the page just by arriving.
        /// </summary>
        public override object InitialFocusStop
        {
            get { return TabsStop; }
        }

        public override bool IsActive()
        {
            SyncLive();
            return Live != null && Live.IsPresent();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            builder.BeginStop(TabsStop);
            BuildTabs(builder);

            builder.BeginStop(RowsStop);
            _rows.BuildRows(builder, Live.GetCurrentContentControls());

            builder.BeginStop(ButtonsStop);
            _rows.AddWindowButton(
                builder,
                Live.GetOkButton(),
                () => ModText.Get(ModStrings.Screens.Close));
        }

        // ---- the category tabs ----

        private void BuildTabs(GraphBuilder builder)
        {
            IReadOnlyList<OptionsMenuAdapter.TabItem> tabs = Live.GetTabs();
            for (int i = 0; i < tabs.Count; i++)
            {
                OptionsMenuAdapter.TabItem tab = tabs[i];
                if (tab == null || !tab.IsVisible())
                {
                    continue;
                }

                int index = i;
                NodeVtable vtable = GraphNodes.Tab(
                    tab.GetLabel,
                    () => Live.GetActiveTabIndex() == index,
                    tab.IsVisible);
                // Focusing the tab IS switching to it; the guard makes re-focusing the showing tab a
                // no-op, so re-entering the bar does not restart the page.
                vtable.OnFocusVisual = () =>
                {
                    if (Live.GetActiveTabIndex() != index)
                    {
                        tab.Select();
                    }
                };
                vtable.OnActivate = () => tab.Select();
                builder.AddItem(new SyntheticNode(ControlId.Structural("options:tab/" + tab.Id), vtable));
            }
        }
    }
}
