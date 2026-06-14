using HarmonyLib;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Battle;
using SongsOfConquest.Common.Gamestate.Facade;
using Lavapotion.Networking;
using SongsOfConquest.Client;
using SongsOfConquest;
using SongsOfConquest.Client.Battle;
using SongsOfConquest.Client.Battle.Facade;
using SongsOfConquest.Client.Battle.Controller;
using SongsOfConquest.Client.Battle.HUD;
using SongsOfConquest.Client.Battle.Menu;
using SongsOfConquest.Client.Battle.View;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Speech;
using System.Collections;
using System.Collections.Generic;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class CombatPatches
    {
        private static readonly HashSet<int> ActivePostBattleMenus = new HashSet<int>();

        [HarmonyPatch(typeof(ClientBattleCommandsFacade), "Ready")]
        [HarmonyPostfix]
        private static void ClientBattleCommandsReadyPostfix(ClientBattleCommandsFacade __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnCombatReady(__instance);
        }

        [HarmonyPatch(typeof(BattleSceneInstaller), "InstallBindings")]
        [HarmonyPostfix]
        private static void BattleSceneInstallerInstallBindingsPostfix(BattleSceneInstaller __instance)
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnBattleSceneReady(__instance);
        }

        [HarmonyPatch(typeof(HumanBattleSpellController), "HandleSpellCastCancelled")]
        [HarmonyPrefix]
        private static void HumanBattleSpellControllerHandleSpellCastCancelledPrefix(HumanBattleSpellController __instance)
        {
            AnnounceSpellCancelledIfTargeting(__instance);
        }

        [HarmonyPatch(typeof(HumanBattleSpellController), "HandleHotkeyExitMenu")]
        [HarmonyPrefix]
        private static void HumanBattleSpellControllerHandleHotkeyExitMenuPrefix(HumanBattleSpellController __instance)
        {
            AnnounceSpellCancelledIfTargeting(__instance);
        }

        [HarmonyPatch(typeof(ClientBattleCommandsFacade), "OnResponseExecuted")]
        [HarmonyPrefix]
        private static void ClientBattleCommandsFacadeOnResponseExecutedPrefix(ICommandResponse r)
        {
            CombatEventNarrator.HandleResponse(r);
            if (r is BattleResultCommand.Response)
            {
                SocAccessPlugin.Instance?.ScreenDetector?.OnCombatEnded();
            }
        }

        private static void AnnounceSpellCancelledIfTargeting(HumanBattleSpellController controller)
        {
            if (controller == null)
            {
                return;
            }

            HumanBattleSpellController.State state = controller.CurrentState;
            if (state == HumanBattleSpellController.State.CastingBacteriaSpell
                || state == HumanBattleSpellController.State.CastingTeleportSpell
                || state == HumanBattleSpellController.State.CastingSummonSpell)
            {
                SpeechPipeline.Output(new SpeechRequest("Spell cancelled", interrupt: false));
            }
        }

        [HarmonyPatch(typeof(BattleAttackPreview), "AddAdditionalText")]
        [HarmonyPrefix]
        private static void BattleAttackPreviewAddAdditionalTextPrefix(BattleAttackPreview __instance, string AdditionalText)
        {
            CombatAdapter.CaptureAttackPreviewAdditionalText(__instance, AdditionalText);
        }

        [HarmonyPatch(typeof(BattleAttackPreview), "Hide")]
        [HarmonyPrefix]
        private static void BattleAttackPreviewHidePrefix(BattleAttackPreview __instance)
        {
            CombatAdapter.ClearAttackPreviewAdditionalText(__instance);
        }

        [HarmonyPatch(typeof(BattleHUDNotificationManager), "ShowTroopBacteriaNotification")]
        [HarmonyPrefix]
        private static void BattleHUDNotificationManagerShowTroopBacteriaNotificationPrefix(int troopId, string localizedText)
        {
            // Bacteria popup names are duplicate noise for screen-reader users.
            // Effect details, such as Momentum stat changes, are read from the
            // structured bacteria modifier events instead.
            CombatEventNarrator.NotifyBacteriaAddedStarted(troopId, localizedText);
        }

        [HarmonyPatch(typeof(AdventureBattleMenu), "Open", new[] { typeof(IBattleResult) })]
        [HarmonyPostfix]
        private static void AdventureBattleMenuOpenPostBattlePostfix(AdventureBattleMenu __instance)
        {
            PostBattleMenu menu = PostBattleResultAdapter.GetPostBattleMenu(__instance);
            if (menu != null)
            {
                ActivePostBattleMenus.Add(menu.GetInstanceID());
            }

            SocAccessPlugin.Instance?.ScreenDetector?.OnPostBattleResultReady(__instance);
        }

        [HarmonyPatch(typeof(PostBattleMenu), "Hide")]
        [HarmonyPrefix]
        private static void PostBattleMenuHidePrefix(PostBattleMenu __instance)
        {
            if (__instance == null || !ActivePostBattleMenus.Remove(__instance.GetInstanceID()))
            {
                return;
            }

            SocAccessPlugin.Instance?.ScreenDetector?.OnPostBattleResultClosed();
        }

        [HarmonyPatch(typeof(PostBattleMenu), "AnimateResults")]
        [HarmonyPostfix]
        private static void PostBattleMenuAnimateResultsPostfix(ref IEnumerator __result)
        {
            __result = PostBattleMenuAnimateResultsWrapper(__result);
        }

        private static IEnumerator PostBattleMenuAnimateResultsWrapper(IEnumerator original)
        {
            while (original != null && original.MoveNext())
            {
                yield return original.Current;
            }

            SocAccessPlugin.Instance?.ScreenDetector?.OnPostBattleResultChanged();
        }

        [HarmonyPatch(typeof(NotificationPanel), "Show", new[] { typeof(string), typeof(UnityEngine.Vector3), typeof(UnityEngine.Vector2) })]
        [HarmonyPostfix]
        private static void NotificationPanelShowWithPivotPostfix(string localizedString)
        {
            CombatEventNarrator.AnnounceNativeNotification(localizedString);
        }

        [HarmonyPatch(typeof(BattleHUDNotificationManager), "Show", new[] { typeof(string), typeof(UnityEngine.Vector2Int) })]
        [HarmonyPostfix]
        private static void BattleHUDNotificationPositionShowPostfix(string localizedText)
        {
            CombatEventNarrator.AnnounceNativeNotification(localizedText);
        }

        [HarmonyPatch(typeof(BattleHUDNotificationManager), "Show", new[] { typeof(bool), typeof(string) })]
        [HarmonyPostfix]
        private static void BattleHUDNotificationSideShowPostfix(string localizedText)
        {
            CombatEventNarrator.AnnounceNativeNotification(localizedText);
        }

        [HarmonyPatch(typeof(BattleHUDNotificationManager), "ShowLarge")]
        [HarmonyPostfix]
        private static void BattleHUDNotificationLargeShowPostfix(string localizedText)
        {
            CombatEventNarrator.AnnounceNativeNotification(localizedText);
        }

        public static void Reset()
        {
            CombatEventNarrator.Reset();
        }

    }
}
