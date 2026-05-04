using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class CampaignMenuScreen : Screen
    {
        private readonly CampaignMenuAdapter _adapter;

        public CampaignMenuScreen(CampaignMenuAdapter adapter)
            : base(BuildRootWidget(adapter))
        {
            _adapter = adapter;
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

        private static ContainerWidget BuildRootWidget(CampaignMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget(
                "campaign-select-screen",
                adapter != null ? adapter.GetTitle() : string.Empty);
            root.AddChild(new TextWidget(
                "title",
                () => adapter != null ? adapter.GetTitle() : string.Empty,
                null,
                includeParentLabelInAnnouncement: false));

            MenuWidget menu = new MenuWidget("campaign-select-menu", "Campaigns");
            if (adapter == null)
            {
                root.AddChild(menu);
                return root;
            }

            AddCampaignItems(menu, adapter);
            root.AddChild(menu);
            AddOptionalButton(root, adapter.CustomCampaignButton);
            AddOptionalButton(root, adapter.TalesButton);
            AddOptionalButton(root, adapter.OptionsButton);
            AddOptionalButton(root, adapter.BackButton);
            return root;
        }

        private static void AddCampaignItems(MenuWidget menu, CampaignMenuAdapter adapter)
        {
            if (menu == null || adapter == null || adapter.CampaignButtons == null)
            {
                return;
            }

            for (int i = 0; i < adapter.CampaignButtons.Count; i++)
            {
                CampaignButtonAdapter item = adapter.CampaignButtons[i];
                if (item == null)
                {
                    continue;
                }

                menu.AddItem(new MenuItemWidget(
                    item.Id,
                    item.GetLabel,
                    item.GetStatus,
                    item.Activate,
                    item.FocusNative,
                    item.IsVisible));
            }
        }

        private static void AddOptionalButton(ContainerWidget root, IMenuButtonAdapter button)
        {
            if (root == null || button == null || !button.IsVisible())
            {
                return;
            }

            root.AddChild(new ButtonWidget(
                button.Id,
                button.GetLabel(),
                button.Activate,
                () => FocusNativeButton(button.Button),
                () => button.IsVisible(),
                () => button.IsVisible()));
        }

        private static void FocusNativeButton(UIButton button)
        {
            if (button == null)
            {
                return;
            }

            Component component = button;
            if (EventSystem.current != null && component != null)
            {
                EventSystem.current.SetSelectedGameObject(component.gameObject);
            }
        }
    }
}
