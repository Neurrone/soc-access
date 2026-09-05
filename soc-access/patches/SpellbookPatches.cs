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
            SocAccessMod.Instance?.ScreenDetector?.OnSpellbookReady(__instance);
        }
    }

    [HarmonyPatch(typeof(SpellBook), "Close")]
    internal static class SpellbookClosePatch
    {
        private static void Prefix(SpellBook __instance, ref bool __state)
        {
            __state = __instance != null && __instance.IsOpen;
        }

        private static void Postfix(SpellBook __instance, bool __state)
        {
            if (!__state)
            {
                return;
            }

            SocAccessMod.Instance?.ScreenDetector?.OnSpellbookClosed(__instance);
        }
    }
}
