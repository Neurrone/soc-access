using System.Reflection;
using HarmonyLib;
using SongsOfConquestAccess.Input;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class UIInputModulePatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(InputSystemUIInputModule), "ProcessNavigation");
        }

        [HarmonyPrefix]
        private static bool ProcessNavigationPrefix()
        {
            SoqAccessPlugin plugin = SoqAccessPlugin.Instance;
            if (plugin == null || plugin.ScreenManager == null || plugin.InputRouter == null)
            {
                return true;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return true;
            }

            // Arrow keys do not arrive through UnityInputManager.HandleInputActionTriggered.
            // Intercept them at the UI module's navigation stage instead, but only when the top
            // accessibility screen actually owns menu navigation.
            if (keyboard.upArrowKey.wasPressedThisFrame
                && plugin.ScreenManager.CurrentScreenClaimsAction(AccessibilityActions.PreviousMenuItem.Key))
            {
                plugin.InputRouter.TryHandleRawKeyboardKey(Key.UpArrow);
                return false;
            }

            if (keyboard.downArrowKey.wasPressedThisFrame
                && plugin.ScreenManager.CurrentScreenClaimsAction(AccessibilityActions.NextMenuItem.Key))
            {
                plugin.InputRouter.TryHandleRawKeyboardKey(Key.DownArrow);
                return false;
            }

            return true;
        }
    }
}
