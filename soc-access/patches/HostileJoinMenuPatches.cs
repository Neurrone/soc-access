using System.Collections;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Common.Adventure.FightOrFlight;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Gamestate.Facade;
using SongsOfConquestAccess.Adapters;

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
            StartWaitForReady(__instance);
        }

        [HarmonyPatch(typeof(HostileJoinMenu), "HandleYesButtonClicked")]
        [HarmonyPostfix]
        private static void HostileJoinMenuHandleYesButtonClickedPostfix(HostileJoinMenu __instance)
        {
            StartWaitForReady(__instance);
        }

        [HarmonyPatch(typeof(HostileJoinMenu), "HandleDoneButtonClicked")]
        [HarmonyPostfix]
        private static void HostileJoinMenuHandleDoneButtonClickedPostfix(HostileJoinMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnHostileJoinMenuClosed(__instance);
        }

        [HarmonyPatch(typeof(HostileJoinMenu), "HandleNoButtonClicked")]
        [HarmonyPostfix]
        private static void HostileJoinMenuHandleNoButtonClickedPostfix(HostileJoinMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnHostileJoinMenuClosed(__instance);
        }

        private static void StartWaitForReady(HostileJoinMenu menu)
        {
            SocAccessMod plugin = SocAccessMod.Instance;
            if (plugin != null && menu != null)
            {
                plugin.StartCoroutine(WaitForReady(menu));
            }
        }

        private static IEnumerator WaitForReady(HostileJoinMenu menu)
        {
            int frames = 0;
            while (menu != null && frames < 120)
            {
                HostileJoinMenuAdapter adapter = new HostileJoinMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    SocAccessMod.Instance?.ScreenDetector?.OnHostileJoinMenuChanged(menu);
                    yield break;
                }

                frames++;
                yield return null;
            }
        }
    }
}
