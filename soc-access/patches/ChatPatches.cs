using HarmonyLib;
using System.Collections.Generic;
using SongsOfConquest.Client.Chat;
using SongsOfConquest.Common.Chat;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Screens;
using SongsOfConquestAccess.Speech;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class ChatPatches
    {
        private static readonly System.Reflection.PropertyInfo ChatWindowContainerProperty =
            AccessTools.Property(typeof(ChatWindow), "Container");
        private static readonly System.Reflection.PropertyInfo ChatButtonContainerProperty =
            AccessTools.Property(typeof(ChatButton), "Container");
        private static ChatWindowBehavior _currentWindow;
        private static ChatButtonBehavior _currentButton;

        public static ChatAdapter CurrentAdapter
        {
            get
            {
                RecoverRuntimeReferences();
                return _currentWindow != null
                    ? new ChatAdapter(_currentWindow, _currentButton)
                    : null;
            }
        }

        public static void Reset()
        {
            _currentWindow = null;
            _currentButton = null;
        }

        [HarmonyPatch(typeof(ChatWindowBehavior), "Initialize")]
        [HarmonyPostfix]
        private static void ChatWindowInitializePostfix(ChatWindowBehavior __instance)
        {
            _currentWindow = __instance;
        }

        [HarmonyPatch(typeof(ChatWindowBehavior), "Dispose")]
        [HarmonyPostfix]
        private static void ChatWindowDisposePostfix(ChatWindowBehavior __instance)
        {
            if (ReferenceEquals(_currentWindow, __instance))
            {
                _currentWindow = null;
            }

            ScreenManager screenManager = SocAccessPlugin.Instance?.ScreenManager;
            if (screenManager != null && screenManager.CurrentScreen is ChatScreen)
            {
                screenManager.Pop<ChatScreen>("chat window disposed");
            }
        }

        [HarmonyPatch(typeof(ChatWindowBehavior), "Show", new[] { typeof(bool), typeof(bool) })]
        [HarmonyPostfix]
        private static void ShowPostfix(ChatWindowBehavior __instance)
        {
            _currentWindow = __instance;
            ChatAdapter adapter = CurrentAdapter;
            if (adapter == null || !adapter.IsOpen)
            {
                return;
            }

            ScreenManager screenManager = SocAccessPlugin.Instance?.ScreenManager;
            if (screenManager == null)
            {
                return;
            }

            if (screenManager.CurrentScreen is ChatScreen)
            {
                // Native chat calls Show from HandleInputFieldChanged while the
                // user is typing. Rebuilding ChatScreen here replaces the
                // focused TextInputWidget, resetting edit echo and interrupting
                // native input focus.
                return;
            }

            screenManager.Push(new ChatScreen(adapter), "chat window shown");
        }

        [HarmonyPatch(typeof(ChatWindowBehavior), "Hide", new[] { typeof(bool) })]
        [HarmonyPostfix]
        private static void HidePostfix()
        {
            ScreenManager screenManager = SocAccessPlugin.Instance?.ScreenManager;
            // Native Hide is also called during initialization and cleanup when
            // the chat window may not be open, so only pop an active top screen.
            if (screenManager != null && screenManager.CurrentScreen is ChatScreen)
            {
                screenManager.Pop<ChatScreen>("chat window hidden");
            }
        }

        [HarmonyPatch(typeof(ChatWindowBehavior), "HandleNewMessage")]
        [HarmonyPostfix]
        private static void HandleNewMessagePostfix(ChatWindowBehavior __instance, int teamId, ChatMessage message)
        {
            _currentWindow = __instance;
            ChatAdapter adapter = CurrentAdapter;
            if (adapter == null || !adapter.IsLocalTeamMessage(teamId))
            {
                return;
            }

            ScreenManager screenManager = SocAccessPlugin.Instance?.ScreenManager;
            ChatScreen chatScreen = screenManager?.CurrentScreen as ChatScreen;
            if (adapter.IsOpen && chatScreen != null)
            {
                chatScreen.RefreshAndAnnounce(message);
                return;
            }

            if (!adapter.IsOpen && !adapter.IsOwnMessage(message))
            {
                SpeechPipeline.Output(new SpeechRequest(
                    ModText.Get(ModStrings.Screens.NewChatMessage),
                    interrupt: false));
            }
        }

        [HarmonyPatch(typeof(ChatButtonBehavior), "Initialize")]
        [HarmonyPostfix]
        private static void ChatButtonInitializePostfix(ChatButtonBehavior __instance)
        {
            _currentButton = __instance;
        }

        [HarmonyPatch(typeof(ChatButtonBehavior), "Dispose")]
        [HarmonyPostfix]
        private static void ChatButtonDisposePostfix(ChatButtonBehavior __instance)
        {
            if (ReferenceEquals(_currentButton, __instance))
            {
                _currentButton = null;
            }
        }

        private static void RecoverRuntimeReferences()
        {
            if (_currentWindow == null)
            {
                _currentWindow = FindRuntimeWindowBehavior();
            }

            if (_currentButton == null)
            {
                _currentButton = FindRuntimeButtonBehavior();
            }
        }

        private static ChatWindowBehavior FindRuntimeWindowBehavior()
        {
            ChatWindow[] windows = Resources.FindObjectsOfTypeAll<ChatWindow>();
            for (int i = 0; i < windows.Length; i++)
            {
                ChatWindow window = windows[i];
                if (!IsLiveSceneInstaller(window))
                {
                    continue;
                }

                ChatWindowBehavior behavior = TryResolveInitializable<ChatWindowBehavior>(
                    GetContainer(window, ChatWindowContainerProperty));
                if (behavior != null)
                {
                    return behavior;
                }
            }

            return null;
        }

        private static ChatButtonBehavior FindRuntimeButtonBehavior()
        {
            ChatButton[] buttons = Resources.FindObjectsOfTypeAll<ChatButton>();
            for (int i = 0; i < buttons.Length; i++)
            {
                ChatButton button = buttons[i];
                if (!IsLiveSceneInstaller(button))
                {
                    continue;
                }

                ChatButtonBehavior behavior = TryResolveInitializable<ChatButtonBehavior>(
                    GetContainer(button, ChatButtonContainerProperty));
                if (behavior != null)
                {
                    return behavior;
                }
            }

            return null;
        }

        private static T TryResolveInitializable<T>(DiContainer container) where T : class
        {
            if (container == null)
            {
                return null;
            }

            try
            {
                List<IInitializable> initializables = container.ResolveAll<IInitializable>();
                for (int i = 0; i < initializables.Count; i++)
                {
                    T match = initializables[i] as T;
                    if (match != null)
                    {
                        return match;
                    }
                }
            }
            catch (System.Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("Failed to recover chat runtime service: " + exception.Message);
            }

            return null;
        }

        private static DiContainer GetContainer(MonoInstallerBase installer, System.Reflection.PropertyInfo property)
        {
            if (installer == null || property == null)
            {
                return null;
            }

            return property.GetValue(installer, null) as DiContainer;
        }

        private static bool IsLiveSceneInstaller(MonoBehaviour installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }
    }
}
