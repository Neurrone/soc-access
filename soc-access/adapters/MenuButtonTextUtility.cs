using HarmonyLib;
using System.Collections.Generic;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal static class MenuButtonTextUtility
    {
        private static readonly AccessTools.FieldRef<UITextMeshLocalization, string> UITextMeshLocalizationKeyRef =
            AccessTools.FieldRefAccess<UITextMeshLocalization, string>("_localizationKey");

        public static string GetDirectButtonText(UIButton button)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveButtonText(button));
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

                string candidate = SpeechTextSanitizer.Normalize(text.text);
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        public static string GetAllVisibleText(UIButton button)
        {
            if (button == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            UITextMesh[] textMeshes = ((Component)button).GetComponentsInChildren<UITextMesh>(includeInactive: false);
            for (int i = 0; i < textMeshes.Length; i++)
            {
                UITextMesh textMesh = textMeshes[i];
                if (textMesh == null || !textMesh.gameObject.activeInHierarchy)
                {
                    continue;
                }

                string candidate = GetResolvedText(textMesh);
                if (!string.IsNullOrWhiteSpace(candidate) && !parts.Contains(candidate))
                {
                    parts.Add(candidate);
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

                string candidate = SpeechTextSanitizer.Normalize(text.text);
                if (!string.IsNullOrWhiteSpace(candidate) && !parts.Contains(candidate))
                {
                    parts.Add(candidate);
                }
            }

            return parts.Count == 0 ? string.Empty : string.Join(". ", parts.ToArray());
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
                string part = SpeechTextSanitizer.Normalize(parts[i]);
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

            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
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
                    return SpeechTextSanitizer.Normalize(GlobalLocalizationVariables.LocalizationHandler.GetText(key));
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

                string candidate = SpeechTextSanitizer.Normalize(text.text);
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
