using System;
using System.Collections.Generic;
using System.Text;

namespace SongsOfConquestAccess.Speech.Spatial
{
    internal static class ConfigurableAnnouncementComposer
    {
        public static string Compose(AnnouncementGroupDefinition group, IEnumerable<AnnouncementPart> parts)
        {
            return Compose(
                group,
                parts,
                ModSettings.GetAnnouncementOrder,
                ModSettings.GetAnnouncementElementEnabled,
                ModSettings.GetAnnouncementElementSuffix);
        }

        internal static string Compose(
            AnnouncementGroupDefinition group,
            IEnumerable<AnnouncementPart> parts,
            Func<AnnouncementGroupDefinition, IReadOnlyList<string>> getOrder,
            Func<AnnouncementGroupDefinition, AnnouncementElementDefinition, bool> isEnabled,
            Func<AnnouncementGroupDefinition, AnnouncementElementDefinition, bool> includeSuffix)
        {
            if (group == null || parts == null)
            {
                return string.Empty;
            }

            Dictionary<string, string> texts = new Dictionary<string, string>();
            foreach (AnnouncementPart part in parts)
            {
                if (part == null || string.IsNullOrWhiteSpace(part.Key) || string.IsNullOrWhiteSpace(part.Text))
                {
                    continue;
                }

                if (!texts.ContainsKey(part.Key))
                {
                    texts.Add(part.Key, part.Text);
                }
            }

            List<RenderedPart> rendered = new List<RenderedPart>();
            IReadOnlyList<string> order = getOrder != null ? getOrder(group) : GetDefaultOrder(group);
            if (order == null)
            {
                order = new string[0];
            }
            for (int i = 0; i < order.Count; i++)
            {
                string key = order[i];
                string text;
                AnnouncementElementDefinition element = group.GetElement(key);
                if (element == null || !texts.TryGetValue(key, out text))
                {
                    continue;
                }

                bool enabled = isEnabled != null ? isEnabled(group, element) : element.DefaultEnabled;
                if (!enabled)
                {
                    continue;
                }

                bool suffix = includeSuffix != null ? includeSuffix(group, element) : element.DefaultSuffix;
                rendered.Add(new RenderedPart(
                    text,
                    suffix ? "," : string.Empty));
            }

            if (rendered.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < rendered.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(rendered[i - 1].Suffix);
                    builder.Append(' ');
                }

                builder.Append(rendered[i].Text);
            }

            return builder.ToString();
        }

        private sealed class RenderedPart
        {
            public RenderedPart(string text, string suffix)
            {
                Text = text;
                Suffix = suffix;
            }

            public string Text { get; private set; }

            public string Suffix { get; private set; }
        }

        private static IReadOnlyList<string> GetDefaultOrder(AnnouncementGroupDefinition group)
        {
            if (group == null || group.Elements == null)
            {
                return new string[0];
            }

            List<string> keys = new List<string>();
            for (int i = 0; i < group.Elements.Count; i++)
            {
                keys.Add(group.Elements[i].Key);
            }

            return keys;
        }
    }
}
