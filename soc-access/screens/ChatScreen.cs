using System.Collections.Generic;
using SongsOfConquest.Common.Chat;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    public sealed class ChatScreen : Screen
    {
        private readonly ChatAdapter _adapter;

        public ChatScreen(ChatAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            ChatAdapter adapter = ChatPatches.CurrentAdapter;
            return adapter != null && adapter.IsOpen ? new ChatScreen(adapter) : null;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsOpen;
        }

        public override bool HasClaimed(string actionKey)
        {
            return actionKey == AccessibilityActions.Cancel.Key || base.HasClaimed(actionKey);
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                if (RootWidget != null && RootWidget.HandleAction(action))
                {
                    return true;
                }

                return _adapter != null && _adapter.Close();
            }

            return base.OnActionJustPressed(action);
        }

        public void Refresh()
        {
            if (!IsPresent())
            {
                return;
            }

            int focusedIndex = RootWidget != null ? RootWidget.FocusedIndex : -1;
            int historyFocusedIndex = GetHistoryFocusedIndex();
            RootWidget = BuildRoot(_adapter);
            RootWidget?.SetFocusByIndexSilently(focusedIndex);
            RestoreHistoryFocus(historyFocusedIndex);
        }

        public void RefreshAndAnnounce(ChatMessage message)
        {
            if (!IsPresent() || _adapter == null)
            {
                return;
            }

            ChatMessageInfo info = _adapter.BuildMessageInfo(message);
            Refresh();
            string text = info.DisplayText;
            if (!string.IsNullOrWhiteSpace(text))
            {
                SpeechPipeline.Output(new SpeechRequest(text, interrupt: false));
            }
        }

        private static ContainerWidget BuildRoot(ChatAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("chat-screen", ModText.Get(ModStrings.Screens.Chat));
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new TextInputWidget(
                "chat-input",
                ModText.Get(ModStrings.Screens.ChatInput),
                () => adapter.InputField,
                () => true,
                adapter.FocusInput,
                adapter.IsInputEnabled,
                adapter.IsInputVisible,
                null));
            root.AddChild(BuildTargetSelector(adapter));
            root.AddChild(new ButtonWidget(
                "chat-send",
                () => adapter.SendLabel,
                adapter.Send,
                adapter.FocusSend,
                adapter.IsSendEnabled,
                adapter.IsSendVisible,
                () => adapter.SendTooltip));
            root.AddChild(BuildHistory(adapter));
            root.AddChild(new ButtonWidget(
                "chat-close",
                () => ModText.Get(ModStrings.Screens.Close),
                adapter.Close,
                adapter.FocusClose,
                adapter.IsCloseEnabled,
                adapter.IsCloseVisible,
                () => adapter.CloseTooltip));
            return root;
        }

        private static MenuWidget BuildTargetSelector(ChatAdapter adapter)
        {
            MenuWidget menu = new MenuWidget(
                "chat-target",
                ModText.Get(ModStrings.Screens.ChatSendTo),
                adapter.IsTargetSelectorVisible,
                adapter.FocusTargetSelector,
                null);
            int count = adapter.TargetOptionCount;
            for (int i = 0; i < count; i++)
            {
                int index = i;
                menu.AddItem(new MenuItemWidget(
                    "chat-target-" + index,
                    () => adapter.GetTargetOptionLabel(index),
                    () => adapter.TargetValue == index ? ModText.Get(ModStrings.UI.Selected) : string.Empty,
                    () => adapter.SetTargetValue(index),
                    () =>
                    {
                        adapter.FocusTargetSelector();
                        if (adapter.TargetValue != index)
                        {
                            adapter.SetTargetValue(index);
                        }
                    },
                    () => true,
                    () => adapter.TargetSelectorTooltip));
            }

            menu.SetFocusedItemById("chat-target-" + adapter.TargetValue);
            return menu;
        }

        private static MenuWidget BuildHistory(ChatAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("chat-history", ModText.Get(ModStrings.Screens.Chat), () => adapter.IsOpen);
            IReadOnlyList<ChatMessageInfo> messages = adapter.GetMessages();
            if (messages.Count == 0)
            {
                menu.AddItem(new MenuItemWidget(
                    "chat-history-empty",
                    () => ModText.Get(ModStrings.Screens.None),
                    null,
                    null,
                    null,
                    () => true));
                return menu;
            }

            for (int i = 0; i < messages.Count; i++)
            {
                int index = i;
                menu.AddItem(new MenuItemWidget(
                    "chat-history-" + index,
                    () => messages[index].DisplayText,
                    null,
                    null,
                    null,
                    () => true));
            }

            menu.SetFocusByIndexSilently(messages.Count - 1);
            return menu;
        }
        private int GetHistoryFocusedIndex()
        {
            MenuWidget history = RootWidget != null ? RootWidget.GetChildById("chat-history") as MenuWidget : null;
            return history != null ? history.FocusedIndex : -1;
        }

        private void RestoreHistoryFocus(int focusedIndex)
        {
            if (focusedIndex < 0 || RootWidget == null)
            {
                return;
            }

            MenuWidget history = RootWidget.GetChildById("chat-history") as MenuWidget;
            history?.SetFocusByIndexSilently(focusedIndex);
        }
    }
}
