using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Screens;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace SongsOfConquestAccess.Input
{
    internal sealed class AccessibilityInputRouter : IDisposable, IObserver<InputEventPtr>
    {
        private const float ReleasePollingDelaySeconds = 0.05f;

        private readonly ScreenManager _screenManager;
        private readonly Dictionary<string, ActiveBindingState> _activeBindings =
            new Dictionary<string, ActiveBindingState>();
        private IDisposable _rawInputSubscription;

        public AccessibilityInputRouter(ScreenManager screenManager)
        {
            _screenManager = screenManager;
            _rawInputSubscription = InputSystem.onEvent.Subscribe(this);
            SoqAccessPlugin.Instance?.LogInfo("AccessibilityInputRouter raw keyboard input attached");
        }

        public void Dispose()
        {
            if (_rawInputSubscription != null)
            {
                _rawInputSubscription.Dispose();
                _rawInputSubscription = null;
                SoqAccessPlugin.Instance?.LogInfo("AccessibilityInputRouter raw keyboard input detached");
            }

            _activeBindings.Clear();
        }

        public void Update()
        {
            ConfirmPendingReleases();
        }

        public void OnCompleted()
        {
            // Required by IObserver<InputEventPtr>. Unity's input event stream
            // stays live until we unsubscribe, so the router has no completion
            // behavior to run here.
        }

        public void OnError(Exception error)
        {
            SoqAccessPlugin.Instance?.LogWarning("AccessibilityInputRouter raw input stream error: " + error);
        }

        public void OnNext(InputEventPtr value)
        {
            if (!value.valid)
            {
                return;
            }

            InputDevice device = InputSystem.GetDeviceById(value.deviceId);
            if (!(device is Keyboard))
            {
                return;
            }

            foreach (InputControl control in InputControlExtensions.EnumerateChangedControls(value, device, 0f))
            {
                KeyControl keyControl = control as KeyControl;
                if (keyControl == null)
                {
                    continue;
                }

                float rawValue;
                if (!InputControlExtensions.ReadValueFromEvent(keyControl, value, out rawValue))
                {
                    continue;
                }

                bool pressed = rawValue >= keyControl.pressPointOrDefault;
                if (pressed)
                {
                    if (TryHandleKeyDown(keyControl.keyCode))
                    {
                        value.handled = true;
                    }
                }
            }
        }

        private bool TryHandleKeyDown(Key key)
        {
            ActiveBindingState activeForKey = FindActiveBindingForKey(key);
            if (activeForKey != null)
            {
                // Modifier state can change while a primary key is still held
                // (for example, releasing Shift before releasing Tab). Do not let
                // the same held primary key trigger a different binding until the
                // primary key has been confirmed released.
                return true;
            }

            KeyboardStateSnapshot state = KeyboardStateSnapshot.Capture();
            BindingMatch match = ResolveClaimedMatch(key, state);
            if (match == null)
            {
                match = ResolveGlobalMatch(key, state);
                if (match == null)
                {
                    return false;
                }

                _screenManager?.HandleGlobalAction(match.Action);
            }
            else
            {
                _screenManager?.DispatchAction(match.Action);
            }

            _activeBindings[match.Binding.Id] = new ActiveBindingState(match.Action, match.Binding);
            return true;
        }

        private ActiveBindingState FindActiveBindingForKey(Key key)
        {
            foreach (ActiveBindingState state in _activeBindings.Values)
            {
                if (state.Binding.UsesKey(key))
                {
                    return state;
                }
            }

            return null;
        }

        private BindingMatch ResolveClaimedMatch(Key key, KeyboardStateSnapshot state)
        {
            for (int i = 0; i < AccessibilityActions.NON_GLOBAL_ACTIONS.Length; i++)
            {
                InputAction action = AccessibilityActions.NON_GLOBAL_ACTIONS[i];
                if (!CurrentScreenClaims(action))
                {
                    continue;
                }

                KeyboardBinding binding = FindMatchingKeyboardBinding(action, key, state);
                if (binding != null)
                {
                    return new BindingMatch(action, binding);
                }
            }

            return null;
        }

        private BindingMatch ResolveGlobalMatch(Key key, KeyboardStateSnapshot state)
        {
            for (int i = 0; i < AccessibilityActions.GLOBAL_ACTIONS.Length; i++)
            {
                InputAction action = AccessibilityActions.GLOBAL_ACTIONS[i];
                KeyboardBinding binding = FindMatchingKeyboardBinding(action, key, state);
                if (binding != null)
                {
                    return new BindingMatch(action, binding);
                }
            }

            return null;
        }

        private static KeyboardBinding FindMatchingKeyboardBinding(
            InputAction action,
            Key key,
            KeyboardStateSnapshot state)
        {
            if (action == null || action.Bindings == null)
            {
                return null;
            }

            for (int i = 0; i < action.Bindings.Count; i++)
            {
                KeyboardBinding binding = action.Bindings[i] as KeyboardBinding;
                if (binding != null && binding.MatchesKeyDown(key, state))
                {
                    return binding;
                }
            }

            return null;
        }

        private void ConfirmPendingReleases()
        {
            if (_activeBindings.Count == 0)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            List<string> released = null;
            foreach (KeyValuePair<string, ActiveBindingState> item in _activeBindings)
            {
                ActiveBindingState state = item.Value;
                if (UnityEngine.Time.unscaledTime - state.ActivatedAtSeconds < ReleasePollingDelaySeconds)
                {
                    continue;
                }

                if (keyboard[state.Binding.Key].isPressed)
                {
                    continue;
                }

                // In testing, handled raw keyboard events did not produce a
                // matching raw Up event for this observer, so raw events are
                // used only for press interception and release detection comes
                // from the current physical keyboard state during Update.
                //
                // Keyboard.current can still be stale when the raw Down event is
                // handled: Tab was observed as not pressed in the same frame
                // that its raw Down event dispatched. Without the short delay
                // above, the binding is activated and released immediately. If
                // Unity then emits a duplicate raw Down for the same physical
                // press, such as Shift+Tab after commander sheet modifier tab
                // focus changes, the duplicate is treated as a new action
                // instead of being suppressed by the active binding.
                if (released == null)
                {
                    released = new List<string>();
                }

                released.Add(item.Key);
            }

            if (released == null)
            {
                return;
            }

            for (int i = 0; i < released.Count; i++)
            {
                _activeBindings.Remove(released[i]);
            }
        }

        private bool CurrentScreenClaims(InputAction action)
        {
            return action != null
                && _screenManager != null
                && _screenManager.CurrentScreenClaimsAction(action.Key);
        }

        private sealed class ActiveBindingState
        {
            public ActiveBindingState(InputAction action, KeyboardBinding binding)
            {
                Action = action;
                Binding = binding;
                ActivatedAtSeconds = UnityEngine.Time.unscaledTime;
            }

            public InputAction Action { get; private set; }

            public KeyboardBinding Binding { get; private set; }

            public float ActivatedAtSeconds { get; private set; }
        }

        private sealed class BindingMatch
        {
            public BindingMatch(InputAction action, KeyboardBinding binding)
            {
                Action = action;
                Binding = binding;
            }

            public InputAction Action { get; private set; }

            public KeyboardBinding Binding { get; private set; }
        }

        internal sealed class KeyboardStateSnapshot
        {
            private KeyboardStateSnapshot(bool ctrl, bool shift, bool alt)
            {
                Ctrl = ctrl;
                Shift = shift;
                Alt = alt;
            }

            public bool Ctrl { get; private set; }

            public bool Shift { get; private set; }

            public bool Alt { get; private set; }

            public static KeyboardStateSnapshot Capture()
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard == null)
                {
                    return new KeyboardStateSnapshot(false, false, false);
                }

                return new KeyboardStateSnapshot(
                    keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed,
                    keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed,
                    keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
            }
        }
    }
}
