using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.Screens;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace SongsOfConquestAccess.Input
{
    public sealed class AccessibilityInputRouter : IDisposable, IObserver<InputEventPtr>
    {
        private const float ReleasePollingDelaySeconds = 0.05f;
        private const float ModifiedReleasePollingDelaySeconds = 0.10f;

        private readonly ScreenManager _screenManager;
        private readonly Dictionary<string, ActiveBindingState> _activeBindings =
            new Dictionary<string, ActiveBindingState>();
        private readonly Queue<Injection> _injections = new Queue<Injection>();
        private IDisposable _rawInputSubscription;

        public AccessibilityInputRouter(ScreenManager screenManager)
        {
            _screenManager = screenManager;
            _rawInputSubscription = InputSystem.onEvent.Subscribe(this);
            FollowKeyboard();
            SocAccessMod.Instance?.LogInfo("AccessibilityInputRouter raw keyboard input attached");
        }

        public void Dispose()
        {
            if (_rawInputSubscription != null)
            {
                _rawInputSubscription.Dispose();
                _rawInputSubscription = null;
                SocAccessMod.Instance?.LogInfo("AccessibilityInputRouter raw keyboard input detached");
            }

            if (_textKeyboard != null)
            {
                _textKeyboard.onTextInput -= OnTextInput;
                _textKeyboard = null;
            }

            _activeBindings.Clear();
            _typed.Length = 0;
        }

        // ---- typed text, for the graph screens' type-ahead search ----
        //
        // A character is not a chord: it comes from the keyboard's own text events (layout, dead
        // keys and shift all resolved by the engine), never from mapping key codes to letters. The
        // characters are queued here and taken once a frame by the navigator (TakeTypedCharacters),
        // so a letter and the control it lands on are the same frame's work.

        private readonly System.Text.StringBuilder _typed = new System.Text.StringBuilder();
        private Keyboard _textKeyboard;

        // Subscribe to whichever keyboard is current. Asked every frame, not once: on a cold start
        // the mod comes up before the input system has a keyboard device at all (Keyboard.current
        // is null in the constructor), and the device can be replaced later. A reference compare
        // per frame is the whole cost.
        private void FollowKeyboard()
        {
            Keyboard current = Keyboard.current;
            if (ReferenceEquals(current, _textKeyboard))
            {
                return;
            }

            if (_textKeyboard != null)
            {
                _textKeyboard.onTextInput -= OnTextInput;
            }

            _textKeyboard = current;
            if (_textKeyboard != null)
            {
                _textKeyboard.onTextInput += OnTextInput;
                SocAccessMod.Instance?.LogInfo("AccessibilityInputRouter following keyboard " + _textKeyboard.deviceId + " for typed text");
            }
        }

        private void OnTextInput(char character)
        {
            // A chord is not typing: Ctrl+R and Alt+R are the mod's own commands, and the character
            // Windows still produces for them must not land in a search (ES2's TypedText.Frame makes
            // the same cut). Shift is typing - capitals.
            KeyboardStateSnapshot state = KeyboardStateSnapshot.Capture();
            if (state.Ctrl || state.Alt)
            {
                return;
            }

            // The stand-down: while the game's own field has the keyboard, the character is that
            // field's and is never a search.
            if (GameTextFocus.IsTyping())
            {
                return;
            }

            if (TypingScreen() != null)
            {
                _typed.Append(character);
            }
        }

        /// <summary>The characters typed since the last call, or null - the navigator's text source.</summary>
        public string TakeTypedCharacters()
        {
            if (_typed.Length == 0)
            {
                return null;
            }

            string typed = _typed.ToString();
            _typed.Length = 0;
            return typed;
        }

        // Whether a bare letter (or a space continuing a search) is the focused graph screen's to
        // hear. A chord with Ctrl or Alt held is not typing; Shift is (capitals).
        private bool TakesTypedKey(Key key, KeyboardStateSnapshot state)
        {
            if (state == null || state.Ctrl || state.Alt)
            {
                return false;
            }

            GraphScreen screen = TypingScreen();
            return screen != null && screen.Navigator != null && screen.Navigator.TakesTypedKey(key);
        }

        private GraphScreen TypingScreen()
        {
            return _screenManager == null ? null : _screenManager.CurrentScreen as GraphScreen;
        }

        public void Update()
        {
            FollowKeyboard();
            DrainInjections();
            ConfirmPendingReleases();
        }

        /// <summary>
        /// Queue an action to be run as though its key had been pressed, and hand back the ticket
        /// the caller waits on. Enqueued rather than run here because the dev server's HTTP thread
        /// is not the Unity main thread, and drained at the top of <see cref="Update"/> so it lands
        /// at the same point in the frame a real key press does - the same claim check, the same
        /// silence, the same dispatch.
        ///
        /// It deliberately does not touch <see cref="_activeBindings"/>: no physical key is down,
        /// so there is no release to wait for and nothing to debounce.
        /// </summary>
        public Injection Inject(InputAction action)
        {
            Injection injection = new Injection
            {
                Action = action,
                ActionKey = action != null ? action.Key : string.Empty,
            };
            lock (_injections)
            {
                _injections.Enqueue(injection);
            }

            return injection;
        }

        private void DrainInjections()
        {
            while (true)
            {
                Injection injection;
                lock (_injections)
                {
                    if (_injections.Count == 0)
                    {
                        return;
                    }

                    injection = _injections.Dequeue();
                }

                RunInjection(injection);
            }
        }

        private void RunInjection(Injection injection)
        {
            try
            {
                InputAction action = injection.Action;
                if (_screenManager == null || _screenManager.CurrentScreen == null)
                {
                    injection.Outcome = "no screen";
                    return;
                }

                // The same stand-down a physical key meets, so the dev server sees what the player
                // would: while the game's field has the keyboard, the mod answers nothing.
                if (GameTextFocus.IsTyping())
                {
                    injection.Outcome = "standing down";
                    return;
                }

                if (_screenManager.CurrentScreenClaimsAction(action))
                {
                    SpeechPipeline.Silence();
                    injection.Outcome = _screenManager.DispatchAction(action)
                        ? "consumed"
                        : "claimed, not handled";
                    return;
                }

                if (_screenManager.CanHandleGlobalAction(action))
                {
                    SpeechPipeline.Silence();
                    _screenManager.HandleGlobalAction(action);
                    injection.Outcome = "consumed (global)";
                    return;
                }

                injection.Outcome = "unclaimed";
            }
            finally
            {
                // Whatever happened, including a throw, the waiting HTTP thread is released.
                injection.Done.Set();
            }
        }

        /// <summary>One queued action and what became of it. The event is set on the main thread
        /// once the action has run; the dev server's handler waits on it.</summary>
        public sealed class Injection
        {
            public readonly System.Threading.ManualResetEvent Done =
                new System.Threading.ManualResetEvent(false);

            public InputAction Action;
            public string ActionKey;
            public string Outcome;
        }

        public void OnCompleted()
        {
            // Required by IObserver<InputEventPtr>. Unity's input event stream
            // stays live until we unsubscribe, so the router has no completion
            // behavior to run here.
        }

        public void OnError(Exception error)
        {
            SocAccessMod.Instance?.LogWarning("AccessibilityInputRouter raw input stream error: " + error);
        }

        public void OnNext(InputEventPtr value)
        {
            if (!value.valid)
            {
                return;
            }

            // Only key state carries presses. The keyboard also emits TEXT events for every typed
            // character (read through Keyboard.onTextInput), and EnumerateChangedControls throws
            // on those.
            if (value.type != StateEvent.Type && value.type != DeltaStateEvent.Type)
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
                    if (TryHandleKeyDown(keyControl))
                    {
                        value.handled = true;
                    }
                }
            }
        }

        private bool TryHandleKeyDown(KeyControl keyControl)
        {
            // THE STAND-DOWN, before any claim is even asked. A game text field with the keyboard owns
            // every key while it has it - the letters, the arrows walking the caret, the Backspace -
            // so the whole layer goes quiet rather than picking which keys to leave alone.
            if (GameTextFocus.IsTyping())
            {
                return false;
            }

            Key key = keyControl.keyCode;
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
            List<BindingMatch> claimedMatches = ResolveClaimedMatches(keyControl, state);
            if (claimedMatches.Count > 0)
            {
                SpeechPipeline.Silence();
                DispatchClaimedMatches(claimedMatches);

                ActiveBindingState claimedBindingState = new ActiveBindingState(
                    claimedMatches[0].Action,
                    claimedMatches[0].Binding,
                    claimedMatches[0].PressedKey);
                _activeBindings[claimedMatches[0].Binding.Id] = claimedBindingState;
                return true;
            }

            BindingMatch match = ResolveGlobalMatch(keyControl, state);
            if (match == null)
            {
                // A letter no binding took is TEXT on a graph screen that searches: claimed here so
                // the game never sees the key, while the character itself arrives through the
                // keyboard's text events (OnTextInput) and is searched with on the next tick.
                return TakesTypedKey(key, state);
            }

            if (_screenManager != null && _screenManager.CanHandleGlobalAction(match.Action))
            {
                // Global actions such as opening the tooltip actions menu
                // can produce new focus speech. Silence only after the
                // preflight proves the action will run, so no-op global
                // presses do not cut off the current announcement.
                SpeechPipeline.Silence();
                _screenManager.HandleGlobalAction(match.Action);

                ActiveBindingState bindingState = new ActiveBindingState(match.Action, match.Binding, match.PressedKey);
                _activeBindings[match.Binding.Id] = bindingState;
                return true;
            }

            return false;
        }

        private ActiveBindingState FindActiveBindingForKey(Key key)
        {
            foreach (ActiveBindingState state in _activeBindings.Values)
            {
                if (state.PressedKey == key)
                {
                    return state;
                }
            }

            return null;
        }

        private List<BindingMatch> ResolveClaimedMatches(KeyControl keyControl, KeyboardStateSnapshot state)
        {
            List<BindingMatch> matches = new List<BindingMatch>();
            for (int i = 0; i < AccessibilityActions.NON_GLOBAL_ACTIONS.Length; i++)
            {
                InputAction action = AccessibilityActions.NON_GLOBAL_ACTIONS[i];
                if (!CurrentScreenClaims(action))
                {
                    continue;
                }

                BindingMatch match = FindMatchingKeyboardBinding(action, keyControl, state);
                if (match != null)
                {
                    matches.Add(match);
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

        private BindingMatch ResolveGlobalMatch(KeyControl keyControl, KeyboardStateSnapshot state)
        {
            for (int i = 0; i < AccessibilityActions.GLOBAL_ACTIONS.Length; i++)
            {
                InputAction action = AccessibilityActions.GLOBAL_ACTIONS[i];
                BindingMatch match = FindMatchingKeyboardBinding(action, keyControl, state);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static BindingMatch FindMatchingKeyboardBinding(
            InputAction action,
            KeyControl keyControl,
            KeyboardStateSnapshot state)
        {
            if (action == null || action.Bindings == null)
            {
                return null;
            }

            for (int i = 0; i < action.Bindings.Count; i++)
            {
                InputBinding binding = action.Bindings[i];
                Key pressedKey;
                if (binding != null && binding.MatchesKeyDown(keyControl, state, out pressedKey))
                {
                    return new BindingMatch(action, binding, pressedKey);
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

                if (keyboard[state.PressedKey].isPressed)
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

        private static float GetReleasePollingDelaySeconds(InputBinding binding)
        {
            if (binding != null && binding.IsModified)
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
            public ActiveBindingState(InputAction action, InputBinding binding, Key pressedKey)
            {
                Action = action;
                Binding = binding;
                PressedKey = pressedKey;
                ActivatedAtSeconds = UnityEngine.Time.unscaledTime;
            }

            public InputAction Action { get; private set; }

            public InputBinding Binding { get; private set; }

            public Key PressedKey { get; private set; }

            public float ActivatedAtSeconds { get; private set; }
        }

        private sealed class BindingMatch
        {
            public BindingMatch(InputAction action, InputBinding binding, Key pressedKey)
            {
                Action = action;
                Binding = binding;
                PressedKey = pressedKey;
            }

            public InputAction Action { get; private set; }

            public InputBinding Binding { get; private set; }

            public Key PressedKey { get; private set; }
        }

        public sealed class KeyboardStateSnapshot
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
