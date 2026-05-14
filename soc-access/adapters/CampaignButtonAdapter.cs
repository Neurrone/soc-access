using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Campaign;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class CampaignButtonAdapter : IMenuButtonAdapter
    {
        private static readonly AccessTools.FieldRef<CampaignButton, UIButton> ButtonRef =
            AccessTools.FieldRefAccess<CampaignButton, UIButton>("_button");
        private static readonly AccessTools.FieldRef<CampaignButton, UITextMesh> DescriptionTextRef =
            AccessTools.FieldRefAccess<CampaignButton, UITextMesh>("_descriptionText");
        private static readonly AccessTools.FieldRef<CampaignButton, UITextMesh> CampaignNameTextRef =
            AccessTools.FieldRefAccess<CampaignButton, UITextMesh>("_campaignNameText");
        private static readonly AccessTools.FieldRef<CampaignButton, UITextMesh> CampaignSubHeaderTextRef =
            AccessTools.FieldRefAccess<CampaignButton, UITextMesh>("_campaignSubHeaderText");
        private static readonly AccessTools.FieldRef<CampaignButton, ICampaignDefinition> DefinitionRef =
            AccessTools.FieldRefAccess<CampaignButton, ICampaignDefinition>("_definition");
        private static readonly AccessTools.FieldRef<CampaignButton, CampaignState> CampaignStateRef =
            AccessTools.FieldRefAccess<CampaignButton, CampaignState>("_campaignState");
        private static readonly MethodInfo HandleBeginHoverMethod =
            AccessTools.Method(typeof(CampaignButton), "HandleBeginHover");

        private readonly CampaignButton _campaignButton;
        private readonly int _campaignNumber;

        public CampaignButtonAdapter(CampaignButton campaignButton, int campaignNumber)
        {
            _campaignButton = campaignButton;
            _campaignNumber = campaignNumber;
            Button = campaignButton != null ? ButtonRef(campaignButton) : null;
        }

        public UIButton Button { get; private set; }

        public string GetLabel()
        {
            ICampaignDefinition definition = GetDefinition();
            return MenuButtonTextUtility.JoinParts(
                PrefixCampaignNumber(_campaignNumber),
                GetLocalizedText(definition != null ? definition.Title : null, GetText(CampaignNameTextRef)),
                GetLocalizedText(definition != null ? definition.SubTitle : null, GetText(CampaignSubHeaderTextRef)),
                GetLocalizedText(definition != null ? definition.Description : null, GetText(DescriptionTextRef)));
        }

        public string GetStatus()
        {
            return BuildProgressStatus();
        }

        public bool IsVisible()
        {
            return _campaignButton != null && MenuButtonAdapterBase.IsButtonVisible(Button);
        }

        public bool IsReady()
        {
            return IsVisible() && Button != null && Button.OnClicked != null;
        }

        public bool IsEnabled()
        {
            return Button != null && Button.Interactable;
        }

        public bool Activate()
        {
            if (!IsVisible() || Button == null || !Button.Active || !Button.Interactable)
            {
                return false;
            }

            return NativeSelectionUtility.Click(Button);
        }

        public void FocusNative()
        {
            if (!IsVisible() || Button == null)
            {
                return;
            }

            Component buttonComponent = Button;
            if (EventSystem.current != null && buttonComponent != null)
            {
                EventSystem.current.SetSelectedGameObject(buttonComponent.gameObject);
            }

            if (_campaignButton != null && HandleBeginHoverMethod != null)
            {
                HandleBeginHoverMethod.Invoke(_campaignButton, new object[] { null });
            }
        }

        private static string PrefixCampaignNumber(int number)
        {
            return number > 0 ? "Campaign " + number : string.Empty;
        }

        private string GetText(AccessTools.FieldRef<CampaignButton, UITextMesh> fieldRef)
        {
            return _campaignButton != null ? GetText(fieldRef(_campaignButton)) : string.Empty;
        }

        private ICampaignDefinition GetDefinition()
        {
            return _campaignButton != null ? DefinitionRef(_campaignButton) : null;
        }

        private CampaignState GetCampaignState()
        {
            return _campaignButton != null ? CampaignStateRef(_campaignButton) : null;
        }

        private string BuildProgressStatus()
        {
            return CampaignProgress.BuildMissionStatus(GetDefinition(), GetCampaignState(), null);
        }

        private static string GetLocalizedText(string localizationKey, string fallback)
        {
            return CampaignProgress.GetLocalizedText(localizationKey, fallback);
        }

        private static string GetText(UITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }
    }
}
