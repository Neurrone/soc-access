using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Lavapotion.Networking;
using SongsOfConquest.Client.Chat;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Chat;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class ChatAdapter : IPresent
    {
        private static readonly FieldInfo WindowSettingsField =
            AccessTools.Field(typeof(ChatWindowBehavior), "_settings");
        private static readonly FieldInfo ClientChatSystemField =
            AccessTools.Field(typeof(ChatWindowBehavior), "_clientChatSystem");
        private static readonly FieldInfo LocalizationHandlerField =
            AccessTools.Field(typeof(ChatWindowBehavior), "_localizationHandler");
        private static readonly FieldInfo IsVisibleField =
            AccessTools.Field(typeof(ChatWindowBehavior), "_isVisible");
        private static readonly MethodInfo TryGetLocalTeamInControlMethod =
            AccessTools.Method(typeof(ChatWindowBehavior), "TryGetLocalTeamInControl");
        private static readonly MethodInfo GetSenderNameMethod =
            AccessTools.Method(typeof(ChatWindowBehavior), "GetSenderName");
        private static readonly MethodInfo RenderMessageMethod =
            AccessTools.Method(typeof(ChatWindowBehavior), "RenderMessage", new[] { typeof(StringBuilder), typeof(ChatMessage) });
        private static readonly MethodInfo HideMethod =
            AccessTools.Method(typeof(ChatWindowBehavior), "Hide", new[] { typeof(bool) });
        private static readonly FieldInfo AbstractChatSystemGameField =
            AccessTools.Field(typeof(AbstractChatSystem), "_game");
        private static readonly FieldInfo ButtonSettingsField =
            AccessTools.Field(typeof(ChatButtonBehavior), "_settings");
        private static readonly FieldInfo ChatEnabledField =
            AccessTools.Field(typeof(ChatButtonBehavior), "_chatEnabled");

        private readonly ChatWindowBehavior _window;
        private readonly ChatButtonBehavior _buttonBehavior;

        public ChatAdapter(ChatWindowBehavior window, ChatButtonBehavior buttonBehavior)
        {
            _window = window;
            _buttonBehavior = buttonBehavior;
        }

        public bool IsOpen
        {
            get
            {
                return _window != null
                    && IsVisibleField != null
                    && IsVisibleField.GetValue(_window) is bool
                    && (bool)IsVisibleField.GetValue(_window);
            }
        }

        /// <summary>The button the game draws to open the chat, so a page that draws it can key a
        /// control on it.</summary>
        public UIButton Button
        {
            get { return GetChatButton(); }
        }

        public bool IsButtonVisible()
        {
            UIButton button = GetChatButton();
            return button != null && button.Active && IsGameObjectVisible(button as Component);
        }

        public bool IsButtonEnabled()
        {
            UIButton button = GetChatButton();
            return IsButtonVisible()
                && IsChatEnabled()
                && button.Interactable;
        }

        public void FocusButton()
        {
            NativeSelectionUtility.Select(GetChatButton());
        }

        public bool Open()
        {
            UIButton button = GetChatButton();
            if (button != null)
            {
                return NativeSelectionUtility.Click(button);
            }

            IClientChatSystem chatSystem = GetChatSystem();
            if (chatSystem == null || chatSystem.ToggleChat == null)
            {
                return false;
            }

            chatSystem.ToggleChat();
            return true;
        }

        public Tooltip ButtonTooltip
        {
            get { return Tooltip.ForComponent(GetChatButton(), LocalizationHandler); }
        }

        public IUITextMeshInputField InputField
        {
            get
            {
                ChatWindowBehavior.Settings settings = WindowSettings;
                return settings != null ? settings.inputField : null;
            }
        }

        /// <summary>The chat window being OPEN is the page being drawn; there is no later step
        /// the mod has to wait for.</summary>
        public bool IsPresent()
        {
            return IsOpen;
        }

        public bool IsInputVisible()
        {
            return IsOpen && InputField != null && InputField.Active;
        }

        public bool IsInputEnabled()
        {
            IUITextMeshInputField input = InputField;
            return input != null && input.Active && input.Interactable;
        }

        public void FocusInput()
        {
            IUITextMeshInputField input = InputField;
            if (input != null)
            {
                input.Select();
                input.ActivateInputField();
            }
        }

        /// <summary>The drawn Send button, so a caller can key a control on it.</summary>
        public UIButton SendButton
        {
            get
            {
                ChatWindowBehavior.Settings settings = WindowSettings;
                return settings != null ? settings.sendButton : null;
            }
        }

        /// <summary>The drawn close cross. The lobby's chat window switches it off (measured
        /// 2026-09-06: <c>CloseButton</c> reads <c>visible=false</c> there), so a caller must ask
        /// whether it is drawn before declaring it.</summary>
        public UIButton CloseButton
        {
            get
            {
                ChatWindowBehavior.Settings settings = WindowSettings;
                return settings != null ? settings.closeButton : null;
            }
        }

        /// <summary>Who the message goes to, as the drop list every combo box in the mod opens.
        /// Null while the window draws no selector, which is the lobby's case
        /// (<c>Settings.hideDropdown</c>).</summary>
        public TargetDropList TargetSelector
        {
            get
            {
                UITextMeshDropdown dropdown = Dropdown;
                return dropdown != null && IsTargetSelectorVisible()
                    ? new TargetDropList(this, dropdown)
                    : null;
            }
        }

        public bool IsTargetSelectorVisible()
        {
            ChatWindowBehavior.Settings settings = WindowSettings;
            if (settings == null || settings.hideDropdown || settings.dropdown == null)
            {
                return false;
            }

            return settings.dropdown.Active
                && IsGameObjectVisible(settings.dropdown as Component)
                && IsGameObjectVisible(settings.dropdownContainer);
        }

        public int TargetOptionCount
        {
            get
            {
                UITextMeshDropdown dropdown = Dropdown;
                if (dropdown == null || dropdown.DropdownValueCount <= 0)
                {
                    return 0;
                }

                return Math.Min(dropdown.DropdownValueCount, 2);
            }
        }

        public string GetTargetOptionLabel(int index)
        {
            switch (index)
            {
                case 0:
                    return GameText.Get(LocalizationHandler, "Chat/All", "All");
                case 1:
                    return GameText.Get(LocalizationHandler, "Chat/Allies", "Allies");
                default:
                    return string.Empty;
            }
        }

        public int TargetValue
        {
            get { return MenuRows.DropdownValue(Dropdown); }
        }

        public bool SetTargetValue(int value)
        {
            return MenuRows.SetDropdownValue(Dropdown, value);
        }

        public void FocusTargetSelector()
        {
            NativeSelectionUtility.Select(Dropdown);
        }

        public Tooltip TargetSelectorTooltip
        {
            get { return Tooltip.ForComponent(Dropdown, LocalizationHandler); }
        }

        public string SendLabel
        {
            get { return GameText.Get(LocalizationHandler, "Common/Chat/Send", "Send"); }
        }

        public bool Send()
        {
            ChatWindowBehavior.Settings settings = WindowSettings;
            return settings != null && NativeSelectionUtility.Click(settings.sendButton);
        }

        public void FocusSend()
        {
            ChatWindowBehavior.Settings settings = WindowSettings;
            NativeSelectionUtility.Select(settings != null ? settings.sendButton : null);
        }

        public bool IsSendVisible()
        {
            ChatWindowBehavior.Settings settings = WindowSettings;
            return IsOpen && settings != null && IsGameObjectVisible(settings.sendButton as Component);
        }

        public bool IsSendEnabled()
        {
            ChatWindowBehavior.Settings settings = WindowSettings;
            return settings != null && settings.sendButton != null && settings.sendButton.Active && settings.sendButton.Interactable;
        }

        public Tooltip SendTooltip
        {
            get
            {
                ChatWindowBehavior.Settings settings = WindowSettings;
                return Tooltip.ForComponent(settings != null ? settings.sendButton : null, LocalizationHandler);
            }
        }

        public bool Close()
        {
            ChatWindowBehavior.Settings settings = WindowSettings;
            if (settings != null && NativeSelectionUtility.Click(settings.closeButton))
            {
                return true;
            }

            if (_window == null || HideMethod == null)
            {
                return false;
            }

            HideMethod.Invoke(_window, new object[] { false });
            return true;
        }

        public void FocusClose()
        {
            ChatWindowBehavior.Settings settings = WindowSettings;
            NativeSelectionUtility.Select(settings != null ? settings.closeButton : null);
        }

        public bool IsCloseVisible()
        {
            return IsOpen;
        }

        public bool IsCloseEnabled()
        {
            return IsOpen;
        }

        public Tooltip CloseTooltip
        {
            get
            {
                ChatWindowBehavior.Settings settings = WindowSettings;
                return Tooltip.ForComponent(settings != null ? settings.closeButton : null, LocalizationHandler);
            }
        }

        // The history as it was last read, and the team and rendered text it was read for.
        // Rendering it is a pass over every message the game has shown this team, so it is redone
        // only when the game's own rendering of the same history has moved: the window writes every
        // message into one text mesh in Refresh(), which it runs for each message that lands and
        // when it opens. Keyed on what the game drew, not on a hook that told us it drew (AGENTS.md,
        // Screen Resolution).
        private List<ChatMessageInfo> _messages;
        private string _messagesRendered;
        private int _messagesTeam = -1;

        public IReadOnlyList<ChatMessageInfo> GetMessages()
        {
            IClientChatSystem chatSystem = GetChatSystem();
            int teamId;
            if (chatSystem == null || !TryGetLocalTeamInControl(out teamId))
            {
                return new List<ChatMessageInfo>();
            }

            string rendered = GetRenderedHistory();
            if (_messages != null && _messagesTeam == teamId && _messagesRendered == rendered)
            {
                return _messages;
            }

            List<ChatMessageInfo> messages = new List<ChatMessageInfo>();
            List<ChatMessage> nativeMessages = new List<ChatMessage>();
            chatSystem.GetMessagesForTeam(teamId, nativeMessages);
            for (int i = 0; i < nativeMessages.Count; i++)
            {
                messages.Add(BuildMessageInfo(nativeMessages[i]));
            }

            _messages = messages;
            _messagesRendered = rendered;
            _messagesTeam = teamId;
            return messages;
        }

        /// <summary>One message as it is spoken: the line the window rendered, without the markup it
        /// rendered it with (a platform icon is a <c>&lt;sprite&gt;</c> tag).
        ///
        /// A message's rendered line never changes once the game has written it, and the whole
        /// history is read on every build, so each distinct line is turned into speech once. The
        /// table lives on the adapter, which lives exactly as long as the chat window: a static one
        /// grew for the session and outlived the game it held the conversation of (AGENTS.md, Screen
        /// Resolution).</summary>
        public string Spoken(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            string spoken;
            if (!_spoken.TryGetValue(text, out spoken))
            {
                spoken = string.Join(" ", SpokenLines.Of(new[] { text }));
                _spoken[text] = spoken;
            }

            return spoken;
        }

        private readonly Dictionary<string, string> _spoken = new Dictionary<string, string>();

        public bool HasUnreadMessages()
        {
            IClientChatSystem chatSystem = GetChatSystem();
            int teamId;
            return chatSystem != null
                && TryGetLocalTeamInControl(out teamId)
                && chatSystem.HasTeamUnreadMessages(teamId);
        }

        [HookWritable]
        public bool IsLocalTeamMessage(int teamId)
        {
            int localTeamId;
            return TryGetLocalTeamInControl(out localTeamId) && localTeamId == teamId;
        }

        [HookWritable]
        public bool IsOwnMessage(ChatMessage message)
        {
            if (message.Type == ChatMessageType.LocalEasterEggResponse)
            {
                return true;
            }

            int localClientId;
            return TryGetLocalClientId(out localClientId) && message.SenderClientId == localClientId;
        }

        public ChatMessageInfo BuildMessageInfo(ChatMessage message)
        {
            string displayText = RenderNativeMessage(message);
            bool isOwn = IsOwnMessage(message);
            bool isLocalResponse = message.Type == ChatMessageType.LocalEasterEggResponse;
            bool isServer = message.Type == ChatMessageType.Server || message.IsFromServer();
            string senderName = string.Empty;
            if (!isLocalResponse)
            {
                senderName = isServer
                    ? GameText.Get(LocalizationHandler, "Chat/Server", "Server")
                    : GetNativeSenderName(message);
            }

            return new ChatMessageInfo(
                senderName,
                isOwn,
                isServer,
                isLocalResponse,
                message.Message,
                displayText);
        }

        /// <summary>What the window has drawn of the history, as the game wrote it: every message
        /// rendered into one text mesh by <c>ChatWindowBehavior.Refresh</c>. The mod's own reading
        /// of the history is redone exactly when this moves.</summary>
        private string GetRenderedHistory()
        {
            ChatWindowBehavior.Settings settings = WindowSettings;
            return settings != null
                ? UITextMeshTextUtility.GetEffectiveText(settings.text)
                : string.Empty;
        }

        private ChatWindowBehavior.Settings WindowSettings
        {
            get
            {
                return _window != null && WindowSettingsField != null
                    ? WindowSettingsField.GetValue(_window) as ChatWindowBehavior.Settings
                    : null;
            }
        }

        private ChatButtonBehavior.Settings? ButtonSettings
        {
            get
            {
                if (_buttonBehavior == null || ButtonSettingsField == null)
                {
                    return null;
                }

                object value = ButtonSettingsField.GetValue(_buttonBehavior);
                return value is ChatButtonBehavior.Settings ? (ChatButtonBehavior.Settings?)value : null;
            }
        }

        private UITextMeshDropdown Dropdown
        {
            get
            {
                ChatWindowBehavior.Settings settings = WindowSettings;
                return settings != null ? settings.dropdown : null;
            }
        }

        private ILocalizationHandler LocalizationHandler
        {
            get
            {
                return _window != null && LocalizationHandlerField != null
                    ? LocalizationHandlerField.GetValue(_window) as ILocalizationHandler
                    : null;
            }
        }

        private IClientChatSystem GetChatSystem()
        {
            return _window != null && ClientChatSystemField != null
                ? ClientChatSystemField.GetValue(_window) as IClientChatSystem
                : null;
        }

        private bool TryGetLocalTeamInControl(out int teamId)
        {
            teamId = -1;
            if (_window == null || TryGetLocalTeamInControlMethod == null)
            {
                return false;
            }

            object[] args = { teamId };
            try
            {
                bool result = (bool)TryGetLocalTeamInControlMethod.Invoke(_window, args);
                teamId = args[0] is int ? (int)args[0] : -1;
                return result;
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("Failed to read local chat team: " + exception.Message);
                return false;
            }
        }

        private bool TryGetLocalClientId(out int clientId)
        {
            clientId = -1;
            IClientChatSystem chatSystem = GetChatSystem();
            if (chatSystem == null || AbstractChatSystemGameField == null)
            {
                return false;
            }

            object game = AbstractChatSystemGameField.GetValue(chatSystem);
            object client = GetMemberValue(game, "client");
            object value = GetMemberValue(client, "ClientID");
            if (value is int)
            {
                clientId = (int)value;
                return true;
            }

            return false;
        }

        private string GetNativeSenderName(ChatMessage message)
        {
            if (_window != null && GetSenderNameMethod != null)
            {
                try
                {
                    string name = GetSenderNameMethod.Invoke(_window, new object[] { message }) as string;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        return name;
                    }
                }
                catch (Exception exception)
                {
                    SocAccessMod.Instance?.LogWarning("Failed to read chat sender name: " + exception.Message);
                }
            }

            if (!string.IsNullOrWhiteSpace(message.SenderPlayerName))
            {
                return message.SenderPlayerName;
            }

            return message.SenderTeamName ?? string.Empty;
        }

        private string RenderNativeMessage(ChatMessage message)
        {
            if (_window != null && RenderMessageMethod != null)
            {
                try
                {
                    StringBuilder builder = new StringBuilder();
                    RenderMessageMethod.Invoke(_window, new object[] { builder, message });
                    return SpokenLines.Clean(builder.ToString());
                }
                catch (Exception exception)
                {
                    SocAccessMod.Instance?.LogWarning("Failed to render chat message: " + exception.Message);
                }
            }

            return message.Message ?? string.Empty;
        }

        private UIButton GetChatButton()
        {
            ChatButtonBehavior.Settings? settings = ButtonSettings;
            return settings.HasValue ? settings.Value.button : null;
        }

        private bool IsChatEnabled()
        {
            if (_buttonBehavior == null || ChatEnabledField == null)
            {
                return true;
            }

            object value = ChatEnabledField.GetValue(_buttonBehavior);
            return !(value is bool) || (bool)value;
        }

        private static bool IsGameObjectVisible(Component component)
        {
            return component != null && IsGameObjectVisible(component.gameObject);
        }

        private static bool IsGameObjectVisible(GameObject gameObject)
        {
            return GameObjects.IsLive(gameObject) && GameObjects.IsLiveSceneObject(gameObject);
        }

        /// <summary>The chat's "send to" dropdown as the mod's own list screen needs it. The game
        /// draws the same <c>UITextMeshDropdown</c> here as everywhere else, so the popup half is the
        /// shared <see cref="DropdownPopup"/>.</summary>
        public sealed class TargetDropList : IDropList
        {
            private readonly ChatAdapter _adapter;
            private readonly UITextMeshDropdown _dropdown;

            public TargetDropList(ChatAdapter adapter, UITextMeshDropdown dropdown)
            {
                _adapter = adapter;
                _dropdown = dropdown;
                GetOptions = ReadOptions;
                GetValue = () => _adapter != null ? _adapter.TargetValue : 0;
                IsEnabled = () => _dropdown != null && _dropdown.Active && _dropdown.Interactable;
                IsVisible = () => _adapter != null && _adapter.IsTargetSelectorVisible();
                OpenPopup = () => DropdownPopup.Show(_dropdown);
                ClosePopup = () => DropdownPopup.Hide(_dropdown);
                IsPopupOpen = () => DropdownPopup.IsOpen(_dropdown);
                FocusOption = index => DropdownPopup.FocusOption(_dropdown, index);
            }

            public string Key
            {
                get { return "chat-target"; }
            }

            /// <summary>The drawn dropdown itself, so a caller can key a control on it.</summary>
            public Component Subject
            {
                get { return _dropdown; }
            }

            public Func<IReadOnlyList<string>> GetOptions { get; private set; }
            public Func<int> GetValue { get; private set; }
            public Func<bool> IsEnabled { get; private set; }
            public Func<bool> IsVisible { get; private set; }
            public Func<bool> OpenPopup { get; private set; }
            public Func<bool> ClosePopup { get; private set; }
            public Func<bool> IsPopupOpen { get; private set; }
            public Func<int, bool> FocusOption { get; private set; }

            public bool SetValue(int value)
            {
                return _adapter != null && _adapter.SetTargetValue(value);
            }

            public void Focus()
            {
                if (_adapter != null)
                {
                    _adapter.FocusTargetSelector();
                }
            }

            private IReadOnlyList<string> ReadOptions()
            {
                List<string> labels = new List<string>();
                int count = _adapter != null ? _adapter.TargetOptionCount : 0;
                for (int i = 0; i < count; i++)
                {
                    labels.Add(_adapter.GetTargetOptionLabel(i));
                }

                return labels;
            }
        }

        private static object GetMemberValue(object instance, string name)
        {
            if (instance == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            Type type = instance.GetType();
            PropertyInfo property = AccessTools.Property(type, name);
            if (property != null)
            {
                return property.GetValue(instance, null);
            }

            FieldInfo field = AccessTools.Field(type, name);
            return field != null ? field.GetValue(instance) : null;
        }
    }

    public sealed class ChatMessageInfo
    {
        public ChatMessageInfo(
            string senderName,
            bool isOwn,
            bool isServer,
            bool isLocalResponse,
            string message,
            string displayText)
        {
            SenderName = senderName ?? string.Empty;
            IsOwn = isOwn;
            IsServer = isServer;
            IsLocalResponse = isLocalResponse;
            Message = message ?? string.Empty;
            DisplayText = displayText ?? string.Empty;
        }

        public string SenderName { get; private set; }
        public bool IsOwn { get; private set; }
        public bool IsServer { get; private set; }
        public bool IsLocalResponse { get; private set; }
        public string Message { get; private set; }
        public string DisplayText { get; private set; }
    }
}
