using System;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class OptionsMenuButtonAdapter : MenuButtonAdapterBase
    {
        public OptionsMenuButtonAdapter(
            UIButton button,
            Func<bool> isVisible = null,
            Func<bool> activate = null)
            : base(button, isVisible, activate)
        {
        }

        protected override string BuildLabel()
        {
            return GetOptionsLabelText();
        }

        private string GetOptionsLabelText()
        {
            Component component = Button;
            Transform parent = component != null ? component.transform.parent : null;
            Transform labelTransform = parent != null ? parent.Find("OptionsLabel") : null;
            if (labelTransform == null)
            {
                return string.Empty;
            }

            UITextMesh textMesh = labelTransform.GetComponent<UITextMesh>();
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }
    }
}
