using System;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess.UI
{
    internal sealed class TextWidget : Widget
    {
        private readonly Func<string> _getText;
        private readonly Action _onFocus;
        private readonly bool _includeParentLabelInAnnouncement;
        private readonly Func<Tooltip> _getTooltip;
        private readonly Func<bool> _isVisible;

        public TextWidget(
            string id,
            Func<string> getText,
            Action onFocus,
            bool includeParentLabelInAnnouncement,
            Tooltip tooltip = null,
            Func<bool> isVisible = null)
            : this(id, getText, onFocus, includeParentLabelInAnnouncement, () => tooltip, isVisible)
        {
        }

        public TextWidget(
            string id,
            Func<string> getText,
            Action onFocus,
            bool includeParentLabelInAnnouncement,
            Func<Tooltip> getTooltip,
            Func<bool> isVisible = null)
            : base(id)
        {
            _getText = getText;
            _onFocus = onFocus;
            _includeParentLabelInAnnouncement = includeParentLabelInAnnouncement;
            _getTooltip = getTooltip;
            _isVisible = isVisible;
        }

        public override bool IsVisible
        {
            get { return _isVisible == null || _isVisible(); }
        }

        public override bool IncludeParentLabelInAnnouncement
        {
            get { return _includeParentLabelInAnnouncement; }
        }

        public override string GetLabel()
        {
            return _getText != null ? _getText() ?? string.Empty : string.Empty;
        }

        public override Tooltip GetTooltip()
        {
            return _getTooltip != null ? _getTooltip() : null;
        }

        protected override void OnFocus()
        {
            _onFocus?.Invoke();
        }
    }
}
