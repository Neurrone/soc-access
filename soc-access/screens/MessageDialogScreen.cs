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
    /// Seven native sources share this class, and the shape is read off each of them every build
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
        private static readonly System.Reflection.PropertyInfo PopupInstallerContainerProperty =
            AccessTools.Property(typeof(PopupMenuInstaller), "Container");
        private static readonly System.Reflection.PropertyInfo RandomEventInstallerContainerProperty =
            AccessTools.Property(typeof(RandomEventMenuInstaller), "Container");
        private static readonly System.Reflection.PropertyInfo CustomMessageInstallerContainerProperty =
            AccessTools.Property(typeof(CustomMessageMenuInstaller), "Container");

        private IInputDialogAdapter _inputAdapter;
        private Action<IUITextMeshInputField, string> _inputSubmitHandler;
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

        /// <summary>The source that has just been written into the slot may have a text field of its
        /// own (the rename box); the one that left must not be left holding a handler of ours.</summary>
        public override void OnLiveChanged(IMessageDialogAdapter previous)
        {
            if (_inputAdapter != null && _inputSubmitHandler != null)
            {
                _inputAdapter.DetachInputSubmit(_inputSubmitHandler);
            }

            _inputAdapter = Live as IInputDialogAdapter;
            _inputSubmitHandler = null;
            if (_inputAdapter != null)
            {
                _inputSubmitHandler = HandleInputSubmit;
                _inputAdapter.AttachInputSubmit(_inputSubmitHandler);
            }
        }

        /// <summary>After a hot reload: the six sources tried in the order the detector's own
        /// handlers would have written them, first one wins. Scanned once, from
        /// <c>ScreenDetector.RecoverRuntimeState</c>.</summary>
        public static void Recover()
        {
            Recovered<MessageDialogScreen>(
                FindActiveMapMessagePopup()
                ?? FindActiveRandomEventMenu()
                ?? FindActiveCustomMessageMenu()
                ?? FindActivePopupMenu()
                ?? FindActiveConfirmPopup()
                ?? FindActiveSystemPopup());
        }

        public static IMessageDialogAdapter FindActiveMapMessagePopup()
        {
            MapMessagePopup[] popups = Resources.FindObjectsOfTypeAll<MapMessagePopup>();
            for (int i = 0; i < popups.Length; i++)
            {
                MapMessagePopup popup = popups[i];
                if (!IsLiveScenePopup(popup))
                {
                    continue;
                }

                MapMessagePopupAdapter adapter = new MapMessagePopupAdapter(popup);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        public static IMessageDialogAdapter FindActiveRandomEventMenu()
        {
            RandomEventMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<RandomEventMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                RandomEventMenuInstaller installer = installers[i];
                if (!IsLiveSceneInstaller(installer))
                {
                    continue;
                }

                RandomEventMenu menu = TryResolveRandomEventMenu(installer);
                if (menu == null)
                {
                    continue;
                }

                RandomEventMenuAdapter adapter = new RandomEventMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        public static IMessageDialogAdapter FindActiveCustomMessageMenu()
        {
            CustomMessageMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<CustomMessageMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                CustomMessageMenuInstaller installer = installers[i];
                if (!IsLiveSceneInstaller(installer))
                {
                    continue;
                }

                CustomMessageMenu menu = TryResolveCustomMessageMenu(installer);
                if (menu == null)
                {
                    continue;
                }

                CustomMessageMenuAdapter adapter = new CustomMessageMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        public static IMessageDialogAdapter FindActivePopupMenu()
        {
            PopupMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<PopupMenuInstaller>();
            PopupMenuAdapter bestAdapter = null;
            int bestSiblingIndex = int.MinValue;

            for (int i = 0; i < installers.Length; i++)
            {
                PopupMenuInstaller installer = installers[i];
                if (!IsLiveSceneInstaller(installer))
                {
                    continue;
                }

                PopupMenu popupMenu = TryResolvePopupMenu(installer);
                if (popupMenu == null)
                {
                    continue;
                }

                PopupMenu.Settings settings = null;
                try
                {
                    settings = PopupSettingsRef(popupMenu);
                }
                catch (Exception)
                {
                    settings = null;
                }

                if (settings == null)
                {
                    continue;
                }

                PopupMenuAdapter adapter = new PopupMenuAdapter(popupMenu, settings);
                if (!adapter.IsPresent())
                {
                    continue;
                }

                int siblingIndex = GetPopupSiblingIndex(settings);
                if (bestAdapter == null || siblingIndex > bestSiblingIndex)
                {
                    bestAdapter = adapter;
                    bestSiblingIndex = siblingIndex;
                }
            }

            return bestAdapter != null ? bestAdapter : null;
        }

        public static IMessageDialogAdapter FindActiveConfirmPopup()
        {
            ConfirmPopup[] popups = Resources.FindObjectsOfTypeAll<ConfirmPopup>();
            ConfirmPopupAdapter bestAdapter = null;
            int bestSiblingIndex = int.MinValue;

            for (int i = 0; i < popups.Length; i++)
            {
                ConfirmPopup popup = popups[i];
                if (!IsLiveScenePopup(popup))
                {
                    continue;
                }

                ConfirmPopupAdapter adapter = new ConfirmPopupAdapter(popup);
                if (!adapter.IsPresent())
                {
                    continue;
                }

                int siblingIndex = popup.transform != null ? popup.transform.GetSiblingIndex() : 0;
                if (bestAdapter == null || siblingIndex > bestSiblingIndex)
                {
                    bestAdapter = adapter;
                    bestSiblingIndex = siblingIndex;
                }
            }

            return bestAdapter != null ? bestAdapter : null;
        }

        public static IMessageDialogAdapter FindActiveSystemPopup()
        {
            SystemPopup[] popups = Resources.FindObjectsOfTypeAll<SystemPopup>();
            SystemPopupAdapter bestAdapter = null;
            int bestSiblingIndex = int.MinValue;

            for (int i = 0; i < popups.Length; i++)
            {
                SystemPopup popup = popups[i];
                if (!IsLiveScenePopup(popup))
                {
                    continue;
                }

                SystemPopupAdapter adapter = new SystemPopupAdapter(popup);
                if (!adapter.IsPresent())
                {
                    continue;
                }

                int siblingIndex = popup.transform != null ? popup.transform.GetSiblingIndex() : 0;
                if (bestAdapter == null || siblingIndex > bestSiblingIndex)
                {
                    bestAdapter = adapter;
                    bestSiblingIndex = siblingIndex;
                }
            }

            return bestAdapter != null ? bestAdapter : null;
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

        public override bool IsActive()
        {
            return Live != null && Live.IsPresent();
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

        /// <summary>While the keyboard is on its way to the game's field, what the player types next
        /// is meant for that field and must not start a search.</summary>
        public override bool CapturesRawInput
        {
            get { return _editor.Pending; }
        }

        public override bool OwnsGameField
        {
            get { return _editor.Pending || _editor.Editing; }
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            // After the navigator, so the word the handover speaks follows the activation's own
            // readout. IsPresent is what tells an edit the player ended from a dialog that went away
            // under it: an Enter in the field submits the dialog, and an ending nobody is left to
            // hear is not announced.
            _editor.Update(IsActive());
        }

        public override void OnUnfocus()
        {
            base.OnUnfocus();
            _editor.Abandon();
        }

        public override void OnPop()
        {
            base.OnPop();
            _editor.Abandon();
            if (_inputAdapter != null && _inputSubmitHandler != null)
            {
                _inputAdapter.DetachInputSubmit(_inputSubmitHandler);
            }
        }

        public object SourceKey
        {
            get { return Live != null ? Live.SourceKey : null; }
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

            IUITextMeshInputField field = _inputAdapter != null && _inputAdapter.HasInputField
                ? _inputAdapter.InputField
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
                    IUITextMeshInputField field = _inputAdapter != null ? _inputAdapter.InputField : null;
                    // Nothing while the game holds the keyboard: the echo is already speaking the keys.
                    return field == null || _editor.Editing ? null : field.InputFieldValue;
                },
                () =>
                {
                    IUITextMeshInputField field = _inputAdapter != null ? _inputAdapter.InputField : null;
                    _editor.Request(field);
                },
                () => _inputAdapter != null && _inputAdapter.HasInputField);
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

            if (actions.Count == 2 && Left(actions[1]) < Left(actions[0]))
            {
                DialogAction first = actions[0];
                actions[0] = actions[1];
                actions[1] = first;
            }

            return actions;
        }

        private float Left(DialogAction action)
        {
            Component button = Live.ButtonOf(action);
            return button != null ? button.transform.position.x : 0f;
        }

        private void HandleInputSubmit(IUITextMeshInputField inputField, string text)
        {
            if (Live != null && Live.IsPositiveActionEnabled)
            {
                Live.ActivateAction(DialogAction.Positive);
            }
        }

        private static bool IsLiveScenePopup(MapMessagePopup popup)
        {
            if (popup == null)
            {
                return false;
            }

            GameObject gameObject = popup.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static bool IsLiveScenePopup(ConfirmPopup popup)
        {
            if (popup == null)
            {
                return false;
            }

            GameObject gameObject = popup.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static bool IsLiveScenePopup(SystemPopup popup)
        {
            if (popup == null)
            {
                return false;
            }

            GameObject gameObject = popup.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static bool IsLiveSceneInstaller(PopupMenuInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static bool IsLiveSceneInstaller(RandomEventMenuInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static bool IsLiveSceneInstaller(CustomMessageMenuInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static PopupMenu TryResolvePopupMenu(PopupMenuInstaller installer)
        {
            if (installer == null || PopupInstallerContainerProperty == null)
            {
                return null;
            }

            DiContainer container = PopupInstallerContainerProperty.GetValue(installer, null) as DiContainer;
            if (container == null)
            {
                return null;
            }

            try
            {
                return container.Resolve<PopupMenu>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static RandomEventMenu TryResolveRandomEventMenu(RandomEventMenuInstaller installer)
        {
            if (installer == null || RandomEventInstallerContainerProperty == null)
            {
                return null;
            }

            DiContainer container = RandomEventInstallerContainerProperty.GetValue(installer, null) as DiContainer;
            if (container == null)
            {
                return null;
            }

            try
            {
                return container.Resolve<RandomEventMenu>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static CustomMessageMenu TryResolveCustomMessageMenu(CustomMessageMenuInstaller installer)
        {
            if (installer == null || CustomMessageInstallerContainerProperty == null)
            {
                return null;
            }

            DiContainer container = CustomMessageInstallerContainerProperty.GetValue(installer, null) as DiContainer;
            if (container == null)
            {
                return null;
            }

            try
            {
                return container.Resolve<CustomMessageMenu>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static int GetPopupSiblingIndex(PopupMenu.Settings settings)
        {
            if (settings == null || settings.TopContainer == null)
            {
                return int.MinValue;
            }

            return settings.TopContainer.GetSiblingIndex();
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return string.IsNullOrWhiteSpace(first) ? second ?? string.Empty : first;
        }
    }
}
