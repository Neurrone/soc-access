using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Common.GameActions;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess.Patches
{
    [HarmonyPatch]
    internal static class MapEntityMiniMenuPatches
    {
        [HarmonyPatch(typeof(MapEntityMiniMenu), "Show")]
        [HarmonyPostfix]
        private static void ShowPostfix(MapEntityMiniMenu __instance)
        {
            MapEntityMiniMenuAdapter adapter = new MapEntityMiniMenuAdapter(__instance);
            if (adapter.IsPresent())
            {
                SocAccessPlugin.Instance?.ScreenDetector?.OnMapEntityMiniMenuReady(__instance);
            }
        }

        [HarmonyPatch(typeof(MapEntityMiniMenu), "Hide", new[] { typeof(HUDActionType) })]
        [HarmonyPrefix]
        private static void HidePrefix(MapEntityMiniMenu __instance, out bool __state)
        {
            __state = new MapEntityMiniMenuAdapter(__instance).IsPresent();
        }

        [HarmonyPatch(typeof(MapEntityMiniMenu), "Hide", new[] { typeof(HUDActionType) })]
        [HarmonyPostfix]
        private static void HidePostfix(MapEntityMiniMenu __instance, bool __state)
        {
            if (__state)
            {
                SocAccessPlugin.Instance?.ScreenDetector?.OnMapEntityMiniMenuClosed(__instance);
            }
        }
    }
}
