using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal abstract class Screen
    {
        protected Screen(ContainerWidget rootWidget)
        {
            RootWidget = rootWidget;
        }

        public ContainerWidget RootWidget { get; protected set; }

        public abstract bool IsPresent();

        public virtual void OnPush()
        {
        }

        public virtual void OnFocus()
        {
            UIManager.RequestFocus(RootWidget);
        }

        public virtual void OnUnfocus()
        {
        }

        public virtual void OnPop()
        {
        }

        public virtual bool HasClaimed(string actionKey)
        {
            return RootWidget.HasClaimInTree(actionKey);
        }

        public virtual bool HasFocusedWidgetClaimed(string actionKey)
        {
            return RootWidget.HasFocusedClaimInTree(actionKey);
        }

        public virtual bool OnActionJustPressed(InputAction action)
        {
            return RootWidget.HandleAction(action);
        }
    }
}
