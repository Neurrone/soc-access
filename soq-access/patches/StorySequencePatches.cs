using HarmonyLib;
using SongsOfConquest.Client.Adventure.Menu;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Common.Gamestate.Facade;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class StorySequencePatches
    {
        private static readonly AccessTools.FieldRef<ReactiveAdventureMenuSystem, IClientAdventureFacade> FacadeRef =
            AccessTools.FieldRefAccess<ReactiveAdventureMenuSystem, IClientAdventureFacade>("_facade");

        [HarmonyPatch(typeof(ReactiveAdventureMenuSystem), "HandleTrigger")]
        [HarmonyPrefix]
        private static void HandleTriggerPrefix(ReactiveAdventureMenuSystem __instance, OnTriggerPayload payload)
        {
            IClientAdventureFacade facade = null;
            if (__instance != null && FacadeRef != null)
            {
                facade = FacadeRef(__instance);
            }

            SoqAccessPlugin.Instance?.ScreenDetector?.OnStorySequenceTrigger(payload, facade);
        }

        [HarmonyPatch(typeof(ReactiveAdventureMenuSystem), "HandleTriggerSeriesCompleted")]
        [HarmonyPostfix]
        private static void HandleTriggerSeriesCompletedPostfix()
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnStorySequenceCompleted();
        }
    }
}
