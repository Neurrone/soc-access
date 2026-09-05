using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    public static class AdventurePlayerMenuPatches
    {
        [HarmonyPatch(typeof(AdventurePlayerMenu), "Show")]
        [HarmonyPostfix]
        private static void AdventurePlayerMenuShowPostfix(AdventurePlayerMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnAdventurePlayerMenuReady(__instance);
        }

        [HarmonyPatch(typeof(AdventurePlayerMenu), "Hide")]
        [HarmonyPrefix]
        private static void AdventurePlayerMenuHidePrefix(AdventurePlayerMenu __instance, ref bool __state)
        {
            __state = __instance != null && ((Component)__instance).gameObject.activeSelf;
        }

        [HarmonyPatch(typeof(AdventurePlayerMenu), "Hide")]
        [HarmonyPostfix]
        private static void AdventurePlayerMenuHidePostfix(AdventurePlayerMenu __instance, bool __state)
        {
            if (__state)
            {
                SocAccessMod.Instance?.ScreenDetector?.OnAdventurePlayerMenuClosed(__instance);
            }
        }

        [HarmonyPatch(typeof(AdventurePlayerMenuEntry), "RefreshInteractable")]
        [HarmonyPostfix]
        private static void AdventurePlayerMenuEntryRefreshInteractablePostfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnAdventurePlayerMenuChanged();
        }

        [HarmonyPatch(typeof(AdventurePlayerMenuEntry), "RefreshResources")]
        [HarmonyPostfix]
        private static void AdventurePlayerMenuEntryRefreshResourcesPostfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnAdventurePlayerMenuChanged();
        }

        [HarmonyPatch(typeof(SendResourcePopup), "Show")]
        [HarmonyPostfix]
        private static void SendResourcePopupShowPostfix(SendResourcePopup __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnSendResourcePopupReady(__instance);
        }

        [HarmonyPatch(typeof(SendResourcePopup), "Hide")]
        [HarmonyPostfix]
        private static void SendResourcePopupHidePostfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnSendResourcePopupHidden();
        }

        [HarmonyPatch(typeof(GiftTownPopup), "Show")]
        [HarmonyPostfix]
        private static void GiftTownPopupShowPostfix(GiftTownPopup __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnGiftTownPopupReady(__instance);
        }

        [HarmonyPatch(typeof(GiftTownPopup), "Hide")]
        [HarmonyPostfix]
        private static void GiftTownPopupHidePostfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnGiftTownPopupHidden();
        }
    }
}
