using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class DialogueMenuPatches
    {
        private static readonly HashSet<DialogueMenu> ActiveMenus = new HashSet<DialogueMenu>();

        [HarmonyPatch(typeof(DialogueMenu), "HandleTypingTextEnter")]
        [HarmonyPostfix]
        private static void DialogueMenuTypingTextEnterPostfix(DialogueMenu __instance)
        {
            SocAccessMod plugin = SocAccessMod.Instance;
            if (plugin != null)
            {
                plugin.StartCoroutine(WaitForDialogueMenuReady(__instance));
            }
        }

        [HarmonyPatch(typeof(DialogueMenu), "HandleNoneEnter")]
        [HarmonyPostfix]
        private static void DialogueMenuNoneEnterPostfix(DialogueMenu __instance)
        {
            if (__instance == null || !ActiveMenus.Remove(__instance))
            {
                return;
            }

            SocAccessMod.Instance?.ScreenDetector?.OnDialogueMenuClosed(__instance);
        }

        private static IEnumerator WaitForDialogueMenuReady(DialogueMenu dialogueMenu)
        {
            // Let DialogueMenu.TypeText assign the new body text before the
            // accessibility screen reads it; otherwise the speaker can update
            // one frame before the previous line is replaced.
            yield return null;

            int frames = 0;
            while (dialogueMenu != null && frames < 600)
            {
                DialogueMenuAdapter adapter = new DialogueMenuAdapter(dialogueMenu);
                if (adapter.IsPresent())
                {
                    ActiveMenus.Add(dialogueMenu);
                    SocAccessMod.Instance?.ScreenDetector?.OnDialogueMenuChanged(dialogueMenu);
                    yield break;
                }

                frames++;
                yield return null;
            }
        }
    }
}
