using System.Collections.Generic;
using HarmonyLib;
using InputActions;
using SongsOfConquest.Client.InputManagement;
using SongsOfConquestAccess.Input;
using UnityEngine.InputSystem;
using GameUIActions = InputActions.UI;
using InputSystemGamepad = UnityEngine.InputSystem.Gamepad;
using UnityInputAction = UnityEngine.InputSystem.InputAction;

namespace SongsOfConquestAccess
{
    [HarmonyPatch(typeof(UnityInputManager), "HandleInputActionTriggered")]
    internal static class InputManagerPatches
    {
        private static readonly AccessTools.FieldRef<UnityInputManager, HashSet<string>> SquelchedBindingPathsRef =
            AccessTools.FieldRefAccess<UnityInputManager, HashSet<string>>("_squelchedBindingPaths");

        [HarmonyPrefix]
        private static bool HandleInputActionTriggeredPrefix(UnityInputManager __instance, UnityInputAction.CallbackContext context)
        {
            if (__instance == null || __instance.CurrentInputLevel == InputLevel.OperatingSystem)
            {
                return true;
            }

            if (IsSquelched(__instance, context))
            {
                return true;
            }

            ActionReference reference = new ActionReference(context.action.name);
            InputSource source = GetInputSource(context);
            InputPhase phase = GetInputPhase(context.phase);
            AccessibilityInputRouter router = SoqAccessPlugin.Instance != null ? SoqAccessPlugin.Instance.InputRouter : null;
            if (router == null || !router.TryHandleKeyboardInput(source, phase, context.control))
            {
                return true;
            }

            // Accessibility handled this keyboard event. Squelch the original
            // bound game action so any other game callbacks using the same key
            // path are ignored for the rest of this frame.
            __instance.SquelchBoundButtonUntilEndOfFrame(reference);

            // Also suppress the game's UI module on its next process tick so the
            // selected Unity control does not process the same key a second time.
            __instance.SquelchUIEventsNextTick();
            SoqAccessPlugin.Instance?.LogInfo("InputManagerPatches consumed input action " + reference.Identifier);
            return false;
        }

        private static bool IsSquelched(UnityInputManager inputManager, UnityInputAction.CallbackContext context)
        {
            if (context.action == null || context.action.type != InputActionType.Button)
            {
                return false;
            }

            HashSet<string> squelchedBindingPaths = SquelchedBindingPathsRef(inputManager);
            if (squelchedBindingPaths == null || squelchedBindingPaths.Count == 0)
            {
                return false;
            }

            foreach (InputBinding binding in context.action.bindings)
            {
                if (squelchedBindingPaths.Contains(binding.effectivePath))
                {
                    return true;
                }
            }

            return false;
        }

        private static InputSource GetInputSource(UnityInputAction.CallbackContext context)
        {
            InputDevice device = context.control != null ? context.control.device : null;
            if (device is InputSystemGamepad)
            {
                return InputSource.Gamepad;
            }

            if (device is Keyboard)
            {
                return InputSource.Keyboard;
            }

            if (device is Mouse)
            {
                return InputSource.Mouse;
            }

            if (device is Touchscreen)
            {
                return InputSource.Touchscreen;
            }

            return InputSource.Unknown;
        }

        private static InputPhase GetInputPhase(InputActionPhase phase)
        {
            switch (phase)
            {
                case InputActionPhase.Started:
                    return InputPhase.Down;
                case InputActionPhase.Performed:
                    return InputPhase.Held;
                case InputActionPhase.Canceled:
                    return InputPhase.Up;
                default:
                    return InputPhase.Unknown;
            }
        }
    }
}
