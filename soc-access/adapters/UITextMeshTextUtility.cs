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

        public static string GetEffectiveText(IUITextMesh textMesh)
        {
            UITextMesh concreteTextMesh = textMesh as UITextMesh;
            if (concreteTextMesh != null)
            {
                string builderText = GetStringBuilderText(concreteTextMesh);
                if (!string.IsNullOrEmpty(builderText))
                {
                    return builderText;
                }
            }

            return textMesh != null ? textMesh.Text ?? string.Empty : string.Empty;
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

        private static string GetStringBuilderText(UITextMesh textMesh)
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
