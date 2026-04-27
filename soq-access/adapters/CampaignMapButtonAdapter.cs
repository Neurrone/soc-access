using System;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Campaign;
using SongsOfConquestAccess.Speech;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class CampaignMapButtonAdapter
    {
        private static readonly AccessTools.FieldRef<CampaignMapButton, UIButton> UnplayedButtonRef =
            AccessTools.FieldRefAccess<CampaignMapButton, UIButton>("_unplayedButton");
        private static readonly AccessTools.FieldRef<CampaignMapButton, UIButton> PlayedBeforeButtonRef =
            AccessTools.FieldRefAccess<CampaignMapButton, UIButton>("_playedBeforeButton");

        private readonly CampaignMapButton _button;

        public CampaignMapButtonAdapter(CampaignMapButton button)
        {
            _button = button;
            Id = BuildId(button);
        }

        public string Id { get; private set; }

        public CampaignMapButton Source
        {
            get { return _button; }
        }

        public ICampaignMapDefinition Definition
        {
            get { return _button != null ? _button.Definition : null; }
        }

        public string GetLabel()
        {
            ICampaignMapDefinition definition = Definition;
            return SpeechTextSanitizer.Normalize(definition != null ? definition.DisplayName : string.Empty);
        }

        public string GetStatus()
        {
            return string.Empty;
        }

        public bool IsVisible()
        {
            return _button != null
                && IsLiveSceneObject(((Component)_button).gameObject)
                && (MenuButtonAdapterBase.IsButtonVisible(GetUnplayedButton())
                    || MenuButtonAdapterBase.IsButtonVisible(GetPlayedBeforeButton()));
        }

        public bool Activate()
        {
            if (!IsVisible())
            {
                return false;
            }

            return NativeSelectionUtility.Click(GetVisibleNativeButton());
        }

        public void FocusNative()
        {
            UIButton nativeButton = GetVisibleNativeButton();
            if (nativeButton == null || EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(((Component)nativeButton).gameObject);
        }

        private UIButton GetVisibleNativeButton()
        {
            UIButton unplayed = GetUnplayedButton();
            if (MenuButtonAdapterBase.IsButtonVisible(unplayed))
            {
                return unplayed;
            }

            UIButton played = GetPlayedBeforeButton();
            if (MenuButtonAdapterBase.IsButtonVisible(played))
            {
                return played;
            }

            return null;
        }

        private UIButton GetUnplayedButton()
        {
            return _button != null ? UnplayedButtonRef(_button) : null;
        }

        private UIButton GetPlayedBeforeButton()
        {
            return _button != null ? PlayedBeforeButtonRef(_button) : null;
        }

        private static string BuildId(CampaignMapButton button)
        {
            ICampaignMapDefinition definition = button != null ? button.Definition : null;
            if (definition != null && !string.IsNullOrWhiteSpace(definition.Identifier))
            {
                return "campaign-map-" + definition.Identifier.ToLowerInvariant();
            }

            return "campaign-map-" + (button != null ? button.GetInstanceID().ToString() : "unknown");
        }

        private static bool IsLiveSceneObject(GameObject gameObject)
        {
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }
    }
}
