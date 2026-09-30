using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.UI;

namespace SongsOfConquestAccess.Adapters
{
    public static class UITextMeshTextUtility
    {
        // Hot reload can desync UITextMesh from TMP_Text on existing popup instances:
        // the public Text/TMP m_text may revert to prefab placeholder strings while the
        // real runtime dialog text still survives in UITextMesh._stringBuilder. For
        // message dialogs we therefore treat _stringBuilder as the most reliable source.
        private static readonly System.Reflection.FieldInfo StringBuilderField =
            AccessTools.Field(typeof(UITextMesh), "_stringBuilder");

        // How TMP last had its text set. UITextMesh writes every text through its string builder and
        // TMP's SetText(StringBuilder), which records SetTextArray; TMP's own text property is then
        // rebuilt lazily and, once the text has been drawn, can still answer an older string - after
        // an empty write, the prefab placeholder ("SkillChoiceHeaderRight"). So while SetTextArray is
        // the source, the builder is what is drawn, empty included.
        private static readonly System.Reflection.FieldInfo InputSourceField =
            AccessTools.Field(typeof(TMPro.TMP_Text), "m_inputSource");

        private static readonly object SetTextArraySource = FindSetTextArraySource();

        /// <summary>Null where the field or the value is missing (a TMP update), which leaves every
        /// read as it was before this check: the builder when it holds text, TMP's text otherwise.
        /// </summary>
        private static object FindSetTextArraySource()
        {
            if (InputSourceField == null
                || !InputSourceField.FieldType.IsEnum
                || !System.Enum.IsDefined(InputSourceField.FieldType, "SetTextArray"))
            {
                return null;
            }

            return System.Enum.Parse(InputSourceField.FieldType, "SetTextArray");
        }

        [HookWritable]
        public static string GetEffectiveText(IUITextMesh textMesh)
        {
            UITextMesh concreteTextMesh = textMesh as UITextMesh;
            if (concreteTextMesh != null)
            {
                string builderText = GetStringBuilderText(concreteTextMesh);
                if (!string.IsNullOrEmpty(builderText) || IsSetThroughBuilder(concreteTextMesh))
                {
                    return builderText;
                }
            }

            return textMesh != null ? textMesh.Text ?? string.Empty : string.Empty;
        }

        /// <summary>What the text says, ready to be spoken: the effective text split on its line
        /// breaks and stripped of the game's rich-text tags, joined back with newlines.</summary>
        public static string Spoken(IUITextMesh textMesh)
        {
            return SongsOfConquestAccess.UI.SpokenLines.Clean(GetEffectiveText(textMesh));
        }

        /// <summary>The lines <see cref="Spoken"/> joins, for a text the game may have written more
        /// than one paragraph into.</summary>
        public static IList<string> SpokenLines(IUITextMesh textMesh)
        {
            return SongsOfConquestAccess.UI.SpokenLines.Of(new[] { GetEffectiveText(textMesh) });
        }

        public static string GetEffectiveButtonText(IUIButton button)
        {
            UIButton concreteButton = button as UIButton;
            if (concreteButton != null && concreteButton.TextMesh != null)
            {
                // Button labels backed by UITextMesh exhibit the same hot-reload behavior
                // as the popup header/body, so resolve them through the text mesh first.
                string textMeshText = GetEffectiveText(concreteButton.TextMesh);
                if (!string.IsNullOrEmpty(textMeshText))
                {
                    return textMeshText;
                }
            }

            return button != null ? button.Text ?? string.Empty : string.Empty;
        }

        /// <summary>Whether TMP's text was last set through the UITextMesh's builder, so an empty
        /// builder means the game wrote nothing rather than that the text lives in TMP alone.</summary>
        private static bool IsSetThroughBuilder(UITextMesh textMesh)
        {
            return InputSourceField != null
                && SetTextArraySource != null
                && SetTextArraySource.Equals(InputSourceField.GetValue(textMesh));
        }

        /// <summary>What the game last WROTE into the text, read off its string builder; empty when the
        /// game wrote nothing, even where the prefab placeholder still sits in the mesh (the overview
        /// rows the game never sets a level for keep the prefab's "9999").</summary>
        public static string GetStringBuilderText(UITextMesh textMesh)
        {
            if (textMesh == null || StringBuilderField == null)
            {
                return string.Empty;
            }

            StringBuilder builder = StringBuilderField.GetValue(textMesh) as StringBuilder;
            return builder != null ? builder.ToString() : string.Empty;
        }
    }
}
