using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Buffers;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// Decides which screen the player is on, every frame, by asking rather than by being told.
    ///
    /// Each tick re-evaluates <see cref="Screen.IsActive"/> across every registered screen, sorts the
    /// survivors by layer, and diffs that list against the stack from last frame. Nothing subscribes
    /// to the game's window events, so there is no way for the mod to end up believing in a screen the
    /// game has closed - a whole class of "the mod is stuck on the previous page" bugs that a
    /// push/pop model has to defend against and this one cannot have.
    ///
    /// The cost is one cheap predicate per screen per frame, which is why IsActive must stay cheap:
    /// never a scene scan, never a subtree walk (AGENTS.md, Performance).
    ///
    /// A screen that throws is treated as inactive and logged ONCE, so one broken page cannot take the
    /// navigation layer down with it and cannot fill the log either.
    /// </summary>
    public sealed class ScreenManager
    {
        private readonly List<Screen> _registered = new List<Screen>();
        private readonly Dictionary<Screen, string> _failures = new Dictionary<Screen, string>();
        private readonly GraphNavigator _navigator;
        private readonly ReviewBufferManager _reviewBuffers;
        private readonly ReviewBufferController _reviewBufferController;
        // The polled stack and the buffers Tick reuses for it. Resolve runs once a frame over some
        // sixty screens and used to allocate a list for the answer and diff it against the old one
        // with List.Contains, which is a walk per screen per side; the two lists are swapped instead
        // and the two sets answer the diff's questions in one hop. _stacked always mirrors _stack.
        private List<Screen> _stack = new List<Screen>();
        private List<Screen> _spare = new List<Screen>();
        private HashSet<Screen> _stacked = new HashSet<Screen>();
        private HashSet<Screen> _wanted = new HashSet<Screen>();
        private Screen _focused;

        public ScreenManager(
            GraphNavigator navigator,
            ReviewBufferManager reviewBuffers,
            ReviewBufferController reviewBufferController)
        {
            _navigator = navigator;
            _reviewBuffers = reviewBuffers;
            _reviewBufferController = reviewBufferController;
            ApplyVisibleReviewBuffers();
        }

        /// <summary>The screen the player is on, or null when none of ours is showing: the top of the
        /// polled stack, or whatever that screen has opened over itself.</summary>
        public Screen Current
        {
            get { return _stack.Count > 0 ? _stack[_stack.Count - 1].Deepest() : null; }
        }

        /// <summary>The polled stack, bottom first. Children are not in it; they hang off the screen
        /// that opened them.</summary>
        public IReadOnlyList<Screen> Stack
        {
            get { return _stack; }
        }

        /// <summary>Every screen the mod knows about, active or not, in registration order.</summary>
        public IReadOnlyList<Screen> RegisteredScreens
        {
            get { return _registered; }
        }

        public void Register(Screen screen)
        {
            if (screen != null && !_registered.Contains(screen))
            {
                screen.Manager = this;
                _registered.Add(screen);
            }
        }

        /// <summary>The registered screen with this key, or null. Case-insensitive: the keys are
        /// typed by hand into dev-server requests.</summary>
        public Screen Find(string key)
        {
            for (int i = 0; i < _registered.Count; i++)
            {
                if (string.Compare(_registered[i].Key, key, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return _registered[i];
                }
            }

            return null;
        }

        /// <summary>The one registered instance of a screen type, whether or not it is showing - what
        /// the detector writes a slot on.</summary>
        [HookWritable]
        public TScreen Registered<TScreen>() where TScreen : Screen
        {
            for (int i = 0; i < _registered.Count; i++)
            {
                TScreen screen = _registered[i] as TScreen;
                if (screen != null)
                {
                    return screen;
                }
            }

            return null;
        }

        /// <summary>An ACTIVE screen of this type, top of the stack first, or null.</summary>
        public TScreen Get<TScreen>() where TScreen : Screen
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                TScreen screen = _stack[i] as TScreen;
                if (screen != null)
                {
                    return screen;
                }
            }

            return null;
        }

        public bool Contains<TScreen>() where TScreen : Screen
        {
            return Get<TScreen>() != null;
        }

        /// <summary>Why a screen's <see cref="Screen.IsActive"/> last threw, or null - reported by
        /// <c>GET /screens</c>.</summary>
        public string LastFailure(Screen screen)
        {
            string message;
            return screen != null && _failures.TryGetValue(screen, out message) ? message : null;
        }

        /// <summary>Whether the player is on this screen right now.</summary>
        public bool IsFocused(Screen screen)
        {
            return screen != null && ReferenceEquals(screen, _focused);
        }

        /// <summary>Whether this screen is on the polled stack, or is a child hanging off it.</summary>
        public bool IsOnStack(Screen screen)
        {
            if (screen == null)
            {
                return false;
            }

            for (Screen at = screen; at != null; at = at.ParentScreen)
            {
                if (_stacked.Contains(at))
                {
                    return true;
                }
            }

            return false;
        }

        public void Tick()
        {
            ApplyDiff(Resolve(_spare, _wanted));
            SyncFocus();

            Screen current = Current;
            if (current != null)
            {
                Safe(current.OnUpdate, current, "OnUpdate");
            }

            // OnUpdate may have changed what is showing (a child closing itself); re-syncing is free
            // when nothing moved.
            SyncFocus();
            GraphNavigator navigator = _navigator;
            if (navigator != null)
            {
                navigator.Update();
            }
        }

        /// <summary>Drop every screen as though the game had closed them all - the mod is going away.
        /// </summary>
        public void Shutdown()
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                Pop(_stack[i]);
            }

            _stack.Clear();
            _stacked.Clear();
            _focused = null;
            _failures.Clear();
            GraphNavigator navigator = _navigator;
            if (navigator != null)
            {
                navigator.Attach(null);
            }

            // Each screen is handed back its half of the registration: one kept alive by anything else
            // would otherwise hold this manager, and through it the whole tree, after the mod has gone.
            for (int i = 0; i < _registered.Count; i++)
            {
                if (_registered[i] != null)
                {
                    _registered[i].Manager = null;
                }
            }

            _registered.Clear();
            ApplyVisibleReviewBuffers();
        }

        /// <summary>A child screen has closed: drop its cursor, so opening the same menu again starts
        /// at the top rather than where the player left it last time.</summary>
        public void ChildClosed(Screen screen)
        {
            GraphNavigator navigator = _navigator;
            GraphScreen graph = screen as GraphScreen;
            if (navigator != null && graph != null && !screen.KeepStateOnPop)
            {
                navigator.ScreenClosed(graph);
            }

            ApplyVisibleReviewBuffers();
        }

        // Active screens, bottom layer first. Insertion-sorted rather than List.Sort, which is not
        // stable: two screens on the same layer must stay in registration order, which is how combat
        // sits above the map.
        private List<Screen> Resolve(List<Screen> active, HashSet<Screen> into)
        {
            active.Clear();
            into.Clear();
            for (int i = 0; i < _registered.Count; i++)
            {
                Screen screen = _registered[i];
                // A screen opened as a CHILD is its parent's, not the poll's: it is reached through
                // Deepest and must not also stand on the stack in its own right.
                if (screen.ParentScreen != null || !IsActive(screen))
                {
                    continue;
                }

                int at = active.Count;
                while (at > 0 && active[at - 1].Layer > screen.Layer)
                {
                    at--;
                }

                active.Insert(at, screen);
                into.Add(screen);
            }

            return active;
        }

        private void ApplyDiff(List<Screen> desired)
        {
            bool changed = false;

            // Closures first, from the top down, then openings from the bottom up, so a screen that
            // replaced another hears about it in the order the player experienced it.
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (!_wanted.Contains(_stack[i]))
                {
                    Pop(_stack[i]);
                    changed = true;
                }
            }

            for (int i = 0; i < desired.Count; i++)
            {
                if (!_stacked.Contains(desired[i]))
                {
                    Safe(desired[i].OnPush, desired[i], "OnPush");
                    changed = true;
                }
            }

            // The lists and their sets change places; what the stack was becomes next frame's
            // scratch, so neither is allocated again.
            _spare = _stack;
            _stack = desired;
            HashSet<Screen> stacked = _stacked;
            _stacked = _wanted;
            _wanted = stacked;
            if (changed)
            {
                ApplyVisibleReviewBuffers();
            }
        }

        // A screen leaving takes whatever it had open with it: the game closed the page, so a menu
        // over it is gone too, and it hears about that before the page does.
        private void Pop(Screen screen)
        {
            if (screen.ActiveChild != null)
            {
                screen.RemoveChild(screen.ActiveChild);
            }

            Safe(screen.OnPop, screen, "OnPop");
            GraphNavigator navigator = _navigator;
            GraphScreen graph = screen as GraphScreen;
            if (navigator != null && graph != null && !screen.KeepStateOnPop)
            {
                navigator.ScreenClosed(graph);
            }
        }

        // The one place focus changes hands, so a screen opening, closing or being covered all
        // announce identically.
        private void SyncFocus()
        {
            Screen current = Current;
            if (ReferenceEquals(current, _focused))
            {
                return;
            }

            if (_focused != null)
            {
                Safe(_focused.OnUnfocus, _focused, "OnUnfocus");
            }

            _focused = current;
            if (current != null)
            {
                Safe(current.OnFocus, current, "OnFocus");
            }

            GraphNavigator navigator = _navigator;
            if (navigator != null)
            {
                navigator.Attach(current as GraphScreen);
            }

            GraphScreen graph = current as GraphScreen;
            if (graph != null)
            {
                // Queued, not interrupting: the focused control's readout follows it.
                Safe(graph.SayName, graph, "SayName");
            }
        }

        private bool IsActive(Screen screen)
        {
            try
            {
                bool active = screen.IsActive();
                if (_failures.Count > 0 && _failures.Remove(screen))
                {
                    SocAccessMod.Instance?.LogInfo("ScreenManager " + screen.Key + ".IsActive answered again");
                }

                return active;
            }
            catch (Exception exception)
            {
                // Once per screen, not once per frame: the poll runs every frame and a broken
                // predicate would otherwise be the whole log.
                if (!_failures.ContainsKey(screen))
                {
                    _failures[screen] = exception.Message;
                    SocAccessMod.Instance?.LogWarning("ScreenManager " + screen.Key + ".IsActive threw: " + exception);
                }

                return false;
            }
        }

        private static void Safe(Action action, Screen screen, string what)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("ScreenManager " + screen.Key + "." + what + " threw: " + exception);
            }
        }

        public bool DispatchAction(InputAction action)
        {
            if (action == null)
            {
                SocAccessMod.Instance?.LogInfo("ScreenManager.DispatchAction ignored because the action was null");
                return false;
            }

            Screen screen = Current;
            if (screen == null)
            {
                SocAccessMod.Instance?.LogInfo("ScreenManager.DispatchAction ignored because there is no active screen");
                return false;
            }

            bool handled = screen.OnActionJustPressed(action);
            SocAccessMod.Instance?.LogInfo(
                "ScreenManager.DispatchAction action "
                + action.Key
                + " on screen "
                + screen.Key
                + " returned "
                + handled);

            // Accessibility actions are offered only to the focused screen. Claiming happens before
            // dispatch in the input router; if a screen claims an action, the router owns that input
            // event even when this method returns false. The return value only reports whether the
            // screen performed an action.
            return handled;
        }

        public bool HandleGlobalAction(InputAction action)
        {
            if (!CanHandleGlobalAction(action))
            {
                return false;
            }

            if (action.Key == AccessibilityActions.SummarizeResources.Key)
            {
                CombatScreen combatScreen = Get<CombatScreen>();
                if (combatScreen != null)
                {
                    return combatScreen.SummarizeResources();
                }

                AdventureMapScreen adventureMapScreen = Get<AdventureMapScreen>();
                return adventureMapScreen != null && adventureMapScreen.SummarizeResources();
            }

            if (action.Key == AccessibilityActions.SummarizeEnemyResources.Key)
            {
                CombatScreen combatScreen = Get<CombatScreen>();
                return combatScreen != null && combatScreen.SummarizeEnemyResources();
            }

            if (_reviewBufferController != null)
            {
                if (action.Key == AccessibilityActions.PreviousBuffer.Key)
                {
                    _reviewBufferController.PreviousBuffer();
                    return true;
                }

                if (action.Key == AccessibilityActions.NextBuffer.Key)
                {
                    _reviewBufferController.NextBuffer();
                    return true;
                }

                if (action.Key == AccessibilityActions.PreviousBufferLine.Key)
                {
                    _reviewBufferController.PreviousBufferLine();
                    return true;
                }

                if (action.Key == AccessibilityActions.NextBufferLine.Key)
                {
                    _reviewBufferController.NextBufferLine();
                    return true;
                }

                if (action.Key == AccessibilityActions.FirstBufferLine.Key)
                {
                    _reviewBufferController.FirstBufferLine();
                    return true;
                }

                if (action.Key == AccessibilityActions.LastBufferLine.Key)
                {
                    _reviewBufferController.LastBufferLine();
                    return true;
                }
            }

            return false;
        }

        public bool CanHandleGlobalAction(InputAction action)
        {
            if (!AccessibilityActions.IsGlobalAction(action))
            {
                return false;
            }

            if (action.Key == AccessibilityActions.SummarizeResources.Key)
            {
                return Get<CombatScreen>() != null || Get<AdventureMapScreen>() != null;
            }

            if (action.Key == AccessibilityActions.SummarizeEnemyResources.Key)
            {
                return Get<CombatScreen>() != null;
            }

            if (IsReviewBufferAction(action))
            {
                return _reviewBufferController != null;
            }

            return false;
        }

        public HashSet<ReviewBufferKind> GetVisibleReviewBuffers()
        {
            HashSet<ReviewBufferKind> result = new HashSet<ReviewBufferKind>();
            for (int i = 0; i < _stack.Count; i++)
            {
                for (Screen screen = _stack[i]; screen != null; screen = screen.ActiveChild)
                {
                    if (screen.VisibleReviewBuffers == null)
                    {
                        continue;
                    }

                    foreach (ReviewBufferKind kind in screen.VisibleReviewBuffers)
                    {
                        result.Add(kind);
                    }
                }
            }

            return result;
        }

        public bool CurrentScreenClaimsAction(InputAction action)
        {
            Screen screen = Current;
            return screen != null && action != null && screen.HasClaimed(action.Key);
        }

        private void ApplyVisibleReviewBuffers()
        {
            if (_reviewBuffers != null)
            {
                _reviewBuffers.SetVisibleBuffers(GetVisibleReviewBuffers());
            }
        }

        private static bool IsReviewBufferAction(InputAction action)
        {
            if (action == null)
            {
                return false;
            }

            return action.Key == AccessibilityActions.PreviousBuffer.Key
                || action.Key == AccessibilityActions.NextBuffer.Key
                || action.Key == AccessibilityActions.PreviousBufferLine.Key
                || action.Key == AccessibilityActions.NextBufferLine.Key
                || action.Key == AccessibilityActions.FirstBufferLine.Key
                || action.Key == AccessibilityActions.LastBufferLine.Key;
        }
    }
}
