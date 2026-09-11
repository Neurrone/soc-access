using System.Collections.Generic;
using System.Text;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// Reads and writes one taxonomy's three slots as one config string, the
    /// way the announcement order already rides in one comma-separated entry.
    ///
    /// The text is three records separated by semicolons, one per slot in slot
    /// order; an empty record is an empty slot. A record is the name, the
    /// selectors and the keywords separated by pipes; selectors and keywords are
    /// comma separated, and a selector is its category key and subcategory key
    /// separated by a colon. Names and keywords are player-authored, so every
    /// separator is backslash escaped and only unescaped at the innermost split.
    ///
    /// Short and long text both read: a record the string does not reach is an
    /// empty slot, and anything past the third is ignored.
    /// </summary>
    public static class ScannerCustomSlotsCodec
    {
        private const char RecordSeparator = ';';
        private const char FieldSeparator = '|';
        private const char ItemSeparator = ',';
        private const char SelectorSeparator = ':';
        private const char EscapePrefix = '\\';

        public static string Encode(ScannerCustomSlots slots)
        {
            if (slots == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < ScannerCustomSlots.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(RecordSeparator);
                }

                AppendCategory(builder, slots.Slot(i));
            }

            return builder.ToString();
        }

        public static ScannerCustomSlots Decode(string text)
        {
            ScannerCustomSlots slots = new ScannerCustomSlots();
            if (string.IsNullOrWhiteSpace(text))
            {
                return slots;
            }

            List<string> records = Split(text, RecordSeparator);
            for (int i = 0; i < records.Count && i < ScannerCustomSlots.Count; i++)
            {
                slots.Set(i, DecodeCategory(records[i]));
            }

            return slots;
        }

        private static void AppendCategory(StringBuilder builder, ScannerCustomCategory category)
        {
            if (category == null)
            {
                return;
            }

            builder.Append(Escape(category.Name));
            builder.Append(FieldSeparator);
            for (int i = 0; i < category.Selectors.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(ItemSeparator);
                }

                ScannerCustomCategorySelector selector = category.Selectors[i];
                builder.Append(Escape(selector.CategoryKey));
                builder.Append(SelectorSeparator);
                builder.Append(Escape(selector.SubcategoryKey));
            }

            builder.Append(FieldSeparator);
            for (int i = 0; i < category.Keywords.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(ItemSeparator);
                }

                builder.Append(Escape(category.Keywords[i]));
            }
        }

        private static ScannerCustomCategory DecodeCategory(string record)
        {
            List<string> fields = Split(record, FieldSeparator);
            if (fields.Count == 0)
            {
                return null;
            }

            ScannerCustomCategory category = new ScannerCustomCategory(Unescape(fields[0]));
            if (string.IsNullOrWhiteSpace(category.Name))
            {
                return null;
            }

            ReadSelectorsAndKeywords(category, fields, 1);
            return category;
        }

        private static void ReadSelectorsAndKeywords(
            ScannerCustomCategory category,
            List<string> fields,
            int first)
        {
            if (fields.Count > first)
            {
                List<string> selectors = Split(fields[first], ItemSeparator);
                for (int i = 0; i < selectors.Count; i++)
                {
                    List<string> parts = Split(selectors[i], SelectorSeparator);
                    if (parts.Count == 2)
                    {
                        category.SetSelector(Unescape(parts[0]), Unescape(parts[1]), selected: true);
                    }
                }
            }

            if (fields.Count > first + 1)
            {
                List<string> keywords = Split(fields[first + 1], ItemSeparator);
                for (int i = 0; i < keywords.Count; i++)
                {
                    category.AddKeyword(Unescape(keywords[i]));
                }
            }
        }

        /// <summary>
        /// Splits on one separator while stepping over escaped characters, so an
        /// outer split leaves the escapes intact for the inner ones.
        /// </summary>
        private static List<string> Split(string text, char separator)
        {
            List<string> parts = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                return parts;
            }

            StringBuilder current = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                char value = text[i];
                if (value == EscapePrefix && i + 1 < text.Length)
                {
                    current.Append(value);
                    current.Append(text[i + 1]);
                    i++;
                    continue;
                }

                if (value == separator)
                {
                    parts.Add(current.ToString());
                    current.Length = 0;
                    continue;
                }

                current.Append(value);
            }

            parts.Add(current.ToString());
            return parts;
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (current == EscapePrefix
                    || current == RecordSeparator
                    || current == FieldSeparator
                    || current == ItemSeparator
                    || current == SelectorSeparator)
                {
                    builder.Append(EscapePrefix);
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        private static string Unescape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (current == EscapePrefix && i + 1 < value.Length)
                {
                    builder.Append(value[i + 1]);
                    i++;
                    continue;
                }

                builder.Append(current);
            }

            return builder.ToString();
        }
    }
}
