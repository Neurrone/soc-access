using System.Collections.Generic;
using _8_UILayer.ClientView.Menu.Paus;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The in-game pause menu, made navigable as a graph. One stop: the menu's buttons, in the order
    /// the game draws them.
    ///
    /// The adapter lists the buttons in the order the pause menu's class declares its serialized
    /// fields, which is not the order on screen: Continue Game is drawn alone at the bottom, below
    /// the column that runs Quick Save, Quick Load, Save Game, Load Game, Restart Mission, Options,
    /// Mod options, Tutorials &amp; Codex, Quit to Main Menu, Quit to Desktop, so Continue Game reads
    /// last. The order is measured off each button's own transform every build, so a layout the game
    /// changes is followed; a stable tiebreak keeps two buttons at one height in list order.
    ///
    /// Buttons the game hides are not declared; buttons it disables stay in the list and say so.
    /// Escape is the game's own: it registers the pause menu's exit action itself, so this screen
    /// claims nothing.
    /// </summary>
    public sealed class PauseMenuScreen : LiveScreen<PauseMenuAdapter>
    {
        private const string MenuStop = "pause-menu";

        /// <summary>The one pause menu the project container holds for the whole game.</summary>
        private readonly ScreenSource<PauseMenu> _source = ScreenSource<PauseMenu>.FromProject();

        protected override object ResolveMenu()
        {
            return _source.Current;
        }

        protected override PauseMenuAdapter Adapt(object menu)
        {
            return new PauseMenuAdapter((PauseMenu)menu);
        }

        public override string Key
        {
            get { return "pause-menu"; }
        }

        /// <summary>Layer 40: over the game it pauses.</summary>
        public override int Layer
        {
            get { return 40; }
        }

        /// <summary>The title the game draws over the menu ("Game Menu").</summary>
        public override string ScreenName
        {
            get
            {
                string title = Live != null ? Live.Title : null;
                return string.IsNullOrWhiteSpace(title) ? null : title;
            }
        }

        /// <summary>NO HANDOVER any more. The menu used to stay active for up to two seconds after it
        /// closed, so the map was not handed back for the frames between the pause menu going and the
        /// options, save/load or codex window arriving. There are no such frames: MenuSystem's
        /// callback runs the close and the open in ONE call, and each of those three screens now reads
        /// its own menu every frame instead of waiting for a hook that answered a frame late.
        /// Measured 2026-09-09 on the adventure map with <c>/wait</c>, which sees single frames: zero
        /// frames with neither menu present on all three routes.</summary>
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

            builder.BeginStop(MenuStop);
            List<PauseMenuAdapter.Item> items = DrawnOrder(Live.Items);
            for (int i = 0; i < items.Count; i++)
            {
                PauseMenuAdapter.Item item = items[i];
                NodeVtable vtable = GraphNodes.Button(item.GetLabel, () => item.Activate(), item.IsEnabled);
                // The game's own selection visual, which is what the mouse leaves behind on hover.
                vtable.OnFocusVisual = item.Select;
                // Arrival lands on the first drawn button (owner ruling 2026-09-07), not on Continue
                // Game, which the game makes its default selectable but draws last.
                builder.AddItem(new DrawnNode(
                    ControlId.For(item.Button, "pause-menu:" + item.Id),
                    vtable,
                    item.Button));
            }
        }

        /// <summary>
        /// The visible items, top to bottom as the game draws them. Measured off each button's own
        /// transform every build; the insertion sort is stable, so two buttons at one height keep the
        /// adapter's order.
        /// </summary>
        private static List<PauseMenuAdapter.Item> DrawnOrder(IReadOnlyList<PauseMenuAdapter.Item> items)
        {
            List<PauseMenuAdapter.Item> drawn = new List<PauseMenuAdapter.Item>();
            List<float> tops = new List<float>();
            for (int i = 0; items != null && i < items.Count; i++)
            {
                PauseMenuAdapter.Item item = items[i];
                if (item == null || item.Button == null || !item.IsVisible())
                {
                    continue;
                }

                drawn.Add(item);
                tops.Add(Top(item));
            }

            for (int i = 1; i < drawn.Count; i++)
            {
                PauseMenuAdapter.Item moving = drawn[i];
                float top = tops[i];
                int j = i - 1;
                while (j >= 0 && tops[j] < top)
                {
                    drawn[j + 1] = drawn[j];
                    tops[j + 1] = tops[j];
                    j--;
                }

                drawn[j + 1] = moving;
                tops[j + 1] = top;
            }

            return drawn;
        }

        private static float Top(PauseMenuAdapter.Item item)
        {
            Component component = item.Button;
            return component != null ? component.transform.position.y : 0f;
        }
    }
}
