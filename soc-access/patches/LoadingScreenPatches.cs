using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess
{
    /// <summary>
    /// One hook, and it is an INTERCEPTION: a load starts while a native tooltip may still be drawn
    /// over the menu the player just left, and nothing takes that tooltip down - the menu under it is
    /// destroyed with the scene, and the tooltip is a project-level object that outlives it. Hiding it
    /// as the loading screen initializes is a change to what the game draws, not a readiness signal.
    ///
    /// The loading screen's own readiness is read from the game
    /// (<see cref="LoadingScreenAdapter.IsPresent"/>): the scene loader waiting for finalization with
    /// the "press any key" prompt drawn. Its close is read from the same state, and the continue the
    /// mod's own button presses goes through <c>LoadingScreenAdapter.Continue</c>, which invokes the
    /// game's <c>FinalizeLoadingScreen</c> directly. So the readiness postfix and the two close
    /// prefixes this class used to carry are gone.
    /// </summary>
    [HarmonyPatch]
    public static class LoadingScreenPatches
    {
        [HarmonyPatch(typeof(LoadingScreenMenu), "Initialize")]
        [HarmonyPrefix]
        private static void InitializePrefix()
        {
            NativeTooltipUtility.HideTooltip();
        }
    }
}
