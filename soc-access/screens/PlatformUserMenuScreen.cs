using System.Collections.Generic;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The lobby's player actions popup, made navigable as a graph in the dialog contract its
    /// family's representative (<see cref="MessageDialogScreen"/>) established: one stop, the heading
    /// as a line of its own and as the screen name, then the actions, then the way out.
    ///
    /// The popup draws less than a dialog does. Measured 2026-09-06 at 1280x800 under
    /// `PlatformUserMenu`: a `PlatformUserMainContainer` at [141,122,163,41] holding one
    /// `PlatformUserButtonEntry` per action ("Set Name" here), a full-screen `UIBlocker` behind it,
    /// and nothing else - no drawn heading and no drawn Cancel. So the heading is the menu's own
    /// localized name ("Show Player Actions", `Lobby/LobbyPlayerMenu/ShowPlayerActions`), which the
    /// widget screen also spoke as the container's name, and the Cancel at the end is the mod's, as
    /// it was before: without it the keyboard has no way out but Escape.
    ///
    /// Focus starts on the first action, there being no body to start on.
    ///
    /// ESCAPE is claimed. `PlatformUserMenu.Show` registers <c>UI.Cancel</c> on <c>Hide</c>
    /// (decompiled line 271) and nothing else; <c>UI.Cancel</c> is the GAMEPAD binding throughout
    /// this game - every keyboard branch registers <c>UI.ExitMenu</c> instead, as
    /// <c>ConfirmPopup.Show</c> lines 182 to 208 and <c>QuitToDesktopPopup.Show</c> lines 145 to 152
    /// both show - so Escape does nothing here. The screen claims it and runs the same <c>Hide</c>
    /// the game's own callback would.
    /// </summary>
    public sealed class PlatformUserMenuScreen : LiveScreen<PlatformUserMenuAdapter>
    {
        private const string MenuStop = "platform-user-menu";

        // A subject of its own for each node the popup gives no component for.
        private readonly object _headingKey = new object();
        private readonly object _cancelKey = new object();

        /// <summary>The one platform user menu the project container holds for the whole game.
        /// </summary>
        private readonly ScreenSource<PlatformUserMenu> _source = ScreenSource<PlatformUserMenu>.FromProject();

        protected override object ResolveMenu()
        {
            return _source.Current;
        }

        protected override PlatformUserMenuAdapter Adapt(object menu)
        {
            return new PlatformUserMenuAdapter((PlatformUserMenu)menu);
        }

        public override string Key
        {
            get { return "platform-user-menu"; }
        }

        /// <summary>Layer 7: a popup over the lobby.</summary>
        public override int Layer
        {
            get { return 7; }
        }

        /// <summary>The menu's own name, spoken once on arrival.</summary>
        public override string ScreenName
        {
            get
            {
                string title = Live != null ? Live.Title : null;
                return string.IsNullOrWhiteSpace(title) ? null : title;
            }
        }

        public override bool IsActive()
        {
            SyncLive();
            return Live != null && Live.IsPresent();
        }

        /// <summary>Escape: the game binds only its gamepad Cancel here, so the key would do nothing;
        /// the screen takes it and closes the popup the way the game does.</summary>
        public override bool ConsumesBack
        {
            get { return IsActive(); }
        }

        public override bool Back()
        {
            return Live != null && Live.Cancel();
        }

        public override void OnUnfocus()
        {
            base.OnUnfocus();
            Live?.HideNativeTooltip();
        }

        public override void OnPop()
        {
            base.OnPop();
            Live?.HideNativeTooltip();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            builder.BeginStop(MenuStop);
            ControlId start = null;

            if (!string.IsNullOrWhiteSpace(Live.Title))
            {
                builder.AddItem(new SyntheticNode(
                    ControlId.For(_headingKey, "platform-user:heading"),
                    GraphNodes.Text(() => Live.Title)));
            }

            IReadOnlyList<PlatformUserMenuAdapter.ActionItem> actions = Live.GetActions();
            for (int i = 0; i < actions.Count; i++)
            {
                PlatformUserMenuAdapter.ActionItem action = actions[i];
                if (action == null || !action.IsVisible)
                {
                    continue;
                }

                PlatformUserMenuAdapter.ActionItem it = action;
                Component entry = it.Entry;
                if (entry == null)
                {
                    continue;
                }

                ControlId id = ControlId.For(entry, it.Id);
                NodeVtable vtable = GraphNodes.Button(
                    () => it.Label,
                    () => it.Activate(),
                    () => it.IsEnabled,
                    it.Tooltip);
                vtable.OnFocusVisual = it.FocusNative;
                builder.AddItem(new DrawnNode(id, vtable, entry));
                if (start == null)
                {
                    start = id;
                }
            }

            // The mod's own way out: the popup draws none, and the game's Cancel is bound to the
            // gamepad only.
            NodeVtable cancel = GraphNodes.Button(() => Live.CancelLabel, () => Live.Cancel());
            cancel.OnFocusVisual = Live.HideNativeTooltip;
            ControlId cancelId = ControlId.For(_cancelKey, "platform-user:cancel");
            builder.AddItem(new SyntheticNode(cancelId, cancel));
            if (start == null)
            {
                start = cancelId;
            }

            // There is no body here, so focus starts on the first action, as the family's contract
            // says it does where a dialog draws none.
            builder.SetStart(start);
        }
    }
}
