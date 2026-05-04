using HarmonyLib;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Gamestate;

namespace SongsOfConquestAccess
{
    [HarmonyPatch(typeof(SpellBook), "Show")]
    internal static class SpellbookShowPatch
    {
        private static void Postfix(SpellBook __instance, ICommanderState commanderState)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnSpellbookReady(__instance);
        }
    }

    [HarmonyPatch(typeof(SpellBook), "Close")]
    internal static class SpellbookClosePatch
    {
        private static void Postfix(SpellBook __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnSpellbookClosed(__instance);
        }
    }
}
