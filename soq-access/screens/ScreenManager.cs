using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class ScreenManager
    {
        private readonly List<Screen> _stack = new List<Screen>();

        public Screen CurrentScreen
        {
            get
            {
                if (_stack.Count == 0)
                {
                    return null;
                }

                return _stack[_stack.Count - 1];
            }
        }

        public void PushScreen(Screen screen)
        {
            if (screen == null)
            {
                return;
            }

            Screen previousTop = CurrentScreen;
            previousTop?.OnUnfocus();
            UIManager.Reset();
            _stack.Add(screen);
            screen.OnPush();
            screen.OnFocus();
        }

        public bool ReplaceTopScreen(Screen replacement)
        {
            if (replacement == null || _stack.Count == 0)
            {
                return false;
            }

            Screen removed = _stack[_stack.Count - 1];
            removed.OnUnfocus();
            removed.OnPop();
            UIManager.Reset();
            _stack[_stack.Count - 1] = replacement;
            replacement.OnPush();
            replacement.OnFocus();
            return true;
        }

        public void InsertScreenBelowTop(Screen screen)
        {
            if (screen == null)
            {
                return;
            }

            if (_stack.Count == 0)
            {
                PushScreen(screen);
                return;
            }

            _stack.Insert(_stack.Count - 1, screen);
            screen.OnPush();
        }

        public bool RemoveScreenForSource(object sourceKey)
        {
            if (sourceKey == null)
            {
                return false;
            }

            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_stack[i].SourceKey, sourceKey))
                {
                    bool wasTop = i == _stack.Count - 1;
                    Screen removed = _stack[i];
                    if (wasTop)
                    {
                        removed.OnUnfocus();
                        UIManager.Reset();
                    }

                    _stack.RemoveAt(i);
                    removed.OnPop();
                    if (wasTop)
                    {
                        CurrentScreen?.OnFocus();
                    }

                    return true;
                }
            }

            return false;
        }

        public TScreen FindScreenForSource<TScreen>(object sourceKey) where TScreen : Screen
        {
            if (sourceKey == null)
            {
                return null;
            }

            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                TScreen screen = _stack[i] as TScreen;
                if (screen != null && ReferenceEquals(screen.SourceKey, sourceKey))
                {
                    return screen;
                }
            }

            return null;
        }

        public bool IsTopScreen<TScreen>() where TScreen : Screen
        {
            return CurrentScreen is TScreen;
        }

        public bool RemoveTopScreen<TScreen>() where TScreen : Screen
        {
            if (_stack.Count == 0)
            {
                return false;
            }

            int lastIndex = _stack.Count - 1;
            if (!(_stack[lastIndex] is TScreen))
            {
                return false;
            }

            Screen removed = _stack[lastIndex];
            removed.OnUnfocus();
            UIManager.Reset();
            _stack.RemoveAt(lastIndex);
            removed.OnPop();
            CurrentScreen?.OnFocus();
            return true;
        }

        public bool RemoveScreens(System.Predicate<Screen> predicate)
        {
            if (predicate == null || _stack.Count == 0)
            {
                return false;
            }

            bool removedAny = false;
            bool removedTop = false;
            Screen previousTop = CurrentScreen;
            if (previousTop != null && predicate(previousTop))
            {
                removedTop = true;
                previousTop.OnUnfocus();
                UIManager.Reset();
            }

            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                Screen screen = _stack[i];
                if (!predicate(screen))
                {
                    continue;
                }

                _stack.RemoveAt(i);
                screen.OnPop();
                removedAny = true;
            }

            if (!removedAny)
            {
                return false;
            }

            if (removedTop)
            {
                CurrentScreen?.OnFocus();
            }

            return true;
        }

        public void Clear()
        {
            if (_stack.Count == 0)
            {
                return;
            }

            _stack[_stack.Count - 1].OnUnfocus();
            UIManager.Reset();
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                _stack[i].OnPop();
            }

            _stack.Clear();
        }

        public void SynchronizeStack(IReadOnlyList<Screen> desiredStack)
        {
            if (desiredStack == null)
            {
                desiredStack = Array.Empty<Screen>();
            }

            int prefixLength = 0;
            int sharedCount = Math.Min(_stack.Count, desiredStack.Count);
            while (prefixLength < sharedCount && AreEquivalent(_stack[prefixLength], desiredStack[prefixLength]))
            {
                prefixLength++;
            }

            if (prefixLength == _stack.Count && prefixLength == desiredStack.Count)
            {
                return;
            }

            if (_stack.Count > 0)
            {
                _stack[_stack.Count - 1].OnUnfocus();
                UIManager.Reset();
            }

            for (int i = _stack.Count - 1; i >= prefixLength; i--)
            {
                Screen removed = _stack[i];
                _stack.RemoveAt(i);
                removed.OnPop();
            }

            for (int i = prefixLength; i < desiredStack.Count; i++)
            {
                Screen added = desiredStack[i];
                if (added == null)
                {
                    continue;
                }

                _stack.Add(added);
                added.OnPush();
            }

            if (_stack.Count > 0)
            {
                _stack[_stack.Count - 1].OnFocus();
            }
        }

        public bool DispatchAction(InputAction action)
        {
            if (action == null)
            {
                SoqAccessPlugin.Instance?.LogInfo("ScreenManager.DispatchAction ignored because the action was null");
                return false;
            }

            if (_stack.Count == 0)
            {
                SoqAccessPlugin.Instance?.LogInfo("ScreenManager.DispatchAction ignored because there is no active screen");
                return false;
            }

            Screen screen = CurrentScreen;
            bool handled = screen.OnActionJustPressed(action);
            SoqAccessPlugin.Instance?.LogInfo(
                "ScreenManager.DispatchAction action "
                + action.Key
                + " on top screen "
                + DescribeScreen(screen)
                + " returned "
                + handled);
            UIManager.Update();

            // Accessibility input is currently modal to the top accessibility screen.
            // Even if the screen ignores a particular action, lower accessibility screens
            // and the native UI beneath them should not receive it.
            return true;
        }

        public bool CurrentScreenClaimsAction(string actionKey)
        {
            Screen screen = CurrentScreen;
            return screen != null && screen.HasClaimed(actionKey);
        }

        private static string DescribeScreen(Screen screen)
        {
            return screen != null ? screen.GetType().Name : "<null>";
        }

        private static bool AreEquivalent(Screen current, Screen desired)
        {
            if (current == null || desired == null)
            {
                return false;
            }

            return current.GetType() == desired.GetType()
                && ReferenceEquals(current.SourceKey, desired.SourceKey);
        }
    }
}
