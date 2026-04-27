using System.Collections;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class DialogueMenuPatches
    {
        [HarmonyPatch(typeof(DialogueMenu), "HandleTypingTextEnter")]
        [HarmonyPostfix]
        private static void DialogueMenuTypingTextEnterPostfix(DialogueMenu __instance)
        {
            SoqAccessPlugin plugin = SoqAccessPlugin.Instance;
            if (plugin != null)
            {
                plugin.StartCoroutine(WaitForDialogueMenuReady(__instance));
            }
        }

        [HarmonyPatch(typeof(DialogueMenu), "HandleNoneEnter")]
        [HarmonyPostfix]
        private static void DialogueMenuNoneEnterPostfix(DialogueMenu __instance)
        {
            DialogueMenuAdvanceGuard.ClearPending(__instance);
            SoqAccessPlugin.Instance?.ScreenDetector?.OnDialogueMenuHidden(__instance);
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
                    DialogueMenuAdvanceGuard.ClearPending(dialogueMenu);
                    SoqAccessPlugin.Instance?.ScreenDetector?.OnDialogueMenuAvailable(dialogueMenu);
                    yield break;
                }

                frames++;
                yield return null;
            }
        }
    }
}
