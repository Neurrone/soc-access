using HarmonyLib;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal static class MenuButtonTextUtility
    {
        private static readonly Regex RichTextTagRegex = new Regex("<.*?>", RegexOptions.Compiled);
        private static readonly AccessTools.FieldRef<UITextMeshLocalization, string> UITextMeshLocalizationKeyRef =
            AccessTools.FieldRefAccess<UITextMeshLocalization, string>("_localizationKey");

        public static string NormalizeForSpeech(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string withoutTags = RichTextTagRegex.Replace(value, string.Empty);
            return Regex.Replace(withoutTags, "\\s+", " ").Trim();
        }

        public static string GetDirectButtonText(UIButton button)
        {
            return NormalizeForSpeech(button != null ? button.Text : string.Empty);
        }

        public static string GetStandardButtonLabel(UIButton button)
        {
            if (button == null)
            {
                return string.Empty;
            }

            string title = GetDirectButtonText(button);
            if (string.IsNullOrWhiteSpace(title))
            {
                title = GetVisibleTextByNodeName(button, "Title");
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                title = GetFirstVisibleText(button);
            }

            // Descriptions are only appended when they come from a localization-backed text node.
            // This avoids speaking placeholder/default scene text that can appear during transitions.
            string description = GetVisibleLocalizedTextByNodeName(button, "Description", "SubHeader", "Subtitle");
            return JoinParts(title, description);
        }

        public static string GetVisibleTextByNodeName(UIButton button, params string[] nodeNames)
        {
            if (button == null || nodeNames == null || nodeNames.Length == 0)
            {
                return string.Empty;
            }

            for (int i = 0; i < nodeNames.Length; i++)
            {
                string candidateName = nodeNames[i];
                if (string.IsNullOrWhiteSpace(candidateName))
                {
                    continue;
                }

                string match = GetVisibleUITextMeshByName(button, candidateName);
                if (!string.IsNullOrWhiteSpace(match))
                {
                    return match;
                }

                match = GetVisibleUnityTextByName(button, candidateName);
                if (!string.IsNullOrWhiteSpace(match))
                {
                    return match;
                }
            }

            return string.Empty;
        }

        public static string GetVisibleLocalizedTextByNodeName(UIButton button, params string[] nodeNames)
        {
            if (button == null || nodeNames == null || nodeNames.Length == 0)
            {
                return string.Empty;
            }

            UITextMesh[] textMeshes = ((Component)button).GetComponentsInChildren<UITextMesh>(includeInactive: false);
            for (int i = 0; i < textMeshes.Length; i++)
            {
                UITextMesh textMesh = textMeshes[i];
                if (textMesh == null
                    || !textMesh.gameObject.activeInHierarchy
                    || !HasMatchingNodeName(textMesh.gameObject.name, nodeNames))
                {
                    continue;
                }

                string candidate = GetLocalizedText(textMesh);
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        public static string GetFirstVisibleText(UIButton button)
        {
            if (button == null)
            {
                return string.Empty;
            }

            UITextMesh[] textMeshes = ((Component)button).GetComponentsInChildren<UITextMesh>(includeInactive: false);
            for (int i = 0; i < textMeshes.Length; i++)
            {
                UITextMesh textMesh = textMeshes[i];
                if (textMesh == null || !textMesh.gameObject.activeInHierarchy)
                {
                    continue;
                }

                string candidate = GetResolvedText(textMesh);
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }

            Text[] texts = ((Component)button).GetComponentsInChildren<Text>(includeInactive: false);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null || !text.gameObject.activeInHierarchy)
                {
                    continue;
                }

                string candidate = NormalizeForSpeech(text.text);
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        public static string JoinParts(params string[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return string.Empty;
            }

            List<string> cleaned = new List<string>();
            for (int i = 0; i < parts.Length; i++)
            {
                string part = NormalizeForSpeech(parts[i]);
                if (!string.IsNullOrWhiteSpace(part))
                {
                    cleaned.Add(part);
                }
            }

            return cleaned.Count == 0 ? string.Empty : string.Join(". ", cleaned.ToArray());
        }

        private static string GetVisibleUITextMeshByName(UIButton button, string nodeName)
        {
            UITextMesh[] textMeshes = ((Component)button).GetComponentsInChildren<UITextMesh>(includeInactive: false);
            for (int i = 0; i < textMeshes.Length; i++)
            {
                UITextMesh textMesh = textMeshes[i];
                if (textMesh == null
                    || !textMesh.gameObject.activeInHierarchy
                    || !string.Equals(textMesh.gameObject.name, nodeName, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string candidate = GetResolvedText(textMesh);
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private static string GetResolvedText(UITextMesh textMesh)
        {
            if (textMesh == null)
            {
                return string.Empty;
            }

            string localized = GetLocalizedText(textMesh);
            if (!string.IsNullOrWhiteSpace(localized))
            {
                return localized;
            }

            return NormalizeForSpeech(textMesh.Text);
        }

        private static string GetLocalizedText(UITextMesh textMesh)
        {
            if (textMesh == null)
            {
                return string.Empty;
            }

            UITextMeshLocalization localization = ((Component)textMesh).GetComponent<UITextMeshLocalization>();
            if (localization != null && GlobalLocalizationVariables.LocalizationHandler != null)
            {
                string key = UITextMeshLocalizationKeyRef(localization);
                if (!string.IsNullOrWhiteSpace(key))
                {
                    return NormalizeForSpeech(GlobalLocalizationVariables.LocalizationHandler.GetText(key));
                }
            }

            return string.Empty;
        }

        private static string GetVisibleUnityTextByName(UIButton button, string nodeName)
        {
            Text[] texts = ((Component)button).GetComponentsInChildren<Text>(includeInactive: false);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null
                    || !text.gameObject.activeInHierarchy
                    || !string.Equals(text.gameObject.name, nodeName, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string candidate = NormalizeForSpeech(text.text);
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private static bool HasMatchingNodeName(string actualName, string[] nodeNames)
        {
            if (string.IsNullOrWhiteSpace(actualName) || nodeNames == null)
            {
                return false;
            }

            for (int i = 0; i < nodeNames.Length; i++)
            {
                if (string.Equals(actualName, nodeNames[i], System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
