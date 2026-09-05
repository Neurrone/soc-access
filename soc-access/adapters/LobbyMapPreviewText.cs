using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Lobby;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess.Adapters
{
    public static class LobbyMapPreviewText
    {
        private static readonly FieldInfo MapNameHeaderField =
            AccessTools.Field(typeof(LobbyMapPreview), "_mapNameHeader");
        private static readonly FieldInfo MapInfoField =
            AccessTools.Field(typeof(LobbyMapPreview), "_mpInfo");

        public static string GetTitle(LobbyMapPreview preview)
        {
            return NormalizeSingleLine(GetText(preview, MapNameHeaderField));
        }

        public static string GetInfo(LobbyMapPreview preview)
        {
            return NormalizeMultiline(GetText(preview, MapInfoField));
        }

        public static string GetSummary(LobbyMapPreview preview)
        {
            return JoinMultilineParts(GetTitle(preview), GetInfo(preview));
        }

        private static string GetText(LobbyMapPreview preview, FieldInfo field)
        {
            UITextMesh textMesh = preview != null && field != null ? field.GetValue(preview) as UITextMesh : null;
            return UITextMeshTextUtility.GetEffectiveText(textMesh);
        }

        private static string NormalizeSingleLine(string value)
        {
            return SpeechTextSanitizer.Normalize(value);
        }

        private static string NormalizeMultiline(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string[] rawLines = value.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            List<string> lines = new List<string>();
            bool lastWasBlank = false;
            for (int i = 0; i < rawLines.Length; i++)
            {
                string line = SpeechTextSanitizer.Normalize(rawLines[i]);
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (lines.Count > 0 && !lastWasBlank)
                    {
                        lines.Add(string.Empty);
                        lastWasBlank = true;
                    }

                    continue;
                }

                lines.Add(line);
                lastWasBlank = false;
            }

            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
            {
                lines.RemoveAt(lines.Count - 1);
            }

            return string.Join(Environment.NewLine, lines.ToArray());
        }

        private static string JoinMultilineParts(params string[] parts)
        {
            List<string> cleaned = new List<string>();
            if (parts != null)
            {
                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i];
                    if (!string.IsNullOrWhiteSpace(part))
                    {
                        cleaned.Add(part.Trim());
                    }
                }
            }

            return cleaned.Count == 0 ? string.Empty : string.Join(Environment.NewLine, cleaned.ToArray());
        }
    }
}
