using System;
using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.Menu.Popup;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// Every message dialog the game puts up, made navigable as a graph: one stop holding the
    /// heading, the body, the field where the source has one, and the buttons.
    ///
    /// Six native sources share this class, and the shape is read off each of them every build
    /// rather than assumed. Measured 2026-09-06 at 1280x800 on the quit popup: the heading ("Quit to
    /// Desktop") is drawn above the body ("Are you sure?"), and the buttons are drawn No at x 508 then
    /// Yes at x 647. The delete-save popup draws No then Yes as well, and the options confirm draws a
    /// tick (Confirm) then a cross (Cancel). So the buttons are read in the order of their drawn left
    /// edges, never in the order positive-then-negative, and which of them exist is asked of the
    /// source each build.
    ///
    /// The reading order is Endless Space 2 Access's three-part dialog contract: the heading first as
    /// a line of its own AND as the screen name, so arrival says it once; focus starting on the body,
    /// so what the dialog asks is the next thing heard; then the field and the buttons. A source with
    /// no heading has no heading node and no screen name - the body is all there is to say.
    ///
    /// THE BODY is a <c>GraphNodes.Paragraphs</c> node over the adapter's own lines: read aloud as
    /// the one message and held in the review buffer one line per paragraph.
    ///
    /// ESCAPE is the game's wherever the game acts on it, which differs per source and is a fact the
    /// adapter answers (<see cref="IMessageDialogAdapter.GameHandlesEscape"/>). Read out of the
    /// decompiled sources 2026-09-06: <c>ConfirmPopup.Show</c> registers <c>UI.ExitMenu</c> on
    /// <c>HandleNoClicked</c> in its KeyboardMouse branch; every <c>PopupMenu.Show</c> overload
    /// registers <c>UI.ExitMenu</c> on <c>HandleNegativeButtonClicked</c> in its non-gamepad branch;
    /// <c>MapMessagePopup.Show</c> and <c>RandomEventMenu.Show</c> register <c>UI.ExitMenu</c> on
    /// <c>Hide</c> unconditionally. Those four keep the key. <c>SystemPopup</c> touches the input
    /// manager nowhere at all and <c>CustomMessageMenu.Show</c> only registers a button poller, so on
    /// those two the screen claims Escape and presses the drawn negative button.
    ///
    /// THE FIELD is the game's own; activating it hands the keyboard over
    /// (<see cref="GameTextEditor"/>). Enter inside it stays the game's, as the owner ruled for
    /// dialogs: <c>UITextMeshInputField</c> raises <c>OnSubmit</c>, and this screen answers it by
    /// pressing the positive action, exactly as the widget screen did.
    /// </summary>
    public sealed class MessageDialogScreen : LiveScreen<IMessageDialogAdapter>
    {
        private const string DialogStop = "message-dialog";

        private static readonly AccessTools.FieldRef<PopupMenu, PopupMenu.Settings> PopupSettingsRef =
            AccessTools.FieldRefAccess<PopupMenu, PopupMenu.Settings>("_settings");
        private static readonly AccessTools.FieldRef<SystemPopupManager, ISystemPopup> SystemPopupRef =
            AccessTools.FieldRefAccess<SystemPopupManager, ISystemPopup>("_popup");

        private readonly GameTextEditor _editor = new GameTextEditor();

        // A subject of its own for each node the source gives no component for. The reconciler seats
        // the cursor by SUBJECT before it looks at the structural key, so two nodes sharing one
        // subject collapse onto whichever was declared first: with the popup itself as the subject of
        // both the heading and the body, focus arrived on the body and fell onto the heading a frame
        // later (measured on the options confirm, 2026-09-06).
        private readonly object _headingKey = new object();
        private readonly object _bodyKey = new object();
        private readonly object _positiveKey = new object();
        private readonly object _negativeKey = new object();

        // THE SIX SOURCES, each resolved from the game and adapted once per object it answers with.
        // The map message, the random event and the custom message are bound in the adventure
        // scene's container; the popup menu, the confirm popup and the system popup are bound in the
        // project's, the system popup only into its manager (WhenInjectedInto), so it comes off that
        // manager's own field. The page belongs to whichever of them is DRAWING, asked in the order
        // the detector's own handlers used to be tried.
        //
        // Two of these adapters ARE disposed by the slot, which AdaptedSource warns about: the game
        // reuses one popup object for every message it shows, so the pairing hands the same adapter
        // out again after its Dispose. That is safe here and only here, because the only thing
        // Dispose lets go of is the submit handler, and Adapt below puts it back every time the slot
        // takes the adapter.
        private readonly AdaptedSource<IMapMessagePopup, IMessageDialogAdapter> _mapMessage =
            new AdaptedSource<IMapMessagePopup, IMessageDialogAdapter>(
                ScreenSource<IMapMessagePopup>.FromScene(LoadedScenes.AdventureScene),
                popup => new MapMessagePopupAdapter((MapMessagePopup)popup));

        private readonly AdaptedSource<IRandomEventMenu, IMessageDialogAdapter> _randomEvent =
            new AdaptedSource<IRandomEventMenu, IMessageDialogAdapter>(
                ScreenSource<IRandomEventMenu>.FromScene(LoadedScenes.AdventureScene),
                menu => new RandomEventMenuAdapter((RandomEventMenu)menu));

        private readonly AdaptedSource<ICustomMessageMenu, IMessageDialogAdapter> _customMessage =
            new AdaptedSource<ICustomMessageMenu, IMessageDialogAdapter>(
                ScreenSource<ICustomMessageMenu>.FromScene(LoadedScenes.AdventureScene),
                menu => new CustomMessageMenuAdapter((CustomMessageMenu)menu));

        private readonly AdaptedSource<IPopupMenu, IMessageDialogAdapter> _popupMenu =
            new AdaptedSource<IPopupMenu, IMessageDialogAdapter>(
                ScreenSource<IPopupMenu>.FromProject(),
                menu => new PopupMenuAdapter(menu, PopupSettingsRef((PopupMenu)menu)));

        private readonly AdaptedSource<ConfirmPopup, IMessageDialogAdapter> _confirmPopup =
            new AdaptedSource<ConfirmPopup, IMessageDialogAdapter>(
                ScreenSource<ConfirmPopup>.FromProject(),
                popup => new ConfirmPopupAdapter(popup));

        private readonly AdaptedSource<ISystemPopup, IMessageDialogAdapter> _systemPopup =
            new AdaptedSource<ISystemPopup, IMessageDialogAdapter>(
                // BindInterfacesTo, so the manager answers only to ISystemPopups; the popup itself is
                // bound WhenInjectedInto<SystemPopupManager> and reachable only off its field.
                ScreenSource<ISystemPopup>.FromOwner(
                    ScreenSource<ISystemPopups>.FromProject(),
                    manager => SystemPopupRef((SystemPopupManager)manager)),
                popup => new SystemPopupAdapter((SystemPopup)popup));

        /// <summary>The adapter itself is what the slot holds here: six unrelated objects draw the
        /// one page, so the "menu" a source answers with IS the adapter over it, built once per
        /// object.</summary>
        protected override object ResolveMenu()
        {
            return Drawing(_mapMessage.Current)
                ?? Drawing(_randomEvent.Current)
                ?? Drawing(_customMessage.Current)
                ?? Drawing(_popupMenu.Current)
                ?? Drawing(_confirmPopup.Current)
                ?? Drawing(_systemPopup.Current);
        }

        /// <summary>The source that has just been written into the slot may have a text field of its
        /// own (the rename box), and the game's field must not be left holding a handler of ours: the
        /// adapter releases it in its own <c>Dispose</c> when the slot lets it go.</summary>
        protected override IMessageDialogAdapter Adapt(object menu)
        {
            IMessageDialogAdapter adapter = (IMessageDialogAdapter)menu;
            IInputDialogAdapter input = adapter as IInputDialogAdapter;
            if (input != null)
            {
                input.AttachInputSubmit(HandleInputSubmit);
            }

            return adapter;
        }

        private static IMessageDialogAdapter Drawing(IMessageDialogAdapter adapter)
        {
            return adapter != null && adapter.IsPresent() ? adapter : null;
        }

        public override string Key
        {
            get { return "message-dialog"; }
        }

        /// <summary>Layer 100: a dialog: over every page, whichever raised it.</summary>
        public override int Layer
        {
            get { return 100; }
        }

        /// <summary>The dialog's own heading, spoken once on arrival. Null where the source draws no
        /// heading: there is nothing to call the dialog but what it says, and the body says that.</summary>
        public override string ScreenName
        {
            get
            {
                string title = Live != null ? Live.Title : null;
                return string.IsNullOrWhiteSpace(title) ? null : title;
            }
        }

        /// <summary>Escape is claimed only on the sources the game leaves it unanswered on, and only
        /// while there is a drawn negative button to press with it.</summary>
        public override bool ConsumesBack
        {
            get
            {
                return Live != null
                    && !Live.GameHandlesEscape
                    && Live.HasNegativeAction
                    && Live.IsNegativeActionEnabled;
            }
        }

        public override bool Back()
        {
            return Live != null
                && Live.IsNegativeActionEnabled
                && Live.ActivateAction(DialogAction.Negative);
        }

        /// <summary>The page's own editor, over the dialog's field. GraphScreen takes the rest of its
        /// lifecycle: the raw-input and field-ownership answers, the per-frame update, and the
        /// abandon on leaving and on popping.</summary>
        public override GameTextEditor Editor
        {
            get { return _editor; }
        }

        /// <summary>The page has gone. The slot goes with it, so the adapter releases the handler it
        /// put on the game's field; a dialog that is still up is adapted again on the next tick.
        /// </summary>
        public override void OnPop()
        {
            base.OnPop();
            Forget();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            builder.BeginStop(DialogStop);
            ControlId start = null;

            if (!string.IsNullOrWhiteSpace(Live.Title))
            {
                builder.AddItem(new SyntheticNode(
                    ControlId.For(_headingKey, "dialog:heading"),
                    GraphNodes.Text(() => Live.Title)));
            }

            // The body is broken into its paragraphs ONCE: the guard and the node ask the same
            // question, and the paragraph node counts them at build.
            IList<string> bodyLines = Live.BodyLines;
            if (bodyLines.Count > 0)
            {
                ControlId bodyId = ControlId.For(_bodyKey, "dialog:body");
                NodeVtable body = GraphNodes.Paragraphs(() => bodyLines);
                body.OnFocusVisual = () => Live.SyncNativeSelection(DialogAction.Body);
                builder.AddItem(new SyntheticNode(bodyId, body));
                start = bodyId;
            }

            IInputDialogAdapter inputAdapter = Live as IInputDialogAdapter;
            IUITextMeshInputField field = inputAdapter != null && inputAdapter.HasInputField
                ? inputAdapter.InputField
                : null;
            Component fieldComponent = field != null ? field.MonoTransform : null;
            if (fieldComponent != null)
            {
                ControlId fieldId = ControlId.For(fieldComponent, "dialog:input");
                builder.AddItem(new DrawnNode(fieldId, EditField(), fieldComponent));
                if (start == null)
                {
                    start = fieldId;
                }
            }

            foreach (DialogAction action in DrawnButtons())
            {
                Component button = Live.ButtonOf(action);
                ControlId buttonId = ControlId.For(
                    (object)button ?? (action == DialogAction.Positive ? _positiveKey : _negativeKey),
                    action == DialogAction.Positive ? "dialog:positive" : "dialog:negative");
                builder.AddItem(new DrawnNode(buttonId, Button(action), button));
                if (start == null)
                {
                    start = buttonId;
                }
            }

            if (start != null)
            {
                // Focus starts on the body, so arrival reads the heading once as the screen name and
                // then what the dialog actually asks. Where there is no body it starts on the field,
                // and failing that on the first button.
                builder.SetStart(start);
            }
        }

        /// <summary>The game's own text box, labelled with the dialog's heading (or with the body
        /// where there is no heading), as the widget it replaces was.</summary>
        private NodeVtable EditField()
        {
            return GraphNodes.EditField(
                () => FirstNonEmpty(Live.Title, Live.Body),
                () =>
                {
                    IUITextMeshInputField field = InputField;
                    // Nothing while the game holds the keyboard: the echo is already speaking the keys.
                    return field == null || _editor.Editing ? null : field.InputFieldValue;
                },
                () => _editor.Request(InputField),
                () => InputField != null);
        }

        private NodeVtable Button(DialogAction action)
        {
            NodeVtable vtable = GraphNodes.Button(
                () => action == DialogAction.Positive ? Live.PositiveLabel : Live.NegativeLabel,
                () => Live.ActivateAction(action),
                () => action == DialogAction.Positive
                    ? Live.IsPositiveActionEnabled
                    : Live.IsNegativeActionEnabled);

            // The button the cursor is on is the button the game shows as selected, which is also what
            // its own Confirm key would press.
            vtable.OnFocusVisual = () => Live.SyncNativeSelection(action);
            return vtable;
        }

        /// <summary>
        /// The buttons the source is drawing, leftmost first. Read off their rectangles every build
        /// rather than off which of them is the positive one: the quit and delete popups draw No then
        /// Yes, while the options confirm draws its tick before its cross, and the reading order is
        /// the drawn one in both cases.
        /// </summary>
        private List<DialogAction> DrawnButtons()
        {
            List<DialogAction> actions = new List<DialogAction>(2);
            if (Live.HasPositiveAction)
            {
                actions.Add(DialogAction.Positive);
            }

            if (Live.HasNegativeAction)
            {
                actions.Add(DialogAction.Negative);
            }

            DrawnOrder.SortByLeft(actions, action => Live.ButtonOf(action));

            return actions;
        }

        private IUITextMeshInputField InputField
        {
            get
            {
                IInputDialogAdapter input = Live as IInputDialogAdapter;
                return input == null ? null : input.InputField;
            }
        }

        private void HandleInputSubmit(IUITextMeshInputField inputField, string text)
        {
            if (Live != null && Live.IsPositiveActionEnabled)
            {
                Live.ActivateAction(DialogAction.Positive);
            }
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return string.IsNullOrWhiteSpace(first) ? second ?? string.Empty : first;
        }
    }
}
