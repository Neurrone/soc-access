using System.Collections;
using HarmonyLib;
using SongsOfConquest.Client.Adventure.Menu.Lobby;
using SongsOfConquestAccess.Screens;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class AdventureLobbyIconDropdownPatches
    {
        [HarmonyPatch(typeof(IconDropdown), "SetupAsColor")]
        [HarmonyPostfix]
        private static void IconDropdownSetupAsColorPostfix(IconDropdown __instance)
        {
            StartReadyProbe(__instance);
        }

        [HarmonyPatch(typeof(IconDropdown), "SetupAsFaction")]
        [HarmonyPostfix]
        private static void IconDropdownSetupAsFactionPostfix(IconDropdown __instance)
        {
            StartReadyProbe(__instance);
        }

        [HarmonyPatch(typeof(IconDropdown), "SetupAsWielder")]
        [HarmonyPostfix]
        private static void IconDropdownSetupAsWielderPostfix(IconDropdown __instance)
        {
            StartReadyProbe(__instance);
        }

        [HarmonyPatch(typeof(IconDropdown), "SetupAsAIDifficulty")]
        [HarmonyPostfix]
        private static void IconDropdownSetupAsAIDifficultyPostfix(IconDropdown __instance)
        {
            StartReadyProbe(__instance);
        }

        [HarmonyPatch(typeof(IconDropdown), "SetupAsPartnership")]
        [HarmonyPostfix]
        private static void IconDropdownSetupAsPartnershipPostfix(IconDropdown __instance)
        {
            StartReadyProbe(__instance);
        }

        [HarmonyPatch(typeof(IconDropdown), "Hide")]
        [HarmonyPostfix]
        private static void IconDropdownHidePostfix(IconDropdown __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyIconDropdownClosed(__instance);
        }

        [HarmonyPatch(typeof(IconDropdown), "OnDestroy")]
        [HarmonyPostfix]
        private static void IconDropdownOnDestroyPostfix(IconDropdown __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyIconDropdownClosed(__instance);
        }

        private static void StartReadyProbe(IconDropdown dropdown)
        {
            SocAccessMod.Instance?.StartCoroutine(WaitForIconDropdownReady(dropdown));
        }

        private static IEnumerator WaitForIconDropdownReady(IconDropdown dropdown)
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (dropdown != null && Time.realtimeSinceStartup < deadline)
            {
                if (AdventureLobbyIconDropdownScreen.FindActiveDropdown(dropdown) != null)
                {
                    SocAccessMod.Instance?.ScreenDetector?.OnAdventureLobbyIconDropdownReady(dropdown);
                    yield break;
                }

                yield return null;
            }
        }
    }
}
