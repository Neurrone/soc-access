using System.Collections.Generic;
using System.Text;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// Reads and writes a whole <see cref="ScannerCustomCategoryList"/> as one
    /// config string, the way the announcement order already rides in one
    /// comma-separated entry.
    ///
    /// The text is the next id, then one record per category, separated by
    /// semicolons. A record is the id, the name, the selectors, the keywords and
    /// the quick key separated by pipes; selectors and keywords are comma
    /// separated, and a selector is its category key and subcategory key
    /// separated by a colon. Names and keywords are player-authored, so every
    /// separator is backslash escaped and only unescaped at the innermost
    /// split.
    ///
    /// Both trailing fields are optional on the way in, so text written before
    /// quick keys existed still reads back with no key set.
    /// </summary>
    internal static class ScannerCustomCategoryCodec
    {
        private const char RecordSeparator = ';';
        private const char FieldSeparator = '|';
        private const char ItemSeparator = ',';
        private const char SelectorSeparator = ':';
        private const char EscapePrefix = '\\';

        public static string Encode(ScannerCustomCategoryList list)
        {
            if (list == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            builder.Append(list.NextId);
            for (int i = 0; i < list.Categories.Count; i++)
            {
                builder.Append(RecordSeparator);
                AppendCategory(builder, list.Categories[i]);
            }

            return builder.ToString();
        }

        public static ScannerCustomCategoryList Decode(string text)
        {
            ScannerCustomCategoryList list = new ScannerCustomCategoryList();
            if (string.IsNullOrWhiteSpace(text))
            {
                return list;
            }

            List<string> records = Split(text, RecordSeparator);
            int nextId;
            if (records.Count > 0 && int.TryParse(Unescape(records[0]), out nextId))
            {
                list.SetNextId(nextId);
            }

            for (int i = 1; i < records.Count; i++)
            {
                ScannerCustomCategory category = DecodeCategory(records[i]);
                if (category != null)
                {
                    list.Restore(category);
                }
            }

            return list;
        }

        private static void AppendCategory(StringBuilder builder, ScannerCustomCategory category)
        {
            builder.Append(category.Id);
            builder.Append(FieldSeparator);
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

            builder.Append(FieldSeparator);
            builder.Append(Escape(ScannerQuickKeys.ToToken(category.QuickKey)));
        }

        private static ScannerCustomCategory DecodeCategory(string record)
        {
            List<string> fields = Split(record, FieldSeparator);
            int id;
            if (fields.Count < 2 || !int.TryParse(Unescape(fields[0]), out id))
            {
                return null;
            }

            ScannerCustomCategory category = new ScannerCustomCategory(id, Unescape(fields[1]));
            if (fields.Count > 2)
            {
                List<string> selectors = Split(fields[2], ItemSeparator);
                for (int i = 0; i < selectors.Count; i++)
                {
                    List<string> parts = Split(selectors[i], SelectorSeparator);
                    if (parts.Count == 2)
                    {
                        category.SetSelector(Unescape(parts[0]), Unescape(parts[1]), selected: true);
                    }
                }
            }

            if (fields.Count > 3)
            {
                List<string> keywords = Split(fields[3], ItemSeparator);
                for (int i = 0; i < keywords.Count; i++)
                {
                    category.AddKeyword(Unescape(keywords[i]));
                }
            }

            if (fields.Count > 4)
            {
                category.SetQuickKey(ScannerQuickKeys.FromToken(Unescape(fields[4])));
            }

            return category;
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
