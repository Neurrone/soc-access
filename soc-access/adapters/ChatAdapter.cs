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
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class ChatAdapter
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

        public string ButtonLabel
        {
            get
            {
                return HasUnreadMessages()
                    ? ModText.Get(ModStrings.Screens.ChatUnreadMessages)
                    : ModText.Get(ModStrings.Screens.Chat);
            }
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
            get
            {
                UITextMeshDropdown dropdown = Dropdown;
                if (dropdown == null || dropdown.DropdownValueCount <= 0)
                {
                    return 0;
                }

                int value = dropdown.DropdownValue;
                if (value < 0)
                {
                    return 0;
                }

                return value >= dropdown.DropdownValueCount ? dropdown.DropdownValueCount - 1 : value;
            }
        }

        public bool SetTargetValue(int value)
        {
            UITextMeshDropdown dropdown = Dropdown;
            if (dropdown == null || !dropdown.Active || !dropdown.Interactable || dropdown.DropdownValueCount <= 0)
            {
                return false;
            }

            if (value < 0)
            {
                value = 0;
            }
            else if (value >= dropdown.DropdownValueCount)
            {
                value = dropdown.DropdownValueCount - 1;
            }

            dropdown.DropdownValue = value;
            return true;
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

        public IReadOnlyList<ChatMessageInfo> GetMessages()
        {
            List<ChatMessageInfo> messages = new List<ChatMessageInfo>();
            IClientChatSystem chatSystem = GetChatSystem();
            int teamId;
            if (chatSystem == null || !TryGetLocalTeamInControl(out teamId))
            {
                return messages;
            }

            List<ChatMessage> nativeMessages = new List<ChatMessage>();
            chatSystem.GetMessagesForTeam(teamId, nativeMessages);
            for (int i = 0; i < nativeMessages.Count; i++)
            {
                messages.Add(BuildMessageInfo(nativeMessages[i]));
            }

            return messages;
        }

        public bool HasUnreadMessages()
        {
            IClientChatSystem chatSystem = GetChatSystem();
            int teamId;
            return chatSystem != null
                && TryGetLocalTeamInControl(out teamId)
                && chatSystem.HasTeamUnreadMessages(teamId);
        }

        public bool IsLocalTeamMessage(int teamId)
        {
            int localTeamId;
            return TryGetLocalTeamInControl(out localTeamId) && localTeamId == teamId;
        }

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
                    return StripColorTags(builder.ToString());
                }
                catch (Exception exception)
                {
                    SocAccessMod.Instance?.LogWarning("Failed to render chat message: " + exception.Message);
                }
            }

            return message.Message ?? string.Empty;
        }

        private static string StripColorTags(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            string result = text.Replace("</color>", string.Empty);
            int index = result.IndexOf("<color=", StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                int end = result.IndexOf('>', index);
                if (end < 0)
                {
                    break;
                }

                result = result.Remove(index, end - index + 1);
                index = result.IndexOf("<color=", StringComparison.OrdinalIgnoreCase);
            }

            return result;
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
            return gameObject != null
                && gameObject.activeInHierarchy
                && gameObject.scene.IsValid()
                && gameObject.scene.isLoaded;
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

    internal sealed class ChatMessageInfo
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
