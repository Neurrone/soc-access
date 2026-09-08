using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Common.GameActions;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess.Patches
{
    [HarmonyPatch]
    public static class MapEntityMiniMenuPatches
    {
        [HarmonyPatch(typeof(MapEntityMiniMenu), "Show")]
        [HarmonyPostfix]
        private static void ShowPostfix(MapEntityMiniMenu __instance)
        {
            MapEntityMiniMenuAdapter adapter = new MapEntityMiniMenuAdapter(__instance);
            if (adapter.IsPresent())
            {
                SocAccessMod.Instance?.ScreenDetector?.OnMapEntityMiniMenuReady(__instance);
            }
        }

        /// <summary>The two places the description block's rows change: SetDetails destroys every
        /// row and builds new ones, and Clear destroys them. The menu re-reads them once after
        /// either, rather than walking the block on every build.</summary>
        [HarmonyPatch(typeof(MiniMenuDescription), "SetDetails")]
        [HarmonyPostfix]
        private static void DescriptionSetPostfix()
        {
            MapEntityMiniMenuAdapter.DescriptionGeneration++;
        }

        [HarmonyPatch(typeof(MiniMenuDescription), "Clear")]
        [HarmonyPostfix]
        private static void DescriptionClearedPostfix()
        {
            MapEntityMiniMenuAdapter.DescriptionGeneration++;
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
                SocAccessMod.Instance?.ScreenDetector?.OnMapEntityMiniMenuClosed(__instance);
            }
        }
    }
}
