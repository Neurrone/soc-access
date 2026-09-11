using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// THE CHAT BUTTON'S NAME. The game draws one icon whether or not anything is waiting to be
    /// read, so the button is named here, where accessibility wording belongs, and says which of
    /// the two it is. The adventure map and the battle HUD both draw the same button.
    /// </summary>
    public static class ChatButtonText
    {
        public static string Label(ChatAdapter chat)
        {
            return chat != null && chat.HasUnreadMessages()
                ? ModText.Get(ModStrings.Screens.ChatUnreadMessages)
                : ModText.Get(ModStrings.Screens.Chat);
        }
    }
}
