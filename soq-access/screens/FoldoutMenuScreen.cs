using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class FoldoutMenuScreen : Screen
    {
        private readonly MainMenuAdapter.NativeFoldoutAdapter _foldout;
        private readonly MainMenuAdapter _owner;

        public FoldoutMenuScreen(MainMenuAdapter owner, MainMenuAdapter.NativeFoldoutAdapter foldout)
            : base(BuildRootWidget(owner, foldout))
        {
            _owner = owner;
            _foldout = foldout;
        }

        public static Screen TryBuildActiveScreen()
        {
            MainMenuAdapter adapter = MainMenuScreen.FindActiveMainMenu();
            if (adapter == null)
            {
                return null;
            }

            if (adapter.ExtrasFoldout != null && adapter.ExtrasFoldout.IsOpen())
            {
                return new FoldoutMenuScreen(adapter, adapter.ExtrasFoldout);
            }

            if (adapter.MultiplayerFoldout != null && adapter.MultiplayerFoldout.IsOpen())
            {
                return new FoldoutMenuScreen(adapter, adapter.MultiplayerFoldout);
            }

            return null;
        }

        public override bool IsPresent()
        {
            return _owner != null
                && _owner.IsPresent()
                && _foldout != null
                && _foldout.IsVisible()
                && _foldout.IsOpen();
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
                return _foldout != null && _foldout.Close();
            }

            return base.OnActionJustPressed(action);
        }

        private static ContainerWidget BuildRootWidget(MainMenuAdapter owner, MainMenuAdapter.NativeFoldoutAdapter foldout)
        {
            string label = foldout != null ? foldout.GetLabel() : string.Empty;
            string id = BuildFoldoutId(owner, foldout);
            ContainerWidget root = new ContainerWidget(id + "-screen", label + " menu");
            MenuWidget menu = new MenuWidget(id + "-menu", label);
            if (foldout == null)
            {
                root.AddChild(menu);
                return root;
            }

            for (int i = 0; i < foldout.Items.Count; i++)
            {
                IMenuButtonAdapter item = foldout.Items[i];
                if (item == null)
                {
                    continue;
                }

                menu.AddItem(new MenuItemWidget(
                    id + "-item-" + i,
                    item.GetLabel,
                    () => BuildMenuButtonStatus(item),
                    item.Activate,
                    null,
                    item.IsVisible));
            }

            root.AddChild(menu);
            return root;
        }

        private static string BuildFoldoutId(MainMenuAdapter owner, MainMenuAdapter.NativeFoldoutAdapter foldout)
        {
            if (owner != null && foldout != null)
            {
                if (ReferenceEquals(foldout, owner.ExtrasFoldout))
                {
                    return "extras";
                }

                if (ReferenceEquals(foldout, owner.MultiplayerFoldout))
                {
                    return "multiplayer";
                }
            }

            return "foldout-menu";
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
    }
}
