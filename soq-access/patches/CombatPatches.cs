using HarmonyLib;
using Lavapotion.Utilities;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Bacterias;
using SongsOfConquest.Common.Battle;
using SongsOfConquest.Common.Battle.Facade;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Gamestate.Facade;
using SongsOfConquest.Common.Spells;
using Lavapotion.Networking;
using SongsOfConquest.Client;
using SongsOfConquest;
using SongsOfConquest.Client.Battle;
using SongsOfConquest.Client.Battle.Facade;
using SongsOfConquest.Client.Battle.HUD;
using SongsOfConquest.Client.Battle.Menu;
using SongsOfConquest.Client.Battle.View;
using SongsOfConquest.Client.Entities.Battle;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class CombatPatches
    {
        private static readonly FieldInfo PuppetViewField = AccessTools.Field(typeof(BattleTroopViewPuppet), "_view");

        [HarmonyPatch(typeof(ClientBattleCommandsFacade), "Ready")]
        [HarmonyPostfix]
        private static void ClientBattleCommandsReadyPostfix()
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnCombatAvailable();
        }

        [HarmonyPatch(typeof(ClientBattleCommandsFacade), "OnResponseExecuted")]
        [HarmonyPrefix]
        private static void ClientBattleCommandsFacadeOnResponseExecutedPrefix(ICommandResponse r)
        {
            CombatEventNarrator.HandleResponse(r);
        }

        [HarmonyPatch(typeof(BattleNewRoundPopup), "ShowRoutine")]
        [HarmonyPostfix]
        private static void BattleNewRoundPopupShowRoutinePostfix(ref IEnumerator __result)
        {
            __result = BattleNewRoundPopupShowRoutineWrapper(__result);
        }

        private static IEnumerator BattleNewRoundPopupShowRoutineWrapper(IEnumerator original)
        {
            if (original == null)
            {
                yield break;
            }

            bool hasStep = original.MoveNext();
            if (hasStep)
            {
                yield return original.Current;
                CombatEventNarrator.NotifyNewRoundPopupVisible();
            }

            while (hasStep && original.MoveNext())
            {
                yield return original.Current;
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

        [HarmonyPatch(typeof(BattleTroopViewPuppet), "PerformMove")]
        [HarmonyPostfix]
        private static void BattleTroopViewPuppetPerformMovePostfix(BattleTroopViewPuppet __instance, List<Vector2Int> path, Async __result)
        {
            CombatEventNarrator.NotifyMoveStarted(GetPuppetTroopId(__instance), path, __result);
        }

        [HarmonyPatch(typeof(BattleTroopViewPuppet), "PerformAttack")]
        [HarmonyPostfix]
        private static void BattleTroopViewPuppetPerformAttackPostfix(BattleTroopViewPuppet __instance, bool didAttackMapEntity, DamageResult result, Async __result)
        {
            CombatEventNarrator.NotifyAttackStarted(GetPuppetTroopId(__instance), didAttackMapEntity, result, __result);
        }

        [HarmonyPatch(typeof(BattleTroopViewPuppet), "PerformBeingPushed")]
        [HarmonyPostfix]
        private static void BattleTroopViewPuppetPerformBeingPushedPostfix(BattleTroopViewPuppet __instance, Vector2Int from, Vector2Int to, Async __result)
        {
            CombatEventNarrator.NotifyPushStarted(GetPuppetTroopId(__instance), from, to, __result);
        }

        [HarmonyPatch(typeof(BattleTroopViewPuppet), "PerformAbility")]
        [HarmonyPostfix]
        private static void BattleTroopViewPuppetPerformAbilityPostfix(
            BattleTroopViewPuppet __instance,
            TroopAbilityType type,
            Vector2Int? targetingPosition,
            Vector2Int[] movementPath,
            Async __result)
        {
            CombatEventNarrator.NotifyAbilityStarted(GetPuppetTroopId(__instance), type, targetingPosition, movementPath, __result);
        }

        [HarmonyPatch(typeof(BattleEffectsSystem), "HandleNewTurn")]
        [HarmonyPrefix]
        private static void BattleEffectsSystemHandleNewTurnPrefix(SongsOfConquest.Common.Battle.Facade.OnNewTurnPayload payload)
        {
            if (payload != null)
            {
                CombatEventNarrator.NotifyNewTurnStarted(payload.NewTroopId);
            }
        }

        [HarmonyPatch(typeof(BattleEffectsSystem), "HandleSpellsCasted")]
        [HarmonyPrefix]
        private static void BattleEffectsSystemHandleSpellsCastedPrefix(SpellCastResponse spellResponse)
        {
            CombatEventNarrator.NotifySpellStarted(spellResponse);
        }

        [HarmonyPatch(typeof(BattleEffectsSystem), "HandleFaeyFire")]
        [HarmonyPrefix]
        private static void BattleEffectsSystemHandleFaeyFirePrefix(OnFaeyFirePayload payload)
        {
            if (payload != null)
            {
                CombatEventNarrator.NotifyFaeyFireStarted(payload.AttackerId, payload.DamageResults);
            }
        }

        [HarmonyPatch(typeof(BattleEffectsSystem), "HandleTroopGeneratedEssence")]
        [HarmonyPrefix]
        private static void BattleEffectsSystemHandleTroopGeneratedEssencePrefix(OnTroopGenerateEssencePayload payload)
        {
            if (payload != null)
            {
                CombatEventNarrator.NotifyEssenceGeneratedStarted(payload.TroopId);
            }
        }

        [HarmonyPatch(typeof(BattleEffectsSystem), "HandleBacteriaAdded")]
        [HarmonyPrefix]
        private static void BattleEffectsSystemHandleBacteriaAddedPrefix(int troopId, BacteriaReference bacteriaRef)
        {
            // The spoken bacteria-added event is tied to the HUD notification hook,
            // because the game deliberately suppresses some bacteria notifications.
        }

        [HarmonyPatch(typeof(BattleEffectsSystem), "HandleBacteriaRemoved")]
        [HarmonyPrefix]
        private static void BattleEffectsSystemHandleBacteriaRemovedPrefix(int troopId, BacteriaReference bacteriaRef)
        {
            CombatEventNarrator.NotifyBacteriaRemovedStarted(troopId, bacteriaRef);
        }

        [HarmonyPatch(typeof(BattleEffectsSystem), "HandleBacteriaModifierApplied")]
        [HarmonyPrefix]
        private static void BattleEffectsSystemHandleBacteriaModifierAppliedPrefix(int troopId, BacteriaReference bacteriaRef)
        {
            CombatEventNarrator.NotifyBacteriaModifierAppliedStarted(troopId, bacteriaRef);
        }

        [HarmonyPatch(typeof(BattleDamageNumberManager), "PlayDamageIndicator")]
        [HarmonyPrefix]
        private static void BattleDamageNumberManagerPlayDamageIndicatorPrefix(DamageResult damageResult, Vector2 direction, bool isMapEntity)
        {
            CombatEventNarrator.NotifyDamageIndicatorStarted(damageResult, isMapEntity);
        }

        [HarmonyPatch(typeof(BattleMapEntityViewManager), "HandleMapEntityCreated")]
        [HarmonyPrefix]
        private static void BattleMapEntityViewManagerHandleMapEntityCreatedPrefix(int entityId)
        {
            CombatEventNarrator.NotifyMapEntityCreatedStarted(entityId);
        }

        [HarmonyPatch(typeof(BattleMapEntityViewManager), "HandleMapEntityDestroyed")]
        [HarmonyPrefix]
        private static void BattleMapEntityViewManagerHandleMapEntityDestroyedPrefix(int entityId)
        {
            CombatEventNarrator.NotifyMapEntityDestroyedStarted(entityId);
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

        [HarmonyPatch(typeof(BattleHUDNotificationManager), "HandleBurrowUp")]
        [HarmonyPrefix]
        private static void BattleHUDNotificationManagerHandleBurrowUpPrefix(IBattleTroopState troop, bool burrowSuccess)
        {
            if (troop != null)
            {
                CombatEventNarrator.NotifyBurrowUpStarted(troop.Id, burrowSuccess);
            }
        }

        [HarmonyPatch(typeof(BattleResultMenu), "ShowResultUI")]
        [HarmonyPostfix]
        private static void BattleResultMenuShowResultUIPostfix(bool isVictory)
        {
            CombatEventNarrator.NotifyBattleResultPopupStarted(isVictory);
        }

        [HarmonyPatch(typeof(BattleResultMenu), "HandleOutcome")]
        [HarmonyPrefix]
        private static void BattleResultMenuHandleOutcomePrefix(BattleOutcome outcome)
        {
            if (outcome != BattleOutcome.Inconclusive)
            {
                SoqAccessPlugin.Instance?.ScreenDetector?.OnCombatInteractionEnded();
            }
        }

        [HarmonyPatch(typeof(AdventureBattleMenu), "Open", new[] { typeof(IBattleResult) })]
        [HarmonyPostfix]
        private static void AdventureBattleMenuOpenPostBattlePostfix(AdventureBattleMenu __instance)
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnPostBattleResultShown(__instance);
        }

        [HarmonyPatch(typeof(PostBattleMenu), "Hide")]
        [HarmonyPostfix]
        private static void PostBattleMenuHidePostfix()
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnPostBattleResultHidden();
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

            SoqAccessPlugin.Instance?.ScreenDetector?.OnPostBattleResultFullyPopulated();
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

        private static int GetPuppetTroopId(BattleTroopViewPuppet puppet)
        {
            if (puppet == null || PuppetViewField == null)
            {
                return -1;
            }

            IBattleTroopView view = PuppetViewField.GetValue(puppet) as IBattleTroopView;
            return view != null && view.TroopState != null ? view.TroopState.Id : -1;
        }
    }
}
