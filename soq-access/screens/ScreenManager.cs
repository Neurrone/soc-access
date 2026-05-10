using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
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

        public void Push(Screen screen, string reason)
        {
            if (screen == null)
            {
                return;
            }

            if (CurrentScreen != null && CurrentScreen.GetType() == screen.GetType())
            {
                SoqAccessPlugin.Instance?.LogWarning(
                    "ScreenManager.Push received duplicate top screen "
                    + DescribeScreen(screen)
                    + " for "
                    + reason);
            }

            Screen previousTop = CurrentScreen;
            previousTop?.OnUnfocus();
            UIManager.Reset();
            _stack.Add(screen);
            screen.OnPush();
            screen.OnFocus();
        }

        public bool RefreshTop<TScreen>(Screen replacement, string reason) where TScreen : Screen
        {
            if (replacement == null)
            {
                SoqAccessPlugin.Instance?.LogWarning("ScreenManager.RefreshTop ignored " + reason + " because replacement was null");
                return false;
            }

            if (!replacement.IsPresent())
            {
                SoqAccessPlugin.Instance?.LogWarning(
                    "ScreenManager.RefreshTop ignored "
                    + reason
                    + " because "
                    + DescribeScreen(replacement)
                    + " is not present");
                return false;
            }

            if (_stack.Count == 0 || !(_stack[_stack.Count - 1] is TScreen))
            {
                SoqAccessPlugin.Instance?.LogWarning(
                    "ScreenManager.RefreshTop ignored "
                    + reason
                    + "; expected top "
                    + typeof(TScreen).Name
                    + " but current top is "
                    + DescribeScreen(CurrentScreen));
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

        public void PushBelowTop(Screen screen, string reason)
        {
            if (screen == null)
            {
                return;
            }

            if (_stack.Count == 0)
            {
                Push(screen, reason);
                return;
            }

            _stack.Insert(_stack.Count - 1, screen);
            screen.OnPush();
        }

        public bool Pop<TScreen>(string reason) where TScreen : Screen
        {
            if (_stack.Count == 0)
            {
                SoqAccessPlugin.Instance?.LogWarning(
                    "ScreenManager.Pop ignored "
                    + reason
                    + "; expected top "
                    + typeof(TScreen).Name
                    + " but stack is empty");
                return false;
            }

            int lastIndex = _stack.Count - 1;
            if (!(_stack[lastIndex] is TScreen))
            {
                SoqAccessPlugin.Instance?.LogWarning(
                    "ScreenManager.Pop ignored "
                    + reason
                    + "; expected top "
                    + typeof(TScreen).Name
                    + " but current top is "
                    + DescribeScreen(CurrentScreen));
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

        public bool Remove<TScreen>(string reason) where TScreen : Screen
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (!(_stack[i] is TScreen))
                {
                    continue;
                }

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

            SoqAccessPlugin.Instance?.LogWarning(
                "ScreenManager.Remove ignored "
                + reason
                + "; no "
                + typeof(TScreen).Name
                + " found in stack");
            return false;
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

            // Accessibility actions are offered only to the top accessibility
            // screen. Claiming happens before dispatch in the input router; if a
            // screen claims an action, the router owns that input event even when
            // this method returns false. The return value only reports whether
            // the screen performed an action.
            return handled;
        }

        public bool HandleGlobalAction(InputAction action)
        {
            if (!CanHandleGlobalAction(action))
            {
                return false;
            }

            if (action.Key == AccessibilityActions.TooltipActionsMenu.Key)
            {
                Tooltip tooltip = UIManager.CurrentWidget.GetTooltip();
                Push(new TooltipActionsMenuScreen(tooltip.Actions, () => Pop<TooltipActionsMenuScreen>("tooltip actions menu closed")), "tooltip actions menu opened");
                return true;
            }

            return false;
        }

        public bool CanHandleGlobalAction(InputAction action)
        {
            if (!AccessibilityActions.IsGlobalAction(action))
            {
                return false;
            }

            if (action.Key == AccessibilityActions.TooltipActionsMenu.Key)
            {
                if (CurrentScreen is TooltipActionsMenuScreen)
                {
                    return false;
                }

                Tooltip tooltip = UIManager.CurrentWidget != null ? UIManager.CurrentWidget.GetTooltip() : null;
                return tooltip != null
                    && tooltip.Actions != null
                    && tooltip.Actions.Count > 0;
            }

            return false;
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
    }
}
