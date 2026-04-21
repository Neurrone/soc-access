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

            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                Screen screen = _stack[i];
                if (!screen.HasClaimed(action.Key))
                {
                    SoqAccessPlugin.Instance?.LogInfo(
                        "ScreenManager.DispatchAction action "
                        + action.Key
                        + " is not claimed by screen "
                        + DescribeScreen(screen));
                    continue;
                }

                bool handled = screen.OnActionJustPressed(action);
                SoqAccessPlugin.Instance?.LogInfo(
                    "ScreenManager.DispatchAction action "
                    + action.Key
                    + " on screen "
                    + DescribeScreen(screen)
                    + " returned "
                    + handled);

                // Once a screen claims an action, lower layers should not receive it even if
                // the currently focused widget does nothing with that action. This keeps native
                // game UI beneath the accessibility screen from responding to owned inputs.
                return true;
            }

            return false;
        }

        public void Reconcile()
        {
            Screen current = CurrentScreen;
            if (current == null)
            {
                return;
            }

            if (!current.IsPresent())
            {
                SoqAccessPlugin.Instance?.LogInfo("ScreenManager detected stale screen: " + DescribeScreen(current));
                bool removed = RemoveScreenForSource(current.SourceKey);
                if (!removed)
                {
                    RemoveTopScreenByType(current.GetType());
                }
            }
        }

        private bool RemoveTopScreenByType(Type screenType)
        {
            if (_stack.Count == 0 || screenType == null || _stack[_stack.Count - 1].GetType() != screenType)
            {
                return false;
            }

            Screen removed = _stack[_stack.Count - 1];
            removed.OnUnfocus();
            UIManager.Reset();
            _stack.RemoveAt(_stack.Count - 1);
            removed.OnPop();
            CurrentScreen?.OnFocus();
            return true;
        }

        private static string DescribeScreen(Screen screen)
        {
            return screen != null ? screen.GetType().Name : "<null>";
        }
    }
}
