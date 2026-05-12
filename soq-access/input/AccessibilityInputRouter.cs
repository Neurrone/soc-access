using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.Screens;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace SongsOfConquestAccess.Input
{
    internal sealed class AccessibilityInputRouter : IDisposable, IObserver<InputEventPtr>
    {
        private const float ReleasePollingDelaySeconds = 0.05f;
        private const float ModifiedReleasePollingDelaySeconds = 0.10f;

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
            List<BindingMatch> claimedMatches = ResolveClaimedMatches(key, state);
            if (claimedMatches.Count > 0)
            {
                SpeechPipeline.Silence();
                DispatchClaimedMatches(claimedMatches);

                ActiveBindingState claimedBindingState = new ActiveBindingState(claimedMatches[0].Action, claimedMatches[0].Binding);
                _activeBindings[claimedMatches[0].Binding.Id] = claimedBindingState;
                return true;
            }

            BindingMatch match = ResolveGlobalMatch(key, state);
            if (match == null)
            {
                return false;
            }

            if (_screenManager != null && _screenManager.CanHandleGlobalAction(match.Action))
            {
                // Global actions such as opening the tooltip actions menu
                // can produce new focus speech. Silence only after the
                // preflight proves the action will run, so no-op global
                // presses do not cut off the current announcement.
                SpeechPipeline.Silence();
                _screenManager.HandleGlobalAction(match.Action);
            }

            ActiveBindingState bindingState = new ActiveBindingState(match.Action, match.Binding);
            _activeBindings[match.Binding.Id] = bindingState;
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

        private List<BindingMatch> ResolveClaimedMatches(Key key, KeyboardStateSnapshot state)
        {
            List<BindingMatch> matches = new List<BindingMatch>();
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
                    matches.Add(new BindingMatch(action, binding));
                }
            }

            return matches;
        }

        private void DispatchClaimedMatches(List<BindingMatch> matches)
        {
            if (matches == null)
            {
                return;
            }

            for (int i = 0; i < matches.Count; i++)
            {
                BindingMatch match = matches[i];
                if (match != null && _screenManager != null && _screenManager.DispatchAction(match.Action))
                {
                    return;
                }
            }
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
                float age = UnityEngine.Time.unscaledTime - state.ActivatedAtSeconds;
                float releaseDelay = GetReleasePollingDelaySeconds(state.Binding);
                if (age < releaseDelay)
                {
                    continue;
                }

                if (keyboard[state.Binding.Key].isPressed)
                {
                    continue;
                }

                // Plain bindings use the shortest release delay that avoids
                // duplicate key-down handling, so rapid arrow navigation remains
                // responsive. Modified bindings use a longer delay because
                // Unity.InputSystem 1.7.0 can emit duplicate raw Tab pressed
                // events for Shift+Tab while the key was not actually released.
                // We also did not observe raw Tab release events in this input
                // path, so release is inferred from Keyboard.current below. From
                // the mod side this debounce is the available workaround unless
                // the game updates to Input System 1.9.0 or newer, which fixes
                // related press/release frame-state bugs:
                // https://docs.unity.cn/Packages/com.unity.inputsystem%401.10/changelog/CHANGELOG.html#190---2024-07-15
                // See also:
                // https://discussions.unity.com/t/keyboard-current-temporarily-stops-registering-ispressed-or-waspressedthisframe-after-scene-load/1496259
                // https://discussions.unity.com/t/keyboard-current-key-waspressedthisframe-fires-multiple-times-before-key-is-released/886444
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

        private static float GetReleasePollingDelaySeconds(KeyboardBinding binding)
        {
            if (binding != null && (binding.Ctrl || binding.Shift || binding.Alt))
            {
                return ModifiedReleasePollingDelaySeconds;
            }

            return ReleasePollingDelaySeconds;
        }

        private bool CurrentScreenClaims(InputAction action)
        {
            return action != null
                && _screenManager != null
                && _screenManager.CurrentScreenClaimsAction(action);
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
