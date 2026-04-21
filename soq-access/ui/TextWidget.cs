using System;

namespace SongsOfConquestAccess.UI
{
    internal sealed class TextWidget : Widget
    {
        private readonly Func<string> _getText;
        private readonly Action _onFocus;
        private readonly bool _includeParentLabelInAnnouncement;

        public TextWidget(string id, Func<string> getText, Action onFocus, bool includeParentLabelInAnnouncement)
            : base(id)
        {
            _getText = getText;
            _onFocus = onFocus;
            _includeParentLabelInAnnouncement = includeParentLabelInAnnouncement;
        }

        public override bool IncludeParentLabelInAnnouncement
        {
            get { return _includeParentLabelInAnnouncement; }
        }

        public override string GetLabel()
        {
            return _getText != null ? _getText() ?? string.Empty : string.Empty;
        }

        protected override void OnFocus()
        {
            _onFocus?.Invoke();
        }
    }
}
