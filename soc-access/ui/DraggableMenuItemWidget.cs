using System;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.UI
{
    internal sealed class DraggableMenuItemWidget : MenuItemWidget
    {
        private readonly Func<bool> _canDrag;
        private readonly Func<bool> _isDragging;

        public DraggableMenuItemWidget(
            string id,
            Func<string> getLabel,
            Func<string> getStatus,
            Func<bool> activate,
            Action onFocus,
            Func<bool> isVisible,
            Func<bool> canDrag,
            Func<bool> isDragging,
            Tooltip tooltip = null,
            Action onUnfocus = null,
            Func<bool> isEnabled = null)
            : this(id, getLabel, getStatus, activate, onFocus, isVisible, canDrag, isDragging, () => tooltip, onUnfocus, isEnabled)
        {
        }

        public DraggableMenuItemWidget(
            string id,
            Func<string> getLabel,
            Func<string> getStatus,
            Func<bool> activate,
            Action onFocus,
            Func<bool> isVisible,
            Func<bool> canDrag,
            Func<bool> isDragging,
            Func<Tooltip> getTooltip,
            Action onUnfocus = null,
            Func<bool> isEnabled = null)
            : base(id, getLabel, getStatus, activate, onFocus, isVisible, getTooltip, onUnfocus, isEnabled)
        {
            _canDrag = canDrag;
            _isDragging = isDragging;
        }

        public bool CanDrag
        {
            get { return IsVisible && IsEnabled() && (_canDrag == null || _canDrag()); }
        }

        public override string GetStatus()
        {
            string status = base.GetStatus();
            if (_isDragging != null && _isDragging())
            {
                return JoinStatus(status, ModText.Get(ModStrings.UI.StatusDragging));
            }

            return CanDrag ? JoinStatus(status, ModText.Get(ModStrings.UI.StatusDraggable)) : status;
        }

        private static string JoinStatus(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first))
            {
                return second ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(second))
            {
                return first;
            }

            return ModText.Get(ModStrings.Common.ListSeparator, first, second);
        }
    }
}
