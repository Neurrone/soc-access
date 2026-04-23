using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal abstract class Screen
    {
        protected Screen(object sourceKey, ContainerWidget rootWidget)
        {
            SourceKey = sourceKey;
            RootWidget = rootWidget;
        }

        public object SourceKey { get; private set; }

        public ContainerWidget RootWidget { get; private set; }

        public abstract bool IsPresent();

        public virtual void OnPush()
        {
        }

        public virtual void OnFocus()
        {
            RootWidget.Focus();
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

        public virtual bool OnActionJustPressed(InputAction action)
        {
            return RootWidget.HandleAction(action);
        }
    }
}
