using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace SongsOfConquestAccess.Input
{
    /// <summary>
    /// Reads and writes a mod gesture's override bindings as one config string, the way
    /// <see cref="Scanner.ScannerCustomCategoryCodec"/> rides a whole list in one entry.
    ///
    /// One token per binding, tokens separated by semicolons; a token is the Unity <see cref="Key"/>
    /// enum name and the three modifier flags separated by commas - the same stable, English form
    /// <see cref="KeyboardBinding.Id"/> is built on, chosen over the spoken chord so a config written
    /// in one language reads back in another. Only <see cref="KeyboardBinding"/>s are written; a
    /// display-name fallback (the OEM1 backslash) is re-derived from the action's default when the
    /// override is applied, so it needs no place on disk.
    ///
    /// The reader is TOLERANT: an unknown key, a malformed token or a stray field is skipped, never
    /// rewritten, so a config a later build wrote survives a run of this one.
    /// </summary>
    public static class KeybindCodec
    {
        private const char BindingSeparator = ';';
        private const char FieldSeparator = ',';

        public static string Encode(IReadOnlyList<InputBinding> bindings)
        {
            if (bindings == null || bindings.Count == 0)
            {
                return string.Empty;
            }

            List<string> tokens = new List<string>();
            for (int i = 0; i < bindings.Count; i++)
            {
                KeyboardBinding chord = bindings[i] as KeyboardBinding;
                if (chord == null)
                {
                    continue;
                }

                // The display name last, hex-encoded: the comma key prints the field separator and
                // the semicolon key the binding separator, and a reader of the older four-field
                // token still reads the rest.
                tokens.Add(chord.Key + FieldSeparator.ToString()
                    + Flag(chord.Ctrl) + FieldSeparator
                    + Flag(chord.Shift) + FieldSeparator
                    + Flag(chord.Alt) + FieldSeparator
                    + HexEncode(chord.DisplayName));
            }

            return string.Join(BindingSeparator.ToString(), tokens.ToArray());
        }

        public static List<KeyboardBinding> Decode(string text)
        {
            List<KeyboardBinding> result = new List<KeyboardBinding>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return result;
            }

            string[] tokens = text.Split(BindingSeparator);
            for (int i = 0; i < tokens.Length; i++)
            {
                KeyboardBinding chord = DecodeToken(tokens[i]);
                if (chord != null)
                {
                    result.Add(chord);
                }
            }

            return result;
        }

        private static KeyboardBinding DecodeToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            string[] fields = token.Split(FieldSeparator);
            if (fields.Length != 4 && fields.Length != 5)
            {
                return null;
            }

            string name = fields[0].Trim();
            Key key;
            // Enum.TryParse also accepts a bare number and yields an out-of-range enum, so require the
            // token to round-trip as a real NAME - that skips both an unknown key and a numeric token.
            if (!TryParseKey(name, out key) || key.ToString() != name)
            {
                return null;
            }

            bool ctrl;
            bool shift;
            bool alt;
            if (!TryFlag(fields[1], out ctrl) || !TryFlag(fields[2], out shift) || !TryFlag(fields[3], out alt))
            {
                return null;
            }

            string displayName = fields.Length == 5 ? HexDecode(fields[4]) : null;
            return new KeyboardBinding(key, ctrl, shift, alt, displayName);
        }

        private static string HexEncode(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text);
            System.Text.StringBuilder hex = new System.Text.StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
            {
                hex.Append(bytes[i].ToString("x2"));
            }

            return hex.ToString();
        }

        // Tolerant like the rest of the reader: anything that is not hex answers empty rather than
        // throwing the whole token away, the chord still being sound without its display name.
        private static string HexDecode(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length % 2 != 0)
            {
                return null;
            }

            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                int value;
                if (!int.TryParse(hex.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out value))
                {
                    return null;
                }

                bytes[i] = (byte)value;
            }

            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        private static bool TryParseKey(string name, out Key key)
        {
            key = default(Key);
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            try
            {
                key = (Key)Enum.Parse(typeof(Key), name);
                return Enum.IsDefined(typeof(Key), key);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string Flag(bool value)
        {
            return value ? "1" : "0";
        }

        private static bool TryFlag(string field, out bool value)
        {
            string text = field == null ? null : field.Trim();
            if (text == "1" || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }

            if (text == "0" || string.Equals(text, "false", StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }

            value = false;
            return false;
        }
    }
}
