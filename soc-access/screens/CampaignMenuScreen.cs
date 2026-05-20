using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
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

        public static Screen TryBuildActiveScreen()
        {
            CampaignMenuAdapter adapter = FindActiveCampaignMenu();
            return adapter != null ? new CampaignMenuScreen(adapter) : null;
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

            MenuWidget menu = new MenuWidget("campaign-select-menu", adapter != null ? adapter.GetTitle() : string.Empty);
            if (adapter == null)
            {
                root.AddChild(menu);
                return root;
            }

            AddCampaignItems(menu, adapter);
            root.AddChild(menu);
            AddOptionalButton(root, "custom-campaigns", adapter.CustomCampaignButton);
            AddOptionalButton(root, "tales", adapter.TalesButton);
            AddOptionalButton(root, "options", adapter.OptionsButton);
            AddOptionalButton(root, "back", adapter.BackButton);
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
                    "campaign-" + i,
                    item.GetLabel,
                    () => BuildMenuButtonStatus(item),
                    item.Activate,
                    item.FocusNative,
                    item.IsVisible));
            }
        }

        private static void AddOptionalButton(ContainerWidget root, string id, IMenuButtonAdapter button)
        {
            if (root == null || button == null || !button.IsVisible())
            {
                return;
            }

            root.AddChild(new ButtonWidget(
                id,
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

        private static string BuildMenuButtonStatus(IMenuButtonAdapter item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            string nativeStatus = item.GetStatus();
            if (item.IsEnabled())
            {
                return nativeStatus;
            }

            return string.IsNullOrWhiteSpace(nativeStatus)
                ? ModText.Get(ModStrings.UI.StatusDisabled)
                : ModText.Get(ModStrings.Screens.DisabledWithReason, ModText.Get(ModStrings.UI.StatusDisabled), nativeStatus);
        }

        private static CampaignMenuAdapter FindActiveCampaignMenu()
        {
            CampaignMenu[] campaignMenus = Resources.FindObjectsOfTypeAll<CampaignMenu>();
            for (int i = 0; i < campaignMenus.Length; i++)
            {
                CampaignMenu campaignMenu = campaignMenus[i];
                if (!IsLiveSceneCampaignMenu(campaignMenu))
                {
                    continue;
                }

                CampaignMenuAdapter adapter = new CampaignMenuAdapter(campaignMenu);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        private static bool IsLiveSceneCampaignMenu(CampaignMenu campaignMenu)
        {
            if (campaignMenu == null)
            {
                return false;
            }

            GameObject gameObject = campaignMenu.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }
    }
}
