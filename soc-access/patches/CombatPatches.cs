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
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    public static class CombatPatches
    {
        [HarmonyPatch(typeof(ClientBattleCommandsFacade), "Ready")]
        [HarmonyPostfix]
        private static void ClientBattleCommandsReadyPostfix(ClientBattleCommandsFacade __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCombatReady(__instance);
        }

        [HarmonyPatch(typeof(BattleSceneInstaller), "InstallBindings")]
        [HarmonyPostfix]
        private static void BattleSceneInstallerInstallBindingsPostfix(BattleSceneInstaller __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnBattleSceneReady(__instance);
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
                SocAccessMod.Instance?.ScreenDetector?.OnCombatEnded();
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
                SpeechPipeline.Output(new SpeechRequest(ModText.Get(ModStrings.Combat.SpellCancelled), interrupt: false));
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

        // The menu the game is hiding was UP, read off its own object: Hide is also how the menu is
        // put away unshown when the PRE-battle menu opens (AdventureBattleMenu.Open sets up the
        // commanders and then hides the post-battle menu), and only a menu that was showing has a
        // page to close. activeSelf rather than activeInHierarchy: confirming the result deactivates
        // the battle menu's container first, and this prefix runs after that.
        [HarmonyPatch(typeof(PostBattleMenu), "Hide")]
        [HarmonyPrefix]
        private static void PostBattleMenuHidePrefix(PostBattleMenu __instance)
        {
            if (__instance == null || !__instance.gameObject.activeSelf)
            {
                return;
            }

            SocAccessMod.Instance?.ScreenDetector?.OnPostBattleResultClosed();
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
