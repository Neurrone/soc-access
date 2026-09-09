using HarmonyLib;
using SongsOfConquest.Client.Chat;
using SongsOfConquest.Common.Chat;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Screens;
using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess
{
    /// <summary>
    /// THE ONE CHAT EVENT: a message has landed. Nothing here decides whether the chat page is
    /// showing - the page finds its own window (<see cref="ChatSource"/>) - and losing this call
    /// costs one announcement and a stale history until the next message.
    /// </summary>
    [HarmonyPatch]
    public static class ChatPatches
    {
        [HarmonyPatch(typeof(ChatWindowBehavior), "HandleNewMessage")]
        [HarmonyPostfix]
        private static void HandleNewMessagePostfix(int teamId, ChatMessage message)
        {
            // The history has changed, whoever it was for: the open window re-reads it on its next
            // build rather than rendering every message again on every build.
            ChatAdapter.MessageGeneration++;
            ChatAdapter adapter = ChatSource.Current;
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
    }
}
