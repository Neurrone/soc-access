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
    public static class ChatPatches
    {
        private static readonly System.Reflection.PropertyInfo ChatWindowContainerProperty =
            AccessTools.Property(typeof(ChatWindow), "Container");
        private static readonly System.Reflection.PropertyInfo ChatButtonContainerProperty =
            AccessTools.Property(typeof(ChatButton), "Container");
        private static ChatWindowBehavior _currentWindow;
        private static ChatButtonBehavior _currentButton;
        private static bool _windowProbed;
        private static bool _buttonProbed;

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
            _windowProbed = false;
            _buttonProbed = false;
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

            SocAccessMod.Instance?.ScreenManager?.Registered<ChatScreen>()?.Forget();
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

            ChatScreen screen = SocAccessMod.Instance?.ScreenManager?.Registered<ChatScreen>();
            if (screen == null || screen.Live != null)
            {
                // Native chat calls Show from HandleInputFieldChanged while the user is typing.
                // Rewriting the slot here would take the editor's field out from under it.
                return;
            }

            screen.Live = adapter;
        }

        [HarmonyPatch(typeof(ChatWindowBehavior), "Hide", new[] { typeof(bool) })]
        [HarmonyPostfix]
        private static void HidePostfix()
        {
            // Native Hide is also called during initialization and cleanup when the chat window may
            // not be open; an empty slot emptied again costs nothing.
            SocAccessMod.Instance?.ScreenManager?.Registered<ChatScreen>()?.Forget();
        }

        [HarmonyPatch(typeof(ChatWindowBehavior), "HandleNewMessage")]
        [HarmonyPostfix]
        private static void HandleNewMessagePostfix(ChatWindowBehavior __instance, int teamId, ChatMessage message)
        {
            _currentWindow = __instance;

            // The history has changed, whoever it was for: the open window re-reads it on its next
            // build rather than rendering every message again on every build.
            ChatAdapter.MessageGeneration++;
            ChatAdapter adapter = CurrentAdapter;
            if (adapter == null || !adapter.IsLocalTeamMessage(teamId))
            {
                return;
            }

            ChatScreen chatScreen = SocAccessMod.Instance?.ScreenManager?.Registered<ChatScreen>();
            if (adapter.IsOpen && chatScreen != null && chatScreen.Live != null)
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
            // The scans exist only to recover references a hot reload missed, so they run once
            // per load: the Initialize postfixes catch every window and button created later.
            if (_currentWindow == null && !_windowProbed)
            {
                _windowProbed = true;
                _currentWindow = FindRuntimeWindowBehavior();
            }

            if (_currentButton == null && !_buttonProbed)
            {
                _buttonProbed = true;
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
                SocAccessMod.Instance?.LogWarning("Failed to recover chat runtime service: " + exception.Message);
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
