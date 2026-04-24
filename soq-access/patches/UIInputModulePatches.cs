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

            // Some UI navigation keys are mapped in AccessibilityInputRouter but do not arrive
            // through UnityInputManager.HandleInputActionTriggered unless the game has an active
            // input action bound for them. Intercept those keys at the UI module's navigation
            // stage instead, but only when the top accessibility screen owns the action.
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

            if (keyboard.homeKey.wasPressedThisFrame
                && plugin.ScreenManager.CurrentScreenClaimsAction(AccessibilityActions.FirstMenuItem.Key))
            {
                plugin.InputRouter.TryHandleRawKeyboardKey(Key.Home);
                return false;
            }

            if (keyboard.endKey.wasPressedThisFrame
                && plugin.ScreenManager.CurrentScreenClaimsAction(AccessibilityActions.LastMenuItem.Key))
            {
                plugin.InputRouter.TryHandleRawKeyboardKey(Key.End);
                return false;
            }

            return true;
        }
    }
}
