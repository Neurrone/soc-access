using UnityEngine;

namespace SongsOfConquestAccess.UI
{
    internal enum CodexContentItemKind
    {
        Heading,
        Text
    }

    internal sealed class CodexContentItem
    {
        public CodexContentItem(CodexContentItemKind kind, string text, RectTransform sourceTransform = null)
        {
            Kind = kind;
            Text = text ?? string.Empty;
            SourceTransform = sourceTransform;
        }

        public CodexContentItemKind Kind { get; private set; }

        public string Text { get; private set; }

        public RectTransform SourceTransform { get; private set; }
    }
}
