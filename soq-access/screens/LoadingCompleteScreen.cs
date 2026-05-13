using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class LoadingCompleteScreen : Screen
    {
        private readonly LoadingScreenAdapter _adapter;

        public LoadingCompleteScreen(LoadingScreenAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public LoadingScreenAdapter Adapter
        {
            get { return _adapter; }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override bool HasClaimed(string actionKey)
        {
            return false;
        }

        public override bool HasFocusedWidgetClaimed(string actionKey)
        {
            return false;
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            return false;
        }

        private static ContainerWidget BuildRoot(LoadingScreenAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("loading-complete-screen", string.Empty);
            root.AddChild(new PassiveButtonWidget(
                "loading-complete-continue",
                () => adapter != null ? adapter.PromptText : string.Empty));
            return root;
        }

        private sealed class PassiveButtonWidget : Widget
        {
            private readonly System.Func<string> _getLabel;

            public PassiveButtonWidget(string id, System.Func<string> getLabel)
                : base(id)
            {
                _getLabel = getLabel;
            }

            public override string GetLabel()
            {
                return _getLabel != null ? _getLabel() ?? string.Empty : string.Empty;
            }

            public override string GetRole()
            {
                return "button";
            }
        }
    }
}
