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
    public sealed class MessageDialogScreen : GraphScreen
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

        private readonly IMessageDialogAdapter _adapter;
        private readonly IInputDialogAdapter _inputAdapter;
        private readonly Action<IUITextMeshInputField, string> _inputSubmitHandler;
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

        public MessageDialogScreen(IMessageDialogAdapter adapter)
        {
            _adapter = adapter;
            _inputAdapter = adapter as IInputDialogAdapter;
            if (_inputAdapter != null)
            {
                _inputSubmitHandler = HandleInputSubmit;
                _inputAdapter.AttachInputSubmit(_inputSubmitHandler);
            }
        }

        public static Screen TryBuildActiveMapMessagePopupScreen()
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
                    return new MessageDialogScreen(adapter);
                }
            }

            return null;
        }

        public static Screen TryBuildActiveRandomEventMenuScreen()
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
                    return new MessageDialogScreen(adapter);
                }
            }

            return null;
        }

        public static Screen TryBuildActiveCustomMessageMenuScreen()
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
                    return new MessageDialogScreen(adapter);
                }
            }

            return null;
        }

        public static Screen TryBuildActivePopupMenuScreen()
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

            return bestAdapter != null ? new MessageDialogScreen(bestAdapter) : null;
        }

        public static Screen TryBuildActiveConfirmPopupScreen()
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

            return bestAdapter != null ? new MessageDialogScreen(bestAdapter) : null;
        }

        public static Screen TryBuildActiveSystemPopupScreen()
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

            return bestAdapter != null ? new MessageDialogScreen(bestAdapter) : null;
        }

        public override string Key
        {
            get { return "message-dialog"; }
        }

        /// <summary>The dialog's own heading, spoken once on arrival. Null where the source draws no
        /// heading: there is nothing to call the dialog but what it says, and the body says that.</summary>
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

        /// <summary>Escape is claimed only on the sources the game leaves it unanswered on, and only
        /// while there is a drawn negative button to press with it.</summary>
        public override bool ConsumesBack
        {
            get
            {
                return _adapter != null
                    && !_adapter.GameHandlesEscape
                    && _adapter.HasNegativeAction
                    && _adapter.IsNegativeActionEnabled;
            }
        }

        public override bool Back()
        {
            return _adapter != null
                && _adapter.IsNegativeActionEnabled
                && _adapter.ActivateAction(DialogAction.Negative);
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

        public override void Update()
        {
            base.Update();

            // After the navigator, so the word the handover speaks follows the activation's own
            // readout. IsPresent is what tells an edit the player ended from a dialog that went away
            // under it: an Enter in the field submits the dialog, and an ending nobody is left to
            // hear is not announced.
            _editor.Update(IsPresent());
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
            get { return _adapter != null ? _adapter.SourceKey : null; }
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            builder.BeginStop(DialogStop);
            ControlId start = null;

            if (!string.IsNullOrWhiteSpace(_adapter.Title))
            {
                builder.AddItem(new SyntheticNode(
                    ControlId.For(_headingKey, "dialog:heading"),
                    GraphNodes.Text(() => _adapter.Title)));
            }

            if (!string.IsNullOrWhiteSpace(_adapter.Body))
            {
                ControlId bodyId = ControlId.For(_bodyKey, "dialog:body");
                NodeVtable body = GraphNodes.Text(() => _adapter.Body, BodyLines);
                body.OnFocusVisual = () => _adapter.SyncNativeSelection(DialogAction.Body);
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
                Component button = _adapter.ButtonOf(action);
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

        /// <summary>
        /// The body's own lines, as a buffer section, and only where there is more than one of them.
        /// An announcement part is already a review-buffer line, so a section repeating a one-line
        /// body would put it in the buffer twice (the rule the campaign menu's port established).
        /// Today every source's adapter collapses whitespace, so this is never more than one line;
        /// it is written this way so that a source that stops collapsing reads as paragraphs rather
        /// than as one run-on line.
        /// </summary>
        private IList<string> BodyLines()
        {
            string body = _adapter != null ? _adapter.Body : null;
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            string[] split = body.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            List<string> lines = new List<string>(split.Length);
            for (int i = 0; i < split.Length; i++)
            {
                string line = split[i].Trim();
                if (line.Length > 0)
                {
                    lines.Add(line);
                }
            }

            return lines.Count > 1 ? lines : null;
        }

        /// <summary>The game's own text box, labelled with the dialog's heading (or with the body
        /// where there is no heading), as the widget it replaces was.</summary>
        private NodeVtable EditField()
        {
            return GraphNodes.EditField(
                () => FirstNonEmpty(_adapter.Title, _adapter.Body),
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
                () => action == DialogAction.Positive ? _adapter.PositiveLabel : _adapter.NegativeLabel,
                () => _adapter.ActivateAction(action),
                () => action == DialogAction.Positive
                    ? _adapter.IsPositiveActionEnabled
                    : _adapter.IsNegativeActionEnabled);

            // The button the cursor is on is the button the game shows as selected, which is also what
            // its own Confirm key would press.
            vtable.OnFocusVisual = () => _adapter.SyncNativeSelection(action);
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
            if (_adapter.HasPositiveAction)
            {
                actions.Add(DialogAction.Positive);
            }

            if (_adapter.HasNegativeAction)
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
            Component button = _adapter.ButtonOf(action);
            return button != null ? button.transform.position.x : 0f;
        }

        private void HandleInputSubmit(IUITextMeshInputField inputField, string text)
        {
            if (_adapter != null && _adapter.IsPositiveActionEnabled)
            {
                _adapter.ActivateAction(DialogAction.Positive);
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
