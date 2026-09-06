using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Buffers;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    public abstract class Screen
    {
        protected Screen(ContainerWidget rootWidget)
        {
            RootWidget = rootWidget;
        }

        public ContainerWidget RootWidget { get; protected set; }

        public abstract bool IsPresent();

        public virtual IEnumerable<ReviewBufferKind> VisibleReviewBuffers
        {
            get
            {
                yield return ReviewBufferKind.Ui;
            }
        }

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

        public virtual void Update()
        {
            RootWidget?.Update();
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

        /// <summary>The tooltip of whatever the player is standing on, or null - what the tooltip
        /// actions menu opens on. Widget screens answer with the focused widget's; graph screens with
        /// the focused node's.</summary>
        public virtual Tooltip CurrentTooltip
        {
            get { return UIManager.CurrentWidget != null ? UIManager.CurrentWidget.GetTooltip() : null; }
        }
    }
}
