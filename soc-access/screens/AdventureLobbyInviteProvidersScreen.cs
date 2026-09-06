using System.Collections.Generic;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The lobby's "invite a friend" provider list, made navigable as a graph in the shape of its
    /// family's representative (<see cref="PlatformUserMenuScreen"/>): one stop, the popup's own
    /// name as the screen name, one button per provider the game drew, then the mod's Cancel.
    ///
    /// PORTED WITHOUT A MEASUREMENT (owner's instruction, 2026-09-06): on the development machine
    /// Invite Friend opens Steam's overlay directly, because the game only shows this list when
    /// MORE THAN ONE social manager can invite (<c>LobbyMultiplayerPanel</c>, decompiled line 353:
    /// <c>if (num &gt; 1)</c>), so the popup never draws here. The widget screen read the
    /// provider buttons in sibling order and offered a Cancel of its own; this does the same. The
    /// owner verifies it on a machine with two providers.
    ///
    /// ESCAPE is claimed. The panel registers only <c>UI.Cancel</c> (line 356), the gamepad
    /// binding throughout this game, so the key would do nothing; the screen runs the panel's own
    /// <c>HandleCancelInvitePopup</c>, which is what its full-screen blocker's click runs too.
    /// </summary>
    public sealed class AdventureLobbyInviteProvidersScreen : GraphScreen
    {
        private const string MenuStop = "invite-providers";

        private readonly AdventureLobbyInviteProvidersAdapter _adapter;

        // A subject of its own for the one node the popup draws nothing for.
        private readonly object _cancelKey = new object();

        public AdventureLobbyInviteProvidersScreen(AdventureLobbyInviteProvidersAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            AdventureLobbyInviteProvidersAdapter adapter = FindActiveInviteProviders(null);
            return adapter != null ? new AdventureLobbyInviteProvidersScreen(adapter) : null;
        }

        public bool Matches(LobbyMultiplayerPanel panel)
        {
            return _adapter != null && ReferenceEquals(_adapter.SourceKey, panel);
        }

        public override string Key
        {
            get { return "invite-providers"; }
        }

        /// <summary>The Invite Friend button's own label, spoken once on arrival.</summary>
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
            return _adapter != null && _adapter.Cancel();
        }

        public override void OnUnfocus()
        {
            base.OnUnfocus();
            _adapter?.HideNativeTooltip();
        }

        public override void OnPop()
        {
            base.OnPop();
            _adapter?.HideNativeTooltip();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            builder.BeginStop(MenuStop);
            ControlId start = null;

            IReadOnlyList<AdventureLobbyInviteProvidersAdapter.ProviderButtonItem> items = _adapter.GetProviderButtons();
            for (int i = 0; i < items.Count; i++)
            {
                AdventureLobbyInviteProvidersAdapter.ProviderButtonItem item = items[i];
                if (item == null || !item.IsVisible || item.Button == null)
                {
                    continue;
                }

                AdventureLobbyInviteProvidersAdapter.ProviderButtonItem it = item;
                ControlId id = ControlId.For(it.Button, it.Id);
                NodeVtable vtable = GraphNodes.Button(
                    () => it.Label,
                    () => it.Activate(),
                    () => it.IsEnabled,
                    it.Tooltip);
                vtable.OnFocusVisual = it.FocusNative;
                builder.AddItem(new DrawnNode(id, vtable, it.Button));
                if (start == null)
                {
                    start = id;
                }
            }

            // The mod's own way out, as the widget screen offered: the popup draws no Cancel of its
            // own, and the game's Cancel is bound to the gamepad only.
            NodeVtable cancel = GraphNodes.Button(() => _adapter.CancelLabel, () => _adapter.Cancel());
            cancel.OnFocusVisual = _adapter.HideNativeTooltip;
            ControlId cancelId = ControlId.For(_cancelKey, "invite-providers:cancel");
            builder.AddItem(new SyntheticNode(cancelId, cancel));
            if (start == null)
            {
                start = cancelId;
            }

            builder.SetStart(start);
        }

        public static AdventureLobbyInviteProvidersAdapter FindActiveInviteProviders(LobbyMultiplayerPanel targetPanel)
        {
            LobbyMultiplayerPanel[] panels = Resources.FindObjectsOfTypeAll<LobbyMultiplayerPanel>();
            for (int i = 0; i < panels.Length; i++)
            {
                LobbyMultiplayerPanel panel = panels[i];
                if (panel == null)
                {
                    continue;
                }

                if (targetPanel != null && !ReferenceEquals(targetPanel, panel))
                {
                    continue;
                }

                AdventureLobbyInviteProvidersAdapter adapter = new AdventureLobbyInviteProvidersAdapter(panel);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }
    }
}
