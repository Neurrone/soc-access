using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.Menu.Lobby;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The lobby's icon dropdown - the faction, colour, starting wielder, partnership or AI
    /// difficulty a player row opens - made navigable as a graph, in family E's drop list shape.
    ///
    /// The game draws its entries as a horizontal strip, and the list is walked Up and Down anyway
    /// (owner ruling 2026-09-06): a list of values is read down whatever the page does with it. The
    /// entry the dropdown was opened ON says "selected" and is where the list lands, read off the
    /// selection layer the game parks that entry on. Enter is the game's own click on the entry.
    ///
    /// The Cancel row at the end is the mod's, as it was in the widget tree: the popup draws no way
    /// out, and the game's own <c>UI.Cancel</c> registration is the GAMEPAD binding (<c>IconDropdown.Show</c>
    /// registers <c>InputActions.UI.Cancel</c> on <c>Hide</c>, and every keyboard branch in this game
    /// registers <c>UI.ExitMenu</c> instead - the finding <see cref="PlatformUserMenuScreen"/>
    /// established). So Escape does nothing here and the screen claims it, running the same
    /// <c>Hide</c> the Cancel row runs.
    /// </summary>
    public sealed class AdventureLobbyIconDropdownScreen : LiveScreen<AdventureLobbyIconDropdownAdapter>
    {
        private const string OptionsStop = "icon-dropdown";

        // The Cancel row is the mod's own control, so it needs a subject the game does not provide.
        private readonly object _cancelKey = new object();

        /// <summary>The ONE icon dropdown the lobby has: <c>LobbyPlayerMenu</c> hands the same
        /// object to every row it spawns, so which row opened it is not a question the source has to
        /// answer (<see cref="LobbySources"/>).</summary>
        protected override object ResolveMenu()
        {
            return LobbySources.Dropdown.Current;
        }

        protected override AdventureLobbyIconDropdownAdapter Adapt(object menu)
        {
            return new AdventureLobbyIconDropdownAdapter((IconDropdown)menu);
        }

        public override string Key
        {
            get { return "adventure-lobby-icon-dropdown"; }
        }

        /// <summary>Layer 24: over the lobby row it was opened from.</summary>
        public override int Layer
        {
            get { return 24; }
        }

        /// <summary>What the dropdown is choosing, in the game's own words ("Colour", "Faction").
        /// </summary>
        public override string ScreenName
        {
            get
            {
                string title = Live != null ? Live.Title : null;
                return string.IsNullOrWhiteSpace(title) ? null : title;
            }
        }

        /// <summary>Whether the dropdown is OPEN, which is the adapter's own reading of the
        /// container <c>IconDropdown.Show</c> turns on and <c>Hide</c> turns off.</summary>
        public override bool IsActive()
        {
            SyncLive();
            return Live != null && Live.IsPresent();
        }

        public override bool ConsumesBack
        {
            get { return IsActive(); }
        }

        public override bool Back()
        {
            return Cancel();
        }

        public override void OnUnfocus()
        {
            base.OnUnfocus();
            if (Live != null)
            {
                Live.HideNativeTooltip();
            }
        }

        public override void OnPop()
        {
            base.OnPop();
            if (Live != null)
            {
                Live.HideNativeTooltip();
            }
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            IReadOnlyList<AdventureLobbyIconDropdownAdapter.OptionItem> items = Live.GetOptions();
            builder.BeginStop(OptionsStop);
            for (int i = 0; i < items.Count; i++)
            {
                AdventureLobbyIconDropdownAdapter.OptionItem item = items[i];
                if (item == null || !item.IsVisible || item.Entry == null)
                {
                    continue;
                }

                AdventureLobbyIconDropdownAdapter.OptionItem it = item;
                NodeVtable vtable = GraphNodes.Choice(
                    () => it.Label,
                    () => it.IsCurrentValue,
                    () => it.Activate(),
                    () => it.IsEnabled,
                    null,
                    it.Tooltip);
                vtable.OnFocusVisual = it.FocusNative;
                builder.AddItem(new DrawnNode(
                    ControlId.For(it.Entry, "icon-dropdown:" + i),
                    vtable,
                    it.Entry));
            }

            builder.AddItem(new SyntheticNode(
                ControlId.For(_cancelKey, "icon-dropdown:cancel"),
                CancelRow()));
        }

        private NodeVtable CancelRow()
        {
            NodeVtable vtable = GraphNodes.Button(() => Live.CancelLabel, () => Cancel());
            vtable.OnFocusVisual = Live.HideNativeTooltip;
            return vtable;
        }

        private bool Cancel()
        {
            return Live != null && Live.Cancel();
        }
    }
}
