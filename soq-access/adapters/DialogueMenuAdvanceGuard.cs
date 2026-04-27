using System.Collections.Generic;
using SongsOfConquest.Client.Menu;

namespace SongsOfConquestAccess.Adapters
{
    internal static class DialogueMenuAdvanceGuard
    {
        private static readonly HashSet<DialogueMenu> PendingMenus = new HashSet<DialogueMenu>();

        public static bool IsPending(DialogueMenu dialogueMenu)
        {
            return dialogueMenu != null && PendingMenus.Contains(dialogueMenu);
        }

        public static void MarkPending(DialogueMenu dialogueMenu)
        {
            if (dialogueMenu != null)
            {
                PendingMenus.Add(dialogueMenu);
            }
        }

        public static void ClearPending(DialogueMenu dialogueMenu)
        {
            if (dialogueMenu != null)
            {
                PendingMenus.Remove(dialogueMenu);
            }
        }
    }
}
