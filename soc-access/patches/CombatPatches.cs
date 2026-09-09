using HarmonyLib;
using SongsOfConquest.Common;
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
