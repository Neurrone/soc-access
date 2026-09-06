using System;
using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Menu.Popup;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The quit confirmation, made navigable as a graph in the three-part dialog contract its
    /// family's representative (<see cref="MessageDialogScreen"/>) established: one stop holding the
    /// heading as a line of its own and as the screen name, the body as the control focus starts on,
    /// then the buttons.
    ///
    /// This popup draws more above the question than a plain dialog does, and everything is read in
    /// the order it is drawn. Measured 2026-09-06 at 1280x800 inside `QuitToDesktopPopup(Clone)` &gt;
    /// `Container`: the follow-us block first (`FollowHeader` at y 286 and the `OpenSteamPageButton`
    /// "FOLLOW" at y 356), then the heading "Quit to Desktop" at y 418, then the body "Are you sure?"
    /// at y 438, then No at x 508 and Yes at x 647. So the follow text and its button read before the
    /// heading, and the buttons are read by their drawn left edges rather than positive-first.
    ///
    /// ESCAPE is the game's. `QuitToDesktopPopup.Show` registers <c>UI.ExitMenu</c> on
    /// <c>HandleCancelClicked</c> in its NON-gamepad branch (and <c>UI.Confirm</c> on
    /// <c>HandleConfirmClicked</c> beside it), so the key already presses No and the screen leaves it
    /// alone. Read 2026-09-06 from
    /// `decompiled/Lavapotion.SongsOfConquest.UILayer.Runtime/SongsOfConquest/Client/Menu/Popup/QuitToDesktopPopup.cs`
    /// lines 145 to 152.
    /// </summary>
    public sealed class QuitToDesktopPopupScreen : GraphScreen
    {
        private const string DialogStop = "quit-to-desktop";
        private const string ConfirmKey = "quit:confirm";
        private const string CancelKey = "quit:cancel";

        private static readonly System.Reflection.PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(QuitToDesktopPopupInstaller), "Container");

        private readonly QuitToDesktopPopupAdapter _adapter;

        // A subject of its own for each node the popup gives no component for: the reconciler seats
        // the cursor by SUBJECT before it looks at the structural key, so two nodes sharing one would
        // collapse onto whichever was declared first (the rule the message dialog's port established).
        private readonly object _followTitleKey = new object();
        private readonly object _headingKey = new object();
        private readonly object _bodyKey = new object();

        public QuitToDesktopPopupScreen(QuitToDesktopPopupAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            QuitToDesktopPopupInstaller[] installers = Resources.FindObjectsOfTypeAll<QuitToDesktopPopupInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                QuitToDesktopPopupInstaller installer = installers[i];
                if (!IsLiveSceneInstaller(installer))
                {
                    continue;
                }

                QuitToDesktopPopup popup = TryResolvePopup(installer);
                if (popup == null)
                {
                    continue;
                }

                QuitToDesktopPopupAdapter adapter = new QuitToDesktopPopupAdapter(popup);
                if (adapter.IsPresent())
                {
                    return new QuitToDesktopPopupScreen(adapter);
                }
            }

            return null;
        }

        public override string Key
        {
            get { return "quit-to-desktop"; }
        }

        /// <summary>The popup's own heading, spoken once on arrival.</summary>
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

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            builder.BeginStop(DialogStop);
            ControlId start = null;

            if (_adapter.HasSteamFollow)
            {
                if (!string.IsNullOrWhiteSpace(_adapter.FollowTitle))
                {
                    NodeVtable followTitle = GraphNodes.Text(() => _adapter.FollowTitle);
                    followTitle.OnFocusVisual = _adapter.SelectBody;
                    builder.AddItem(new SyntheticNode(
                        ControlId.For(_followTitleKey, "quit:follow-title"),
                        followTitle));
                }

                Component followButton = _adapter.SteamFollowButton;
                if (followButton != null)
                {
                    NodeVtable follow = GraphNodes.Button(
                        () => _adapter.SteamFollowLabel,
                        () => _adapter.ActivateSteamFollow(),
                        () => _adapter.HasSteamFollow);
                    follow.OnFocusVisual = _adapter.SelectSteamFollow;
                    builder.AddItem(new DrawnNode(
                        ControlId.For(followButton, "quit:follow"),
                        follow,
                        followButton));
                }
            }

            if (!string.IsNullOrWhiteSpace(_adapter.Title))
            {
                builder.AddItem(new SyntheticNode(
                    ControlId.For(_headingKey, "quit:heading"),
                    GraphNodes.Text(() => _adapter.Title)));
            }

            if (!string.IsNullOrWhiteSpace(_adapter.Description))
            {
                ControlId bodyId = ControlId.For(_bodyKey, "quit:body");
                NodeVtable body = GraphNodes.Text(() => _adapter.Description);
                body.OnFocusVisual = _adapter.SelectBody;
                builder.AddItem(new SyntheticNode(bodyId, body));
                start = bodyId;
            }

            foreach (KeyValuePair<string, Component> button in DrawnButtons())
            {
                ControlId buttonId = ControlId.For(button.Value, button.Key);
                builder.AddItem(new DrawnNode(buttonId, Button(button.Key), button.Value));
                if (start == null)
                {
                    start = buttonId;
                }
            }

            if (start != null)
            {
                // Focus starts on the body, so arrival reads the heading once as the screen name and
                // then what the popup actually asks.
                builder.SetStart(start);
            }
        }

        private NodeVtable Button(string key)
        {
            bool confirm = key == ConfirmKey;
            NodeVtable vtable = GraphNodes.Button(
                () => confirm ? _adapter.ConfirmLabel : _adapter.CancelLabel,
                () => { if (confirm) { _adapter.ActivateConfirm(); } else { _adapter.ActivateCancel(); } },
                () => confirm ? _adapter.HasConfirm : _adapter.HasCancel);

            // The button the cursor is on is the button the game shows as selected, which is also
            // what its own Confirm key would press.
            vtable.OnFocusVisual = confirm ? (Action)_adapter.SelectConfirm : _adapter.SelectCancel;
            return vtable;
        }

        /// <summary>The buttons the popup is drawing, leftmost first: it draws No at x 508 and Yes at
        /// x 647, and the reading order is the drawn one rather than positive-then-negative.</summary>
        private List<KeyValuePair<string, Component>> DrawnButtons()
        {
            List<KeyValuePair<string, Component>> buttons = new List<KeyValuePair<string, Component>>(2);
            if (_adapter.HasConfirm && _adapter.ConfirmButton != null)
            {
                buttons.Add(new KeyValuePair<string, Component>(ConfirmKey, _adapter.ConfirmButton));
            }

            if (_adapter.HasCancel && _adapter.CancelButton != null)
            {
                buttons.Add(new KeyValuePair<string, Component>(CancelKey, _adapter.CancelButton));
            }

            if (buttons.Count == 2 && Left(buttons[1].Value) < Left(buttons[0].Value))
            {
                KeyValuePair<string, Component> first = buttons[0];
                buttons[0] = buttons[1];
                buttons[1] = first;
            }

            return buttons;
        }

        private static float Left(Component button)
        {
            return button != null ? button.transform.position.x : 0f;
        }

        private static bool IsLiveSceneInstaller(QuitToDesktopPopupInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static QuitToDesktopPopup TryResolvePopup(QuitToDesktopPopupInstaller installer)
        {
            if (installer == null || InstallerContainerProperty == null)
            {
                return null;
            }

            DiContainer container = InstallerContainerProperty.GetValue(installer, null) as DiContainer;
            if (container == null)
            {
                return null;
            }

            try
            {
                return container.Resolve<QuitToDesktopPopup>();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
