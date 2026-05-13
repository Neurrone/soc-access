using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class TaleSelectScreen : Screen
    {
        private readonly TaleSelectAdapter _adapter;

        public TaleSelectScreen(TaleSelectAdapter adapter)
            : base(BuildRootWidget(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            TaleSelectAdapter adapter = FindActiveTaleSelect();
            return adapter != null ? new TaleSelectScreen(adapter) : null;
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

        private static ContainerWidget BuildRootWidget(TaleSelectAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget(
                "tale-select-screen",
                adapter != null ? adapter.GetTitle() : string.Empty);
            root.AddChild(new TextWidget(
                "title",
                () => adapter != null ? adapter.GetTitle() : string.Empty,
                null,
                includeParentLabelInAnnouncement: false));

            MenuWidget menu = new MenuWidget("tale-select-menu", "Campaigns and tales");
            if (adapter == null)
            {
                root.AddChild(menu);
                return root;
            }

            AddTaleItems(menu, adapter);
            root.AddChild(menu);
            AddOptionalButton(root, "options", adapter.OptionsButton);
            AddOptionalButton(root, "back", adapter.BackButton);
            return root;
        }

        private static void AddTaleItems(MenuWidget menu, TaleSelectAdapter adapter)
        {
            if (menu == null || adapter == null || adapter.Tales == null)
            {
                return;
            }

            for (int i = 0; i < adapter.Tales.Count; i++)
            {
                TaleButtonAdapter item = adapter.Tales[i];
                if (item == null)
                {
                    continue;
                }

                menu.AddItem(new MenuItemWidget(
                    "tale-" + i,
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
            NativeSelectionUtility.Select(component);
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

            return string.IsNullOrWhiteSpace(nativeStatus) ? "disabled" : "disabled. " + nativeStatus;
        }

        private static TaleSelectAdapter FindActiveTaleSelect()
        {
            TaleButtonLayoutCoordinator[] coordinators = Resources.FindObjectsOfTypeAll<TaleButtonLayoutCoordinator>();
            for (int i = 0; i < coordinators.Length; i++)
            {
                TaleButtonLayoutCoordinator coordinator = coordinators[i];
                if (!IsLiveSceneCoordinator(coordinator))
                {
                    continue;
                }

                TaleSelectAdapter adapter = new TaleSelectAdapter(coordinator);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        private static bool IsLiveSceneCoordinator(TaleButtonLayoutCoordinator coordinator)
        {
            if (coordinator == null)
            {
                return false;
            }

            GameObject gameObject = ((Component)coordinator).gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }
    }
}
