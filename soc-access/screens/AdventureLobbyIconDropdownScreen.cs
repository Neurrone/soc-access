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
    public sealed class AdventureLobbyIconDropdownScreen : GraphScreen
    {
        private const string OptionsStop = "icon-dropdown";

        private readonly AdventureLobbyIconDropdownAdapter _adapter;

        // The Cancel row is the mod's own control, so it needs a subject the game does not provide.
        private readonly object _cancelKey = new object();

        public AdventureLobbyIconDropdownScreen(AdventureLobbyIconDropdownAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            AdventureLobbyIconDropdownAdapter adapter = FindActiveDropdown(null);
            return adapter != null ? new AdventureLobbyIconDropdownScreen(adapter) : null;
        }

        public bool Matches(IconDropdown dropdown)
        {
            return _adapter != null && ReferenceEquals(_adapter.SourceKey, dropdown);
        }

        public override string Key
        {
            get { return "adventure-lobby-icon-dropdown"; }
        }

        /// <summary>What the dropdown is choosing, in the game's own words ("Colour", "Faction").
        /// </summary>
        public override string ScreenName
        {
            get
            {
                string title = _adapter != null ? _adapter.Title : null;
                return string.IsNullOrWhiteSpace(title) ? null : title;
            }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override bool ConsumesBack
        {
            get { return IsPresent(); }
        }

        public override bool Back()
        {
            return Cancel();
        }

        public override void OnUnfocus()
        {
            base.OnUnfocus();
            if (_adapter != null)
            {
                _adapter.HideNativeTooltip();
            }
        }

        public override void OnPop()
        {
            base.OnPop();
            if (_adapter != null)
            {
                _adapter.HideNativeTooltip();
            }
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            IReadOnlyList<AdventureLobbyIconDropdownAdapter.OptionItem> items = _adapter.GetOptions();
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
                    () => Activate(it),
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
            NodeVtable vtable = GraphNodes.Button(() => _adapter.CancelLabel, () => Cancel());
            vtable.OnFocusVisual = _adapter.HideNativeTooltip;
            return vtable;
        }

        private bool Cancel()
        {
            return _adapter != null && _adapter.Cancel();
        }

        private void Activate(AdventureLobbyIconDropdownAdapter.OptionItem item)
        {
            SocAccessMod mod = SocAccessMod.Instance;
            ScreenDetector detector = mod != null ? mod.ScreenDetector : null;
            IconDropdown dropdown = _adapter != null ? _adapter.SourceKey as IconDropdown : null;
            if (detector != null)
            {
                detector.OnAdventureLobbyIconDropdownOptionActivating(dropdown, item.TypeName);
            }

            if (!item.Activate() && detector != null)
            {
                detector.OnAdventureLobbyIconDropdownOptionActivationFailed(dropdown);
            }
        }

        public static AdventureLobbyIconDropdownAdapter FindActiveDropdown(IconDropdown targetDropdown)
        {
            IconDropdown[] dropdowns = Resources.FindObjectsOfTypeAll<IconDropdown>();
            for (int i = 0; i < dropdowns.Length; i++)
            {
                IconDropdown dropdown = dropdowns[i];
                if (dropdown == null)
                {
                    continue;
                }

                if (targetDropdown != null && !ReferenceEquals(targetDropdown, dropdown))
                {
                    continue;
                }

                AdventureLobbyIconDropdownAdapter adapter = new AdventureLobbyIconDropdownAdapter(dropdown);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }
    }
}
