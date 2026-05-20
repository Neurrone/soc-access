using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess.UI
{
    internal abstract class Widget
    {
        private static readonly IReadOnlyList<string> EmptyTooltipLines = new string[0];

        protected Widget(string id)
        {
            Id = id ?? string.Empty;
        }

        public string Id { get; private set; }

        public Widget Parent { get; internal set; }

        public bool IsFocused { get; private set; }

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

        // Returns tooltip text, native visual display metadata, and optional
        // structured actions for this widget. Null means this widget has no
        // tooltip. Tooltip text lines are used for focus speech and future buffer
        // review. Visual metadata is used only to display the game's native
        // visual tooltip.
        public virtual Tooltip GetTooltip()
        {
            return null;
        }

        public virtual string GetFocusMessage()
        {
            List<string> parts = new List<string>(4);
            AddIfNotEmpty(parts, GetLabel());
            AddIfNotEmpty(parts, GetRole());
            AddIfNotEmpty(parts, GetStatus());
            return TerminateSentence(string.Join(" ", parts.ToArray()));
        }

        public IReadOnlyList<string> GetTooltipTextLines()
        {
            Tooltip tooltip = GetTooltip();
            return tooltip != null && tooltip.TextLines != null ? tooltip.TextLines : EmptyTooltipLines;
        }

        public string GetTooltipActionsText()
        {
            return BuildAvailableActionsText(GetTooltip());
        }

        public virtual bool ClaimsAction(string actionKey)
        {
            return false;
        }

        public virtual bool HasClaimInTree(string actionKey)
        {
            return ClaimsAction(actionKey);
        }

        public virtual bool HasFocusedClaimInTree(string actionKey)
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

        public virtual bool EnsureFocus()
        {
            return IsVisible;
        }

        public void Focus()
        {
            if (IsFocused)
            {
                return;
            }

            IsFocused = true;
            OnFocus();
        }

        public void Unfocus()
        {
            if (!IsFocused)
            {
                return;
            }

            IsFocused = false;
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

        protected static string TerminateSentence(string value)
        {
            value = value != null ? value.Trim() : string.Empty;
            if (value.Length == 0)
            {
                return string.Empty;
            }

            char last = value[value.Length - 1];
            return last == '.' || last == '!' || last == '?' || last == ':' || last == ';'
                ? value
                : value + ".";
        }

        private static string BuildAvailableActionsText(Tooltip tooltip)
        {
            if (tooltip == null || tooltip.Actions == null || tooltip.Actions.Count == 0)
            {
                return string.Empty;
            }

            List<string> labels = new List<string>();
            for (int i = 0; i < tooltip.Actions.Count; i++)
            {
                TooltipAction action = tooltip.Actions[i];
                string label = SpeechTextSanitizer.Normalize(action != null ? action.Label : null);
                if (!string.IsNullOrWhiteSpace(label))
                {
                    labels.Add(label);
                }
            }

            if (labels.Count == 0)
            {
                return string.Empty;
            }

            // Enriched tooltips remove native mouse/gamepad instruction rows such
            // as "CTRL + Drop" and replace them with structured actions. Announce
            // the action labels here so the player knows the Applications menu
            // has commands.
            return ModText.Get(ModStrings.UI.AvailableActions, ModText.JoinList(labels));
        }
    }
}
