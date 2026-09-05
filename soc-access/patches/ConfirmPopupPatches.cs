using System;
using HarmonyLib;
using SongsOfConquest.Client.InputManagement;
using SongsOfConquest.Client.Menu;
using UnityEngine;

namespace SongsOfConquestAccess.Patches
{
    [HarmonyPatch]
    internal static class ConfirmPopupPatches
    {
        [HarmonyPatch(typeof(ConfirmPopup), "Show", new Type[] { typeof(string), typeof(string), typeof(Vector2), typeof(bool), typeof(InputLevel) })]
        [HarmonyPostfix]
        private static void ShowPostfix(ConfirmPopup __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnConfirmPopupReady(__instance);
        }

        [HarmonyPatch(typeof(ConfirmPopup), "ShowConfirmOnly", new Type[] { typeof(string), typeof(string), typeof(Vector2), typeof(bool), typeof(InputLevel) })]
        [HarmonyPostfix]
        private static void ShowConfirmOnlyPostfix(ConfirmPopup __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnConfirmPopupReady(__instance);
        }

        [HarmonyPatch(typeof(ConfirmPopup), "ShowDenyOnly", new Type[] { typeof(string), typeof(string), typeof(Vector2), typeof(bool), typeof(InputLevel) })]
        [HarmonyPostfix]
        private static void ShowDenyOnlyPostfix(ConfirmPopup __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnConfirmPopupReady(__instance);
        }

        [HarmonyPatch(typeof(ConfirmPopup), "ShowButtonLess", new Type[] { typeof(string), typeof(string), typeof(Vector2), typeof(bool), typeof(InputLevel) })]
        [HarmonyPostfix]
        private static void ShowButtonLessPostfix(ConfirmPopup __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnConfirmPopupReady(__instance);
        }

        [HarmonyPatch(typeof(ConfirmPopup), "Close", new Type[] { typeof(bool) })]
        [HarmonyPostfix]
        private static void ClosePostfix(ConfirmPopup __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnConfirmPopupClosed(__instance);
        }
    }
}
