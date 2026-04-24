using System.Collections.Generic;

namespace SongsOfConquestAccess.UI
{
    internal abstract class Widget
    {
        protected Widget(string id)
        {
            Id = id ?? string.Empty;
        }

        public string Id { get; private set; }

        public Widget Parent { get; internal set; }

        public virtual bool IsVisible
        {
            get { return true; }
        }

        public virtual bool IncludeParentLabelInAnnouncement
        {
            get { return false; }
        }

        public virtual bool AnnounceName
        {
            get { return false; }
        }

        public virtual string GetLabel()
        {
            return string.Empty;
        }

        public virtual string GetRole()
        {
            return string.Empty;
        }

        public virtual string GetStatus()
        {
            return string.Empty;
        }

        public virtual string GetTooltip()
        {
            return string.Empty;
        }

        public virtual string GetFocusMessage()
        {
            List<string> parts = new List<string>(3);
            AddIfNotEmpty(parts, GetLabel());
            AddIfNotEmpty(parts, GetRole());
            AddIfNotEmpty(parts, GetStatus());
            return string.Join(" ", parts.ToArray());
        }

        public virtual bool ClaimsAction(string actionKey)
        {
            return false;
        }

        public virtual bool HasClaimInTree(string actionKey)
        {
            return ClaimsAction(actionKey);
        }

        public virtual bool HandleAction(Input.InputAction action)
        {
            return false;
        }

        public virtual Widget GetFocusedWidget()
        {
            return this;
        }

        public void Focus()
        {
            OnFocus();
        }

        public void Unfocus()
        {
            OnUnfocus();
        }

        protected virtual void OnFocus()
        {
        }

        protected virtual void OnUnfocus()
        {
        }

        protected static void AddIfNotEmpty(List<string> parts, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add(value);
            }
        }
    }
}
