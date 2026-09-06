using System.Globalization;
using System.Text;

namespace SongsOfConquestAccess.UI.Graph
{
    /// <summary>Text folding shared by matching code (type-ahead search).</summary>
    public static class TextUtil
    {
        /// <summary>Fold accents away for matching ("Séance" matches "seance"); ligatures œ/æ expand.
        /// Ported from OniAccess (VisionNotIncluded) with permission.</summary>
        public static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string decomposed = text.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder(decomposed.Length);
            for (int i = 0; i < decomposed.Length; i++)
            {
                char c = decomposed[i];
                switch (c)
                {
                    case 'œ':
                    case 'Œ':
                        sb.Append("oe");
                        break;
                    case 'æ':
                    case 'Æ':
                        sb.Append("ae");
                        break;
                    default:
                        if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Only the letters and digits, lowercased - what two spellings of the same words have in
        /// common.
        ///
        /// It exists so that "already said" can be asked about text the game wrote twice in two
        /// styles. An icon named "Over Colonization" sits against the words "Over-colonization
        /// penalty", and reading both stutters; the hyphen, the capital and the space are exactly the
        /// differences that must not hide the repetition, and they are exactly what this drops.
        /// </summary>
        public static string LettersAndDigits(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            StringBuilder kept = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsLetterOrDigit(c)) kept.Append(char.ToLowerInvariant(c));
            }
            return kept.ToString();
        }

        /// <summary>
        /// Whether the text contains a LETTER - the test for "are these words, or only a figure".
        ///
        /// A widget's drawn text is a name where the game wrote words on it and is not a name where it
        /// drew a number: "0/11" and "50" name nothing a player can tell from the one beside it, and
        /// two of a card's five figures were literally both "30". A string with a letter anywhere in it
        /// passes, so "1st Patriots Navy", "Titanium-70" and "G-War Camps" are names as they should be.
        ///
        /// Deliberately not a check on the whole string being letters: a name that CONTAINS digits is
        /// still a name, and the thing being rejected is the string with no word in it at all.
        /// </summary>
        public static bool HasLetters(string text)
        {
            for (int i = 0; text != null && i < text.Length; i++)
                if (char.IsLetter(text[i])) return true;
            return false;
        }

        /// <summary>Null/empty/all-whitespace test. (<c>string.IsNullOrWhiteSpace</c> is .NET 4.0; the
        /// game's Mono runtime is on the 3.5 profile.)</summary>
        public static bool IsBlank(string s)
        {
            if (string.IsNullOrEmpty(s)) return true;
            for (int i = 0; i < s.Length; i++)
                if (!char.IsWhiteSpace(s[i])) return false;
            return true;
        }
    }
}
