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
            : base(foldout != null ? foldout.SourceKey : null, BuildRootWidget(foldout))
        {
            _owner = owner;
            _foldout = foldout;
        }

        public override bool IsPresent()
        {
            return _owner != null
                && _owner.IsPresent()
                && _foldout != null
                && _foldout.IsVisible()
                && _foldout.IsOpen();
        }

        public override void OnFocus()
        {
            base.OnFocus();
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                return _foldout != null && _foldout.Close();
            }

            return base.OnActionJustPressed(action);
        }

        private static ContainerWidget BuildRootWidget(MainMenuAdapter.NativeFoldoutAdapter foldout)
        {
            string label = foldout != null ? foldout.GetLabel() : string.Empty;
            ContainerWidget root = new ContainerWidget((foldout != null ? foldout.Id : "foldout-menu") + "-screen", label + " menu");
            MenuWidget menu = new MenuWidget((foldout != null ? foldout.Id : "foldout-menu") + "-menu", label);
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
                    item.Id,
                    item.GetLabel,
                    item.GetStatus,
                    item.Activate,
                    item.Focus,
                    item.IsVisible));
            }

            root.AddChild(menu);
            return root;
        }
    }
}
