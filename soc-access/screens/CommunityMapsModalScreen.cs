using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class CommunityMapsModalScreen : Screen
    {
        private readonly CommunityMapsModalAdapter _adapter;
        private CommunityMapsModalState _state;
        private string _signature;

        public CommunityMapsModalScreen(CommunityMapsModalAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
            _state = adapter != null ? adapter.State : CommunityMapsModalState.None;
            _signature = BuildSignature(adapter, _state);
        }

        public static Screen TryBuildActiveScreen()
        {
            CommunityMapsModalAdapter adapter = CommunityMapsModalAdapter.TryCreate();
            return adapter != null && adapter.IsPresent() ? new CommunityMapsModalScreen(adapter) : null;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public CommunityMapsModalState State
        {
            get { return _state; }
        }

        public override void OnUnfocus()
        {
            RootWidget?.Unfocus();
        }

        public override void Update()
        {
            CommunityMapsModalState state = _adapter != null ? _adapter.State : CommunityMapsModalState.None;
            if (!ShouldPollSignature(state))
            {
                base.Update();
                return;
            }

            string signature = BuildSignature(_adapter, state);
            if (state != _state || signature != _signature)
            {
                RootWidget = BuildRoot(_adapter);
                _state = state;
                _signature = signature;
                UIManager.RequestFocus(RootWidget);
                return;
            }

            base.Update();
        }

        public override bool HasClaimed(string actionKey)
        {
            return actionKey == AccessibilityActions.Cancel.Key || base.HasClaimed(actionKey);
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                if (RootWidget != null && RootWidget.HandleAction(action))
                {
                    return true;
                }

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
            _state = _adapter != null ? _adapter.State : CommunityMapsModalState.None;
            _signature = BuildSignature(_adapter, _state);
            RootWidget?.SetFocusByIndexSilently(focusedIndex);
        }

        private static string BuildSignature(CommunityMapsModalAdapter adapter, CommunityMapsModalState state)
        {
            if (adapter == null)
            {
                return state.ToString();
            }

            List<string> parts = new List<string>();
            parts.Add(state.ToString());
            parts.Add(adapter.Title);

            IReadOnlyList<CommunityMapsModalAdapter.TextItem> texts = adapter.GetTexts();
            for (int i = 0; i < texts.Count; i++)
            {
                parts.Add("text:" + texts[i].Text);
            }

            IReadOnlyList<CommunityMapsModalAdapter.InputItem> inputs = adapter.GetInputs();
            for (int i = 0; i < inputs.Count; i++)
            {
                parts.Add("input:" + inputs[i].Label);
            }

            IReadOnlyList<CommunityMapsModalAdapter.FiveDigitInputItem> fiveDigitInputs = adapter.GetFiveDigitInputs();
            for (int i = 0; i < fiveDigitInputs.Count; i++)
            {
                parts.Add("five-digit-input:" + fiveDigitInputs[i].Label);
            }

            IReadOnlyList<CommunityMapsModalAdapter.ActionItem> actions = adapter.GetActions();
            for (int i = 0; i < actions.Count; i++)
            {
                parts.Add("action:" + actions[i].Label + ":" + actions[i].IsEnabled);
            }

            return string.Join("\n", parts.ToArray());
        }

        private static bool ShouldPollSignature(CommunityMapsModalState state)
        {
            return state != CommunityMapsModalState.ConfirmUninstall;
        }

        private static ContainerWidget BuildRoot(CommunityMapsModalAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("community-maps-modal", adapter != null ? adapter.Title : string.Empty);
            if (adapter == null)
            {
                return root;
            }

            if (adapter.State == CommunityMapsModalState.ContextMenu)
            {
                root.AddChild(BuildContextMenu(adapter));
                return root;
            }

            IReadOnlyList<CommunityMapsModalAdapter.TextItem> texts = adapter.GetTexts();
            for (int i = 0; i < texts.Count; i++)
            {
                CommunityMapsModalAdapter.TextItem item = texts[i];
                if (adapter.State != CommunityMapsModalState.DownloadQueue && i == 0 && texts.Count > 1)
                {
                    continue;
                }

                CommunityMapsModalAdapter.TextItem captured = item;
                root.AddChild(new TextWidget(
                    "community-maps-modal-text-" + captured.Index,
                    () => captured.Text,
                    null,
                    includeParentLabelInAnnouncement: false,
                    isVisible: () => !string.IsNullOrWhiteSpace(captured.Text)));
            }

            IReadOnlyList<CommunityMapsModalAdapter.InputItem> inputs = adapter.GetInputs();
            for (int i = 0; i < inputs.Count; i++)
            {
                CommunityMapsModalAdapter.InputItem input = inputs[i];
                CommunityMapsModalAdapter.InputItem captured = input;
                root.AddChild(new TmpInputFieldWidget(
                    "community-maps-modal-input-" + captured.Index,
                    captured.Label,
                    () => captured.Field));
            }

            IReadOnlyList<CommunityMapsModalAdapter.FiveDigitInputItem> fiveDigitInputs = adapter.GetFiveDigitInputs();
            for (int i = 0; i < fiveDigitInputs.Count; i++)
            {
                CommunityMapsModalAdapter.FiveDigitInputItem input = fiveDigitInputs[i];
                CommunityMapsModalAdapter.FiveDigitInputItem captured = input;
                root.AddChild(new FiveDigitCodeInputWidget(
                    "community-maps-modal-five-digit-input-" + captured.Index,
                    captured.Label,
                    () => captured.Value,
                    captured.Focus,
                    captured.Activate,
                    () => captured.IsVisible));
            }

            IReadOnlyList<CommunityMapsModalAdapter.ActionItem> actions = adapter.GetActions();
            for (int i = 0; i < actions.Count; i++)
            {
                CommunityMapsModalAdapter.ActionItem action = actions[i];
                CommunityMapsModalAdapter.ActionItem captured = action;
                root.AddChild(new ButtonWidget(
                    "community-maps-modal-action-" + captured.Index,
                    () => captured.Label,
                    captured.Activate,
                    captured.Focus,
                    () => captured.IsEnabled,
                    () => true));
            }

            if (adapter.State == CommunityMapsModalState.DownloadQueue)
            {
                root.AddChild(new ButtonWidget(
                    "community-maps-modal-downloads-back",
                    ModText.Get(ModStrings.Screens.Back),
                    adapter.Cancel,
                    null,
                    () => true));
            }

            return root;
        }

        private static MenuWidget BuildContextMenu(CommunityMapsModalAdapter adapter)
        {
            MenuWidget menu = new MenuWidget(
                "community-maps-context-menu",
                adapter != null ? adapter.Title : string.Empty);
            IReadOnlyList<CommunityMapsModalAdapter.ActionItem> actions = adapter.GetActions();
            for (int i = 0; i < actions.Count; i++)
            {
                CommunityMapsModalAdapter.ActionItem action = actions[i];
                CommunityMapsModalAdapter.ActionItem captured = action;
                menu.AddItem(new MenuItemWidget(
                    "community-maps-context-menu-action-" + captured.Index,
                    () => captured.Label,
                    null,
                    captured.Activate,
                    captured.Focus,
                    () => captured.IsEnabled));
            }

            return menu;
        }
    }
}
