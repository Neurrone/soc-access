using System.Text;
using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess.Events
{
    // These events correspond to visual adventure-map notifications shown by the game.
    // They model native notification UI so screen-reader output follows the same feedback
    // sighted players receive.
    // Detailed adventure-map notification with an icon and optional body text.
    // Used for claim notifications, claim-with-resources popups, pillage/repair,
    // siege state changes, artifact notifications, and similar large floating
    // notifications displayed by IconNotification.ShowNotification(...).
    internal sealed class AdventureIconNotificationEvent : IAccessibilityEvent
    {
        public AdventureIconNotificationEvent(string header, string body)
        {
            Header = header ?? string.Empty;
            Body = body ?? string.Empty;
        }

        public string Kind { get { return AccessibilityEvents.Notification.AdventureIcon; } }
        public string Header { get; private set; }

        public string Body { get; private set; }

        public string GetSpeechText()
        {
            return SpeechTextSanitizer.Normalize(NotificationSpeechText.JoinNonEmpty(null, Header, Body));
        }
    }

    // Small floating adventure-map notification text.
    // Used for resource rewards, penalties, bacteria/stat changes, commander
    // messages, entity messages, inventory/market feedback, cursor messages,
    // and other transient text displayed by SimpleNotification.Show(...).
    internal sealed class AdventureSimpleNotificationEvent : IAccessibilityEvent
    {
        public AdventureSimpleNotificationEvent(string text)
        {
            Text = text ?? string.Empty;
        }

        public string Kind { get { return AccessibilityEvents.Notification.AdventureSimple; } }
        public string Text { get; private set; }

        public string GetSpeechText()
        {
            return SpeechTextSanitizer.Normalize(Text);
        }
    }

    // Wielder level-up popup shown above the commander on the adventure map.
    // The native component displays only the wielder name and the reached level
    // number, so speech mirrors those visible fields without adding extra wording.
    internal sealed class CommanderLevelUpNotificationEvent : IAccessibilityEvent
    {
        public CommanderLevelUpNotificationEvent(string wielderName, int reachedLevel)
        {
            WielderName = wielderName ?? string.Empty;
            LevelText = reachedLevel.ToString();
        }

        public string Kind { get { return AccessibilityEvents.Notification.CommanderLevelUp; } }
        public string WielderName { get; private set; }

        public string LevelText { get; private set; }

        public string GetSpeechText()
        {
            return SpeechTextSanitizer.Normalize(NotificationSpeechText.JoinNonEmpty(null, WielderName, LevelText));
        }
    }

    // Persistent right-side adventure notification HUD entry.
    // Used for strategic/log-style notifications such as lost entities, beacon
    // progress, town threats, player defeated/disconnected, remaining rounds,
    // hostile growth, and commander max-level messages.
    internal sealed class AdventureHudNotificationEvent : IAccessibilityEvent
    {
        public AdventureHudNotificationEvent(string text)
        {
            Text = text ?? string.Empty;
        }

        public string Kind { get { return AccessibilityEvents.Notification.AdventureHud; } }
        public string Text { get; private set; }

        public string GetSpeechText()
        {
            return SpeechTextSanitizer.Normalize(Text);
        }
    }

    // Transient objective update animation shown on the adventure HUD.
    // Used when objectives are added, updated, or completed; speech mirrors the
    // visible header plus objective line including progress/optional text.
    internal sealed class ObjectiveNotificationEvent : IAccessibilityEvent
    {
        public ObjectiveNotificationEvent(string text)
        {
            Text = text ?? string.Empty;
        }

        public string Kind { get { return AccessibilityEvents.Notification.Objective; } }
        public string Text { get; private set; }

        public string GetSpeechText()
        {
            return SpeechTextSanitizer.Normalize(Text);
        }
    }

    // Non-modal adventure-map new-turn popup shown when control returns to a
    // local human player/team in online or hotseat-style games. Modal versions
    // requiring confirm/cancel are handled as popup screens, not notifications.
    internal sealed class AdventureNewTurnPopupEvent : IAccessibilityEvent
    {
        public AdventureNewTurnPopupEvent(string text)
        {
            Text = text ?? string.Empty;
        }

        public string Kind { get { return AccessibilityEvents.Notification.AdventureNewTurn; } }
        public string Text { get; private set; }

        public string GetSpeechText()
        {
            return SpeechTextSanitizer.Normalize(Text);
        }
    }

    // World-lore panel notification from AdventureMenuSystem.ShowWorldNotification(...).
    // Used for larger map-entity messages and denied interactions that display
    // localized header/body/effects text in the world lore panel.
    internal sealed class WorldMessageNotificationEvent : IAccessibilityEvent
    {
        public WorldMessageNotificationEvent(int entityId, int commanderId, string header, string body, string effects)
        {
            EntityId = entityId;
            CommanderId = commanderId;
            Header = header ?? string.Empty;
            Body = body ?? string.Empty;
            Effects = effects ?? string.Empty;
        }

        public string Kind { get { return AccessibilityEvents.Notification.WorldMessage; } }
        public int EntityId { get; private set; }

        public int CommanderId { get; private set; }

        public string Header { get; private set; }

        public string Body { get; private set; }

        public string Effects { get; private set; }

        public string GetSpeechText()
        {
            return SpeechTextSanitizer.Normalize(NotificationSpeechText.JoinNonEmpty("Notification", Header, Body, Effects));
        }

    }

    // Lightweight centered global adventure notification.
    // Used for short centered messages such as game-saved style feedback.
    internal sealed class CenteredNotificationEvent : IAccessibilityEvent
    {
        private readonly string _text;

        public CenteredNotificationEvent(string text)
        {
            _text = SpeechTextSanitizer.Normalize(text);
            if (string.IsNullOrWhiteSpace(_text))
            {
                throw new System.ArgumentException("Centered notification text must be non-empty.", "text");
            }
        }

        public string Kind { get { return AccessibilityEvents.Notification.Centered; } }
        public string GetSpeechText()
        {
            return _text;
        }
    }

    // Prominent centered adventure notification.
    // Used for major centered warnings or loss-style messages that the game
    // displays through CenteredNotificationHeavy.
    internal sealed class CenteredHeavyNotificationEvent : IAccessibilityEvent
    {
        private readonly string _text;

        public CenteredHeavyNotificationEvent(string text)
        {
            _text = SpeechTextSanitizer.Normalize(text);
            if (string.IsNullOrWhiteSpace(_text))
            {
                throw new System.ArgumentException("Centered heavy notification text must be non-empty.", "text");
            }
        }

        public string Kind { get { return AccessibilityEvents.Notification.CenteredHeavy; } }
        public string GetSpeechText()
        {
            return _text;
        }
    }

    internal static class NotificationSpeechText
    {
        public static string JoinNonEmpty(string prefix, params string[] values)
        {
            StringBuilder builder = new StringBuilder(prefix ?? string.Empty);
            if (values == null)
            {
                return builder.ToString();
            }

            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    if (builder.Length > 0)
                    {
                        builder.Append(": ");
                    }

                    builder.Append(value);
                }
            }

            return builder.ToString();
        }
    }
}
