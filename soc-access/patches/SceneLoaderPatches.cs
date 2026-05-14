using HarmonyLib;
using SongsOfConquest.Client.Adventure.View;
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
            SocAccessPlugin plugin = SocAccessPlugin.Instance;
            if (plugin == null || __instance == null)
            {
                return;
            }

            if (state == SceneLoaderState.LoadingScene && __instance.Current == SceneType.Adventure)
            {
                plugin.ScreenDetector?.OnAdventureMapClosed();
                return;
            }

            if (state == SceneLoaderState.None && __instance.Current == SceneType.Adventure)
            {
                plugin.ScreenDetector?.OnAdventureMapReady();
            }
        }

        [HarmonyPatch(typeof(AdventureViewInstaller), "InstallBindings")]
        [HarmonyPostfix]
        private static void AdventureViewInstallerInstallBindingsPostfix(AdventureViewInstaller __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnAdventureViewReady(__instance);
        }
    }
}
