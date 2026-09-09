using HarmonyLib;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess.Patches
{
    [HarmonyPatch]
    public static class MapEntityMiniMenuPatches
    {
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
    }
}
