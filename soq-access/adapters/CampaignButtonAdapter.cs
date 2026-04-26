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
            Id = BuildId(campaignButton);
        }

        public string Id { get; private set; }

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
            List<string> parts = new List<string>();
            if (Button != null && !Button.Interactable)
            {
                parts.Add("disabled");
            }

            string status = BuildProgressStatus();
            if (!string.IsNullOrWhiteSpace(status))
            {
                parts.Add(status);
            }

            return parts.Count == 0 ? string.Empty : string.Join(". ", parts.ToArray());
        }

        public bool IsVisible()
        {
            return _campaignButton != null && MenuButtonAdapterBase.IsButtonVisible(Button);
        }

        public bool IsReady()
        {
            return IsVisible() && Button != null && Button.OnClicked != null;
        }

        public bool Activate()
        {
            if (!IsVisible() || Button == null || !Button.Active || !Button.Interactable)
            {
                return false;
            }

            Button.OnClicked?.Invoke();
            return true;
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

        private static string BuildId(CampaignButton campaignButton)
        {
            if (campaignButton == null)
            {
                return "campaign";
            }

            ICampaignDefinition definition = DefinitionRef(campaignButton);
            if (definition != null && !string.IsNullOrWhiteSpace(definition.Identifier))
            {
                return "campaign-" + definition.Identifier.ToLowerInvariant();
            }

            return "campaign-" + campaignButton.GetInstanceID();
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
            ICampaignDefinition definition = GetDefinition();
            CampaignState state = GetCampaignState();
            if (definition == null || definition.Maps == null || definition.Maps.Count == 0 || state == null)
            {
                return string.Empty;
            }

            int available = 0;
            int completed = 0;
            for (int i = 0; i < definition.Maps.Count; i++)
            {
                ICampaignMapDefinition map = definition.Maps[i];
                if (map == null)
                {
                    continue;
                }

                CampaignLevelState level = state.GetLevel(map);
                bool unlocked = i == 0 || (level != null && !level.IsLocked);
                bool isCompleted = level != null && level.IsCompleted;
                if (unlocked || isCompleted)
                {
                    available++;
                }

                if (isCompleted)
                {
                    completed++;
                }
            }

            if (completed >= definition.Maps.Count)
            {
                return GetLocalizedText("Common/CampaignSelectMenu/CampaignCompleted", "campaign completed");
            }

            List<string> parts = new List<string>();
            parts.Add(completed + " of " + definition.Maps.Count + " missions completed");
            if (available > 0)
            {
                parts.Add(available + " available");
            }

            return string.Join(". ", parts.ToArray());
        }

        private static string GetLocalizedText(string localizationKey, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(localizationKey) && GlobalLocalizationVariables.LocalizationHandler != null)
            {
                string localized = SpeechTextSanitizer.Normalize(
                    GlobalLocalizationVariables.LocalizationHandler.GetText(localizationKey));
                if (!string.IsNullOrWhiteSpace(localized))
                {
                    return localized;
                }
            }

            return fallback;
        }

        private static string GetText(UITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }
    }
}
