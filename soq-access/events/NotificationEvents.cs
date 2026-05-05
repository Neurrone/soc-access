using System.Collections.Generic;
using System.Text;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Rewards;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Events
{
    // These events correspond to visual adventure-map notifications shown by the game.
    // They model native notification UI so screen-reader output follows the same feedback
    // sighted players receive.
    internal sealed class WorldRewardNotificationEvent : IAccessibilityEvent
    {
        public WorldRewardNotificationEvent(int commanderId, Vector2Int rewardTile, IList<RuntimeRewardDataContainer> rewardDataContainers)
        {
            CommanderId = commanderId;
            RewardTile = rewardTile;
            RewardDataContainers = rewardDataContainers != null
                ? new List<RuntimeRewardDataContainer>(rewardDataContainers)
                : new List<RuntimeRewardDataContainer>();
        }

        public string Kind { get { return AccessibilityEvents.Notification.WorldReward; } }

        public bool Interrupt { get { return false; } }

        public int CommanderId { get; private set; }

        public Vector2Int RewardTile { get; private set; }

        public IReadOnlyList<RuntimeRewardDataContainer> RewardDataContainers { get; private set; }

        public string GetSpeechText()
        {
            List<string> parts = BuildRewardSummaries();
            return parts.Count == 0
                ? string.Empty
                : SpeechTextSanitizer.Normalize("Reward: " + string.Join(", ", parts.ToArray()));
        }

        private List<string> BuildRewardSummaries()
        {
            List<string> parts = new List<string>();
            if (RewardDataContainers == null || RewardDataContainers.Count == 0)
            {
                return parts;
            }

            for (int i = 0; i < RewardDataContainers.Count; i++)
            {
                string part = BuildRewardPart(RewardDataContainers[i]);
                if (!string.IsNullOrWhiteSpace(part))
                {
                    parts.Add(part);
                }
            }

            return parts;
        }

        private static string BuildRewardPart(RuntimeRewardDataContainer reward)
        {
            try
            {
                switch (reward.RewardType)
                {
                    case RuntimeRewardType.Experience:
                        RuntimeRewardExperience experience = reward.RewardData as RuntimeRewardExperience;
                        return experience != null ? experience.Experience + " experience" : "experience";
                    case RuntimeRewardType.Level:
                        RuntimeRewardLevel level = reward.RewardData as RuntimeRewardLevel;
                        return level != null ? level.LevelsToAdd + " level" + PluralSuffix(level.LevelsToAdd) : "level";
                    case RuntimeRewardType.Resource:
                    case RuntimeRewardType.RandomExoticResource:
                        RuntimeRewardResource resource = reward.RewardData as RuntimeRewardResource;
                        return resource != null ? resource.AmountMinMax.max + " " + FormatResource(resource.Type) : "resources";
                    case RuntimeRewardType.Artifact:
                    case RuntimeRewardType.RandomArtifact:
                        return "artifact";
                    case RuntimeRewardType.Skill:
                    case RuntimeRewardType.RandomSkill:
                        return "skill";
                    case RuntimeRewardType.Troops:
                    case RuntimeRewardType.RandomTroopInFaction:
                        return "troops";
                    case RuntimeRewardType.Bacteria:
                        return "effect";
                    case RuntimeRewardType.StoryObjective:
                        return "objective progress";
                    default:
                        return reward.RewardType.ToString();
                }
            }
            catch (System.Exception exception)
            {
                SoqAccessPlugin.Instance?.LogWarning("Failed to build reward notification speech for " + reward.RewardType + ": " + exception.Message);
                return reward.RewardType.ToString();
            }
        }

        private static string FormatResource(ResourceType type)
        {
            return type.ToString().ToLowerInvariant();
        }

        private static string PluralSuffix(int amount)
        {
            return amount == 1 ? string.Empty : "s";
        }

    }

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

        public bool Interrupt { get { return false; } }

        public int EntityId { get; private set; }

        public int CommanderId { get; private set; }

        public string Header { get; private set; }

        public string Body { get; private set; }

        public string Effects { get; private set; }

        public string GetSpeechText()
        {
            return SpeechTextSanitizer.Normalize(JoinNonEmpty("Notification", Header, Body, Effects));
        }

        private static string JoinNonEmpty(string prefix, params string[] values)
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

    internal sealed class DeniedMoveNotificationEvent : IAccessibilityEvent
    {
        public DeniedMoveNotificationEvent(DeniedMoveReason reason, string localizedMessage)
        {
            Reason = reason;
            LocalizedMessage = localizedMessage ?? string.Empty;
        }

        public string Kind { get { return AccessibilityEvents.Notification.DeniedMove; } }

        public bool Interrupt { get { return false; } }

        public DeniedMoveReason Reason { get; private set; }

        public string LocalizedMessage { get; private set; }

        public string GetSpeechText()
        {
            return SpeechTextSanitizer.Normalize(LocalizedMessage);
        }
    }

    internal sealed class DeniedEntityInteractionNotificationEvent : IAccessibilityEvent
    {
        public DeniedEntityInteractionNotificationEvent(int entityId, int commanderId, string entityName, string localizedMessage)
        {
            EntityId = entityId;
            CommanderId = commanderId;
            EntityName = entityName ?? string.Empty;
            LocalizedMessage = localizedMessage ?? string.Empty;
        }

        public string Kind { get { return AccessibilityEvents.Notification.DeniedEntityInteraction; } }

        public bool Interrupt { get { return false; } }

        public int EntityId { get; private set; }

        public int CommanderId { get; private set; }

        public string EntityName { get; private set; }

        public string LocalizedMessage { get; private set; }

        public string GetSpeechText()
        {
            return SpeechTextSanitizer.Normalize(LocalizedMessage);
        }
    }

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

        public bool Interrupt { get { return false; } }

        public string GetSpeechText()
        {
            return _text;
        }
    }

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

        public bool Interrupt { get { return false; } }

        public string GetSpeechText()
        {
            return _text;
        }
    }
}
