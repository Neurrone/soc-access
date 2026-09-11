using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Menu.Popup;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

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
    public sealed class QuitToDesktopPopupScreen : LiveScreen<QuitToDesktopPopupAdapter>
    {
        private const string DialogStop = "quit-to-desktop";
        private const string ConfirmKey = "quit:confirm";
        private const string CancelKey = "quit:cancel";

        /// <summary>The one quit popup the project container holds for the whole game.</summary>
        private readonly ScreenSource<IQuitToDesktopPopup> _source = ScreenSource<IQuitToDesktopPopup>.FromProject();

        // A subject of its own for each node the popup gives no component for: the reconciler seats
        // the cursor by SUBJECT before it looks at the structural key, so two nodes sharing one would
        // collapse onto whichever was declared first (the rule the message dialog's port established).
        private readonly object _followTitleKey = new object();
        private readonly object _headingKey = new object();
        private readonly object _bodyKey = new object();

        protected override object ResolveMenu()
        {
            return _source.Current;
        }

        protected override QuitToDesktopPopupAdapter Adapt(object menu)
        {
            return new QuitToDesktopPopupAdapter((QuitToDesktopPopup)menu);
        }

        public override string Key
        {
            get { return "quit-to-desktop"; }
        }

        /// <summary>Layer 100: a dialog: over every page, whichever raised it.</summary>
        public override int Layer
        {
            get { return 100; }
        }

        /// <summary>The popup's own heading, spoken once on arrival.</summary>
        public override string ScreenName
        {
            get
            {
                string title = Live != null ? Live.Title : null;
                return string.IsNullOrWhiteSpace(title) ? null : title;
            }
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            builder.BeginStop(DialogStop);
            ControlId start = null;

            if (Live.HasSteamFollow)
            {
                if (!string.IsNullOrWhiteSpace(Live.FollowTitle))
                {
                    NodeVtable followTitle = GraphNodes.Text(() => Live.FollowTitle);
                    followTitle.OnFocusVisual = Live.SelectBody;
                    builder.AddItem(new SyntheticNode(
                        ControlId.For(_followTitleKey, "quit:follow-title"),
                        followTitle));
                }

                Component followButton = Live.SteamFollowButton;
                if (followButton != null)
                {
                    NodeVtable follow = GraphNodes.Button(
                        () => Live.SteamFollowLabel,
                        () => Live.ActivateSteamFollow(),
                        () => Live.HasSteamFollow);
                    follow.OnFocusVisual = Live.SelectSteamFollow;
                    builder.AddItem(new DrawnNode(
                        ControlId.For(followButton, "quit:follow"),
                        follow,
                        followButton));
                }
            }

            if (!string.IsNullOrWhiteSpace(Live.Title))
            {
                builder.AddItem(new SyntheticNode(
                    ControlId.For(_headingKey, "quit:heading"),
                    GraphNodes.Text(() => Live.Title)));
            }

            if (Live.HasDescription)
            {
                ControlId bodyId = ControlId.For(_bodyKey, "quit:body");
                NodeVtable body = GraphNodes.Paragraphs(() => Live.DescriptionLines);
                body.OnFocusVisual = Live.SelectBody;
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
                () => confirm ? Live.ConfirmLabel : Live.CancelLabel,
                () => { if (confirm) { Live.ActivateConfirm(); } else { Live.ActivateCancel(); } },
                () => confirm ? Live.HasConfirm : Live.HasCancel);

            // The button the cursor is on is the button the game shows as selected, which is also
            // what its own Confirm key would press.
            vtable.OnFocusVisual = confirm ? (Action)Live.SelectConfirm : Live.SelectCancel;
            return vtable;
        }

        /// <summary>The buttons the popup is drawing, leftmost first: it draws No at x 508 and Yes at
        /// x 647, and the reading order is the drawn one rather than positive-then-negative.</summary>
        private List<KeyValuePair<string, Component>> DrawnButtons()
        {
            List<KeyValuePair<string, Component>> buttons = new List<KeyValuePair<string, Component>>(2);
            if (Live.HasConfirm && Live.ConfirmButton != null)
            {
                buttons.Add(new KeyValuePair<string, Component>(ConfirmKey, Live.ConfirmButton));
            }

            if (Live.HasCancel && Live.CancelButton != null)
            {
                buttons.Add(new KeyValuePair<string, Component>(CancelKey, Live.CancelButton));
            }

            DrawnOrder.SortByLeft(buttons, button => button.Value);

            return buttons;
        }
    }
}
