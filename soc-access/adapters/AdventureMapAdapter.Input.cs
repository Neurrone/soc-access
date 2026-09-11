using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Lavapotion.Cartography;
using Lavapotion.Pathfinding;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.Map;
using SongsOfConquest.Client.Adventure.Menu;
using SongsOfConquest.Client.Adventure.View;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Client.Grid;
using SongsOfConquest.Client.InputManagement;
using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Gamestate.Commander;
using SongsOfConquest.Common.Localization;
using SongsOfConquest.Common.Map;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Bookmarks;
using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.Speech.Spatial;
using SongsOfConquestAccess.UI;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// NATIVE INPUT: the map's primary and secondary actions, and the next-wielder and next-town
    /// selections, each invoked through the game's own input path rather than reimplemented
    /// (AGENTS.md, "Native Input Equivalence").
    ///
    /// Split out of AdventureMapAdapter.cs as a pure move; nothing here changed with the split.
    /// </summary>
    public sealed partial class AdventureMapAdapter
    {
        public bool HandlePrimaryAction(Vector2Int position)
        {
            Vector2Int tilePosition = ClampToMap(position);

            try
            {
                TryInvokeNativeMapInput(
                    tilePosition,
                    "primary",
                    ModText.Get(ModStrings.Events.MapInputNotReady),
                    ModText.Get(ModStrings.Events.MapTileNotTargetable),
                    delegate(object inputModule, ScreenInputOverride screenInputOverride)
                    {
                        InvokeNativeInputModuleAction(inputModule, "HandlePrimaryInputStart");
                        InvokeNativeInputModuleAction(inputModule, "HandlePrimaryInputClick");
                    });
                return true;
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("AdventureMapAdapter primary action failed: " + exception);
                PublishDenied(tilePosition, ModText.Get(ModStrings.Events.MapPrimaryActionFailed));
                return true;
            }
        }

        public bool HandleSecondaryAction(Vector2Int position)
        {
            Vector2Int tilePosition = ClampToMap(position);

            try
            {
                TryInvokeNativeMapInput(
                    tilePosition,
                    "secondary",
                    ModText.Get(ModStrings.Events.MapInteractionNotReady),
                    ModText.Get(ModStrings.Events.MapTileNotTargetable),
                    delegate(object inputModule, ScreenInputOverride screenInputOverride)
                    {
                        InvokeNativeInputModuleAction(inputModule, "HandleSecondaryInputStart");
                        InvokeNativeInputModuleAction(inputModule, "HandleSecondaryInputEnded");
                    });
                return true;
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("AdventureMapAdapter secondary action failed: " + exception);
                PublishDenied(tilePosition, ModText.Get(ModStrings.Events.MapSecondaryActionFailed));
                return true;
            }
        }

        public bool TrySelectNextWielder()
        {
            if (_humanAdventureController == null)
            {
                SocAccessMod.Instance?.LogWarning("AdventureMapAdapter could not select next wielder: controller is not available.");
                return true;
            }

            try
            {
                _humanAdventureController.TrySelectNextIdleCommander();
                return true;
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("AdventureMapAdapter next wielder action failed: " + exception);
                return true;
            }
        }

        public bool TrySelectNextSettlement()
        {
            if (_humanAdventureController == null)
            {
                SocAccessMod.Instance?.LogWarning("AdventureMapAdapter could not select next settlement: controller is not available.");
                return true;
            }

            try
            {
                _humanAdventureController.TrySelectNextTown();
                return true;
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("AdventureMapAdapter next settlement action failed: " + exception);
                return true;
            }
        }

        private bool TryInvokeNativeMapInput(
            Vector2Int tilePosition,
            string inputName,
            string unavailableMessage,
            string targetFailureMessage,
            Action<object, ScreenInputOverride> invoke)
        {
            if (_humanAdventureControllerFacade == null)
            {
                PublishDenied(tilePosition, unavailableMessage);
                return false;
            }

            object inputModule = GetCurrentInputModule();
            if (inputModule == null)
            {
                SocAccessMod.Instance?.LogWarning("AdventureMapAdapter could not invoke native " + inputName + " action because current input module was null");
                PublishDenied(tilePosition, unavailableMessage);
                return false;
            }

            ScreenInputOverride screenInputOverride;
            if (!TryBeginScreenInputOverride(tilePosition, out screenInputOverride))
            {
                PublishDenied(tilePosition, targetFailureMessage);
                return false;
            }

            try
            {
                InvokeNativeInputModuleAction(inputModule, "UpdateCurrentTile");
                invoke?.Invoke(inputModule, screenInputOverride);
            }
            finally
            {
                screenInputOverride.Restore();
            }

            return true;
        }

        private bool TryBeginScreenInputOverride(Vector2Int tilePosition, out ScreenInputOverride screenInputOverride)
        {
            screenInputOverride = null;
            if (_inputManager == null || _inputManager.Screen == null || _inputManager.Screen.Primary == null)
            {
                SocAccessMod.Instance?.LogWarning("AdventureMapAdapter could not override native screen input because primary screen input was unavailable");
                return false;
            }

            if (_cameraController == null || _cameraController.Camera == null)
            {
                SocAccessMod.Instance?.LogWarning("AdventureMapAdapter could not override native screen input because the adventure camera was unavailable");
                return false;
            }

            object response = ScreenInputOverride.ResolveWritableResponse(_inputManager.Screen.Primary);
            if (response == null)
            {
                SocAccessMod.Instance?.LogWarning("AdventureMapAdapter could not override native screen input because no writable ScreenInputResponse could be resolved from " + _inputManager.Screen.Primary.GetType().FullName);
                return false;
            }

            Vector3 worldPosition = GetWorldCenter(tilePosition);
            Vector3 screenPosition3 = _cameraController.Camera.WorldToScreenPoint(worldPosition);
            Vector2 screenPosition = new Vector2(screenPosition3.x, screenPosition3.y);
            if (screenPosition3.z < 0f
                || screenPosition.x < 0f
                || screenPosition.y < 0f
                || screenPosition.x > Screen.width
                || screenPosition.y > Screen.height)
            {
                SocAccessMod.Instance?.LogWarning("AdventureMapAdapter could not target tile " + FormatTile(tilePosition) + " because its screen position is outside the current view: " + screenPosition);
                return false;
            }

            // A still mouse over the world at the tile's screen point, put back in the finally that
            // ends TryInvokeNativeMapInput. The same five properties the game's own mouse writes, so
            // the native handler sees exactly what a click there would have left it (AGENTS.md,
            // "Native Input Equivalence").
            screenInputOverride = ScreenInputOverride.ApplyMouseClick(response, screenPosition, "AdventureMapAdapter");
            return screenInputOverride != null;
        }

        private object GetCurrentInputModule()
        {
            if (_humanAdventureController == null || _currentInputModuleField == null)
            {
                return null;
            }

            return _currentInputModuleField.GetValue(_humanAdventureController);
        }

        private void InvokeNativeInputModuleAction(object inputModule, string methodName)
        {
            MethodInfo method = inputModule != null ? AccessTools.Method(inputModule.GetType(), methodName) : null;
            if (method == null)
            {
                throw new MissingMethodException(inputModule != null ? inputModule.GetType().FullName : "<null>", methodName);
            }

            method.Invoke(inputModule, null);
        }

        private void PublishDenied(Vector2Int tilePosition, string message)
        {
            AccessibilityEventBus.Publish(new MapActionFailedEvent(tilePosition, message));
        }
    }
}
