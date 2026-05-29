using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class CustomCampaignSelectScreen : Screen
    {
        private readonly CustomCampaignSelectAdapter _adapter;

        public CustomCampaignSelectScreen(CustomCampaignSelectAdapter adapter)
            : base(BuildRootWidget(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            CustomCampaignSelectAdapter adapter = new CustomCampaignSelectAdapter(null);
            return adapter.IsPresent() ? new CustomCampaignSelectScreen(adapter) : null;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                return _adapter != null
                    && _adapter.BackButton != null
                    && _adapter.BackButton.Activate();
            }

            return base.OnActionJustPressed(action);
        }

        public void AnnounceStatusChanged(CustomCampaignEntry entry)
        {
            CustomCampaignEntryMenuItemWidget focused = UIManager.CurrentWidget as CustomCampaignEntryMenuItemWidget;
            if (focused == null || !ReferenceEquals(focused.Source, entry))
            {
                return;
            }

            string status = focused.GetInstallationText();
            if (string.IsNullOrWhiteSpace(status) || string.Equals(status, focused.LastSpokenInstallationText))
            {
                return;
            }

            focused.LastSpokenInstallationText = status;
            UIManager.RequestFocusSilently(focused);
            SpeechPipeline.Output(new SpeechRequest(status, interrupt: false));
        }

        private static ContainerWidget BuildRootWidget(CustomCampaignSelectAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget(
                "custom-campaign-select-screen",
                adapter != null ? adapter.GetTitle() : string.Empty);
            MenuWidget menu = new MenuWidget("custom-campaign-select-menu", adapter != null ? adapter.GetTitle() : string.Empty);
            if (adapter == null)
            {
                root.AddChild(menu);
                return root;
            }

            AddCampaignItems(menu, adapter);
            root.AddChild(menu);
            AddDownloadTip(root, adapter.DownloadTip);
            AddOptionalButton(root, "options", adapter.OptionsButton);
            AddOptionalButton(root, "back", adapter.BackButton);
            return root;
        }

        private static void AddCampaignItems(MenuWidget menu, CustomCampaignSelectAdapter adapter)
        {
            if (menu == null || adapter == null || adapter.CampaignEntries == null)
            {
                return;
            }

            for (int i = 0; i < adapter.CampaignEntries.Count; i++)
            {
                CustomCampaignEntryAdapter item = adapter.CampaignEntries[i];
                if (item == null)
                {
                    continue;
                }

                menu.AddItem(new CustomCampaignEntryMenuItemWidget(
                    "custom-campaign-" + i,
                    item));
            }
        }

        private static void AddDownloadTip(ContainerWidget root, CustomCampaignEntryAdapter tip)
        {
            if (root == null || tip == null || !tip.IsVisible())
            {
                return;
            }

            root.AddChild(new ButtonWidget(
                "find-more",
                () => BuildDownloadTipLabel(tip),
                tip.Activate,
                tip.FocusNative,
                tip.IsEnabled,
                tip.IsVisible));
        }

        private static void AddOptionalButton(ContainerWidget root, string id, IMenuButtonAdapter button)
        {
            if (root == null || button == null || !button.IsVisible())
            {
                return;
            }

            root.AddChild(new ButtonWidget(
                id,
                button.GetLabel,
                button.Activate,
                () => FocusNativeButton(button.Button),
                button.IsEnabled,
                button.IsVisible));
        }

        private static void FocusNativeButton(UIButton button)
        {
            if (button == null)
            {
                return;
            }

            NativeSelectionUtility.Select((Component)button);
        }

        private static string BuildEntryLabel(CustomCampaignEntryAdapter item)
        {
            return item != null
                ? JoinNativeLines(item.GetTitle(), item.GetDescription())
                : string.Empty;
        }

        private static string BuildDownloadTipLabel(CustomCampaignEntryAdapter item)
        {
            return item != null
                ? JoinNativeLines(item.GetTitle(), item.GetDescription(), item.GetActionText())
                : string.Empty;
        }

        private static string BuildEntryStatus(CustomCampaignEntryAdapter item)
        {
            return item != null
                ? JoinNativeLines(item.GetActionText(), item.GetInstallationText())
                : string.Empty;
        }

        private static string JoinNativeLines(params string[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return string.Empty;
            }

            System.Collections.Generic.List<string> lines = new System.Collections.Generic.List<string>();
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i] != null ? parts[i].Trim() : string.Empty;
                if (!string.IsNullOrWhiteSpace(part))
                {
                    lines.Add(part);
                }
            }

            return lines.Count == 0 ? string.Empty : string.Join("\n", lines.ToArray());
        }

        private sealed class CustomCampaignEntryMenuItemWidget : MenuItemWidget
        {
            private readonly CustomCampaignEntryAdapter _adapter;

            public CustomCampaignEntryMenuItemWidget(string id, CustomCampaignEntryAdapter adapter)
                : base(
                    id,
                    () => BuildEntryLabel(adapter),
                    () => BuildEntryStatus(adapter),
                    adapter != null ? adapter.Activate : (System.Func<bool>)null,
                    adapter != null ? adapter.FocusNative : (System.Action)null,
                    adapter != null ? adapter.IsVisible : (System.Func<bool>)null)
            {
                _adapter = adapter;
            }

            public CustomCampaignEntry Source
            {
                get { return _adapter != null ? _adapter.Source : null; }
            }

            public string LastSpokenInstallationText { get; set; }

            public string GetInstallationText()
            {
                return _adapter != null ? _adapter.GetInstallationText() ?? string.Empty : string.Empty;
            }

            public override bool ClaimsAction(string actionKey)
            {
                return IsVisible
                    && _adapter != null
                    && _adapter.IsEnabled()
                    && actionKey == AccessibilityActions.Activate.Key;
            }

            public override bool HandleAction(InputAction action)
            {
                if (action == null || action.Key != AccessibilityActions.Activate.Key)
                {
                    return false;
                }

                return _adapter != null && _adapter.IsEnabled() && _adapter.Activate();
            }
        }
    }
}
