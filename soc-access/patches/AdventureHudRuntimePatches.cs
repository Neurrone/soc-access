using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.InputManagement;
using SongsOfConquest.Client.Battle.UI;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Gamestate.Facade;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    public static class AdventureHudRuntimePatches
    {
        private static readonly FieldInfo AdventureInputManagerField =
            AccessTools.Field(typeof(AdventureHUDStateHandler), "_inputManager");
        private static readonly FieldInfo BattleInputManagerField =
            AccessTools.Field(typeof(BattleHUDStateHandler), "_inputManager");
        private static readonly FieldInfo BattleHudVisibleField =
            AccessTools.Field(typeof(BattleHUDStateHandler), "_isHudVisible");
        private static readonly FieldInfo AdventureCameraSelectionHandlerField =
            AccessTools.Field(typeof(AdventureCameraController), "_selectionHandler");
        private static readonly FieldInfo AdventureCameraFacadeField =
            AccessTools.Field(typeof(AdventureCameraController), "_facade");
        private static readonly FieldInfo TeamQueueRoundTextsField =
            AccessTools.Field(typeof(TeamQueueHUDBehaviour), "_roundTexts");
        private static readonly PropertyInfo CameraMovementForcedProperty =
            AccessTools.Property(typeof(AbstractCameraController), "IsMovementForced");

        [HarmonyPatch(typeof(AdventureHUDStateHandler), "ToggleHud")]
        [HarmonyPrefix]
        private static void AdventureToggleHudPrefix(AdventureHUDStateHandler __instance, ref HudToggleState __state)
        {
            __state = new HudToggleState(__instance != null && __instance.IsVisible, IsKeyboardMouse(AdventureInputManagerField, __instance));
        }

        [HarmonyPatch(typeof(AdventureHUDStateHandler), "ToggleHud")]
        [HarmonyPostfix]
        private static void AdventureToggleHudPostfix(AdventureHUDStateHandler __instance, HudToggleState __state)
        {
            if (!__state.IsKeyboardMouse || __instance == null || __state.WasVisible == __instance.IsVisible)
            {
                return;
            }

            AccessibilityEventBus.Publish(new MapHudVisibilityChangedEvent(__instance.IsVisible));
        }

        [HarmonyPatch(typeof(BattleHUDStateHandler), "ToggleHud")]
        [HarmonyPrefix]
        private static void BattleToggleHudPrefix(BattleHUDStateHandler __instance, ref HudToggleState __state)
        {
            __state = new HudToggleState(GetBattleHudVisible(__instance), IsKeyboardMouse(BattleInputManagerField, __instance));
        }

        [HarmonyPatch(typeof(BattleHUDStateHandler), "ToggleHud")]
        [HarmonyPostfix]
        private static void BattleToggleHudPostfix(BattleHUDStateHandler __instance, HudToggleState __state)
        {
            bool isVisible = GetBattleHudVisible(__instance);
            if (!__state.IsKeyboardMouse || __state.WasVisible == isVisible)
            {
                return;
            }

            AccessibilityEventBus.Publish(new MapHudVisibilityChangedEvent(isVisible));
        }

        [HarmonyPatch(typeof(NotificationHUD), "HandleEntryMoreInformation")]
        [HarmonyPrefix]
        private static void NotificationMoreInformationPrefix(NotificationHUDEntryInformation entry)
        {
            if (entry == null || entry.WorldPosition == Vector2Int.zero)
            {
                return;
            }

            PublishMapCameraFocus("notification.more_information", entry.WorldPosition);
        }

        [HarmonyPatch(typeof(AdventureCameraController), "HandleHotkeyFocusSelected")]
        [HarmonyPostfix]
        private static void HandleHotkeyFocusSelectedPostfix(AdventureCameraController __instance)
        {
            if (IsCameraMovementForced(__instance))
            {
                return;
            }

            PublishSelectedCameraFocus(__instance, "camera.handle_hotkey_focus_selected");
        }

        [HarmonyPatch(typeof(AdventureCameraController), "OnCommanderSelectionChanged")]
        [HarmonyPostfix]
        private static void OnCommanderSelectionChangedPostfix(AdventureCameraController __instance, CommanderChangedPayload payload)
        {
            if (IsCameraMovementForced(__instance) || payload == null)
            {
                return;
            }

            if (payload.SelectionSource == SelectionSource.World || payload.SelectionSource == SelectionSource.None)
            {
                return;
            }

            ICommanderState commander = payload.SelectedCommander;
            IClientAdventureFacade facade = GetAdventureFacade(__instance);
            if (commander == null || facade == null || !facade.Teams.GetIsLocal(commander.TeamId))
            {
                return;
            }

            PublishMapCameraFocus("camera.commander_selection_changed." + payload.SelectionSource, commander.Position);
        }

        [HarmonyPatch(typeof(AdventureCameraController), "HandleMapEntityChanged")]
        [HarmonyPostfix]
        private static void HandleMapEntityChangedPostfix(AdventureCameraController __instance, MapEntityChangedPayload payload)
        {
            if (IsCameraMovementForced(__instance) || payload == null)
            {
                return;
            }

            IMapEntity selectedMapEntity = payload.SelectedMapEntity;
            if (selectedMapEntity == null)
            {
                return;
            }

            bool centeredBySelection =
                (payload.SelectionSource == SelectionSource.TownListHUD
                    || payload.SelectionSource == SelectionSource.CommanderHUD
                    || payload.SelectionSource == SelectionSource.StoreCommander
                    || payload.SelectionSource == SelectionSource.GameLogic)
                && IsTownLikeMapEntity(selectedMapEntity);
            bool centeredByHotkey = payload.SelectionSource == SelectionSource.Hotkey;
            if (centeredBySelection || centeredByHotkey)
            {
                PublishMapCameraFocus("camera.map_entity_changed." + payload.SelectionSource, selectedMapEntity.Position);
            }
        }

        [HarmonyPatch(typeof(TownListUI), "CenterCameraOnEntry")]
        [HarmonyPostfix]
        private static void TownListCenterCameraOnEntryPostfix(ITownListHUDEntry entry)
        {
            if (entry == null || entry.Town == null)
            {
                return;
            }

            PublishMapCameraFocus("town_list.center_camera_on_entry", entry.Town.Position);
        }

        [HarmonyPatch(typeof(TeamQueueHUDBehaviour), "HandleNewTurn")]
        [HarmonyPostfix]
        private static void TeamQueueHandleNewTurnPostfix(TeamQueueHUDBehaviour __instance, OnNewTurnPayload payload)
        {
            if (payload == null || payload.NewRoundNumber == payload.OldRoundNumber)
            {
                return;
            }

            string roundLabel = GetRoundLabel(__instance);
            if (string.IsNullOrWhiteSpace(roundLabel))
            {
                return;
            }

            AccessibilityEventBus.Publish(new MapRoundChangedEvent(roundLabel));
        }

        private static bool IsKeyboardMouse(FieldInfo inputManagerField, object instance)
        {
            IInputManager inputManager = inputManagerField != null && instance != null
                ? inputManagerField.GetValue(instance) as IInputManager
                : null;
            return inputManager != null && inputManager.CurrentInputMode == InputMode.KeyboardMouse;
        }

        private static bool GetBattleHudVisible(BattleHUDStateHandler handler)
        {
            if (handler == null || BattleHudVisibleField == null)
            {
                return false;
            }

            object value = BattleHudVisibleField.GetValue(handler);
            return value is bool && (bool)value;
        }

        private static void PublishSelectedCameraFocus(AdventureCameraController controller, string source)
        {
            ISelectionHandler selectionHandler = GetSelectionHandler(controller);
            if (selectionHandler == null)
            {
                return;
            }

            if (selectionHandler.SelectedCommander != null)
            {
                PublishMapCameraFocus(source + ".commander", selectionHandler.SelectedCommander.Position);
                return;
            }

            if (selectionHandler.SelectedMapEntity != null)
            {
                PublishMapCameraFocus(source + ".map_entity", selectionHandler.SelectedMapEntity.Position);
            }
        }

        private static void PublishMapCameraFocus(string source, Vector2Int tile)
        {
            AccessibilityEventBus.Publish(new MapCameraFocusEvent(tile));
        }

        private static string GetRoundLabel(TeamQueueHUDBehaviour teamQueueHud)
        {
            UITextMesh[] texts = TeamQueueRoundTextsField != null && teamQueueHud != null
                ? TeamQueueRoundTextsField.GetValue(teamQueueHud) as UITextMesh[]
                : null;
            if (texts == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < texts.Length; i++)
            {
                UITextMesh text = texts[i];
                if (text == null)
                {
                    continue;
                }

                string label = SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(text));
                if (!string.IsNullOrWhiteSpace(label))
                {
                    return label;
                }
            }

            return string.Empty;
        }

        private static ISelectionHandler GetSelectionHandler(AdventureCameraController controller)
        {
            return AdventureCameraSelectionHandlerField != null && controller != null
                ? AdventureCameraSelectionHandlerField.GetValue(controller) as ISelectionHandler
                : null;
        }

        private static IClientAdventureFacade GetAdventureFacade(AdventureCameraController controller)
        {
            return AdventureCameraFacadeField != null && controller != null
                ? AdventureCameraFacadeField.GetValue(controller) as IClientAdventureFacade
                : null;
        }

        private static bool IsCameraMovementForced(AdventureCameraController controller)
        {
            if (controller == null || CameraMovementForcedProperty == null)
            {
                return false;
            }

            object value = CameraMovementForcedProperty.GetValue(controller, null);
            return value is bool && (bool)value;
        }

        private static bool IsTownLikeMapEntity(IMapEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            return entity.Category == MapEntityCategory.Settlement
                || entity.Category == MapEntityCategory.Town
                || entity.Category == MapEntityCategory.Building
                || entity.Category == MapEntityCategory.BuildSite
                || entity.Category == MapEntityCategory.TroopDwelling
                || entity.Category == MapEntityCategory.ResourceGenerator;
        }

        private struct HudToggleState
        {
            public HudToggleState(bool wasVisible, bool isKeyboardMouse)
            {
                WasVisible = wasVisible;
                IsKeyboardMouse = isKeyboardMouse;
            }

            public bool WasVisible { get; private set; }

            public bool IsKeyboardMouse { get; private set; }
        }
    }
}
