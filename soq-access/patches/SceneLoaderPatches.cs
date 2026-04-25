using HarmonyLib;
using SongsOfConquest.Client.Menu.Loading;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class SceneLoaderPatches
    {
        [HarmonyPatch(typeof(SceneLoader), "SetState")]
        [HarmonyPostfix]
        private static void SetStatePostfix(SceneLoader __instance, SceneLoaderState state)
        {
            SoqAccessPlugin plugin = SoqAccessPlugin.Instance;
            if (plugin == null || __instance == null)
            {
                return;
            }

            if (state == SceneLoaderState.LoadingScene && __instance.Current == SceneType.Adventure)
            {
                plugin.ScreenDetector?.OnAdventureSceneUnloading();
                return;
            }

            if (state == SceneLoaderState.None && __instance.Current == SceneType.Adventure)
            {
                plugin.ScreenDetector?.OnAdventureSceneLoaded();
            }
        }
    }
}
