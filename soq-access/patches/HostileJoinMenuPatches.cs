using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Common.Adventure.FightOrFlight;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Gamestate.Facade;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class HostileJoinMenuPatches
    {
        [HarmonyPatch(typeof(HostileJoinMenu), "ShowMenu", new[]
        {
            typeof(ICommanderState),
            typeof(ICommanderState),
            typeof(OnHostileOffersToJoinPayload)
        })]
        [HarmonyPostfix]
        private static void HostileJoinMenuShowMenuPostfix(HostileJoinMenu __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnHostileJoinMenuChanged(__instance);
        }

        [HarmonyPatch(typeof(HostileJoinMenu), "HandleYesButtonClicked")]
        [HarmonyPostfix]
        private static void HostileJoinMenuHandleYesButtonClickedPostfix(HostileJoinMenu __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnHostileJoinMenuChanged(__instance);
        }

        [HarmonyPatch(typeof(HostileJoinMenu), "HandleDoneButtonClicked")]
        [HarmonyPostfix]
        private static void HostileJoinMenuHandleDoneButtonClickedPostfix(HostileJoinMenu __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnHostileJoinMenuClosed(__instance);
        }

        [HarmonyPatch(typeof(HostileJoinMenu), "HandleNoButtonClicked")]
        [HarmonyPostfix]
        private static void HostileJoinMenuHandleNoButtonClickedPostfix(HostileJoinMenu __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnHostileJoinMenuClosed(__instance);
        }
    }
}
