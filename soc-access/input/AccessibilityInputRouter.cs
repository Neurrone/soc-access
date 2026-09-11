using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.Screens;
using SongsOfConquestAccess.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace SongsOfConquestAccess.Input
{
    public sealed class AccessibilityInputRouter : IDisposable, IObserver<InputEventPtr>
    {
        private readonly ScreenManager _screenManager;
        private readonly Dictionary<string, ActiveBindingState> _activeBindings =
            new Dictionary<string, ActiveBindingState>();
        private readonly Queue<Injection> _injections = new Queue<Injection>();
        private IDisposable _rawInputSubscription;

        public AccessibilityInputRouter(ScreenManager screenManager)
        {
            _screenManager = screenManager;
            _rawInputSubscription = InputSystem.onEvent.Subscribe(this);
            UnityEngine.Application.focusChanged += OnFocusChanged;
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

            UnityEngine.Application.focusChanged -= OnFocusChanged;
            if (_textKeyboard != null)
            {
                _textKeyboard.onTextInput -= OnTextInput;
                _textKeyboard = null;
            }

            _activeBindings.Clear();
            _typed.Length = 0;
        }

        // A key released while the window had no focus sends no event this side ever sees, so the
        // held-key table is emptied when focus goes: a stale entry would hide that key's next press.
        private void OnFocusChanged(bool focused)
        {
            if (!focused)
            {
                _activeBindings.Clear();
            }
        }

        // ---- typed text, for the graph screens' type-ahead search ----
        //
        // A character is not a chord: it comes from the keyboard's own text events (layout, dead
        // keys and shift all resolved by the engine), never from mapping key codes to letters. The
        // characters are queued here and taken once a frame by the navigator (TakeTypedCharacters),
        // so a letter and the control it lands on are the same frame's work.

        private readonly System.Text.StringBuilder _typed = new System.Text.StringBuilder();
        private Keyboard _textKeyboard;
        private Screen _screenTyped;

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
            if (StandingDown())
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

            // The characters were queued under whatever screen was focused when they were typed
            // (_screenTyped, set the last time ForgetTypedAcrossScreens ran). The letter that OPENS a
            // page is delivered while the old screen is still up, then the game makes the new page
            // current in the same frame - before ForgetTypedAcrossScreens gets to clear the queue -
            // and the new page's type-ahead tick would otherwise search for the letter that summoned
            // it (c opens the wielder sheet, v the spellbook). If the focused screen has moved on
            // since these were typed, they are not this screen's to search with.
            if (!ReferenceEquals(_screenManager == null ? null : _screenManager.Current, _screenTyped))
            {
                _typed.Length = 0;
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

        // The stand-down applies wherever the game's own text field holds the keyboard, or the game's
        // key-binding capture is listening for the next key, whatever is on the screen stack: while
        // it does, every key is the game's and the mod's layer goes quiet rather than picking which
        // keys to leave alone. One line covers the raw-key path, the typed-character path and
        // injections, so during a capture the mod answers nothing and arrows, Escape and letters all
        // reach the game to be bound.
        private bool StandingDown()
        {
            return GameTextFocus.IsTyping() || KeyCaptureFocus.IsCapturing();
        }

        /// <summary>
        /// The graph screen whose search may hear the keyboard - null while one is arriving.
        ///
        /// The key that OPENS a page is not typing INTO it. The game acts on the press (C opens the
        /// wielder sheet, V the spellbook) while the same press's character event is still in the
        /// input queue, so by the time the character is delivered the page it opened is already the
        /// focused screen and its type-ahead would search for the letter that summoned it. The
        /// navigator's first Update on a screen is the line between the two: everything before it
        /// belongs to the press that opened the page, and is dropped.
        /// </summary>
        private GraphScreen TypingScreen()
        {
            GraphScreen screen = _screenManager == null ? null : _screenManager.Current as GraphScreen;
            GraphNavigator navigator = screen == null ? null : screen.Navigator;
            return navigator != null && navigator.HasTicked(screen) ? screen : null;
        }

        // Characters queued for the page that had the keyboard are not the next page's: the focused
        // screen can change between two frames with no key of the mod's at all.
        private void ForgetTypedAcrossScreens()
        {
            Screen current = _screenManager == null ? null : _screenManager.Current;
            if (ReferenceEquals(current, _screenTyped))
            {
                return;
            }

            _screenTyped = current;
            _typed.Length = 0;
        }

        public void Update()
        {
            FollowKeyboard();
            ForgetTypedAcrossScreens();
            DrainInjections();
        }

        /// <summary>
        /// Queue an action to be run as though its key had been pressed, and hand back the ticket
        /// the caller waits on. Enqueued rather than run here because the dev server's HTTP thread
        /// is not the Unity main thread, and drained at the top of <see cref="Update"/> so it lands
        /// at the same point in the frame a real key press does - the same claim check, the same
        /// silence, the same dispatch.
        ///
        /// It deliberately does not touch <see cref="_activeBindings"/>: no physical key is down,
        /// so there is no release to wait for.
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
                if (_screenManager == null || _screenManager.Current == null)
                {
                    injection.Outcome = "no screen";
                    return;
                }

                // The same stand-down a physical key meets, so the dev server sees what the player
                // would: while the game's field has the keyboard, the mod answers nothing.
                if (StandingDown())
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

            Keyboard keyboard = InputSystem.GetDeviceById(value.deviceId) as Keyboard;
            if (keyboard == null)
            {
                return;
            }

            HideHeldKeys(keyboard, value);

            foreach (InputControl control in InputControlExtensions.EnumerateChangedControls(value, keyboard, 0f))
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
                if (pressed && TryHandleKeyDown(keyControl))
                {
                    Hide(keyControl, value);
                }
            }
        }

        // HOW A CONSUMED KEY IS KEPT FROM THE GAME, and the bug that lived here for a long time.
        //
        // A key the mod takes must not reach the game. This used to be done by marking the whole
        // event handled, which makes the input system skip it - and skipping it means the keyboard
        // device never records the key as down. Everything that followed came from that one gap:
        //
        // - Keyboard.current[key].isPressed stayed false for a consumed key, so the release wait
        //   that keyed on it could only be a timer (50 ms plain, 100 ms with a modifier), tuned by
        //   several attempts against the symptoms below and never long enough.
        // - The key's release changed nothing against the device's stale state, so no release was
        //   ever observed here, and the comment of the day blamed the input system for that.
        // - Every later event while the key was physically held - the Shift going up a moment
        //   before the Tab under it, another key rolled over, the OS auto-repeat - carried the key
        //   as down against a device that still said up, so it enumerated as a NEW press. Inside
        //   the timer it was swallowed; after it, Shift+Tab fired twice, or Tab fired after
        //   Shift+Tab. Marking those events handled too hid the Shift release from the game and
        //   from the device, which is where the "modifier state can change while the primary key
        //   is held" note came from.
        //
        // Now only the consumed key's own bit is cleared in the event (WriteValueIntoEvent) and the
        // event is left to be applied: the game never sees the key, every other key in the same
        // event reaches the device as it should, and the device agrees with what the mod let
        // through. While the key stays physically held, each event still carries its true state,
        // so the held-key table reads it off the event itself: down means "hide it again", up means
        // released. No timer, no isPressed, no phantom presses.
        //
        // Input System 1.7.0's own frame-state bugs (fixed in 1.9.0) are real but were not what
        // this was:
        // https://docs.unity.cn/Packages/com.unity.inputsystem%401.10/changelog/CHANGELOG.html#190---2024-07-15
        // https://discussions.unity.com/t/keyboard-current-temporarily-stops-registering-ispressed-or-waspressedthisframe-after-scene-load/1496259
        // https://discussions.unity.com/t/keyboard-current-key-waspressedthisframe-fires-multiple-times-before-key-is-released/886444
        private void HideHeldKeys(Keyboard keyboard, InputEventPtr value)
        {
            if (_activeBindings.Count == 0)
            {
                return;
            }

            List<string> released = null;
            foreach (KeyValuePair<string, ActiveBindingState> item in _activeBindings)
            {
                KeyControl keyControl = keyboard[item.Value.PressedKey];
                float rawValue;
                if (keyControl == null || !InputControlExtensions.ReadValueFromEvent(keyControl, value, out rawValue))
                {
                    // A delta event that does not carry this key says nothing about it.
                    continue;
                }

                if (rawValue >= keyControl.pressPointOrDefault)
                {
                    Hide(keyControl, value);
                }
                else
                {
                    if (released == null)
                    {
                        released = new List<string>();
                    }

                    released.Add(item.Key);
                }
            }

            if (released != null)
            {
                for (int i = 0; i < released.Count; i++)
                {
                    _activeBindings.Remove(released[i]);
                }
            }
        }

        // Clear the key's bit in the event, so the device and the game see it as up.
        private static void Hide(KeyControl keyControl, InputEventPtr value)
        {
            InputControlExtensions.WriteValueIntoEvent(keyControl, 0f, value);
        }

        private bool TryHandleKeyDown(KeyControl keyControl)
        {
            // THE STAND-DOWN, before any claim is even asked. A game text field with the keyboard owns
            // every key while it has it - the letters, the arrows walking the caret, the Backspace -
            // so the whole layer goes quiet rather than picking which keys to leave alone.
            if (StandingDown())
            {
                return false;
            }

            // THE MOD'S OWN CAPTURE grabs the next key while it is armed - the reverse of the
            // stand-down above. A pure modifier keydown is not a binding, so the capture waits; the
            // first real key becomes the gesture's chord, with no cancel, matching the game's own
            // rebind. No text field is up to catch these keys, which is why the router has to.
            //
            // The modifier press is NOT marked handled: a handled event is one the input system
            // skips, so its key state never updates and Ctrl+R would arrive as a bare R when the
            // modifier flags are read off the keyboard below.
            if (ModKeyCapture.IsArmed)
            {
                Key captureKey = keyControl.keyCode;
                if (IsModifierKey(captureKey))
                {
                    return false;
                }

                // The character the key printed rides along: a chord captured on a key Unity
                // calls OEM1 but the keyboard prints as a backslash should still fire on a keyboard
                // where that character sits on the Backslash key (see ModSettings.ApplyKeybindOverride).
                KeyboardStateSnapshot captureState = KeyboardStateSnapshot.Capture();
                ModKeyCapture.Complete(new KeyboardBinding(
                    captureKey, captureState.Ctrl, captureState.Shift, captureState.Alt, keyControl.displayName));
                return true;
            }

            Key key = keyControl.keyCode;
            ActiveBindingState activeForKey = FindActiveBindingForKey(key);
            if (activeForKey != null)
            {
                // Still held since the press that was consumed (HideHeldKeys clears its bit before
                // the enumeration, so this is only reached for a key the same event both hid and
                // reported); one press is one dispatch until the key is seen up.
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

        // A modifier pressed on its own is not a binding: the mod's capture lets it through and waits
        // for the key it modifies. AltGr and the platform (Windows/Command) keys are included so a chord
        // that leans on them is never mistaken for a bare modifier press.
        private static bool IsModifierKey(Key key)
        {
            switch (key)
            {
                case Key.LeftShift:
                case Key.RightShift:
                case Key.LeftCtrl:
                case Key.RightCtrl:
                case Key.LeftAlt:
                case Key.RightAlt:
                case Key.LeftMeta:
                case Key.RightMeta:
                case Key.None:
                    return true;
                default:
                    return false;
            }
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
            }

            public InputAction Action { get; private set; }

            public InputBinding Binding { get; private set; }

            public Key PressedKey { get; private set; }
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
