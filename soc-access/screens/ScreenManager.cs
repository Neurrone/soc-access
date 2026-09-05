using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Buffers;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class ScreenManager
    {
        private readonly List<Screen> _stack = new List<Screen>();
        private readonly ReviewBufferManager _reviewBuffers;
        private readonly ReviewBufferController _reviewBufferController;

        public ScreenManager(ReviewBufferManager reviewBuffers, ReviewBufferController reviewBufferController)
        {
            _reviewBuffers = reviewBuffers;
            _reviewBufferController = reviewBufferController;
            ApplyVisibleReviewBuffers();
        }

        /// <summary>The stack bottom first, read-only, for the dev server's dump header.</summary>
        internal IReadOnlyList<Screen> Stack
        {
            get { return _stack; }
        }

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
                SocAccessMod.Instance?.LogWarning(
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
            ApplyVisibleReviewBuffers();
            screen.OnFocus();
        }

        public bool RefreshTop<TScreen>(Screen replacement, string reason) where TScreen : Screen
        {
            if (replacement == null)
            {
                SocAccessMod.Instance?.LogWarning("ScreenManager.RefreshTop ignored " + reason + " because replacement was null");
                return false;
            }

            if (!replacement.IsPresent())
            {
                SocAccessMod.Instance?.LogWarning(
                    "ScreenManager.RefreshTop ignored "
                    + reason
                    + " because "
                    + DescribeScreen(replacement)
                    + " is not present");
                return false;
            }

            if (_stack.Count == 0 || !(_stack[_stack.Count - 1] is TScreen))
            {
                SocAccessMod.Instance?.LogWarning(
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
            ApplyVisibleReviewBuffers();
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
            ApplyVisibleReviewBuffers();
        }

        public void PushBottom(Screen screen, string reason)
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

            // Base screens can be restored below native follow-up overlays such
            // as claim or story menus. Do not disturb the focused top screen.
            _stack.Insert(0, screen);
            screen.OnPush();
            ApplyVisibleReviewBuffers();
        }

        public bool Pop<TScreen>(string reason) where TScreen : Screen
        {
            if (_stack.Count == 0)
            {
                SocAccessMod.Instance?.LogWarning(
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
                SocAccessMod.Instance?.LogWarning(
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
            ApplyVisibleReviewBuffers();
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
                ApplyVisibleReviewBuffers();

                if (wasTop)
                {
                    CurrentScreen?.OnFocus();
                }

                return true;
            }

            SocAccessMod.Instance?.LogWarning(
                "ScreenManager.Remove ignored "
                + reason
                + "; no "
                + typeof(TScreen).Name
                + " found in stack");
            return false;
        }

        public bool Contains<TScreen>() where TScreen : Screen
        {
            return Get<TScreen>() != null;
        }

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
            ApplyVisibleReviewBuffers();
        }

        public void Update()
        {
            CurrentScreen?.Update();
        }

        public bool DispatchAction(InputAction action)
        {
            if (action == null)
            {
                SocAccessMod.Instance?.LogInfo("ScreenManager.DispatchAction ignored because the action was null");
                return false;
            }

            if (_stack.Count == 0)
            {
                SocAccessMod.Instance?.LogInfo("ScreenManager.DispatchAction ignored because there is no active screen");
                return false;
            }

            Screen screen = CurrentScreen;
            bool handled = screen.OnActionJustPressed(action);
            SocAccessMod.Instance?.LogInfo(
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

            if (action.Key == AccessibilityActions.OpenModSettings.Key)
            {
                Push(new ModSettingsScreen(() => Pop<ModSettingsScreen>("mod settings closed")), "mod settings opened");
                return true;
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

            if (action.Key == AccessibilityActions.OpenModSettings.Key)
            {
                return !(CurrentScreen is ModSettingsScreen);
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
                Screen screen = _stack[i];
                if (screen == null || screen.VisibleReviewBuffers == null)
                {
                    continue;
                }

                foreach (ReviewBufferKind kind in screen.VisibleReviewBuffers)
                {
                    result.Add(kind);
                }
            }

            return result;
        }

        public bool CurrentScreenClaimsAction(InputAction action)
        {
            Screen screen = CurrentScreen;
            if (screen == null || action == null)
            {
                return false;
            }

            return action.ClaimScope == InputClaimScope.Screen
                ? screen.HasClaimed(action.Key)
                : screen.HasFocusedWidgetClaimed(action.Key);
        }

        private static string DescribeScreen(Screen screen)
        {
            return screen != null ? screen.GetType().Name : "<null>";
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
