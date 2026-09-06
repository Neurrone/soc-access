using System.Collections.Generic;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Chat;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The chat window, made navigable as a graph. One stop, read in the order the window draws
    /// itself: the messages so far, who the message goes to where the window offers a choice, the box
    /// to type in, Send, and the way out.
    ///
    /// THE FIELD IS THE EXCEPTION TO THE EDIT CONTRACT (owner ruling, with a dialog's field): Enter
    /// inside the box is the GAME's own submit and sends the message
    /// (<c>ChatWindowBehavior.Initialize</c> puts <c>HandleInputFieldSubmit</c> on the field's
    /// <c>OnSubmit</c>, decompiled line 146), so the mod adds no commit of its own. Everything else is
    /// <see cref="GameTextEditor"/>'s usual contract - "editing" as the keyboard changes hands, the
    /// characters echoed as they are typed, and "edited" or "Cancelled" on the way out - and an
    /// arriving message is spoken by <c>ChatPatches</c> as it lands.
    ///
    /// Measured 2026-09-06 at 1280x800 in the online lobby through <c>/gui/unity</c>: the window at
    /// [83,468,463,222] with the message list in a scroll view at [86,471,457,182] and a bottom row
    /// at y 655 holding the box (x 86) and Send (x 461). The lobby's window draws NEITHER the "send
    /// to" dropdown (<c>Settings.hideDropdown</c>) NOR the close cross (<c>CloseButton</c> reads
    /// <c>visible=false</c>), and an empty history draws no line of its own - so the history declares
    /// nothing while it is empty, the selector is declared only where it is drawn, and the way out is
    /// the mod's own row, as the platform user menu's Cancel is.
    ///
    /// Escape is CLAIMED: <c>ChatWindowBehavior.Show</c> registers <c>InputActions.UI.Cancel</c> and
    /// <c>Common.ToggleChatType</c> and nothing else (decompiled, lines 399 to 402), and
    /// <c>UI.Cancel</c> is this game's GAMEPAD binding throughout - every keyboard branch registers
    /// <c>UI.ExitMenu</c> instead - so the key would otherwise do nothing here.
    /// </summary>
    public sealed class ChatScreen : GraphScreen
    {
        private const string ChatStop = "chat";

        private readonly ChatAdapter _adapter;
        private readonly GameTextEditor _editor = new GameTextEditor();

        // Subjects of their own for the lines the game gives no component for: a message is text the
        // window renders into one mesh, and the way out is the mod's own row.
        private readonly Dictionary<string, object> _markers = new Dictionary<string, object>();

        public ChatScreen(ChatAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            ChatAdapter adapter = ChatPatches.CurrentAdapter;
            return adapter != null && adapter.IsOpen ? new ChatScreen(adapter) : null;
        }

        public override string Key
        {
            get { return "chat"; }
        }

        public override string ScreenName
        {
            get { return ModText.Get(ModStrings.Screens.Chat); }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsOpen;
        }

        public override bool ConsumesBack
        {
            get { return true; }
        }

        public override bool Back()
        {
            return _adapter != null && _adapter.Close();
        }

        /// <summary>While the keyboard is on its way to the message box, what the player types next is
        /// meant for that box and must not start a search.</summary>
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
            // readout. IsPresent is what tells an edit the player ended from a window that went away
            // under it: Enter in the box sends and the game may close the window with it.
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
        }

        /// <summary>Kept for <c>ChatPatches</c>, which calls it whenever the window changes. The graph
        /// is declared afresh on every operation, so there is nothing to rebuild.</summary>
        public void Refresh()
        {
        }

        /// <summary>A message has arrived while the window is open: it is spoken as it lands, the
        /// graph having already grown a row for it.</summary>
        public void RefreshAndAnnounce(ChatMessage message)
        {
            if (!IsPresent() || _adapter == null)
            {
                return;
            }

            string text = Spoken(_adapter.BuildMessageInfo(message).DisplayText);
            if (!string.IsNullOrWhiteSpace(text))
            {
                SpeechPipeline.Output(new SpeechRequest(text, interrupt: false));
            }
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            builder.BeginStop(ChatStop);
            AddMessages(builder);
            AddTargetSelector(builder);
            ControlId field = AddMessageField(builder);
            AddSend(builder);
            AddClose(builder);
            if (field != null)
            {
                // The window is opened to type in, so that is where the cursor starts.
                builder.SetStart(field);
            }
        }

        /// <summary>The messages so far, oldest first, as the window renders them. An empty history
        /// declares nothing: the window draws no line saying so. The rendered line carries the game's
        /// own markup - a platform icon is a <c>&lt;sprite&gt;</c> tag - so it goes through
        /// <see cref="SpokenLines"/> like every other string the game wrote for its renderer.</summary>
        private void AddMessages(GraphBuilder builder)
        {
            IReadOnlyList<ChatMessageInfo> messages = _adapter.GetMessages();
            for (int i = 0; i < messages.Count; i++)
            {
                ChatMessageInfo message = messages[i];
                if (message == null || string.IsNullOrWhiteSpace(message.DisplayText))
                {
                    continue;
                }

                string text = Spoken(message.DisplayText);
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                string key = "chat:message/" + i;
                builder.AddItem(new SyntheticNode(
                    ControlId.For(Marker(key), key),
                    GraphNodes.Text(() => text)));
            }
        }

        /// <summary>Who the message goes to, where the window draws the choice: a combo box over the
        /// game's own dropdown, opening the mod's drop list. The lobby's window hides it.</summary>
        private void AddTargetSelector(GraphBuilder builder)
        {
            ChatAdapter.TargetDropList target = _adapter.TargetSelector;
            Component subject = target != null ? target.Subject : null;
            if (subject == null)
            {
                return;
            }

            ChatAdapter.TargetDropList it = target;
            string label = ModText.Get(ModStrings.Screens.ChatSendTo);
            NodeVtable vtable = GraphNodes.ComboBox(
                () => label,
                () => CurrentOption(it),
                () => DropListScreen.Open(it, label, index => it.SetValue(index)),
                it.IsEnabled,
                _adapter.TargetSelectorTooltip);
            vtable.OnFocusVisual = it.Focus;
            builder.AddItem(new DrawnNode(ControlId.For(subject, "chat:target"), vtable, subject));
        }

        private static string CurrentOption(ChatAdapter.TargetDropList target)
        {
            IReadOnlyList<string> options = target.GetOptions();
            int value = target.GetValue();
            return options != null && value >= 0 && value < options.Count ? options[value] : null;
        }

        /// <summary>The box the message is typed into. Its label is the mod's own word for it, the
        /// window drawing none; its Enter hands the keyboard to the game's field, and the player's
        /// Enter inside it is the game's own submit, which sends.</summary>
        private ControlId AddMessageField(GraphBuilder builder)
        {
            IUITextMeshInputField field = _adapter.InputField;
            Component subject = field != null ? field.MonoTransform : null;
            if (subject == null || !_adapter.IsInputVisible())
            {
                return null;
            }

            NodeVtable vtable = GraphNodes.EditField(
                () => ModText.Get(ModStrings.Screens.ChatInput),
                () =>
                {
                    IUITextMeshInputField live = _adapter.InputField;
                    // Nothing while the game holds the keyboard: the echo is already speaking the keys.
                    return live == null || _editor.Editing ? null : live.InputFieldValue;
                },
                () => _editor.RequestSilentEnd(_adapter.InputField),
                _adapter.IsInputEnabled);
            ControlId id = ControlId.For(subject, "chat:input");
            builder.AddItem(new DrawnNode(id, vtable, subject));
            return id;
        }

        private void AddSend(GraphBuilder builder)
        {
            UIButton send = _adapter.SendButton;
            if (send == null || !_adapter.IsSendVisible())
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(
                () => _adapter.SendLabel,
                () => _adapter.Send(),
                _adapter.IsSendEnabled,
                _adapter.SendTooltip);
            vtable.OnFocusVisual = _adapter.FocusSend;
            builder.AddItem(new DrawnNode(ControlId.For(send, "chat:send"), vtable, send));
        }

        /// <summary>The way out. The lobby's window draws no close cross, so this is the mod's own
        /// row - keyed on a subject of its own and labelled with the mod's word, as the platform user
        /// menu's Cancel is - and it runs the same Hide the game's own close would.</summary>
        private void AddClose(GraphBuilder builder)
        {
            NodeVtable vtable = GraphNodes.Button(
                () => ModText.Get(ModStrings.Screens.Close),
                () => _adapter.Close(),
                _adapter.IsCloseEnabled,
                _adapter.CloseTooltip);
            vtable.OnFocusVisual = _adapter.FocusClose;
            builder.AddItem(new SyntheticNode(
                ControlId.For(Marker("chat:close"), "chat:close"),
                vtable));
        }

        /// <summary>One message as it is spoken: the line the window rendered, without the markup it
        /// rendered it with.</summary>
        private static string Spoken(string text)
        {
            return string.Join(" ", SpokenLines.Of(new[] { text }));
        }

        private object Marker(string key)
        {
            object marker;
            if (!_markers.TryGetValue(key, out marker))
            {
                marker = new object();
                _markers.Add(key, marker);
            }

            return marker;
        }
    }
}
