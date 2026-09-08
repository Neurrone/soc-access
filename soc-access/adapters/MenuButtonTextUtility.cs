using HarmonyLib;
using System.Collections.Generic;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public static class MenuButtonTextUtility
    {
        private static readonly AccessTools.FieldRef<UITextMeshLocalization, string> UITextMeshLocalizationKeyRef =
            AccessTools.FieldRefAccess<UITextMeshLocalization, string>("_localizationKey");

        public static string GetDirectButtonText(UIButton button)
        {
            return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveButtonText(button));
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

            UITextMesh[] textMeshes = TextMeshesOf(button);
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

            UITextMesh[] textMeshes = TextMeshesOf(button);
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

            Text[] texts = UnityTextsOf(button);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null || !text.gameObject.activeInHierarchy)
                {
                    continue;
                }

                string candidate = SpokenLines.Clean(text.text);
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        public static string GetAllVisibleText(UIButton button)
        {
            List<string> parts = GetDistinctParts(GetAllVisibleTextParts(button));
            return parts.Count == 0 ? string.Empty : string.Join(". ", parts.ToArray());
        }

        public static List<string> GetAllVisibleTextParts(UIButton button)
        {
            if (button == null)
            {
                return new List<string>();
            }

            List<string> parts = new List<string>();
            UITextMesh[] textMeshes = TextMeshesOf(button);
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
                    parts.Add(candidate);
                }
            }

            Text[] texts = UnityTextsOf(button);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null || !text.gameObject.activeInHierarchy)
                {
                    continue;
                }

                string candidate = SpokenLines.Clean(text.text);
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    parts.Add(candidate);
                }
            }

            return parts;
        }

        private static List<string> GetDistinctParts(List<string> parts)
        {
            List<string> distinctParts = new List<string>();
            if (parts == null)
            {
                return distinctParts;
            }

            for (int i = 0; i < parts.Count; i++)
            {
                string part = parts[i];
                if (!distinctParts.Contains(part))
                {
                    distinctParts.Add(part);
                }
            }

            return distinctParts;
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
                string part = SpokenLines.Clean(parts[i]);
                if (!string.IsNullOrWhiteSpace(part))
                {
                    cleaned.Add(part);
                }
            }

            return cleaned.Count == 0 ? string.Empty : string.Join(". ", cleaned.ToArray());
        }

        private static string GetVisibleUITextMeshByName(UIButton button, string nodeName)
        {
            UITextMesh[] textMeshes = TextMeshesOf(button);
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

            return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(textMesh));
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
                    return SpokenLines.Clean(GlobalLocalizationVariables.LocalizationHandler.GetText(key));
                }
            }

            return string.Empty;
        }

        private static string GetVisibleUnityTextByName(UIButton button, string nodeName)
        {
            Text[] texts = UnityTextsOf(button);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null
                    || !text.gameObject.activeInHierarchy
                    || !string.Equals(text.gameObject.name, nodeName, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string candidate = SpokenLines.Clean(text.text);
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        // ---- the text nodes under a button ----

        /// <summary>
        /// The text nodes under a button, walked once per button and kept. A label read walked the
        /// whole subtree - twice for every visible text, up to five times for a standard label - for
        /// a set of components that does not change while the button is drawn, and the FOCUSED row's
        /// label is read twice a frame. Both sets are kept even when empty, so a button with no text
        /// of a kind costs one walk and not one per frame.
        ///
        /// The sets are walked WITH the inactive nodes and filtered on activeInHierarchy where they
        /// are read, which is what every reader here did anyway, so what a read answers is unchanged.
        /// A subtree the game has torn down - a destroyed node, or a different number of children, as
        /// a pooled list row gets - is walked again rather than answered from what the old subtree
        /// left behind.
        /// </summary>
        private sealed class ButtonTexts
        {
            public UITextMesh[] TextMeshes;
            public Text[] Texts;
            public int ChildCount;
        }

        private static readonly Dictionary<UIButton, ButtonTexts> TextsByButton =
            new Dictionary<UIButton, ButtonTexts>();

        private static readonly UITextMesh[] NoTextMeshes = new UITextMesh[0];
        private static readonly Text[] NoTexts = new Text[0];

        /// <summary>Forget every button's text nodes - the mod is going away.</summary>
        public static void Reset()
        {
            TextsByButton.Clear();
        }

        private static UITextMesh[] TextMeshesOf(UIButton button)
        {
            ButtonTexts texts = TextsOf(button);
            return texts != null ? texts.TextMeshes : NoTextMeshes;
        }

        private static Text[] UnityTextsOf(UIButton button)
        {
            ButtonTexts texts = TextsOf(button);
            return texts != null ? texts.Texts : NoTexts;
        }

        private static ButtonTexts TextsOf(UIButton button)
        {
            if (button == null)
            {
                return null;
            }

            Component root = button;
            ButtonTexts texts;
            if (TextsByButton.TryGetValue(button, out texts) && StillDescribes(texts, root))
            {
                return texts;
            }

            texts = new ButtonTexts
            {
                TextMeshes = root.GetComponentsInChildren<UITextMesh>(includeInactive: true),
                Texts = root.GetComponentsInChildren<Text>(includeInactive: true),
                ChildCount = root.transform.childCount,
            };
            TextsByButton[button] = texts;
            return texts;
        }

        // Whether a kept set still describes the button: nothing in it has been destroyed and the
        // button has the same number of children it had when the set was taken.
        private static bool StillDescribes(ButtonTexts texts, Component root)
        {
            if (texts.ChildCount != root.transform.childCount)
            {
                return false;
            }

            for (int i = 0; i < texts.TextMeshes.Length; i++)
            {
                if (texts.TextMeshes[i] == null)
                {
                    return false;
                }
            }

            for (int i = 0; i < texts.Texts.Length; i++)
            {
                if (texts.Texts[i] == null)
                {
                    return false;
                }
            }

            return true;
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
