using System.Collections.Generic;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Events
{
    internal enum StoryCameraFocusKind
    {
        Point,
        Wielder,
        ConversationArea
    }

    internal sealed class StoryCameraFocusTarget
    {
        public StoryCameraFocusTarget(string label, Vector2Int tile)
        {
            Label = string.IsNullOrWhiteSpace(label) ? ModText.Get(ModStrings.UI.Target) : label;
            Tile = tile;
        }

        public string Label { get; private set; }

        public Vector2Int Tile { get; private set; }

        public string ToSpeech()
        {
            return ModText.Get(ModStrings.Events.StoryCameraFocusTargetAt, Label, Tile.x + ", " + Tile.y);
        }
    }

    internal sealed class StoryCameraFocusStartedEvent : IAccessibilityEvent
    {
        public StoryCameraFocusStartedEvent(StoryCameraFocusKind focusKind, IEnumerable<StoryCameraFocusTarget> targets, string focusReference)
        {
            FocusKind = focusKind;
            FocusReference = focusReference ?? string.Empty;
            Targets = targets != null
                ? new List<StoryCameraFocusTarget>(targets)
                : new List<StoryCameraFocusTarget>();
        }

        public string Kind { get { return AccessibilityEvents.Story.CameraFocusStarted; } }
        public StoryCameraFocusKind FocusKind { get; private set; }

        public string FocusReference { get; private set; }

        public IReadOnlyList<StoryCameraFocusTarget> Targets { get; private set; }

        public string GetSpeechText()
        {
            if (Targets == null || Targets.Count == 0)
            {
                return string.Empty;
            }

            if (FocusKind == StoryCameraFocusKind.ConversationArea)
            {
                return SpeechTextSanitizer.Normalize(
                    ModText.Get(ModStrings.Events.StoryCameraFocusConversationArea, JoinTargets()));
            }

            return SpeechTextSanitizer.Normalize(ModText.Get(ModStrings.Events.StoryCameraFocusAreaAround, Targets[0].ToSpeech()));
        }

        private string JoinTargets()
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < Targets.Count; i++)
            {
                if (Targets[i] != null)
                {
                    parts.Add(Targets[i].ToSpeech());
                }
            }

            return string.Join("; ", parts.ToArray());
        }
    }
}
