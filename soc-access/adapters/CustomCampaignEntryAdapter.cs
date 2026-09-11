using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class CustomCampaignEntryAdapter
    {
        private static readonly AccessTools.FieldRef<CustomCampaignEntry, UIButton> ButtonRef =
            AccessTools.FieldRefAccess<CustomCampaignEntry, UIButton>("_button");
        private static readonly AccessTools.FieldRef<CustomCampaignEntry, UITextMesh> TitleTextRef =
            AccessTools.FieldRefAccess<CustomCampaignEntry, UITextMesh>("_titleText");
        private static readonly AccessTools.FieldRef<CustomCampaignEntry, UITextMesh> DescriptionTextRef =
            AccessTools.FieldRefAccess<CustomCampaignEntry, UITextMesh>("_descriptionText");
        private static readonly AccessTools.FieldRef<CustomCampaignEntry, UITextMesh> InstallationTextRef =
            AccessTools.FieldRefAccess<CustomCampaignEntry, UITextMesh>("_installationText");
        private static readonly FieldInfo CampaignDefinitionField =
            AccessTools.Field(typeof(CustomCampaignEntry), "_campaignDefinition");
        private static readonly FieldInfo ModReferenceField =
            AccessTools.Field(typeof(CustomCampaignEntry), "_modReference");
        private static readonly FieldInfo InstallationOverlayField =
            AccessTools.Field(typeof(CustomCampaignEntry), "_installationOverlay");

        private readonly CustomCampaignEntry _entry;

        public CustomCampaignEntryAdapter(CustomCampaignEntry entry)
        {
            _entry = entry;
            Button = entry != null ? ButtonRef(entry) : null;
        }

        public CustomCampaignEntry Source
        {
            get { return _entry; }
        }

        public UIButton Button { get; private set; }

        public bool HasCampaignDefinition
        {
            get { return GetCampaignDefinition() != null; }
        }

        public bool HasModReference
        {
            get { return GetModReference() != null; }
        }

        public string GetTitle()
        {
            return ReadTextMesh(TitleTextRef);
        }

        public string GetDescription()
        {
            return ReadTextMesh(DescriptionTextRef);
        }

        /// <summary>The paragraphs the card draws under its title, kept apart rather than run
        /// together: a mod's blurb can be written in more than one.</summary>
        public IList<string> GetDescriptionLines()
        {
            return SpokenLines.Of(new[] { GetDescription() });
        }

        public string GetActionText()
        {
            return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveButtonText(Button));
        }

        public string GetInstallationText()
        {
            if (!IsInstallationVisible())
            {
                return string.Empty;
            }

            return ReadTextMesh(InstallationTextRef);
        }

        public bool IsVisible()
        {
            if (_entry == null)
            {
                return false;
            }

            GameObject gameObject = ((Component)_entry).gameObject;
            return GameObjects.IsLiveSceneObject(gameObject)
                && gameObject.activeInHierarchy
                && HasAnyVisibleTextOrAction();
        }

        public bool IsEnabled()
        {
            return Button != null && Button.Active && Button.Interactable;
        }

        public bool Activate()
        {
            return NativeSelectionUtility.Click(Button);
        }

        public void FocusNative()
        {
            if (!IsVisible())
            {
                return;
            }

            NativeSelectionUtility.Select((Component)_entry);
        }

        public bool Matches(CustomCampaignEntry entry)
        {
            return ReferenceEquals(_entry, entry);
        }

        private bool HasAnyVisibleTextOrAction()
        {
            return !string.IsNullOrWhiteSpace(GetTitle())
                || !string.IsNullOrWhiteSpace(GetDescription())
                || !string.IsNullOrWhiteSpace(GetActionText())
                || !string.IsNullOrWhiteSpace(GetInstallationText());
        }

        private object GetCampaignDefinition()
        {
            return _entry != null && CampaignDefinitionField != null ? CampaignDefinitionField.GetValue(_entry) : null;
        }

        private object GetModReference()
        {
            return _entry != null && ModReferenceField != null ? ModReferenceField.GetValue(_entry) : null;
        }

        private bool IsInstallationVisible()
        {
            UITransform overlay = _entry != null && InstallationOverlayField != null
                ? InstallationOverlayField.GetValue(_entry) as UITransform
                : null;
            if (overlay == null || !overlay.Active)
            {
                return false;
            }

            GameObject gameObject = ((Component)overlay).gameObject;
            return gameObject != null && gameObject.activeInHierarchy;
        }

        private string ReadTextMesh(AccessTools.FieldRef<CustomCampaignEntry, UITextMesh> fieldRef)
        {
            if (_entry == null || fieldRef == null)
            {
                return string.Empty;
            }

            try
            {
                return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(fieldRef(_entry)));
            }
            catch (System.NullReferenceException)
            {
                return string.Empty;
            }
        }
    }
}
