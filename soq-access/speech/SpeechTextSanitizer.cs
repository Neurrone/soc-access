using System.Text.RegularExpressions;

namespace SongsOfConquestAccess.Speech
{
    internal static class SpeechTextSanitizer
    {
        // Native tooltip/detail strings can include Unity rich-text tags, for example
        // "<color=#...><b>+10%</b></color> Melee Resistance".
        private static readonly Regex RichTextTagRegex = new Regex("<.*?>", RegexOptions.Compiled);
        private static readonly Regex WhitespaceRegex = new Regex("\\s+", RegexOptions.Compiled);

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string withoutTags = RichTextTagRegex.Replace(value, string.Empty);
            return WhitespaceRegex.Replace(withoutTags, " ").Trim();
        }
    }
}
