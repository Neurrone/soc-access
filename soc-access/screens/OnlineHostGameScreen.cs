using SongsOfConquest.Client.Adventure.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    public sealed class OnlineHostGameScreen : Screen
    {
        private readonly OnlineHostGameAdapter _adapter;

        public OnlineHostGameScreen(OnlineHostGameAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            OnlineHostGameAdapter adapter = OnlineHostGameAdapter.TryCreateActive();
            return adapter != null ? new OnlineHostGameScreen(adapter) : null;
        }

        public bool Matches(GameListMenu menu)
        {
            return _adapter != null && ReferenceEquals(_adapter.SourceKey, menu);
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override bool HasClaimed(string actionKey)
        {
            return actionKey == AccessibilityActions.Cancel.Key
                || base.HasClaimed(actionKey);
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                return _adapter != null && _adapter.Cancel();
            }

            return base.OnActionJustPressed(action);
        }

        public void Refresh()
        {
            if (!IsPresent())
            {
                return;
            }

            int focusedIndex = RootWidget != null ? RootWidget.FocusedIndex : -1;
            RootWidget = BuildRoot(_adapter);
            RootWidget.SetFocusByIndexSilently(focusedIndex);
        }

        private static ContainerWidget BuildRoot(OnlineHostGameAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("online-host-game-screen", adapter != null ? adapter.Title : string.Empty);
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new TextWidget(
                "online-host-game-description",
                () => adapter.Description,
                null,
                includeParentLabelInAnnouncement: false,
                isVisible: () => adapter.HasDescription));
            root.AddChild(new TextInputWidget(
                "online-host-game-name",
                adapter.Title,
                () => adapter.InputField,
                null,
                adapter.FocusInput,
                adapter.IsInputEnabled,
                adapter.IsInputVisible,
                adapter.GetInputTooltip));
            root.AddChild(new CheckboxWidget(
                "online-host-game-invite-only",
                () => adapter.InviteOnlyLabel,
                adapter.ToggleInviteOnly,
                adapter.IsInviteOnlyChecked,
                adapter.IsInviteOnlyVisible,
                adapter.IsInviteOnlyEnabled,
                adapter.GetInviteOnlyTooltip));
            AddButton(root, "online-host-game-cancel", adapter.NegativeButton, adapter);
            AddButton(root, "online-host-game-confirm", adapter.PositiveButton, adapter);
            return root;
        }

        private static void AddButton(ContainerWidget root, string id, IMenuButtonAdapter button, OnlineHostGameAdapter adapter)
        {
            root.AddChild(new ButtonWidget(
                id,
                () => button != null ? button.GetLabel() : string.Empty,
                () => button != null && button.Activate(),
                () => NativeSelectionUtility.Select(button != null ? button.Button as UnityEngine.Component : null),
                () => button != null && button.IsEnabled(),
                () => button != null && button.IsVisible(),
                () => adapter.GetButtonTooltip(button)));
        }
    }
}
