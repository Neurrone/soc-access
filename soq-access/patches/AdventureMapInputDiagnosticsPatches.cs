using System;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.InputManagement;
using SongsOfConquest.Common.Gamestate;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [HarmonyPatch(typeof(MouseKeyboardHumanAdventureControllerModule), "HandleSecondaryInputEnded")]
    internal static class AdventureMapInputDiagnosticsPatches
    {
        private static readonly FieldInfo CurrentHoverTileField =
            AccessTools.Field(typeof(MouseKeyboardHumanAdventureControllerModule), "_currentHoverTile");

        private static readonly MethodInfo GetTileAtMousePosMethod =
            AccessTools.Method(typeof(MouseKeyboardHumanAdventureControllerModule), "GetTileAtMousePos");

        private static readonly MethodInfo GetTrueTileMethod =
            AccessTools.Method(typeof(MouseKeyboardHumanAdventureControllerModule), "GetTrueTile");

        private static readonly PropertyInfo ControllerProperty =
            AccessTools.Property(typeof(HumanAdventureController.AbstractInputModule), "Controller");

        private static readonly FieldInfo InputManagerField =
            AccessTools.Field(typeof(HumanAdventureController.AbstractInputModule), "_inputManager");

        private static readonly FieldInfo SelectionHandlerField =
            AccessTools.Field(typeof(HumanAdventureController.AbstractInputModule), "_selectionHandler");

        [HarmonyPrefix]
        private static void HandleSecondaryInputEndedPrefix(MouseKeyboardHumanAdventureControllerModule __instance)
        {
            LogNativeSecondaryInput(__instance, "before");
        }

        [HarmonyPostfix]
        private static void HandleSecondaryInputEndedPostfix(MouseKeyboardHumanAdventureControllerModule __instance)
        {
            LogNativeSecondaryInput(__instance, "after");
        }

        private static void LogNativeSecondaryInput(MouseKeyboardHumanAdventureControllerModule instance, string phase)
        {
            if (instance == null)
            {
                return;
            }

            try
            {
                IHumanAdventureControllerFacade controller = ControllerProperty?.GetValue(instance, null) as IHumanAdventureControllerFacade;
                IInputManager inputManager = InputManagerField?.GetValue(instance) as IInputManager;
                ISelectionHandler selectionHandler = SelectionHandlerField?.GetValue(instance) as ISelectionHandler;
                ICommanderState selectedCommander = selectionHandler != null ? selectionHandler.SelectedCommander : null;

                Vector2Int hoverTile = GetVector2IntField(CurrentHoverTileField, instance);
                Vector2Int mouseTile = InvokeVector2Int(GetTileAtMousePosMethod, instance);
                Vector2Int trueTile = InvokeVector2Int(GetTrueTileMethod, instance);
                Vector2 screenPosition = inputManager != null && inputManager.Screen != null && inputManager.Screen.Primary != null
                    ? inputManager.Screen.Primary.Position
                    : Vector2.zero;
                bool isOverUi = inputManager != null && inputManager.Screen != null && inputManager.Screen.Primary != null && inputManager.Screen.Primary.IsOverUI;
                bool isPanning = inputManager != null && inputManager.Screen != null && inputManager.Screen.Primary != null && inputManager.Screen.Primary.IsPanning;
                bool isOverGameWindow = inputManager != null && inputManager.Screen != null && inputManager.Screen.Primary != null && inputManager.Screen.Primary.IsOverGameWindow;

                SoqAccessPlugin.Instance?.LogInfo(
                    "Adventure native secondary input diagnostic "
                    + phase
                    + ": screenPosition="
                    + screenPosition
                    + "; isOverUI="
                    + isOverUi
                    + "; isPanning="
                    + isPanning
                    + "; isOverGameWindow="
                    + isOverGameWindow
                    + "; mouseTile="
                    + FormatTile(mouseTile)
                    + "; trueTile="
                    + FormatTile(trueTile)
                    + "; hoverTile="
                    + FormatTile(hoverTile)
                    + "; currentDestinationTile="
                    + FormatTile(controller != null ? controller.CurrentDestinationTile : Vector2Int.zero)
                    + "; controllerState="
                    + (controller != null ? controller.StateMachine.CurrentStateType.ToString() : "<null>")
                    + "; selectedCommander="
                    + DescribeCommander(selectedCommander)
                    + "; selectedCommanderDestination="
                    + DescribeDestination(selectedCommander));
            }
            catch (Exception exception)
            {
                SoqAccessPlugin.Instance?.LogWarning("Adventure native secondary input diagnostic failed: " + exception);
            }
        }

        private static Vector2Int GetVector2IntField(FieldInfo field, object instance)
        {
            if (field == null || instance == null)
            {
                return Vector2Int.zero;
            }

            object value = field.GetValue(instance);
            return value is Vector2Int ? (Vector2Int)value : Vector2Int.zero;
        }

        private static Vector2Int InvokeVector2Int(MethodInfo method, object instance)
        {
            if (method == null || instance == null)
            {
                return Vector2Int.zero;
            }

            object value = method.Invoke(instance, null);
            return value is Vector2Int ? (Vector2Int)value : Vector2Int.zero;
        }

        private static string DescribeCommander(ICommanderState commander)
        {
            if (commander == null)
            {
                return "<null>";
            }

            return "id="
                + commander.Id
                + ",team="
                + commander.TeamId
                + ",position="
                + FormatTile(commander.Position)
                + ",state="
                + commander.InternalState
                + ",movesLeft="
                + commander.MovesLeft;
        }

        private static string DescribeDestination(ICommanderState commander)
        {
            if (commander == null || commander.Destination == null || !commander.Destination.HasDestination)
            {
                return "<none>";
            }

            return FormatTile(commander.Destination.Destination);
        }

        private static string FormatTile(Vector2Int tile)
        {
            return tile.x + "," + tile.y;
        }
    }
}
